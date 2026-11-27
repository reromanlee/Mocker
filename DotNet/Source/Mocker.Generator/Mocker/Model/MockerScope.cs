using reromanlee.Mocker.Generator.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace reromanlee.Mocker.Generator.Mocker
{
    /// <summary>
    /// Everything resolved under one composite with deterministic ordering and member naming helpers.
    /// </summary>
    internal sealed class MockerScope
    {
        private readonly Dictionary<string, string> _pathsByFullName = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<MockerTarget>> _implementorsByComponent = new Dictionary<string, List<MockerTarget>>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _initializationByComponent = new Dictionary<string, bool>(StringComparer.Ordinal);

        public MockerScope(MockerTarget composite, ImmutableArray<MockerTarget> all, MockerTree tree, HashSet<string> invalidTargets)
        {
            Composite = composite;
            Tree = tree;
            Branches = new List<MockerTarget>();
            Components = new List<MockerTarget>();

            foreach (MockerTarget target in all)
            {
                if (invalidTargets != null && invalidTargets.Contains(target.Type.FullName))
                {
                    continue;
                }

                string path;
                string rootFullName;

                if (tree.Resolve(target, out path, out rootFullName) != MockerStatus.Resolved)
                {
                    continue;
                }

                if (!string.Equals(rootFullName, composite.Type.FullName, StringComparison.Ordinal))
                {
                    continue;
                }

                _pathsByFullName[target.Type.FullName] = path;

                switch (target.Role)
                {
                    case MockerRole.Composite:
                    case MockerRole.Node:
                        Branches.Add(target);
                        break;

                    case MockerRole.Component:
                        Components.Add(target);
                        break;

                    case MockerRole.Implementor:
                        List<MockerTarget> implementors;

                        if (!_implementorsByComponent.TryGetValue(target.ParentFullName, out implementors))
                        {
                            implementors = new List<MockerTarget>();
                            _implementorsByComponent[target.ParentFullName] = implementors;
                        }

                        implementors.Add(target);
                        break;
                }
            }

            Branches.Sort((left, right) => string.CompareOrdinal(GetPath(left), GetPath(right)));
            Components.Sort((left, right) => string.CompareOrdinal(GetPath(left), GetPath(right)));

            foreach (List<MockerTarget> implementors in _implementorsByComponent.Values)
            {
                implementors.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            }
        }

        public MockerTarget Composite { get; }

        public MockerTree Tree { get; }

        public List<MockerTarget> Branches { get; }

        public List<MockerTarget> Components { get; }

        public string GetPath(MockerTarget target)
        {
            string path;

            return _pathsByFullName.TryGetValue(target.Type.FullName, out path) ? path : string.Empty;
        }

        public string GetId(MockerTarget target)
        {
            return GetPath(target).Replace(".", "_");
        }

        public string GetMemberId(MockerTarget target)
        {
            string path = GetPath(target);
            int firstSeparator = path.IndexOf('.');

            return firstSeparator < 0 ? path.Replace(".", "_") : path.Substring(firstSeparator + 1).Replace(".", "_");
        }

        public string GetFieldId(MockerTarget target)
        {
            string id = GetMemberId(target);

            return id.Length == 0 ? "_value" : "_" + char.ToLowerInvariant(id[0]) + id.Substring(1);
        }

        public string GetChoicesAccessor(MockerTarget target)
        {
            string path = GetPath(target);
            int firstSeparator = path.IndexOf('.');

            return firstSeparator < 0 ? "_choices" : "_choices." + path.Substring(firstSeparator + 1);
        }

        public string GetChoicesTypeName(MockerTarget branch)
        {
            return GetId(branch) + "Choices";
        }

        public string GetEnumType(MockerTarget component)
        {
            string name = ScriptNames.GetComponentStem(component.Type.Name, component.IsInterface) + "Implementor";
            string fullName = component.Type.HasNamespace ? component.Type.Namespace + "." + name : name;

            return ScriptNames.Qualified(fullName);
        }

        public string GetMockType(MockerTarget component)
        {
            string name = ScriptNames.GetComponentStem(component.Type.Name, component.IsInterface) + "Mock";
            string fullName = component.Type.HasNamespace ? component.Type.Namespace + "." + name : name;

            return ScriptNames.Qualified(fullName);
        }

        public List<MockerTarget> GetImplementors(MockerTarget component)
        {
            List<MockerTarget> implementors;

            return _implementorsByComponent.TryGetValue(component.Type.FullName, out implementors) ? implementors : new List<MockerTarget>();
        }

        public bool NeedsInitialization(MockerTarget component)
        {
            bool needed;

            if (_initializationByComponent.TryGetValue(component.Type.FullName, out needed))
            {
                return needed;
            }

            // Assume false while traversing so constructor cycles terminate cleanly here;
            // real cycles fail deterministically during construction.
            _initializationByComponent[component.Type.FullName] = false;

            foreach (MockerTarget implementor in GetImplementors(component))
            {
                if (implementor.Details.IsAsyncInitializable)
                {
                    needed = true;
                    break;
                }

                foreach (string parameterType in implementor.Details.ParameterTypes)
                {
                    MockerTarget dependency;

                    if (TryGetComponent(parameterType, out dependency) && NeedsInitialization(dependency))
                    {
                        needed = true;
                        break;
                    }
                }

                if (needed)
                {
                    break;
                }
            }

            _initializationByComponent[component.Type.FullName] = needed;

            return needed;
        }

        public bool TryGetComponent(string qualifiedType, out MockerTarget component)
        {
            foreach (MockerTarget candidate in Components)
            {
                if (string.Equals(ScriptNames.Qualified(candidate.Type.FullName), qualifiedType, StringComparison.Ordinal))
                {
                    component = candidate;
                    return true;
                }
            }

            component = default(MockerTarget);
            return false;
        }
    }
}
