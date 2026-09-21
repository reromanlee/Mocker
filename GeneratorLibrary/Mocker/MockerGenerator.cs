using GeneratorLibrary.Common;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace GeneratorLibrary.Mocker
{
    /// <summary>
    /// Runs in every assembly that references Mocker and does whatever that assembly calls for: an assembly
    /// holding a composite gets the API, an assembly holding implementors publishes them, and the assembly
    /// marked as the root gets the enums and the wiring. The roles come from the attributes that are
    /// actually there, so no assembly ever has to be named or configured.
    /// </summary>
    [Generator]
    public class MockerGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValuesProvider<MockerTarget> composites = context.FindAllByAttribute(MockerAttributes.Composite, MockerTarget.FromComposite);
            IncrementalValuesProvider<MockerTarget> nodes = context.FindAllByAttribute(MockerAttributes.Node, MockerTarget.FromNode);
            IncrementalValuesProvider<MockerTarget> components = context.FindAllByAttribute(MockerAttributes.Component, MockerTarget.FromComponent);
            IncrementalValuesProvider<MockerTarget> implementors = context.FindAllByAttribute(MockerAttributes.Implementor, MockerTarget.FromImplementor);

            IncrementalValuesProvider<MockScript> mocks = context.FindAllByAttribute(MockerAttributes.Component, MockerMockWriter.Build);

            context.RegisterSourceOutput(mocks, (productionContext, mock) =>
            {
                if (!mock.IsSupported)
                {
                    productionContext.Report(MockerDiagnostics.NotMockable, mock.Location, mock.UnsupportedMember);
                }
                else if (mock.Name.Length != 0)
                {
                    productionContext.AddScript(mock.Name, mock.Content);
                }
            });

            IncrementalValueProvider<ImmutableArray<MockerTarget>> local = ProviderExtensions.CollectAll(composites, nodes, components, implementors);
            IncrementalValueProvider<MockerImports> published = context.CompilationProvider.Select((compilation, _) => ReadImports(compilation));

            context.RegisterSourceOutput(local.Combine(published), (productionContext, batch) =>
            {
                MockerImports imports = batch.Right;
                ImmutableArray<MockerTarget> localTargets = batch.Left;
                ImmutableArray<MockerTarget> allTargets = localTargets.AddRange(imports.Targets);

                MockerTree tree = new MockerTree(allTargets);

                MockerValidator.Validate(productionContext, localTargets, allTargets, tree);
                MockerExportWriter.Write(productionContext, localTargets);
                MockerApiWriter.Write(productionContext, localTargets, tree);

                if (imports.IsRoot)
                {
                    MockerValidator.ValidateRoot(productionContext, imports, allTargets);
                    MockerEnumWriter.Write(productionContext, allTargets, tree, imports);

                    foreach (MockerTarget target in allTargets)
                    {
                        if (target.Role != MockerRole.Composite || !imports.Owns(target.Type.FullName))
                        {
                            continue;
                        }

                        MockerScope scope = new MockerScope(target, allTargets, tree);

                        MockerChoicesWriter.Write(productionContext, scope);
                        MockerWiringWriter.Write(productionContext, scope, allTargets);
                    }
                }
            });
        }

        private static MockerImports ReadImports(Compilation compilation)
        {
            List<MockerTarget> published = new List<MockerTarget>();

            foreach (AttributeData attribute in AssemblyScanner.FindAssemblyAttributes(compilation, MockerAttributes.Export))
            {
                MockerTarget target;

                if (MockerTarget.TryFromExport(attribute, out target))
                {
                    published.Add(target);
                }
            }

            published.Sort((left, right) => string.CompareOrdinal(left.Type.FullName, right.Type.FullName));

            AttributeData root = AssemblyScanner.FindAssemblyAttribute(compilation, MockerAttributes.Root);

            if (root == null)
            {
                return new MockerImports(published.ToImmutableArray(), false, ImmutableArray<string>.Empty, default(ScriptLocation));
            }

            ImmutableArray<string>.Builder composites = ImmutableArray.CreateBuilder<string>();

            foreach (TypeTarget composite in root.GetTypeArrayArgument(0))
            {
                if (!composite.IsEmpty)
                {
                    composites.Add(composite.FullName);
                }
            }

            return new MockerImports(published.ToImmutableArray(), true, composites.ToImmutable(), ScriptLocation.From(root.ApplicationSyntaxReference));
        }
    }
}
