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
        /// Finds an assembly level attribute on the assembly being compiled.
        /// </summary>
        /// <param name="compilation">Compilation to check.</param>
        /// <param name="attributeMetadataName">Full metadata name of the attribute to look for.</param>
        /// <returns>The attribute with its arguments, or null when this assembly is not marked with it.</returns>
        public static AttributeData FindAssemblyAttribute(Compilation compilation, string attributeMetadataName)
        {
            INamedTypeSymbol attributeType = compilation.GetTypeByMetadataName(attributeMetadataName);

            if (attributeType == null)
            {
                return null;
            }

            foreach (AttributeData attribute in compilation.Assembly.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
                {
                    return attribute;
                }
            }

            return null;
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
