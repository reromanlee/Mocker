using GeneratorLibrary.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace GeneratorLibrary.Mocker
{
    /// <summary>
    /// Everything under one composite, with the names generated code refers to it by. Built once per
    /// composite so the writers do not each have to re-walk the tree.
    /// </summary>
    internal sealed class MockerScope
    {
        private readonly Dictionary<string, string> _pathsByFullName = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<MockerTarget>> _implementorsByComponent = new Dictionary<string, List<MockerTarget>>(StringComparer.Ordinal);

        public MockerScope(MockerTarget composite, ImmutableArray<MockerTarget> all, MockerTree tree)
        {
            Composite = composite;
            Tree = tree;
            Branches = new List<MockerTarget>();
            Components = new List<MockerTarget>();

            foreach (MockerTarget target in all)
            {
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

        /// <summary>
        /// The composite at the root of this scope.
        /// </summary>
        public MockerTarget Composite { get; }

        /// <summary>
        /// The tree these came from.
        /// </summary>
        public MockerTree Tree { get; }

        /// <summary>
        /// The composite and every node under it.
        /// </summary>
        public List<MockerTarget> Branches { get; }

        /// <summary>
        /// Every component under the composite.
        /// </summary>
        public List<MockerTarget> Components { get; }

        /// <summary>
        /// Path a type is reached by, such as "Money.Ads.Interstitial".
        /// </summary>
        public string GetPath(MockerTarget target)
        {
            string path;

            return _pathsByFullName.TryGetValue(target.Type.FullName, out path) ? path : string.Empty;
        }

        /// <summary>
        /// The path as a single identifier, used to name generated fields and methods.
        /// </summary>
        public string GetId(MockerTarget target)
        {
            return GetPath(target).Replace(".", "_");
        }

        /// <summary>
        /// The path without the composite at the front, used to name members of that composite's factory,
        /// where the composite name would only repeat itself.
        /// </summary>
        public string GetMemberId(MockerTarget target)
        {
            string path = GetPath(target);
            int firstSeparator = path.IndexOf('.');

            return firstSeparator < 0 ? path.Replace(".", "_") : path.Substring(firstSeparator + 1).Replace(".", "_");
        }

        /// <summary>
        /// The member name a generated field uses, which is the member id in the repository's field style.
        /// </summary>
        public string GetFieldId(MockerTarget target)
        {
            string id = GetMemberId(target);

            return id.Length == 0 ? "_value" : "_" + char.ToLowerInvariant(id[0]) + id.Substring(1);
        }

        /// <summary>
        /// How generated code reaches this type's choices, such as "_choices.Ads.Interstitial".
        /// </summary>
        public string GetChoicesAccessor(MockerTarget target)
        {
            string path = GetPath(target);
            int firstSeparator = path.IndexOf('.');

            return firstSeparator < 0 ? "_choices" : "_choices." + path.Substring(firstSeparator + 1);
        }

        /// <summary>
        /// Name of the generated choices class for a composite or node.
        /// </summary>
        public string GetChoicesTypeName(MockerTarget branch)
        {
            return GetId(branch) + "Choices";
        }

        /// <summary>
        /// Fully qualified name of the enum listing a component's implementors.
        /// </summary>
        public string GetEnumType(MockerTarget component)
        {
            string name = ScriptNames.TrimInterfacePrefix(component.Type.Name) + "Implementor";
            string fullName = component.Type.HasNamespace ? component.Type.Namespace + "." + name : name;

            return ScriptNames.Qualified(fullName);
        }

        /// <summary>
        /// Fully qualified name of the generated mock for a component.
        /// </summary>
        public string GetMockType(MockerTarget component)
        {
            string name = ScriptNames.TrimInterfacePrefix(component.Type.Name) + "Mock";
            string fullName = component.Type.HasNamespace ? component.Type.Namespace + "." + name : name;

            return ScriptNames.Qualified(fullName);
        }

        /// <summary>
        /// Everything that implements a component, ordered by name.
        /// </summary>
        public List<MockerTarget> GetImplementors(MockerTarget component)
        {
            List<MockerTarget> implementors;

            return _implementorsByComponent.TryGetValue(component.Type.FullName, out implementors) ? implementors : new List<MockerTarget>();
        }

        /// <summary>
        /// Finds the component a constructor parameter refers to, when it refers to one in this scope.
        /// </summary>
        /// <param name="qualifiedType">Parameter type as generated code would write it.</param>
        /// <param name="component">The component it names.</param>
        /// <returns>True when the parameter is a component of this composite.</returns>
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
