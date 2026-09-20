using Microsoft.CodeAnalysis;
using System;

namespace GeneratorLibrary
{
    /// <summary>
    /// Limits generation to a single assembly, so a script is written once instead of once per compiled assembly.
    /// </summary>
    public static class TargetAssembly
    {
        /// <summary>
        /// Assembly that receives the generated scripts. In Unity this is the "name" field of the .asmdef that owns
        /// the folder the generator .dll is placed in, or "Assembly-CSharp" for scripts that have no .asmdef.
        /// </summary>
        public const string Name = "Mocker";

        /// <summary>
        /// Tracks whether the assembly currently being compiled is <see cref="Name"/>.
        /// </summary>
        /// <param name="context">Context the compilation is read from.</param>
        /// <returns>Provider that only changes when the answer changes, so generation is not re-run on every keystroke.</returns>
        public static IncrementalValueProvider<bool> ObserveTargetAssembly(this IncrementalGeneratorInitializationContext context)
        {
            return context.CompilationProvider.Select((compilation, _) => string.Equals(compilation.AssemblyName, Name, StringComparison.Ordinal));
        }
    }
}
