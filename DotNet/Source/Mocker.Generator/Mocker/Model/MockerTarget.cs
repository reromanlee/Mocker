using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using reromanlee.Mocker.Generator.Common;
using System;
using System.Linq;
using System.Threading;

namespace reromanlee.Mocker.Generator.Mocker
{
    /// <summary>
    /// Equatable value record for a composite, node, component, or implementor discovered in
    /// local syntax or imported from a referenced assembly's <c>[MockerExport]</c> attributes.
    /// </summary>
    public readonly struct MockerTarget : IEquatable<MockerTarget>
    {
        private readonly string _name;
        private readonly string _parentFullName;

        public MockerTarget(
            TypeTarget type,
            MockerRole role,
            string name,
            string parentFullName,
            ScriptLocation location,
            bool isPartial,
            bool implementsParent,
            bool isExternal,
            bool isAbstract,
            bool isInterface,
            ImplementorDetails details)
        {
            Type = type;
            Role = role;
            _name = name;
            _parentFullName = parentFullName;
            Location = location;
            IsPartial = isPartial;
            ImplementsParent = implementsParent;
            IsExternal = isExternal;
            IsAbstract = isAbstract;
            IsInterface = isInterface;
            Details = details;
        }

        public TypeTarget Type { get; }

        public MockerRole Role { get; }

        public ScriptLocation Location { get; }

        public bool IsPartial { get; }

        public bool ImplementsParent { get; }

        public bool IsExternal { get; }

        public bool IsAbstract { get; }

        public bool IsInterface { get; }

        public ImplementorDetails Details { get; }

        public string Name
        {
            get { return _name ?? string.Empty; }
        }

        public string ParentFullName
        {
            get { return _parentFullName ?? string.Empty; }
        }

        public bool HasParent
        {
            get { return ParentFullName.Length != 0; }
        }

        public static MockerTarget FromComposite(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            TypeTarget type = GetDeclaredType(context);

            return new MockerTarget(
                type,
                MockerRole.Composite,
                type.Name,
                string.Empty,
                GetLocation(context),
                IsDeclaredPartial(context),
                true,
                false,
                IsDeclaredAbstract(context),
                false,
                default(ImplementorDetails));
        }

        public static MockerTarget FromNode(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            return FromNamedChild(context, MockerRole.Node);
        }

        public static MockerTarget FromComponent(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            return FromNamedChild(context, MockerRole.Component);
        }

        public static MockerTarget FromImplementor(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            TypeTarget type = GetDeclaredType(context);
            INamedTypeSymbol targetComponent = GetAttribute(context).GetTypeSymbolArgument(0);
            INamedTypeSymbol declared = context.TargetSymbol as INamedTypeSymbol;
            bool implementsParent = SatisfiesComponentContract(declared, targetComponent);

            return new MockerTarget(
                type,
                MockerRole.Implementor,
                type.Name,
                TypeTarget.From(targetComponent).FullName,
                GetLocation(context),
                true,
                implementsParent,
                false,
                false,
                false,
                ImplementorDetails.From(declared));
        }

        public static bool TryFromExport(AttributeData attribute, out MockerTarget target)
        {
            target = default(MockerTarget);

            MockerRole role;

            if (!Enum.TryParse(attribute.GetStringArgument(0), false, out role))
            {
                return false;
            }

            INamedTypeSymbol symbol = attribute.GetTypeSymbolArgument(1);
            TypeTarget type = TypeTarget.From(symbol);
            string name = attribute.GetStringArgument(2);
            TypeTarget parent = attribute.GetTypeArgument(3);
            bool isInterface = symbol != null && symbol.TypeKind == TypeKind.Interface;
            bool isAbstract = symbol != null && symbol.IsAbstract && !symbol.IsStatic;
            ImplementorDetails details = role == MockerRole.Implementor ? ImplementorDetails.From(symbol) : default(ImplementorDetails);

            target = new MockerTarget(
                type,
                role,
                name.Length == 0 ? type.Name : name,
                parent.FullName,
                default(ScriptLocation),
                true,
                true,
                true,
                isAbstract,
                isInterface,
                details);

            return !type.IsEmpty;
        }

        private static bool SatisfiesComponentContract(INamedTypeSymbol declared, INamedTypeSymbol component)
        {
            if (declared == null || component == null)
            {
                return false;
            }

            if (component.TypeKind == TypeKind.Interface)
            {
                return declared.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, component));
            }

            if (component.TypeKind == TypeKind.Class)
            {
                for (INamedTypeSymbol baseType = declared.BaseType; baseType != null; baseType = baseType.BaseType)
                {
                    if (SymbolEqualityComparer.Default.Equals(baseType, component))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static MockerTarget FromNamedChild(GeneratorAttributeSyntaxContext context, MockerRole role)
        {
            INamedTypeSymbol symbol = context.TargetSymbol as INamedTypeSymbol;
            TypeTarget type = TypeTarget.From(symbol);
            AttributeData attribute = GetAttribute(context);
            string name = attribute.GetStringArgument(0);
            TypeTarget parent = attribute.GetTypeArgument(1);
            bool isInterface = symbol != null && symbol.TypeKind == TypeKind.Interface;
            bool isAbstract = symbol != null && symbol.IsAbstract && !symbol.IsStatic;

            return new MockerTarget(
                type,
                role,
                name.Length == 0 ? type.Name : name,
                parent.FullName,
                GetLocation(context),
                IsDeclaredPartial(context),
                true,
                false,
                isAbstract,
                isInterface,
                default(ImplementorDetails));
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

        private static bool IsDeclaredAbstract(GeneratorAttributeSyntaxContext context)
        {
            INamedTypeSymbol symbol = context.TargetSymbol as INamedTypeSymbol;

            return symbol != null && symbol.IsAbstract && !symbol.IsStatic;
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
                && IsExternal == other.IsExternal
                && IsAbstract == other.IsAbstract
                && IsInterface == other.IsInterface
                && Details.Equals(other.Details);
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
                hash = (hash * 397) ^ (IsPartial ? 1 : 0);
                hash = (hash * 397) ^ (ImplementsParent ? 1 : 0);
                hash = (hash * 397) ^ (IsExternal ? 1 : 0);
                hash = (hash * 397) ^ (IsAbstract ? 1 : 0);
                hash = (hash * 397) ^ (IsInterface ? 1 : 0);
                hash = (hash * 397) ^ Details.GetHashCode();
                return hash;
            }
        }
    }
}
