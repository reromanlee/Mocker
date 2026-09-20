using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;

namespace GeneratorLibrary.Common
{
    /// <summary>
    /// Joins several searches together, so generation can look at everything that was found at once.
    /// </summary>
    public static class ProviderExtensions
    {
        /// <summary>
        /// Gathers every provider into one batch, which is what generation that links types to each other needs.
        /// </summary>
        /// <typeparam name="T">Value the providers carry.</typeparam>
        /// <param name="providers">Searches to gather, such as one per attribute.</param>
        /// <returns>Provider of everything the searches found.</returns>
        public static IncrementalValueProvider<ImmutableArray<T>> CollectAll<T>(params IncrementalValuesProvider<T>[] providers)
        {
            if (providers == null || providers.Length == 0)
            {
                throw new ArgumentException("At least one provider is required.", nameof(providers));
            }

            IncrementalValueProvider<ImmutableArray<T>> collected = providers[0].Collect();

            for (int index = 1; index < providers.Length; index++)
            {
                collected = collected.Combine(providers[index].Collect()).Select((batch, _) => batch.Left.AddRange(batch.Right));
            }

            return collected;
        }
    }
}
