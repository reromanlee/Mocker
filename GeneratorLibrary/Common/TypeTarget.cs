using Microsoft.CodeAnalysis;
using System;

namespace GeneratorLibrary.Common
{
    /// <summary>
    /// A type found by an attribute, reduced to plain values so the generator pipeline can cache it between runs.
    /// </summary>
    public readonly struct TypeTarget : IEquatable<TypeTarget>
    {
        private readonly string _assemblyName;
        private readonly string _namespace;
        private readonly string _name;

        public TypeTarget(string assemblyName, string namespaceName, string name)
        {
            _assemblyName = assemblyName;
            _namespace = namespaceName;
            _name = name;
        }

        /// <summary>
        /// Assembly that declares the type, which is also the assembly its generated script is created in.
        /// </summary>
        public string AssemblyName
        {
            get { return _assemblyName ?? string.Empty; }
        }

        /// <summary>
        /// Namespace of the type, or an empty string when it sits in the global namespace.
        /// </summary>
        public string Namespace
        {
            get { return _namespace ?? string.Empty; }
        }

        /// <summary>
        /// Name of the type without its namespace.
        /// </summary>
        public string Name
        {
            get { return _name ?? string.Empty; }
        }

        /// <summary>
        /// Whether the type sits in a namespace, so generated code knows if it needs a namespace block.
        /// </summary>
        public bool HasNamespace
        {
            get { return Namespace.Length != 0; }
        }

        /// <summary>
        /// Whether a type was found at all, so a missing or misspelled reference can be told apart from a real one.
        /// </summary>
        public bool IsEmpty
        {
            get { return Name.Length == 0; }
        }

        /// <summary>
        /// Name of the type including its namespace. Used to link attributes to each other and to name scripts.
        /// </summary>
        public string FullName
        {
            get { return HasNamespace ? Namespace + "." + Name : Name; }
        }

        /// <summary>
        /// Reads the values needed for generation off the symbol the attribute was found on.
        /// </summary>
        /// <param name="symbol">Type the attribute is applied to, or that a typeof() argument points at.</param>
        /// <returns>Cacheable description of the type, or an empty target when there is no symbol.</returns>
        public static TypeTarget From(INamedTypeSymbol symbol)
        {
            if (symbol == null)
            {
                return default(TypeTarget);
            }

            IAssemblySymbol assembly = symbol.ContainingAssembly;
            INamespaceSymbol containingNamespace = symbol.ContainingNamespace;
            bool isGlobal = containingNamespace == null || containingNamespace.IsGlobalNamespace;

            return new TypeTarget(
                assembly == null ? string.Empty : assembly.Name,
                isGlobal ? string.Empty : containingNamespace.ToDisplayString(),
                symbol.Name);
        }

        /// <summary>
        /// Whether the type is declared in the named assembly.
        /// </summary>
        /// <param name="assemblyName">Assembly name to compare against, such as an .asmdef name.</param>
        /// <returns>True when the type belongs to that assembly.</returns>
        public bool IsInAssembly(string assemblyName)
        {
            return string.Equals(AssemblyName, assemblyName, StringComparison.Ordinal);
        }

        public bool Equals(TypeTarget other)
        {
            return string.Equals(AssemblyName, other.AssemblyName, StringComparison.Ordinal)
                && string.Equals(Namespace, other.Namespace, StringComparison.Ordinal)
                && string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is TypeTarget other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = AssemblyName.GetHashCode();
                hash = (hash * 397) ^ Namespace.GetHashCode();
                hash = (hash * 397) ^ Name.GetHashCode();
                return hash;
            }
        }
    }
}
