using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Threading;

namespace GeneratorLibrary.Common
{
    /// <summary>
    /// Finds every type in the compiled assembly that carries a given attribute.
    /// </summary>
    public static class AttributeFinder
    {
        /// <summary>
        /// Finds all types marked with the attribute and converts each one with a custom transform.
        /// </summary>
        /// <typeparam name="T">Cacheable value the transform produces.</typeparam>
        /// <param name="context">Context the search runs in.</param>
        /// <param name="attributeMetadataName">Full metadata name of the attribute, such as "reromanlee.Mocker.CompositeAttribute".</param>
        /// <param name="transform">Reads what generation needs off every match.</param>
        /// <returns>Provider that only re-runs for the types that actually changed.</returns>
        public static IncrementalValuesProvider<T> FindAllByAttribute<T>(this IncrementalGeneratorInitializationContext context, string attributeMetadataName, Func<GeneratorAttributeSyntaxContext, CancellationToken, T> transform)
        {
            return context.SyntaxProvider.ForAttributeWithMetadataName(
                attributeMetadataName,
                predicate: (node, _) => node is ClassDeclarationSyntax,
                transform: transform);
        }

        /// <summary>
        /// Finds all types marked with the attribute and describes each one as a <see cref="TypeTarget"/>.
        /// </summary>
        /// <param name="context">Context the search runs in.</param>
        /// <param name="attributeMetadataName">Full metadata name of the attribute, such as "reromanlee.Mocker.CompositeAttribute".</param>
        /// <returns>Provider of every marked type, each one knowing the assembly it belongs to.</returns>
        public static IncrementalValuesProvider<TypeTarget> FindAllByAttribute(this IncrementalGeneratorInitializationContext context, string attributeMetadataName)
        {
            return context.FindAllByAttribute(attributeMetadataName, (attributeContext, _) => TypeTarget.From((INamedTypeSymbol)attributeContext.TargetSymbol));
        }

        /// <summary>
        /// Keeps only the types declared in the named assembly, for generation that belongs to one assembly alone.
        /// </summary>
        /// <param name="targets">Types to filter.</param>
        /// <param name="assemblyName">Assembly name to keep, such as an .asmdef name.</param>
        /// <returns>Provider of the types that belong to that assembly.</returns>
        public static IncrementalValuesProvider<TypeTarget> InAssembly(this IncrementalValuesProvider<TypeTarget> targets, string assemblyName)
        {
            return targets.Where(target => target.IsInAssembly(assemblyName));
        }
    }
}
