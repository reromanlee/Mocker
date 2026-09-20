using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace GeneratorLibrary.Mocker
{
    /// <summary>
    /// Resolves the parent written on each attribute into the API path it describes, such as
    /// Tools.Ads.Interstitial, by walking from a type up to the composite at the root of its tree.
    /// </summary>
    internal sealed class MockerTree
    {
        private readonly Dictionary<string, MockerTarget> _targetsByFullName;

        public MockerTree(ImmutableArray<MockerTarget> targets)
        {
            _targetsByFullName = new Dictionary<string, MockerTarget>(StringComparer.Ordinal);

            foreach (MockerTarget target in targets)
            {
                _targetsByFullName[target.Type.FullName] = target;
            }
        }

        /// <summary>
        /// Looks up the type a target hangs off, which is what checking the pairing needs.
        /// </summary>
        /// <param name="target">Type whose parent is wanted.</param>
        /// <param name="parent">The parent, when it carries a Mocker attribute.</param>
        /// <returns>True when a parent was written and found.</returns>
        public bool TryGetParent(MockerTarget target, out MockerTarget parent)
        {
            parent = default(MockerTarget);

            return target.HasParent && _targetsByFullName.TryGetValue(target.ParentFullName, out parent);
        }

        /// <summary>
        /// Follows a type up to the composite it belongs to.
        /// </summary>
        /// <param name="target">Type to resolve.</param>
        /// <param name="path">Path it is reached by, such as "Tools.Ads.Interstitial".</param>
        /// <param name="rootFullName">Full name of the composite at the root.</param>
        /// <returns>Whether the chain resolved, and what stopped it when it did not.</returns>
        public MockerStatus Resolve(MockerTarget target, out string path, out string rootFullName)
        {
            path = string.Empty;
            rootFullName = string.Empty;

            List<string> segments = new List<string>();
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            MockerTarget current = target;

            while (true)
            {
                if (!visited.Add(current.Type.FullName))
                {
                    return MockerStatus.Cycle;
                }

                segments.Add(current.Name);

                if (current.Role == MockerRole.Composite)
                {
                    segments.Reverse();
                    path = string.Join(".", segments);
                    rootFullName = current.Type.FullName;

                    return MockerStatus.Resolved;
                }

                if (!current.HasParent)
                {
                    return MockerStatus.MissingParent;
                }

                MockerTarget parent;

                if (!_targetsByFullName.TryGetValue(current.ParentFullName, out parent))
                {
                    return MockerStatus.UnknownParent;
                }

                current = parent;
            }
        }
    }
}
