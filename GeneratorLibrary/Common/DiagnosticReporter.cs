using Microsoft.CodeAnalysis;

namespace GeneratorLibrary.Common
{
    /// <summary>
    /// Reports a problem found during generation against the exact code that caused it. Reports reach the
    /// Unity console and the IDE the same way compiler messages do, so a generator never has to fail silently.
    /// </summary>
    public static class DiagnosticReporter
    {
        /// <summary>
        /// Describes a problem that stops generation and fails the compilation.
        /// </summary>
        /// <param name="id">Short stable code, such as "MOCK001".</param>
        /// <param name="title">What the rule checks.</param>
        /// <param name="messageFormat">Message with {0} placeholders filled in when it is reported.</param>
        /// <param name="category">Group the rule belongs to, such as the package name.</param>
        /// <returns>Rule that can be reported.</returns>
        public static DiagnosticDescriptor CreateError(string id, string title, string messageFormat, string category)
        {
            return new DiagnosticDescriptor(id, title, messageFormat, category, DiagnosticSeverity.Error, true);
        }

        /// <summary>
        /// Describes a problem worth attention that still lets the compilation through.
        /// </summary>
        /// <param name="id">Short stable code, such as "MOCK001".</param>
        /// <param name="title">What the rule checks.</param>
        /// <param name="messageFormat">Message with {0} placeholders filled in when it is reported.</param>
        /// <param name="category">Group the rule belongs to, such as the package name.</param>
        /// <returns>Rule that can be reported.</returns>
        public static DiagnosticDescriptor CreateWarning(string id, string title, string messageFormat, string category)
        {
            return new DiagnosticDescriptor(id, title, messageFormat, category, DiagnosticSeverity.Warning, true);
        }

        /// <summary>
        /// Reports a rule against the code that broke it.
        /// </summary>
        /// <param name="context">Context the report is sent to.</param>
        /// <param name="descriptor">Rule that was broken.</param>
        /// <param name="location">Where in the code it was broken.</param>
        /// <param name="messageArguments">Values for the placeholders in the message.</param>
        public static void Report(this SourceProductionContext context, DiagnosticDescriptor descriptor, ScriptLocation location, params object[] messageArguments)
        {
            context.ReportDiagnostic(Diagnostic.Create(descriptor, location.ToLocation(), messageArguments));
        }
    }
}
