using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using reromanlee.Mocker.Generator.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace reromanlee.Mocker.Generator.Mocker
{
    /// <summary>
    /// Roslyn incremental source generator for Mocker. Emits composite/node APIs, partial mocks,
    /// cross-assembly export metadata, selection enums, choices trees, and composition root wiring.
    /// </summary>
    [Generator]
    public class MockerGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValuesProvider<MockerTarget> composites = context.FindAllByAttribute(MockerAttributes.Composite, MockerTarget.FromComposite);
            IncrementalValuesProvider<MockerTarget> nodes = context.FindAllByAttribute(MockerAttributes.Node, MockerTarget.FromNode);
            IncrementalValuesProvider<MockerTarget> components = context.FindAllByAttribute(MockerAttributes.Component, MockerTarget.FromComponent);
            IncrementalValuesProvider<MockerTarget> implementors = context.FindAllByAttribute(MockerAttributes.Implementor, MockerTarget.FromImplementor);

            IncrementalValuesProvider<ComponentMockModel> mockModels = context.FindAllByAttribute(
                MockerAttributes.Component,
                MockerMockWriter.ExtractComponent);

            IncrementalValueProvider<EquatableArray<UserPartialMock>> userPartialMocks = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => IsPartialMockClassSyntax(node),
                    transform: static (syntaxContext, cancellationToken) => MockerMockWriter.ExtractUserPartialMock(syntaxContext, cancellationToken))
                .Where(static mock => mock.FullName.Length != 0)
                .Collect()
                .Select(static (mocks, _) => new EquatableArray<UserPartialMock>(mocks));

            IncrementalValuesProvider<MockScript> mocks = mockModels
                .Combine(userPartialMocks)
                .Select(static (pair, _) => MockerMockWriter.BuildScript(pair.Left, pair.Right));

            context.RegisterSourceOutput(mocks, static (productionContext, mock) =>
            {
                if (mock.FailureKind == MockFailureKind.UnsupportedMember)
                {
                    productionContext.Report(MockerDiagnostics.NotMockable, mock.Location, mock.UnsupportedMember);
                }
                else if (mock.FailureKind == MockFailureKind.MissingParameterlessConstructor)
                {
                    productionContext.Report(
                        MockerDiagnostics.AbstractComponentMissingParameterlessConstructor,
                        mock.Location,
                        mock.ComponentName,
                        mock.MockTypeName);
                }
                else if (mock.Name.Length != 0)
                {
                    productionContext.AddScript(mock.Name, mock.Content);
                }
            });

            IncrementalValueProvider<EquatableArray<MockerTarget>> local = ProviderExtensions.CollectAll(composites, nodes, components, implementors);
            IncrementalValueProvider<MockerImports> published = context.CompilationProvider.Select(static (compilation, _) => ReadImports(compilation));

            context.RegisterSourceOutput(local.Combine(published), static (productionContext, batch) =>
            {
                MockerImports imports = batch.Right;
                ImmutableArray<MockerTarget> localTargets = batch.Left.AsImmutableArray();
                ImmutableArray<MockerTarget> allTargets = localTargets.AddRange(imports.Targets);

                MockerTree tree = new MockerTree(allTargets);
                HashSet<string> invalidTargets = MockerValidator.Validate(productionContext, localTargets, allTargets, tree);

                MockerExportWriter.Write(productionContext, localTargets, invalidTargets);
                MockerApiWriter.Write(productionContext, localTargets, tree, invalidTargets);

                if (imports.IsRoot)
                {
                    MockerValidator.ValidateRoot(productionContext, imports, allTargets);
                    MockerEnumWriter.Write(productionContext, allTargets, tree, imports, invalidTargets);

                    foreach (MockerTarget target in allTargets)
                    {
                        if (target.Role != MockerRole.Composite
                            || invalidTargets.Contains(target.Type.FullName)
                            || !imports.Owns(target.Type.FullName))
                        {
                            continue;
                        }

                        MockerScope scope = new MockerScope(target, allTargets, tree, invalidTargets);

                        MockerChoicesWriter.Write(productionContext, scope);
                        MockerWiringWriter.Write(productionContext, scope, allTargets);
                    }
                }
            });
        }

        private static bool IsPartialMockClassSyntax(SyntaxNode node)
        {
            ClassDeclarationSyntax declaration = node as ClassDeclarationSyntax;

            return declaration != null
                && declaration.Identifier.ValueText.EndsWith("Mock", StringComparison.Ordinal)
                && declaration.Modifiers.Any(SyntaxKind.PartialKeyword);
        }

        private static MockerImports ReadImports(Compilation compilation)
        {
            List<MockerTarget> published = new List<MockerTarget>();

            foreach (AttributeData attribute in AssemblyScanner.FindAssemblyAttributes(compilation, MockerAttributes.Export))
            {
                MockerTarget target;

                if (MockerTarget.TryFromExport(attribute, out target))
                {
                    published.Add(target);
                }
            }

            published.Sort((left, right) => string.CompareOrdinal(left.Type.FullName, right.Type.FullName));

            AttributeData root = AssemblyScanner.FindAssemblyAttribute(compilation, MockerAttributes.Root);

            if (root == null)
            {
                return new MockerImports(published.ToImmutableArray(), false, ImmutableArray<string>.Empty, default(ScriptLocation));
            }

            ImmutableArray<string>.Builder composites = ImmutableArray.CreateBuilder<string>();

            foreach (TypeTarget composite in root.GetTypeArrayArgument(0))
            {
                if (!composite.IsEmpty)
                {
                    composites.Add(composite.FullName);
                }
            }

            return new MockerImports(published.ToImmutableArray(), true, composites.ToImmutable(), ScriptLocation.From(root.ApplicationSyntaxReference));
        }
    }
}
