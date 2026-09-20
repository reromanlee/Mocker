using GeneratorLibrary.Common;
using Microsoft.CodeAnalysis;

namespace GeneratorLibrary.Mocker
{
    /// <summary>
    /// Everything the generator can complain about. Each rule is reported against the attribute that
    /// caused it, so it lands in the Unity console and the IDE pointing at the exact line.
    /// </summary>
    public static class MockerDiagnostics
    {
        private const string Category = "Mocker";

        /// <summary>
        /// A type that receives generated code was not declared partial.
        /// </summary>
        public static readonly DiagnosticDescriptor NotPartial = DiagnosticReporter.CreateError(
            "MOCK001",
            "Type must be partial",
            "{0} '{1}' must be declared partial, otherwise Mocker cannot generate into it",
            Category);

        /// <summary>
        /// A node, component or implementor was written without the type it hangs off.
        /// </summary>
        public static readonly DiagnosticDescriptor MissingParent = DiagnosticReporter.CreateError(
            "MOCK002",
            "Parent type is missing",
            "{0} '{1}' does not say what it hangs off, so it has no place in a composite",
            Category);

        /// <summary>
        /// The type a node, component or implementor points at carries no Mocker attribute.
        /// </summary>
        public static readonly DiagnosticDescriptor UnknownParent = DiagnosticReporter.CreateError(
            "MOCK003",
            "Parent type is not part of a composite",
            "{0} '{1}' hangs off '{2}', which carries no Mocker attribute",
            Category);

        /// <summary>
        /// The type a node, component or implementor points at has the wrong role for that pairing.
        /// </summary>
        public static readonly DiagnosticDescriptor WrongParentRole = DiagnosticReporter.CreateError(
            "MOCK004",
            "Parent type has the wrong role",
            "{0} '{1}' hangs off '{2}' ({3}). Expected {4}.",
            Category);

        /// <summary>
        /// A chain of parents loops back on itself and never reaches a composite.
        /// </summary>
        public static readonly DiagnosticDescriptor CircularParent = DiagnosticReporter.CreateError(
            "MOCK005",
            "Parent chain is circular",
            "{0} '{1}' is part of a loop of parents, so it never reaches a composite",
            Category);

        /// <summary>
        /// Two children of the same parent claim the same name, which cannot be generated.
        /// </summary>
        public static readonly DiagnosticDescriptor DuplicateName = DiagnosticReporter.CreateError(
            "MOCK006",
            "Name is already taken",
            "More than one child of '{0}' is named '{1}'",
            Category);

        /// <summary>
        /// An implementor asked for a component belonging to a different composite.
        /// </summary>
        public static readonly DiagnosticDescriptor ForeignComponent = DiagnosticReporter.CreateError(
            "MOCK012",
            "Dependency belongs to another composite",
            "'{0}' asks for '{1}', which belongs to composite '{2}'. Composites do not share instances, so pass it through Dependencies instead.",
            Category);

        /// <summary>
        /// A component has a member no do-nothing implementation can satisfy.
        /// </summary>
        public static readonly DiagnosticDescriptor NotMockable = DiagnosticReporter.CreateError(
            "MOCK011",
            "Component cannot be mocked",
            "No mock can be generated for this component because of member '{0}'. Give it an implementor of its own, or change the member so that doing nothing is a valid answer.",
            Category);

        /// <summary>
        /// A composite was not declared abstract, so no runnable subclass can be generated for it.
        /// </summary>
        public static readonly DiagnosticDescriptor CompositeNotAbstract = DiagnosticReporter.CreateError(
            "MOCK010",
            "Composite must be abstract",
            "Composite '{0}' must be declared abstract. The assembly that declares it cannot see the implementors, so the runnable subclass is generated in the root instead.",
            Category);

        /// <summary>
        /// A root named something that is not a composite it can see.
        /// </summary>
        public static readonly DiagnosticDescriptor UnknownRootComposite = DiagnosticReporter.CreateError(
            "MOCK009",
            "Root names something that is not a composite",
            "This root claims '{0}', which is not a composite it can see. Reference the assembly that declares it, or remove it.",
            Category);

        /// <summary>
        /// An implementor claims the name the generated enum keeps for its unselected value.
        /// </summary>
        public static readonly DiagnosticDescriptor ReservedName = DiagnosticReporter.CreateError(
            "MOCK008",
            "Name is reserved",
            "Implementor '{0}' cannot be named '{1}', because the generated enum keeps that name for its unselected value",
            Category);

        /// <summary>
        /// An implementor was pointed at an interface it does not actually implement.
        /// </summary>
        public static readonly DiagnosticDescriptor NotImplemented = DiagnosticReporter.CreateError(
            "MOCK007",
            "Implementor does not implement its component",
            "Implementor '{0}' is registered for '{1}' but does not implement it",
            Category);
    }
}
