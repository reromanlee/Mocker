using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace reromanlee.Mocker.Generator.Mocker
{
    /// <summary>
    /// Constructor resolution state for a candidate implementor type.
    /// </summary>
    public enum ImplementorConstructorStatus
    {
        Valid = 0,
        AbstractOrStatic = 1,
        NoPublicConstructor = 2,
        AmbiguousConstructor = 3
    }

    /// <summary>
    /// Constructor signature and lifecycle contracts extracted from an implementor type.
    /// </summary>
    public readonly struct ImplementorDetails : IEquatable<ImplementorDetails>
    {
        private readonly ImmutableArray<string> _parameterTypes;
        private readonly string _awaitableType;

        public ImplementorDetails(
            ImmutableArray<string> parameterTypes,
            string awaitableType,
            bool isDisposable,
            ImplementorConstructorStatus constructorStatus)
        {
            _parameterTypes = parameterTypes;
            _awaitableType = awaitableType;
            IsDisposable = isDisposable;
            ConstructorStatus = constructorStatus;
        }

        public ImmutableArray<string> ParameterTypes
        {
            get { return _parameterTypes.IsDefault ? ImmutableArray<string>.Empty : _parameterTypes; }
        }

        public string AwaitableType
        {
            get { return _awaitableType ?? string.Empty; }
        }

        public bool IsDisposable { get; }

        public ImplementorConstructorStatus ConstructorStatus { get; }

        public bool IsAsyncInitializable
        {
            get { return AwaitableType.Length != 0; }
        }

        public static ImplementorDetails From(INamedTypeSymbol implementor)
        {
            if (implementor == null)
            {
                return default(ImplementorDetails);
            }

            ImplementorConstructorStatus constructorStatus = ImplementorConstructorStatus.Valid;
            ImmutableArray<string>.Builder parameters = ImmutableArray.CreateBuilder<string>();

            if (implementor.IsAbstract || implementor.IsStatic)
            {
                constructorStatus = ImplementorConstructorStatus.AbstractOrStatic;
            }
            else
            {
                List<IMethodSymbol> publicConstructors = new List<IMethodSymbol>();
                int maxParameterCount = -1;

                foreach (IMethodSymbol candidate in implementor.InstanceConstructors)
                {
                    if (candidate.IsStatic || candidate.DeclaredAccessibility != Accessibility.Public)
                    {
                        continue;
                    }

                    publicConstructors.Add(candidate);

                    if (candidate.Parameters.Length > maxParameterCount)
                    {
                        maxParameterCount = candidate.Parameters.Length;
                    }
                }

                if (publicConstructors.Count == 0)
                {
                    constructorStatus = ImplementorConstructorStatus.NoPublicConstructor;
                }
                else
                {
                    IMethodSymbol selected = null;
                    int tiedCount = 0;

                    foreach (IMethodSymbol candidate in publicConstructors)
                    {
                        if (candidate.Parameters.Length == maxParameterCount)
                        {
                            selected = candidate;
                            tiedCount++;
                        }
                    }

                    if (tiedCount > 1)
                    {
                        constructorStatus = ImplementorConstructorStatus.AmbiguousConstructor;
                    }
                    else if (selected != null)
                    {
                        foreach (IParameterSymbol parameter in selected.Parameters)
                        {
                            parameters.Add(parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                        }
                    }
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

            return new ImplementorDetails(parameters.ToImmutable(), awaitableType, isDisposable, constructorStatus);
        }

        public bool Equals(ImplementorDetails other)
        {
            if (IsDisposable != other.IsDisposable
                || ConstructorStatus != other.ConstructorStatus
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
                hash = (hash * 397) ^ (int)ConstructorStatus;

                foreach (string parameter in ParameterTypes)
                {
                    hash = (hash * 397) ^ parameter.GetHashCode();
                }

                return hash;
            }
        }
    }
}
