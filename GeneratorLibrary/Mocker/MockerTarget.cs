using GeneratorLibrary.Common;
using Microsoft.CodeAnalysis;
using System;
using System.Threading;

namespace GeneratorLibrary.Mocker
{
    /// <summary>
    /// One detected attribute, flattened into the values the tree is built from: what the type is,
    /// the name it is exposed under, and the type it hangs off.
    /// </summary>
    public readonly struct MockerTarget : IEquatable<MockerTarget>
    {
        private readonly string _name;
        private readonly string _parentFullName;

        public MockerTarget(TypeTarget type, MockerRole role, string name, string parentFullName)
        {
            Type = type;
            Role = role;
            _name = name;
            _parentFullName = parentFullName;
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

            return new MockerTarget(type, MockerRole.Composite, type.Name, string.Empty);
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
            TypeTarget implemented = GetAttribute(context).GetTypeArgument(0);

            return new MockerTarget(type, MockerRole.Implementor, type.Name, implemented.FullName);
        }

        private static MockerTarget FromNamedChild(GeneratorAttributeSyntaxContext context, MockerRole role)
        {
            TypeTarget type = GetDeclaredType(context);
            AttributeData attribute = GetAttribute(context);
            string name = attribute.GetStringArgument(0);
            TypeTarget parent = attribute.GetTypeArgument(1);

            return new MockerTarget(type, role, name.Length == 0 ? type.Name : name, parent.FullName);
        }

        private static TypeTarget GetDeclaredType(GeneratorAttributeSyntaxContext context)
        {
            return TypeTarget.From(context.TargetSymbol as INamedTypeSymbol);
        }

        private static AttributeData GetAttribute(GeneratorAttributeSyntaxContext context)
        {
            return context.Attributes.Length == 0 ? null : context.Attributes[0];
        }

        public bool Equals(MockerTarget other)
        {
            return Type.Equals(other.Type)
                && Role == other.Role
                && string.Equals(Name, other.Name, StringComparison.Ordinal)
                && string.Equals(ParentFullName, other.ParentFullName, StringComparison.Ordinal);
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
                return hash;
            }
        }
    }
}
