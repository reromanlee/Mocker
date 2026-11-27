using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace reromanlee.Mocker.Generator.Common
{
    /// <summary>
    /// Structural-equality wrapper around <see cref="ImmutableArray{T}"/> so Roslyn incremental
    /// generator steps compare collections by element value instead of underlying array reference.
    /// </summary>
    /// <typeparam name="T">Equatable value carried in the incremental pipeline.</typeparam>
    public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
        where T : IEquatable<T>
    {
        public static readonly EquatableArray<T> Empty = new EquatableArray<T>(ImmutableArray<T>.Empty);

        private readonly ImmutableArray<T> _items;

        public EquatableArray(ImmutableArray<T> items)
        {
            _items = items;
        }

        public int Length
        {
            get { return _items.IsDefault ? 0 : _items.Length; }
        }

        public bool IsEmpty
        {
            get { return Length == 0; }
        }

        public T this[int index]
        {
            get { return AsImmutableArray()[index]; }
        }

        public ImmutableArray<T> AsImmutableArray()
        {
            return _items.IsDefault ? ImmutableArray<T>.Empty : _items;
        }

        public EquatableArray<T> AddRange(EquatableArray<T> other)
        {
            if (IsEmpty)
            {
                return other;
            }

            if (other.IsEmpty)
            {
                return this;
            }

            return new EquatableArray<T>(AsImmutableArray().AddRange(other.AsImmutableArray()));
        }

        public ImmutableArray<T>.Enumerator GetEnumerator()
        {
            return AsImmutableArray().GetEnumerator();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)AsImmutableArray()).GetEnumerator();
        }

        public bool Equals(EquatableArray<T> other)
        {
            ImmutableArray<T> left = AsImmutableArray();
            ImmutableArray<T> right = other.AsImmutableArray();

            if (left.Length != right.Length)
            {
                return false;
            }

            for (int index = 0; index < left.Length; index++)
            {
                if (!left[index].Equals(right[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is EquatableArray<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            ImmutableArray<T> items = AsImmutableArray();

            unchecked
            {
                int hash = items.Length;

                for (int index = 0; index < items.Length; index++)
                {
                    hash = (hash * 397) ^ items[index].GetHashCode();
                }

                return hash;
            }
        }
    }
}
