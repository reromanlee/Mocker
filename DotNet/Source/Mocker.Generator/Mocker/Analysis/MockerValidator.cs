using Microsoft.CodeAnalysis;
using reromanlee.Mocker.Generator.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace reromanlee.Mocker.Generator.Mocker
{
    /// <summary>
    /// Validates every local and cross-assembly Mocker target, reporting diagnostics and returning
    /// the set of invalid target full names so downstream writers suppress broken code emission.
    /// </summary>
    internal static class MockerValidator
    {
        public static HashSet<string> Validate(
            SourceProductionContext context,
            ImmutableArray<MockerTarget> local,
            ImmutableArray<MockerTarget> all,
            MockerTree tree)
        {
            HashSet<string> invalid = new HashSet<string>(StringComparer.Ordinal);

            foreach (MockerTarget target in local)
            {
                bool hasError = false;

                if (!ValidatePartial(context, target))
                {
                    hasError = true;
                }

                if (!ValidateAbstract(context, target))
                {
                    hasError = true;
                }

                if (!ValidateReservedName(context, target))
                {
                    hasError = true;
                }

                if (!ValidateConstructibleImplementor(context, target))
                {
                    hasError = true;
                }

                if (!ValidateParent(context, target, tree))
                {
                    hasError = true;
                }

                if (hasError && !target.Type.IsEmpty)
                {
                    invalid.Add(target.Type.FullName);
                }
            }

            ValidateNames(context, all, tree, invalid);

            return invalid;
        }

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

        private static bool ValidatePartial(SourceProductionContext context, MockerTarget target)
        {
            if (target.Role == MockerRole.Implementor || target.IsPartial)
            {
                return true;
            }

            context.Report(MockerDiagnostics.NotPartial, target.Location, target.Role, target.Type.Name);

            return false;
        }

        private static bool ValidateAbstract(SourceProductionContext context, MockerTarget target)
        {
            if (target.Role == MockerRole.Composite && !target.IsAbstract)
            {
                context.Report(MockerDiagnostics.CompositeNotAbstract, target.Location, target.Type.Name);

                return false;
            }

            if (target.Role == MockerRole.Component && !target.IsInterface && !target.IsAbstract)
            {
                context.Report(MockerDiagnostics.ComponentNotAbstract, target.Location, target.Type.Name);

                return false;
            }

            return true;
        }

        private static bool ValidateReservedName(SourceProductionContext context, MockerTarget target)
        {
            if (target.Role != MockerRole.Implementor || !string.Equals(target.Name, MockerEnumWriter.NoneMember, StringComparison.Ordinal))
            {
                return true;
            }

            context.Report(MockerDiagnostics.ReservedName, target.Location, target.Type.Name, target.Name);

            return false;
        }

        private static bool ValidateConstructibleImplementor(SourceProductionContext context, MockerTarget target)
        {
            if (target.Role != MockerRole.Implementor)
            {
                return true;
            }

            switch (target.Details.ConstructorStatus)
            {
                case ImplementorConstructorStatus.AbstractOrStatic:
                    context.Report(MockerDiagnostics.ImplementorNotConstructible, target.Location, target.Type.Name, "it is abstract or static");
                    return false;

                case ImplementorConstructorStatus.NoPublicConstructor:
                    context.Report(MockerDiagnostics.ImplementorNotConstructible, target.Location, target.Type.Name, "it has no public constructor");
                    return false;

                case ImplementorConstructorStatus.AmbiguousConstructor:
                    context.Report(MockerDiagnostics.ImplementorNotConstructible, target.Location, target.Type.Name, "it has multiple public constructors with the same maximum parameter count");
                    return false;

                default:
                    return true;
            }
        }

        private static bool ValidateParent(SourceProductionContext context, MockerTarget target, MockerTree tree)
        {
            if (target.Role == MockerRole.Composite)
            {
                return true;
            }

            if (!target.HasParent)
            {
                context.Report(MockerDiagnostics.MissingParent, target.Location, target.Role, target.Type.Name);

                return false;
            }

            MockerTarget parent;

            if (!tree.TryGetParent(target, out parent))
            {
                context.Report(MockerDiagnostics.UnknownParent, target.Location, target.Role, target.Type.Name, target.ParentFullName);

                return false;
            }

            if (!IsValidParent(target.Role, parent.Role))
            {
                context.Report(MockerDiagnostics.WrongParentRole, target.Location, target.Role, target.Type.Name, parent.Type.Name, parent.Role, DescribeExpectedParent(target.Role));

                return false;
            }

            if (target.Role == MockerRole.Implementor && !target.ImplementsParent)
            {
                context.Report(MockerDiagnostics.NotImplemented, target.Location, target.Type.Name, target.ParentFullName);

                return false;
            }

            string path;
            string rootFullName;

            if (tree.Resolve(target, out path, out rootFullName) == MockerStatus.Cycle)
            {
                context.Report(MockerDiagnostics.CircularParent, target.Location, target.Role, target.Type.Name);

                return false;
            }

            return true;
        }

        private static void ValidateNames(
            SourceProductionContext context,
            ImmutableArray<MockerTarget> all,
            MockerTree tree,
            HashSet<string> invalid)
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

                    if (!target.Type.IsEmpty)
                    {
                        invalid.Add(target.Type.FullName);
                    }
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
