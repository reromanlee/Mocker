using System;
using System.Collections.Immutable;

namespace GeneratorLibrary.Mocker
{
    /// <summary>
    /// What this assembly learned from the assemblies it references: the types they published, and
    /// whether this assembly is the one where the wiring should be generated.
    /// </summary>
    public readonly struct MockerImports : IEquatable<MockerImports>
    {
        private readonly ImmutableArray<MockerTarget> _targets;

        public MockerImports(ImmutableArray<MockerTarget> targets, bool isRoot)
        {
            _targets = targets;
            IsRoot = isRoot;
        }

        /// <summary>
        /// Types published by referenced assemblies.
        /// </summary>
        public ImmutableArray<MockerTarget> Targets
        {
            get { return _targets.IsDefault ? ImmutableArray<MockerTarget>.Empty : _targets; }
        }

        /// <summary>
        /// Whether this assembly carries the root marker.
        /// </summary>
        public bool IsRoot { get; }

        public bool Equals(MockerImports other)
        {
            if (IsRoot != other.IsRoot || Targets.Length != other.Targets.Length)
            {
                return false;
            }

            for (int index = 0; index < Targets.Length; index++)
            {
                if (!Targets[index].Equals(other.Targets[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is MockerImports other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = IsRoot ? 1 : 0;

                foreach (MockerTarget target in Targets)
                {
                    hash = (hash * 397) ^ target.GetHashCode();
                }

                return hash;
            }
        }
    }
}
