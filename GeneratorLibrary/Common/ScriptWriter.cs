using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Text;

namespace GeneratorLibrary.Common
{
    /// <summary>
    /// Turns a complete .cs file content into a single generated script that is added to the compilation.
    /// </summary>
    public static class ScriptWriter
    {
        private const string GeneratedExtension = ".g.cs";
        private const string SourceExtension = ".cs";

        /// <summary>
        /// Adds a generated script while the generator produces its output.
        /// </summary>
        /// <param name="context">Context the script is added to.</param>
        /// <param name="scriptName">Name of the script, with or without an extension. "Tools" becomes "Tools.g.cs".</param>
        /// <param name="scriptContent">Complete content of the .cs file, including usings and namespace.</param>
        public static void AddScript(this SourceProductionContext context, string scriptName, string scriptContent)
        {
            context.AddSource(GetFileName(scriptName), GetSourceText(scriptContent));
        }

        /// <summary>
        /// Adds a generated script before the compilation is parsed, for content that never depends on user code.
        /// </summary>
        /// <param name="context">Context the script is added to.</param>
        /// <param name="scriptName">Name of the script, with or without an extension. "Tools" becomes "Tools.g.cs".</param>
        /// <param name="scriptContent">Complete content of the .cs file, including usings and namespace.</param>
        public static void AddScript(this IncrementalGeneratorPostInitializationContext context, string scriptName, string scriptContent)
        {
            context.AddSource(GetFileName(scriptName), GetSourceText(scriptContent));
        }

        /// <summary>
        /// Adds a generated script next to the type it was generated for, named after that type.
        /// The script is created in <see cref="TypeTarget.AssemblyName"/>, because a generator can only add
        /// source to the assembly it is compiling, which is the assembly the attribute was found in.
        /// </summary>
        /// <param name="context">Context the script is added to.</param>
        /// <param name="target">Type the script was generated for.</param>
        /// <param name="scriptContent">Complete content of the .cs file, including usings and namespace.</param>
        public static void AddLocalScript(this SourceProductionContext context, TypeTarget target, string scriptContent)
        {
            context.AddScript(target.FullName, scriptContent);
        }

        private static string GetFileName(string scriptName)
        {
            string name = scriptName == null ? string.Empty : scriptName.Trim();

            if (name.Length == 0)
            {
                throw new ArgumentException("Script name cannot be empty.", nameof(scriptName));
            }

            if (name.EndsWith(GeneratedExtension, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }

            if (name.EndsWith(SourceExtension, StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - SourceExtension.Length);
            }

            return name + GeneratedExtension;
        }

        private static SourceText GetSourceText(string scriptContent)
        {
            return SourceText.From(scriptContent ?? string.Empty, Encoding.UTF8);
        }
    }
}
