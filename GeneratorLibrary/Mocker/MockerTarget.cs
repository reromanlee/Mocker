using GeneratorLibrary.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Linq;
using System.Threading;

namespace GeneratorLibrary.Mocker
{
    /// <summary>
    /// One detected attribute, flattened into the values the tree is built and checked from: what the type
    /// is, the name it is exposed under, the type it hangs off, and where it was written.
    /// </summary>
    public readonly struct MockerTarget : IEquatable<MockerTarget>
    {
        private readonly string _name;
        private readonly string _parentFullName;

        public MockerTarget(TypeTarget type, MockerRole role, string name, string parentFullName, ScriptLocation location, bool isPartial, bool implementsParent, bool isExternal)
        {
            Type = type;
            Role = role;
            _name = name;
            _parentFullName = parentFullName;
            Location = location;
            IsPartial = isPartial;
            ImplementsParent = implementsParent;
            IsExternal = isExternal;
        }

        /// <summary>
        /// The type the attribute was written on.
        /// </summary>
        public TypeTarget Type { get; }

        /// <summary>
        /// What the type is within a composite.
        /// </summary>
        public MockerRole Role { get; }

        /// <summary>
        /// Where the attribute was written, so problems can be reported against it.
        /// </summary>
        public ScriptLocation Location { get; }

        /// <summary>
        /// Whether the type is declared partial, which it must be to receive generated code.
        /// </summary>
        public bool IsPartial { get; }

        /// <summary>
        /// For an implementor, whether it really implements the interface it was pointed at.
        /// Always true for the other roles, which have nothing to implement.
        /// </summary>
        public bool ImplementsParent { get; }

        /// <summary>
        /// Whether this was published by another assembly rather than declared here. External targets are
        /// not checked again, because the generator already checked them where they were written.
        /// </summary>
        public bool IsExternal { get; }

        /// <summary>
        /// Name this is exposed under in the generated API. Nodes and components take it from their
        /// attribute, composites and implementors use their own type name.
        /// </summary>
        public string Name
        {
            get { return _name ?? string.Empty; }
        }

        /// <summary>
        /// Full name of the type this hangs off: the parent for a node or component, the implemented
        /// interface for an implementor, and nothing for a composite.
        /// </summary>
        public string ParentFullName
        {
            get { return _parentFullName ?? string.Empty; }
        }

        /// <summary>
        /// Whether a parent was written at all, so an empty or misspelled typeof() can be told apart.
        /// </summary>
        public bool HasParent
        {
            get { return ParentFullName.Length != 0; }
        }

        /// <summary>
        /// Reads a [Composite] class.
        /// </summary>
        public static MockerTarget FromComposite(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            TypeTarget type = GetDeclaredType(context);

            return new MockerTarget(type, MockerRole.Composite, type.Name, string.Empty, GetLocation(context), IsDeclaredPartial(context), true, false);
        }

        /// <summary>
        /// Reads a [Node(name, parentType)] class.
        /// </summary>
        public static MockerTarget FromNode(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            return FromNamedChild(context, MockerRole.Node);
        }

        /// <summary>
        /// Reads a [Component(name, parentType)] interface.
        /// </summary>
        public static MockerTarget FromComponent(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            return FromNamedChild(context, MockerRole.Component);
        }

        /// <summary>
        /// Reads an [Implementor(interfaceType)] class, which hangs off the component it implements.
        /// </summary>
        public static MockerTarget FromImplementor(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            TypeTarget type = GetDeclaredType(context);
            INamedTypeSymbol implemented = GetAttribute(context).GetTypeSymbolArgument(0);
            INamedTypeSymbol declared = context.TargetSymbol as INamedTypeSymbol;

            bool implementsParent = implemented != null
                && declared != null
                && declared.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, implemented));

            return new MockerTarget(type, MockerRole.Implementor, type.Name, TypeTarget.From(implemented).FullName, GetLocation(context), true, implementsParent, false);
        }

        /// <summary>
        /// Reads a type published by another assembly through its generated assembly attribute.
        /// </summary>
        /// <param name="attribute">The published attribute, holding the role, the type, its name and its parent.</param>
        /// <param name="target">The published type, marked as coming from outside this assembly.</param>
        /// <returns>True when the attribute named a role this version understands.</returns>
        public static bool TryFromExport(AttributeData attribute, out MockerTarget target)
        {
            target = default(MockerTarget);

            MockerRole role;

            if (!Enum.TryParse(attribute.GetStringArgument(0), false, out role))
            {
                return false;
            }

            TypeTarget type = attribute.GetTypeArgument(1);
            string name = attribute.GetStringArgument(2);
            TypeTarget parent = attribute.GetTypeArgument(3);

            target = new MockerTarget(type, role, name.Length == 0 ? type.Name : name, parent.FullName, default(ScriptLocation), true, true, true);

            return !type.IsEmpty;
        }

        private static MockerTarget FromNamedChild(GeneratorAttributeSyntaxContext context, MockerRole role)
        {
            TypeTarget type = GetDeclaredType(context);
            AttributeData attribute = GetAttribute(context);
            string name = attribute.GetStringArgument(0);
            TypeTarget parent = attribute.GetTypeArgument(1);

            return new MockerTarget(type, role, name.Length == 0 ? type.Name : name, parent.FullName, GetLocation(context), IsDeclaredPartial(context), true, false);
        }

        private static TypeTarget GetDeclaredType(GeneratorAttributeSyntaxContext context)
        {
            return TypeTarget.From(context.TargetSymbol as INamedTypeSymbol);
        }

        private static AttributeData GetAttribute(GeneratorAttributeSyntaxContext context)
        {
            return context.Attributes.Length == 0 ? null : context.Attributes[0];
        }

        private static ScriptLocation GetLocation(GeneratorAttributeSyntaxContext context)
        {
            AttributeData attribute = GetAttribute(context);

            if (attribute != null && attribute.ApplicationSyntaxReference != null)
            {
                return ScriptLocation.From(attribute.ApplicationSyntaxReference);
            }

            return ScriptLocation.From(context.TargetNode);
        }

        private static bool IsDeclaredPartial(GeneratorAttributeSyntaxContext context)
        {
            TypeDeclarationSyntax declaration = context.TargetNode as TypeDeclarationSyntax;

            return declaration != null && declaration.Modifiers.Any(SyntaxKind.PartialKeyword);
        }

        public bool Equals(MockerTarget other)
        {
            return Type.Equals(other.Type)
                && Role == other.Role
                && string.Equals(Name, other.Name, StringComparison.Ordinal)
                && string.Equals(ParentFullName, other.ParentFullName, StringComparison.Ordinal)
                && Location.Equals(other.Location)
                && IsPartial == other.IsPartial
                && ImplementsParent == other.ImplementsParent
                && IsExternal == other.IsExternal;
        }

        public override bool Equals(object obj)
        {
            return obj is MockerTarget other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Type.GetHashCode();
                hash = (hash * 397) ^ (int)Role;
                hash = (hash * 397) ^ Name.GetHashCode();
                hash = (hash * 397) ^ ParentFullName.GetHashCode();
                hash = (hash * 397) ^ Location.GetHashCode();
                return hash;
            }
        }
    }
}
