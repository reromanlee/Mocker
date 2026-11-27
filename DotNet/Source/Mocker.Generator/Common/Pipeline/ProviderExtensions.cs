using Microsoft.CodeAnalysis;
using System;

namespace reromanlee.Mocker.Generator.Common
{
    /// <summary>
    /// Joins incremental providers into a single value-equatable batch so downstream generation
    /// remains cached across unrelated syntax edits.
    /// </summary>
    public static class ProviderExtensions
    {
        /// <summary>
        /// Collects every provider into one <see cref="EquatableArray{T}"/> batch.
        /// </summary>
        /// <typeparam name="T">Equatable value carried by each provider.</typeparam>
        /// <param name="providers">Non-empty set of providers to merge.</param>
        /// <returns>Combined provider comparing collected sequences by element value.</returns>
        public static IncrementalValueProvider<EquatableArray<T>> CollectAll<T>(params IncrementalValuesProvider<T>[] providers)
            where T : IEquatable<T>
        {
            if (providers == null || providers.Length == 0)
            {
                throw new ArgumentException("At least one provider is required.", nameof(providers));
            }

            IncrementalValueProvider<EquatableArray<T>> collected = providers[0]
                .Collect()
                .Select((items, _) => new EquatableArray<T>(items));

            for (int index = 1; index < providers.Length; index++)
            {
                IncrementalValueProvider<EquatableArray<T>> next = providers[index]
                    .Collect()
                    .Select((items, _) => new EquatableArray<T>(items));

                collected = collected
                    .Combine(next)
                    .Select((batch, _) => batch.Left.AddRange(batch.Right));
            }

            return collected;
        }
    }
}
