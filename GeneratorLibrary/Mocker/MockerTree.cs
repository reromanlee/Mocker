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
        private const int MaxDepth = 32;

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
        /// Path the type is reached by, such as "Tools.Ads.Interstitial".
        /// </summary>
        /// <param name="target">Type to resolve.</param>
        /// <returns>The path, or an empty string when a parent is missing or the chain never reaches a composite.</returns>
        public string GetPath(MockerTarget target)
        {
            List<string> segments;

            if (!TryWalkToRoot(target, out segments, out _))
            {
                return string.Empty;
            }

            segments.Reverse();

            return string.Join(".", segments);
        }

        /// <summary>
        /// Full name of the composite the type belongs to, which is how output is grouped per composite.
        /// </summary>
        /// <param name="target">Type to resolve.</param>
        /// <returns>The composite full name, or an empty string when the type is not under one.</returns>
        public string GetRootFullName(MockerTarget target)
        {
            MockerTarget root;

            if (!TryWalkToRoot(target, out _, out root))
            {
                return string.Empty;
            }

            return root.Type.FullName;
        }

        private bool TryWalkToRoot(MockerTarget target, out List<string> segments, out MockerTarget root)
        {
            segments = new List<string>();
            root = target;

            MockerTarget current = target;

            for (int depth = 0; depth < MaxDepth; depth++)
            {
                segments.Add(current.Name);

                if (current.Role == MockerRole.Composite)
                {
                    root = current;
                    return true;
                }

                if (!current.HasParent || !_targetsByFullName.TryGetValue(current.ParentFullName, out current))
                {
                    return false;
                }
            }

            return false;
        }
    }
}
