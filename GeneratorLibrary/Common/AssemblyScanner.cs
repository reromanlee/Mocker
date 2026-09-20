using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace GeneratorLibrary.Common
{
    /// <summary>
    /// Reads assembly level attributes off everything the compiled assembly references. This is how a
    /// generator learns about types in other assemblies without walking all of their types.
    /// </summary>
    public static class AssemblyScanner
    {
        /// <summary>
        /// Whether the assembly being compiled carries the given assembly level attribute.
        /// </summary>
        /// <param name="compilation">Compilation to check.</param>
        /// <param name="attributeMetadataName">Full metadata name of the attribute to look for.</param>
        /// <returns>True when this assembly is marked with it.</returns>
        public static bool HasAssemblyAttribute(Compilation compilation, string attributeMetadataName)
        {
            INamedTypeSymbol attributeType = compilation.GetTypeByMetadataName(attributeMetadataName);

            if (attributeType == null)
            {
                return false;
            }

            foreach (AttributeData attribute in compilation.Assembly.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds every assembly level attribute of the given kind on the referenced assemblies.
        /// The assembly being compiled is not included, since its own types are found from source.
        /// </summary>
        /// <param name="compilation">Compilation whose references are read.</param>
        /// <param name="attributeMetadataName">Full metadata name of the attribute to look for.</param>
        /// <returns>Every matching attribute, in reference order.</returns>
        public static IEnumerable<AttributeData> FindAssemblyAttributes(Compilation compilation, string attributeMetadataName)
        {
            INamedTypeSymbol attributeType = compilation.GetTypeByMetadataName(attributeMetadataName);

            if (attributeType == null)
            {
                yield break;
            }

            foreach (IAssemblySymbol assembly in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                foreach (AttributeData attribute in assembly.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
                    {
                        yield return attribute;
                    }
                }
            }
        }
    }
}
