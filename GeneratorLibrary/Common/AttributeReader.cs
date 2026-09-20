using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace GeneratorLibrary.Common
{
    /// <summary>
    /// Reads the arguments written on an attribute, returning empty values instead of throwing when an
    /// argument is missing or was written with the wrong type.
    /// </summary>
    public static class AttributeReader
    {
        /// <summary>
        /// Reads a string argument, such as the name a node or component is exposed under.
        /// </summary>
        /// <param name="attribute">Attribute the argument was written on.</param>
        /// <param name="index">Position of the argument in the constructor.</param>
        /// <returns>The string, or an empty string when it is missing.</returns>
        public static string GetStringArgument(this AttributeData attribute, int index)
        {
            return GetArgument(attribute, index).Value as string ?? string.Empty;
        }

        /// <summary>
        /// Reads a type argument, such as the parent a node or component is attached to.
        /// </summary>
        /// <param name="attribute">Attribute the argument was written on.</param>
        /// <param name="index">Position of the argument in the constructor.</param>
        /// <returns>The type it points at, or an empty target when it is missing.</returns>
        public static TypeTarget GetTypeArgument(this AttributeData attribute, int index)
        {
            return TypeTarget.From(GetArgument(attribute, index).Value as INamedTypeSymbol);
        }

        private static TypedConstant GetArgument(AttributeData attribute, int index)
        {
            if (attribute == null)
            {
                return default(TypedConstant);
            }

            ImmutableArray<TypedConstant> arguments = attribute.ConstructorArguments;

            return index >= 0 && index < arguments.Length ? arguments[index] : default(TypedConstant);
        }
    }
}
