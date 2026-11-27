using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using reromanlee.Mocker;
using reromanlee.Mocker.Generator.Mocker;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace reromanlee.Mocker.Generator.Tests.Common
{
    internal sealed class GenerationResult
    {
        public GenerationResult(
            CSharpCompilation outputCompilation,
            ImmutableArray<Diagnostic> generatorDiagnostics,
            ImmutableArray<Diagnostic> compilationDiagnostics,
            GeneratorDriverRunResult runResult)
        {
            OutputCompilation = outputCompilation;
            GeneratorDiagnostics = generatorDiagnostics;
            CompilationDiagnostics = compilationDiagnostics;
            RunResult = runResult;
        }

        public CSharpCompilation OutputCompilation { get; }

        public ImmutableArray<Diagnostic> GeneratorDiagnostics { get; }

        public ImmutableArray<Diagnostic> CompilationDiagnostics { get; }

        public GeneratorDriverRunResult RunResult { get; }

        public IReadOnlyDictionary<string, string> GeneratedSources
        {
            get
            {
                Dictionary<string, string> sources = new Dictionary<string, string>(StringComparer.Ordinal);

                foreach (GeneratedSourceResult generated in RunResult.Results[0].GeneratedSources)
                {
                    sources[generated.HintName] = generated.SourceText.ToString();
                }

                return sources;
            }
        }

        public byte[] EmitAssemblyBytes()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                Microsoft.CodeAnalysis.Emit.EmitResult emit = OutputCompilation.Emit(stream);

                if (!emit.Success)
                {
                    string errors = string.Join("\n", emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
                    Assert.Fail("Failed to emit generated assembly:\n" + errors + "\n\nGenerated files:\n" + DumpGeneratedSources());
                }

                return stream.ToArray();
            }
        }

        public MetadataReference EmitMetadataReference()
        {
            return MetadataReference.CreateFromImage(EmitAssemblyBytes());
        }

        public LoadedAssemblyScope LoadInCollectibleContext(params byte[][] dependencyAssemblies)
        {
            byte[] mainBytes = EmitAssemblyBytes();
            return new LoadedAssemblyScope(mainBytes, dependencyAssemblies);
        }

        public void AssertNoErrors()
        {
            Diagnostic[] genErrors = GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            Diagnostic[] compErrors = CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

            if (genErrors.Length > 0 || compErrors.Length > 0)
            {
                string message = "Generator errors:\n" + string.Join("\n", genErrors.Select(d => d.ToString()))
                    + "\nCompilation errors:\n" + string.Join("\n", compErrors.Select(d => d.ToString()))
                    + "\nGenerated sources:\n" + DumpGeneratedSources();
                Assert.Fail(message);
            }
        }

        public void AssertNoWarningsOrErrors()
        {
            AssertNoErrors();

            Diagnostic[] warnings = CompilationDiagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Warning)
                .ToArray();

            if (warnings.Length > 0)
            {
                Assert.Fail("Unexpected compiler warnings:\n" + string.Join("\n", warnings.Select(d => d.ToString()))
                    + "\nGenerated sources:\n" + DumpGeneratedSources());
            }
        }

        public string DumpGeneratedSources()
        {
            List<string> chunks = new List<string>();

            foreach (KeyValuePair<string, string> pair in GeneratedSources)
            {
                chunks.Add($"--- {pair.Key} ---\n{pair.Value}");
            }

            return string.Join("\n", chunks);
        }
    }

    internal sealed class LoadedAssemblyScope : IDisposable
    {
        private readonly CollectibleLoadContext _context;

        public LoadedAssemblyScope(byte[] mainAssembly, byte[][] dependencyAssemblies)
        {
            _context = new CollectibleLoadContext();

            if (dependencyAssemblies != null)
            {
                foreach (byte[] dependency in dependencyAssemblies)
                {
                    using (MemoryStream depStream = new MemoryStream(dependency))
                    {
                        _context.LoadFromStream(depStream);
                    }
                }
            }

            using (MemoryStream stream = new MemoryStream(mainAssembly))
            {
                Assembly = _context.LoadFromStream(stream);
            }
        }

        public Assembly Assembly { get; }

        public void Dispose()
        {
            _context.Unload();
        }

        private sealed class CollectibleLoadContext : AssemblyLoadContext
        {
            public CollectibleLoadContext()
                : base(isCollectible: true)
            {
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                return null;
            }
        }
    }

    internal static class GeneratorHarness
    {
        private static readonly ImmutableArray<MetadataReference> BaseReferences = BuildBaseReferences();

        public static CSharpCompilation CreateCompilation(
            string assemblyName,
            IEnumerable<string> sources,
            IEnumerable<MetadataReference> additionalReferences = null,
            LanguageVersion languageVersion = LanguageVersion.CSharp9)
        {
            CSharpParseOptions parseOptions = new CSharpParseOptions(languageVersion);
            List<SyntaxTree> trees = new List<SyntaxTree>();
            int index = 0;

            foreach (string source in sources)
            {
                trees.Add(CSharpSyntaxTree.ParseText(source, parseOptions, path: $"{assemblyName}_{index}.cs"));
                index++;
            }

            List<MetadataReference> references = new List<MetadataReference>(BaseReferences);

            if (additionalReferences != null)
            {
                references.AddRange(additionalReferences);
            }

            CSharpCompilationOptions compilationOptions = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release);

            return CSharpCompilation.Create(assemblyName, trees, references, compilationOptions);
        }

        public static GenerationResult Run(
            string assemblyName,
            string source,
            IEnumerable<MetadataReference> additionalReferences = null,
            LanguageVersion languageVersion = LanguageVersion.CSharp9)
        {
            return Run(assemblyName, new[] { source }, additionalReferences, languageVersion);
        }

        public static GenerationResult Run(
            string assemblyName,
            IEnumerable<string> sources,
            IEnumerable<MetadataReference> additionalReferences = null,
            LanguageVersion languageVersion = LanguageVersion.CSharp9)
        {
            CSharpCompilation compilation = CreateCompilation(assemblyName, sources, additionalReferences, languageVersion);
            MockerGenerator generator = new MockerGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                generators: new[] { generator.AsSourceGenerator() },
                parseOptions: new CSharpParseOptions(languageVersion),
                driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> generatorDiagnostics);

            CSharpCompilation csharpOutput = (CSharpCompilation)outputCompilation;
            ImmutableArray<Diagnostic> compilationDiagnostics = csharpOutput.GetDiagnostics();

            return new GenerationResult(csharpOutput, generatorDiagnostics, compilationDiagnostics, driver.GetRunResult());
        }

        private static ImmutableArray<MetadataReference> BuildBaseReferences()
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;

            if (!string.IsNullOrEmpty(trustedAssemblies))
            {
                foreach (string path in trustedAssemblies.Split(Path.PathSeparator))
                {
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        string fileName = Path.GetFileName(path);

                        if (fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
                            || fileName.Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase)
                            || fileName.Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase))
                        {
                            paths.Add(path);
                        }
                    }
                }
            }

            paths.Add(typeof(Dependencies).Assembly.Location);

            ImmutableArray<MetadataReference>.Builder builder = ImmutableArray.CreateBuilder<MetadataReference>(paths.Count);

            foreach (string path in paths)
            {
                builder.Add(MetadataReference.CreateFromFile(path));
            }

            return builder.ToImmutable();
        }
    }
}
