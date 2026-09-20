using GeneratorLibrary.Common;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace GeneratorLibrary.Mocker
{
    /// <summary>
    /// What the generated factory needs to know about an implementor beyond where it sits in the tree:
    /// what its constructor asks for, whether it initializes asynchronously, and whether it needs disposing.
    /// </summary>
    public readonly struct ImplementorDetails : IEquatable<ImplementorDetails>
    {
        private readonly ImmutableArray<string> _parameterTypes;
        private readonly string _awaitableType;

        public ImplementorDetails(ImmutableArray<string> parameterTypes, string awaitableType, bool isDisposable)
        {
            _parameterTypes = parameterTypes;
            _awaitableType = awaitableType;
            IsDisposable = isDisposable;
        }

        /// <summary>
        /// Constructor parameter types, fully qualified, in declaration order.
        /// </summary>
        public ImmutableArray<string> ParameterTypes
        {
            get { return _parameterTypes.IsDefault ? ImmutableArray<string>.Empty : _parameterTypes; }
        }

        /// <summary>
        /// What its InitializeAsync returns, or an empty string when it has none.
        /// </summary>
        public string AwaitableType
        {
            get { return _awaitableType ?? string.Empty; }
        }

        /// <summary>
        /// Whether it should be disposed when the composite is.
        /// </summary>
        public bool IsDisposable { get; }

        /// <summary>
        /// Whether it needs initializing before use.
        /// </summary>
        public bool IsAsyncInitializable
        {
            get { return AwaitableType.Length != 0; }
        }

        /// <summary>
        /// Reads what generation needs off an implementor, whether it is declared here or came from metadata.
        /// </summary>
        /// <param name="implementor">The concrete class.</param>
        /// <returns>Its constructor and lifecycle facts.</returns>
        public static ImplementorDetails From(INamedTypeSymbol implementor)
        {
            if (implementor == null)
            {
                return default(ImplementorDetails);
            }

            ImmutableArray<string>.Builder parameters = ImmutableArray.CreateBuilder<string>();
            IMethodSymbol constructor = implementor.Constructors
                .Where(candidate => !candidate.IsStatic && candidate.DeclaredAccessibility == Accessibility.Public)
                .OrderByDescending(candidate => candidate.Parameters.Length)
                .FirstOrDefault();

            if (constructor != null)
            {
                foreach (IParameterSymbol parameter in constructor.Parameters)
                {
                    parameters.Add(parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                }
            }

            string awaitableType = string.Empty;
            bool isDisposable = false;

            foreach (INamedTypeSymbol implemented in implementor.AllInterfaces)
            {
                bool isAsyncContract = implemented.Name == "IAsyncInitializable"
                    && implemented.TypeArguments.Length == 1
                    && implemented.ContainingNamespace != null
                    && implemented.ContainingNamespace.ToDisplayString() == "reromanlee.Mocker";

                if (isAsyncContract)
                {
                    awaitableType = implemented.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                }
                else if (implemented.ToDisplayString() == "System.IDisposable")
                {
                    isDisposable = true;
                }
            }

            return new ImplementorDetails(parameters.ToImmutable(), awaitableType, isDisposable);
        }

        public bool Equals(ImplementorDetails other)
        {
            if (IsDisposable != other.IsDisposable
                || !string.Equals(AwaitableType, other.AwaitableType, StringComparison.Ordinal)
                || ParameterTypes.Length != other.ParameterTypes.Length)
            {
                return false;
            }

            for (int index = 0; index < ParameterTypes.Length; index++)
            {
                if (!string.Equals(ParameterTypes[index], other.ParameterTypes[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is ImplementorDetails other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = AwaitableType.GetHashCode();
                hash = (hash * 397) ^ (IsDisposable ? 1 : 0);

                foreach (string parameter in ParameterTypes)
                {
                    hash = (hash * 397) ^ parameter.GetHashCode();
                }

                return hash;
            }
        }
    }
}
