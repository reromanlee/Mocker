using GeneratorLibrary.Common;
using System;
using System.Collections.Immutable;

namespace GeneratorLibrary.Mocker
{
    /// <summary>
    /// What this assembly learned from the assemblies it references, and whether it is a place where the
    /// wiring should be generated.
    /// </summary>
    public readonly struct MockerImports : IEquatable<MockerImports>
    {
        private readonly ImmutableArray<MockerTarget> _targets;
        private readonly ImmutableArray<string> _rootComposites;

        public MockerImports(ImmutableArray<MockerTarget> targets, bool isRoot, ImmutableArray<string> rootComposites, ScriptLocation rootLocation)
        {
            _targets = targets;
            _rootComposites = rootComposites;
            IsRoot = isRoot;
            RootLocation = rootLocation;
        }

        /// <summary>
        /// Types published by referenced assemblies.
        /// </summary>
        public ImmutableArray<MockerTarget> Targets
        {
            get { return _targets.IsDefault ? ImmutableArray<MockerTarget>.Empty : _targets; }
        }

        /// <summary>
        /// Composites this root said it owns. Empty means it takes every composite it can see.
        /// </summary>
        public ImmutableArray<string> RootComposites
        {
            get { return _rootComposites.IsDefault ? ImmutableArray<string>.Empty : _rootComposites; }
        }

        /// <summary>
        /// Whether this assembly carries the root marker.
        /// </summary>
        public bool IsRoot { get; }

        /// <summary>
        /// Where the root marker was written, so problems with it can be reported against it.
        /// </summary>
        public ScriptLocation RootLocation { get; }

        /// <summary>
        /// Whether a composite belongs to this root, which every composite does when none were named.
        /// </summary>
        /// <param name="compositeFullName">Full name of the composite to check.</param>
        /// <returns>True when this root should generate for it.</returns>
        public bool Owns(string compositeFullName)
        {
            return RootComposites.Length == 0 || RootComposites.Contains(compositeFullName);
        }

        public bool Equals(MockerImports other)
        {
            if (IsRoot != other.IsRoot || !RootLocation.Equals(other.RootLocation))
            {
                return false;
            }

            if (Targets.Length != other.Targets.Length || RootComposites.Length != other.RootComposites.Length)
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

            for (int index = 0; index < RootComposites.Length; index++)
            {
                if (!string.Equals(RootComposites[index], other.RootComposites[index], StringComparison.Ordinal))
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

                foreach (string composite in RootComposites)
                {
                    hash = (hash * 397) ^ composite.GetHashCode();
                }

                return hash;
            }
        }
    }
}
