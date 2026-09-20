using GeneratorLibrary.Common;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace GeneratorLibrary.Mocker
{
    /// <summary>
    /// Checks everything that was detected and reports what is wrong with it. Without this a mistake in an
    /// attribute would just make a branch quietly disappear from the generated API.
    /// </summary>
    internal static class MockerValidator
    {
        /// <summary>
        /// Reports every problem in what was detected.
        /// </summary>
        /// <param name="context">Context the reports are sent to.</param>
        /// <param name="local">Everything declared in this assembly, which is what gets checked.</param>
        /// <param name="all">Everything known, including implementors published by other assemblies.</param>
        /// <param name="tree">Resolved parent links for those types.</param>
        public static void Validate(SourceProductionContext context, ImmutableArray<MockerTarget> local, ImmutableArray<MockerTarget> all, MockerTree tree)
        {
            foreach (MockerTarget target in local)
            {
                ValidatePartial(context, target);
                ValidateAbstract(context, target);
                ValidateReservedName(context, target);
                ValidateParent(context, target, tree);
            }

            ValidateNames(context, all, tree);
        }

        /// <summary>
        /// Reports composites a root claims but cannot see, which would otherwise just generate nothing.
        /// </summary>
        /// <param name="context">Context the reports are sent to.</param>
        /// <param name="imports">The root marker and the composites it claims.</param>
        /// <param name="all">Everything known, declared here and published by referenced assemblies.</param>
        public static void ValidateRoot(SourceProductionContext context, MockerImports imports, ImmutableArray<MockerTarget> all)
        {
            foreach (string claimed in imports.RootComposites)
            {
                if (!IsKnownComposite(claimed, all))
                {
                    context.Report(MockerDiagnostics.UnknownRootComposite, imports.RootLocation, claimed);
                }
            }
        }

        private static bool IsKnownComposite(string fullName, ImmutableArray<MockerTarget> all)
        {
            foreach (MockerTarget target in all)
            {
                if (target.Role == MockerRole.Composite && string.Equals(target.Type.FullName, fullName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidatePartial(SourceProductionContext context, MockerTarget target)
        {
            if (target.Role == MockerRole.Implementor || target.IsPartial)
            {
                return;
            }

            context.Report(MockerDiagnostics.NotPartial, target.Location, target.Role, target.Type.Name);
        }

        private static void ValidateAbstract(SourceProductionContext context, MockerTarget target)
        {
            if (target.Role != MockerRole.Composite || target.IsAbstract)
            {
                return;
            }

            context.Report(MockerDiagnostics.CompositeNotAbstract, target.Location, target.Type.Name);
        }

        private static void ValidateReservedName(SourceProductionContext context, MockerTarget target)
        {
            if (target.Role != MockerRole.Implementor || !string.Equals(target.Name, MockerEnumWriter.NoneMember, StringComparison.Ordinal))
            {
                return;
            }

            context.Report(MockerDiagnostics.ReservedName, target.Location, target.Type.Name, target.Name);
        }

        private static void ValidateParent(SourceProductionContext context, MockerTarget target, MockerTree tree)
        {
            if (target.Role == MockerRole.Composite)
            {
                return;
            }

            if (!target.HasParent)
            {
                context.Report(MockerDiagnostics.MissingParent, target.Location, target.Role, target.Type.Name);

                return;
            }

            MockerTarget parent;

            if (!tree.TryGetParent(target, out parent))
            {
                context.Report(MockerDiagnostics.UnknownParent, target.Location, target.Role, target.Type.Name, target.ParentFullName);

                return;
            }

            if (!IsValidParent(target.Role, parent.Role))
            {
                context.Report(MockerDiagnostics.WrongParentRole, target.Location, target.Role, target.Type.Name, parent.Type.Name, parent.Role, DescribeExpectedParent(target.Role));

                return;
            }

            if (target.Role == MockerRole.Implementor && !target.ImplementsParent)
            {
                context.Report(MockerDiagnostics.NotImplemented, target.Location, target.Type.Name, target.ParentFullName);

                return;
            }

            string path;
            string rootFullName;

            if (tree.Resolve(target, out path, out rootFullName) == MockerStatus.Cycle)
            {
                context.Report(MockerDiagnostics.CircularParent, target.Location, target.Role, target.Type.Name);
            }
        }

        private static void ValidateNames(SourceProductionContext context, ImmutableArray<MockerTarget> all, MockerTree tree)
        {
            Dictionary<(string Parent, string Name), List<MockerTarget>> siblings = new Dictionary<(string Parent, string Name), List<MockerTarget>>();

            foreach (MockerTarget target in all)
            {
                MockerTarget parent;

                if (target.Role == MockerRole.Composite || !tree.TryGetParent(target, out parent))
                {
                    continue;
                }

                (string Parent, string Name) key = (target.ParentFullName, target.Name);
                List<MockerTarget> group;

                if (!siblings.TryGetValue(key, out group))
                {
                    group = new List<MockerTarget>();
                    siblings[key] = group;
                }

                group.Add(target);
            }

            foreach (KeyValuePair<(string Parent, string Name), List<MockerTarget>> group in siblings)
            {
                if (group.Value.Count < 2)
                {
                    continue;
                }

                foreach (MockerTarget target in group.Value)
                {
                    context.Report(MockerDiagnostics.DuplicateName, target.Location, group.Key.Parent, group.Key.Name);
                }
            }
        }

        private static bool IsValidParent(MockerRole role, MockerRole parentRole)
        {
            switch (role)
            {
                case MockerRole.Node:
                case MockerRole.Component:
                    return parentRole == MockerRole.Composite || parentRole == MockerRole.Node;

                case MockerRole.Implementor:
                    return parentRole == MockerRole.Component;

                default:
                    return false;
            }
        }

        private static string DescribeExpectedParent(MockerRole role)
        {
            return role == MockerRole.Implementor ? "a component" : "a composite or a node";
        }
    }
}
