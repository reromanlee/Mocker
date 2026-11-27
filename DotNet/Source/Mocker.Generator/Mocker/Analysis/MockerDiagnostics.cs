using Microsoft.CodeAnalysis;
using reromanlee.Mocker.Generator.Common;

namespace reromanlee.Mocker.Generator.Mocker
{
    /// <summary>
    /// Compile-time diagnostics (<c>MOCK001</c> through <c>MOCK015</c>) reported against the exact
    /// attribute or declaration that violated a Mocker contract.
    /// </summary>
    public static class MockerDiagnostics
    {
        private const string Category = "Mocker";

        public static readonly DiagnosticDescriptor NotPartial = DiagnosticReporter.CreateError(
            "MOCK001",
            "Type must be partial",
            "{0} '{1}' must be declared partial, otherwise Mocker cannot generate into it",
            Category);

        public static readonly DiagnosticDescriptor MissingParent = DiagnosticReporter.CreateError(
            "MOCK002",
            "Parent type is missing",
            "{0} '{1}' does not say what it hangs off, so it has no place in a composite",
            Category);

        public static readonly DiagnosticDescriptor UnknownParent = DiagnosticReporter.CreateError(
            "MOCK003",
            "Parent type is not part of a composite",
            "{0} '{1}' hangs off '{2}', which carries no Mocker attribute",
            Category);

        public static readonly DiagnosticDescriptor WrongParentRole = DiagnosticReporter.CreateError(
            "MOCK004",
            "Parent type has the wrong role",
            "{0} '{1}' hangs off '{2}' ({3}). Expected {4}.",
            Category);

        public static readonly DiagnosticDescriptor CircularParent = DiagnosticReporter.CreateError(
            "MOCK005",
            "Parent chain is circular",
            "{0} '{1}' is part of a loop of parents, so it never reaches a composite",
            Category);

        public static readonly DiagnosticDescriptor DuplicateName = DiagnosticReporter.CreateError(
            "MOCK006",
            "Name is already taken",
            "More than one child of '{0}' is named '{1}'",
            Category);

        public static readonly DiagnosticDescriptor NotImplemented = DiagnosticReporter.CreateError(
            "MOCK007",
            "Implementor does not satisfy its component contract",
            "Implementor '{0}' is registered for '{1}' but does not implement or inherit from it",
            Category);

        public static readonly DiagnosticDescriptor ReservedName = DiagnosticReporter.CreateError(
            "MOCK008",
            "Name is reserved",
            "Implementor '{0}' cannot be named '{1}', because the generated enum keeps that name for its unselected value",
            Category);

        public static readonly DiagnosticDescriptor UnknownRootComposite = DiagnosticReporter.CreateError(
            "MOCK009",
            "Root names something that is not a composite",
            "This root claims '{0}', which is not a composite it can see. Reference the assembly that declares it, or remove it.",
            Category);

        public static readonly DiagnosticDescriptor CompositeNotAbstract = DiagnosticReporter.CreateError(
            "MOCK010",
            "Composite must be abstract",
            "Composite '{0}' must be declared abstract. The assembly that declares it cannot see the implementors, so the runnable subclass is generated in the root instead.",
            Category);

        public static readonly DiagnosticDescriptor NotMockable = DiagnosticReporter.CreateError(
            "MOCK011",
            "Component cannot be mocked",
            "No mock can be generated for this component because of member '{0}'. Implement that member in a partial mock class, or change the member so a do-nothing stub is valid.",
            Category);

        public static readonly DiagnosticDescriptor ForeignComponent = DiagnosticReporter.CreateError(
            "MOCK012",
            "Dependency belongs to another composite",
            "'{0}' asks for '{1}', which belongs to composite '{2}'. Composites do not share instances, so pass it through Dependencies instead.",
            Category);

        public static readonly DiagnosticDescriptor ImplementorNotConstructible = DiagnosticReporter.CreateError(
            "MOCK013",
            "Implementor cannot be constructed",
            "Implementor '{0}' cannot be constructed by Mocker: {1}",
            Category);

        public static readonly DiagnosticDescriptor ComponentNotAbstract = DiagnosticReporter.CreateError(
            "MOCK014",
            "Component class must be abstract",
            "Component '{0}' must be declared as an interface or an abstract class",
            Category);

        public static readonly DiagnosticDescriptor AbstractComponentMissingParameterlessConstructor = DiagnosticReporter.CreateError(
            "MOCK015",
            "Abstract component requires a parameterless constructor for its mock",
            "Abstract component '{0}' has no accessible parameterless constructor for generated mock '{1}'. Add a protected parameterless constructor, or declare a public parameterless constructor in partial class '{1}'.",
            Category);
    }
}
