using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using reromanlee.Mocker.Generator.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace reromanlee.Mocker.Generator.Mocker
{
    /// <summary>
    /// Extracts equatable component mock models and emits partial do-nothing mocks for both
    /// interface components and abstract class components while skipping hand-written partial members.
    /// </summary>
    internal static class MockerMockWriter
    {
        private const string TaskType = "global::System.Threading.Tasks.Task";
        private const string AwaitableType = "global::UnityEngine.Awaitable";
        private const string CompletionSource = "global::UnityEngine.AwaitableCompletionSource";

        private static readonly SymbolDisplayFormat SignatureTypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
            .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        public static ComponentMockModel ExtractComponent(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            INamedTypeSymbol component = context.TargetSymbol as INamedTypeSymbol;
            ScriptLocation location = GetAttributeOrNodeLocation(context);

            if (component == null)
            {
                return default(ComponentMockModel);
            }

            bool isInterface = component.TypeKind == TypeKind.Interface;
            bool isAbstractClass = component.TypeKind == TypeKind.Class && component.IsAbstract && !component.IsStatic;

            if (!isInterface && !isAbstractClass)
            {
                return default(ComponentMockModel);
            }

            TypeTarget type = TypeTarget.From(component);
            string mockName = ScriptNames.GetComponentStem(type.Name, isInterface) + "Mock";
            string mockFullName = type.HasNamespace ? type.Namespace + "." + mockName : mockName;
            bool hasAccessibleParameterlessConstructor = isInterface || HasAccessibleParameterlessConstructor(component);

            ImmutableArray<MockMemberModel>.Builder members = ImmutableArray.CreateBuilder<MockMemberModel>();
            HashSet<string> seenKeys = new HashSet<string>(StringComparer.Ordinal);

            IEnumerable<ISymbol> candidates = isInterface
                ? GetAllInterfaceMembers(component)
                : GetUnimplementedAbstractMembers(component);

            foreach (ISymbol member in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                MockMemberModel built;

                if (TryBuildMemberModel(member, isInterface, out built) && seenKeys.Add(built.MemberKey))
                {
                    members.Add(built);
                }
            }

            return new ComponentMockModel(
                type,
                mockName,
                mockFullName,
                isInterface,
                true,
                hasAccessibleParameterlessConstructor,
                new EquatableArray<MockMemberModel>(members.ToImmutable()),
                location);
        }

        public static UserPartialMock ExtractUserPartialMock(GeneratorSyntaxContext context, CancellationToken cancellationToken)
        {
            ClassDeclarationSyntax declaration = context.Node as ClassDeclarationSyntax;

            if (declaration == null)
            {
                return default(UserPartialMock);
            }

            INamedTypeSymbol symbol = context.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken) as INamedTypeSymbol;

            if (symbol == null)
            {
                return default(UserPartialMock);
            }

            TypeTarget target = TypeTarget.From(symbol);
            bool hasExplicitPublicParameterlessConstructor = false;

            foreach (IMethodSymbol ctor in symbol.InstanceConstructors)
            {
                if (!ctor.IsImplicitlyDeclared
                    && ctor.Parameters.Length == 0
                    && ctor.DeclaredAccessibility == Accessibility.Public)
                {
                    hasExplicitPublicParameterlessConstructor = true;
                    break;
                }
            }

            List<string> memberKeys = new List<string>();

            foreach (ISymbol member in symbol.GetMembers())
            {
                if (member.IsImplicitlyDeclared)
                {
                    continue;
                }

                string key = BuildMemberKey(member);

                if (key != null && !memberKeys.Contains(key))
                {
                    memberKeys.Add(key);
                }
            }

            memberKeys.Sort(StringComparer.Ordinal);

            return new UserPartialMock(
                target.FullName,
                hasExplicitPublicParameterlessConstructor,
                new EquatableArray<string>(memberKeys.ToImmutableArray()));
        }

        public static MockScript BuildScript(ComponentMockModel model, EquatableArray<UserPartialMock> userMocks)
        {
            if (!model.IsValidForMock || model.Type.IsEmpty)
            {
                return default(MockScript);
            }

            bool hasUserParameterlessConstructor = false;
            HashSet<string> userMemberKeys = null;

            foreach (UserPartialMock userMock in userMocks)
            {
                if (!string.Equals(userMock.FullName, model.MockFullName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (userMock.HasPublicParameterlessConstructor)
                {
                    hasUserParameterlessConstructor = true;
                }

                if (!userMock.MemberKeys.IsEmpty)
                {
                    if (userMemberKeys == null)
                    {
                        userMemberKeys = new HashSet<string>(StringComparer.Ordinal);
                    }

                    foreach (string key in userMock.MemberKeys)
                    {
                        userMemberKeys.Add(key);
                    }
                }
            }

            if (!model.IsInterface && !model.HasAccessibleParameterlessConstructor && !hasUserParameterlessConstructor)
            {
                return new MockScript(
                    string.Empty,
                    string.Empty,
                    MockFailureKind.MissingParameterlessConstructor,
                    model.Type.Name,
                    model.MockName,
                    string.Empty,
                    model.Location);
            }

            ScriptEmitter emitter = new ScriptEmitter();
            bool needsAwaitable = false;
            bool needsAwaitableResult = false;

            emitter.Line("// <auto-generated />");
            emitter.Line("#nullable disable");

            using (emitter.Namespace(model.Type.Namespace))
            {
                emitter.Line($"/// <summary>Does nothing, so that a {model.Type.Name} nobody provided still answers every call.</summary>");

                using (emitter.Block($"public partial class {model.MockName} : {ScriptNames.Qualified(model.Type.FullName)}"))
                {
                    foreach (MockMemberModel member in model.Members)
                    {
                        if (userMemberKeys != null && userMemberKeys.Contains(member.MemberKey))
                        {
                            continue;
                        }

                        if (member.IsUnsupported)
                        {
                            return new MockScript(
                                string.Empty,
                                string.Empty,
                                MockFailureKind.UnsupportedMember,
                                model.Type.Name,
                                model.MockName,
                                member.MemberName,
                                model.Location);
                        }

                        if (member.Kind == MockMemberKind.Method)
                        {
                            using (emitter.Block(member.Declaration))
                            {
                                if (member.BodyLines.Length != 0)
                                {
                                    string[] lines = member.BodyLines.Split('\n');

                                    for (int index = 0; index < lines.Length; index++)
                                    {
                                        emitter.Line(lines[index]);
                                    }
                                }
                            }
                        }
                        else
                        {
                            emitter.Line(member.Declaration);
                        }

                        if (member.NeedsAwaitable)
                        {
                            needsAwaitable = true;
                        }

                        if (member.NeedsAwaitableResult)
                        {
                            needsAwaitableResult = true;
                        }
                    }

                    WriteAwaitableHelpers(emitter, needsAwaitable, needsAwaitableResult);
                }
            }

            return new MockScript(
                model.Type.FullName + ".Mock",
                emitter.ToString(),
                MockFailureKind.None,
                model.Type.Name,
                model.MockName,
                string.Empty,
                model.Location);
        }

        private static bool HasAccessibleParameterlessConstructor(INamedTypeSymbol component)
        {
            foreach (IMethodSymbol ctor in component.InstanceConstructors)
            {
                if (ctor.Parameters.Length != 0)
                {
                    continue;
                }

                switch (ctor.DeclaredAccessibility)
                {
                    case Accessibility.Public:
                    case Accessibility.Protected:
                    case Accessibility.ProtectedOrInternal:
                    case Accessibility.Internal:
                        return true;
                }
            }

            return false;
        }

        private static IEnumerable<ISymbol> GetAllInterfaceMembers(INamedTypeSymbol component)
        {
            foreach (ISymbol member in component.GetMembers())
            {
                yield return member;
            }

            foreach (INamedTypeSymbol inherited in component.AllInterfaces)
            {
                foreach (ISymbol member in inherited.GetMembers())
                {
                    yield return member;
                }
            }
        }

        private static IEnumerable<ISymbol> GetUnimplementedAbstractMembers(INamedTypeSymbol component)
        {
            HashSet<ISymbol> overridden = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            List<ISymbol> abstractMembers = new List<ISymbol>();

            for (INamedTypeSymbol current = component;
                current != null && current.SpecialType != SpecialType.System_Object;
                current = current.BaseType)
            {
                foreach (ISymbol member in current.GetMembers())
                {
                    IMethodSymbol method = member as IMethodSymbol;

                    if (method != null && method.OverriddenMethod != null)
                    {
                        for (IMethodSymbol ancestor = method.OverriddenMethod; ancestor != null; ancestor = ancestor.OverriddenMethod)
                        {
                            overridden.Add(ancestor);
                        }
                    }

                    IPropertySymbol property = member as IPropertySymbol;

                    if (property != null && property.OverriddenProperty != null)
                    {
                        for (IPropertySymbol ancestor = property.OverriddenProperty; ancestor != null; ancestor = ancestor.OverriddenProperty)
                        {
                            overridden.Add(ancestor);
                        }
                    }

                    IEventSymbol eventSymbol = member as IEventSymbol;

                    if (eventSymbol != null && eventSymbol.OverriddenEvent != null)
                    {
                        for (IEventSymbol ancestor = eventSymbol.OverriddenEvent; ancestor != null; ancestor = ancestor.OverriddenEvent)
                        {
                            overridden.Add(ancestor);
                        }
                    }

                    if (member.IsAbstract && !overridden.Contains(member))
                    {
                        abstractMembers.Add(member);
                    }
                }
            }

            return abstractMembers;
        }

        private static bool TryBuildMemberModel(ISymbol member, bool isInterface, out MockMemberModel model)
        {
            IPropertySymbol property = member as IPropertySymbol;

            if (property != null)
            {
                model = BuildPropertyModel(property, isInterface);
                return true;
            }

            IEventSymbol eventSymbol = member as IEventSymbol;

            if (eventSymbol != null)
            {
                model = BuildEventModel(eventSymbol, isInterface);
                return true;
            }

            IMethodSymbol method = member as IMethodSymbol;

            if (method != null && method.MethodKind == MethodKind.Ordinary)
            {
                model = BuildMethodModel(method, isInterface);
                return true;
            }

            model = default(MockMemberModel);
            return false;
        }

        private static MockMemberModel BuildEventModel(IEventSymbol eventSymbol, bool isInterface)
        {
            string modifiers = isInterface ? "public" : GetAccessibilityKeyword(eventSymbol.DeclaredAccessibility) + " override";
            string declaration = $"{modifiers} event {Display(eventSymbol.Type)} {eventSymbol.Name} {{ add {{ }} remove {{ }} }}";

            return new MockMemberModel(
                MockMemberKind.Event,
                BuildEventKey(eventSymbol),
                eventSymbol.Name,
                declaration,
                string.Empty,
                false,
                false,
                false);
        }

        private static MockMemberModel BuildPropertyModel(IPropertySymbol property, bool isInterface)
        {
            string key = BuildPropertyKey(property);

            if (property.ReturnsByRef || property.ReturnsByRefReadonly)
            {
                return new MockMemberModel(MockMemberKind.Property, key, property.Name, string.Empty, string.Empty, true, false, false);
            }

            bool needsAwaitable = false;
            bool needsAwaitableResult = false;
            string body = string.Empty;

            if (property.GetMethod != null)
            {
                string returned = GetReturnExpression(property.Type, ref needsAwaitable, ref needsAwaitableResult);

                if (returned == null)
                {
                    return new MockMemberModel(MockMemberKind.Property, key, property.Name, string.Empty, string.Empty, true, false, false);
                }

                string getterPrefix = !isInterface && property.GetMethod.DeclaredAccessibility != property.DeclaredAccessibility
                    ? GetAccessibilityKeyword(property.GetMethod.DeclaredAccessibility) + " "
                    : string.Empty;

                body += $" {getterPrefix}get {{ return {returned}; }}";
            }

            if (property.SetMethod != null)
            {
                string setterPrefix = !isInterface && property.SetMethod.DeclaredAccessibility != property.DeclaredAccessibility
                    ? GetAccessibilityKeyword(property.SetMethod.DeclaredAccessibility) + " "
                    : string.Empty;
                string setterKeyword = property.SetMethod.IsInitOnly ? "init" : "set";

                body += $" {setterPrefix}{setterKeyword} {{ }}";
            }

            string modifiers = isInterface ? "public" : GetAccessibilityKeyword(property.DeclaredAccessibility) + " override";
            string declaration = $"{modifiers} {Display(property.Type)} {GetPropertyName(property)} {{{body} }}";

            return new MockMemberModel(
                MockMemberKind.Property,
                key,
                property.Name,
                declaration,
                string.Empty,
                false,
                needsAwaitable,
                needsAwaitableResult);
        }

        private static MockMemberModel BuildMethodModel(IMethodSymbol method, bool isInterface)
        {
            string key = BuildMethodKey(method);

            if (method.ReturnsByRef || method.ReturnsByRefReadonly)
            {
                return new MockMemberModel(MockMemberKind.Method, key, method.Name, string.Empty, string.Empty, true, false, false);
            }

            bool needsAwaitable = false;
            bool needsAwaitableResult = false;
            string returnType = method.ReturnsVoid ? "void" : Display(method.ReturnType);
            string returned = method.ReturnsVoid
                ? string.Empty
                : GetReturnExpression(method.ReturnType, ref needsAwaitable, ref needsAwaitableResult);

            if (!method.ReturnsVoid && returned == null)
            {
                return new MockMemberModel(MockMemberKind.Method, key, method.Name, string.Empty, string.Empty, true, false, false);
            }

            List<string> parameters = new List<string>();

            foreach (IParameterSymbol parameter in method.Parameters)
            {
                parameters.Add($"{GetModifier(parameter)}{Display(parameter.Type)} {ScriptNames.Escape(parameter.Name)}");
            }

            string typeParameters = method.TypeParameters.Length == 0
                ? string.Empty
                : "<" + string.Join(", ", method.TypeParameters.Select(parameter => parameter.Name)) + ">";

            string modifiers = isInterface ? "public" : GetAccessibilityKeyword(method.DeclaredAccessibility) + " override";
            // C# CS0460 forbids repeating generic constraints on override methods; only interface implementations carry them.
            string constraints = isInterface ? GetInterfaceConstraints(method) : string.Empty;
            string declaration = $"{modifiers} {returnType} {method.Name}{typeParameters}({string.Join(", ", parameters)}){constraints}";

            List<string> bodyLines = new List<string>();

            foreach (IParameterSymbol parameter in method.Parameters)
            {
                if (parameter.RefKind == RefKind.Out)
                {
                    bodyLines.Add($"{ScriptNames.Escape(parameter.Name)} = default({Display(parameter.Type)});");
                }
            }

            if (!method.ReturnsVoid)
            {
                bodyLines.Add($"return {returned};");
            }

            return new MockMemberModel(
                MockMemberKind.Method,
                key,
                method.Name,
                declaration,
                string.Join("\n", bodyLines),
                false,
                needsAwaitable,
                needsAwaitableResult);
        }

        private static string GetPropertyName(IPropertySymbol property)
        {
            if (property.Parameters.Length == 0)
            {
                return property.Name;
            }

            List<string> parameters = new List<string>();

            foreach (IParameterSymbol parameter in property.Parameters)
            {
                parameters.Add($"{GetModifier(parameter)}{Display(parameter.Type)} {ScriptNames.Escape(parameter.Name)}");
            }

            return $"this[{string.Join(", ", parameters)}]";
        }

        private static void WriteAwaitableHelpers(ScriptEmitter emitter, bool needsAwaitable, bool needsAwaitableResult)
        {
            if (needsAwaitable)
            {
                emitter.Line();

                using (emitter.Block($"private static {AwaitableType} CompletedAwaitable()"))
                {
                    emitter.Line($"{CompletionSource} source = new {CompletionSource}();");
                    emitter.Line("source.SetResult();");
                    emitter.Line();
                    emitter.Line("return source.Awaitable;");
                }
            }

            if (needsAwaitableResult)
            {
                emitter.Line();

                using (emitter.Block($"private static {AwaitableType}<TResult> CompletedAwaitable<TResult>(TResult result)"))
                {
                    emitter.Line($"{CompletionSource}<TResult> source = new {CompletionSource}<TResult>();");
                    emitter.Line("source.SetResult(result);");
                    emitter.Line();
                    emitter.Line("return source.Awaitable;");
                }
            }
        }

        private static string GetModifier(IParameterSymbol parameter)
        {
            string modifier = string.Empty;

            switch (parameter.RefKind)
            {
                case RefKind.Ref:
                    modifier = "ref ";
                    break;

                case RefKind.Out:
                    modifier = "out ";
                    break;

                case RefKind.In:
                    modifier = "in ";
                    break;
            }

            return parameter.IsParams ? "params " + modifier : modifier;
        }

        private static string GetInterfaceConstraints(IMethodSymbol method)
        {
            List<string> constraints = new List<string>();

            foreach (ITypeParameterSymbol parameter in method.TypeParameters)
            {
                List<string> parts = new List<string>();

                if (parameter.HasReferenceTypeConstraint)
                {
                    parts.Add("class");
                }
                else if (parameter.HasUnmanagedTypeConstraint)
                {
                    parts.Add("unmanaged");
                }
                else if (parameter.HasValueTypeConstraint)
                {
                    parts.Add("struct");
                }
                else if (parameter.HasNotNullConstraint)
                {
                    parts.Add("notnull");
                }

                foreach (ITypeSymbol constraint in parameter.ConstraintTypes)
                {
                    parts.Add(Display(constraint));
                }

                if (parameter.HasConstructorConstraint && !parameter.HasValueTypeConstraint && !parameter.HasUnmanagedTypeConstraint)
                {
                    parts.Add("new()");
                }

                if (parts.Count > 0)
                {
                    constraints.Add($" where {parameter.Name} : {string.Join(", ", parts)}");
                }
            }

            return string.Join(string.Empty, constraints);
        }

        private static string GetAccessibilityKeyword(Accessibility accessibility)
        {
            switch (accessibility)
            {
                case Accessibility.Protected:
                    return "protected";

                case Accessibility.Internal:
                    return "internal";

                case Accessibility.ProtectedOrInternal:
                    return "protected internal";

                case Accessibility.ProtectedAndInternal:
                    return "private protected";

                default:
                    return "public";
            }
        }

        private static string GetReturnExpression(ITypeSymbol returnType, ref bool needsAwaitable, ref bool needsAwaitableResult)
        {
            string displayed = Display(returnType);

            if (displayed == TaskType)
            {
                return TaskType + ".CompletedTask";
            }

            if (displayed == AwaitableType)
            {
                needsAwaitable = true;
                return "CompletedAwaitable()";
            }

            INamedTypeSymbol named = returnType as INamedTypeSymbol;
            bool isGeneric = named != null && named.TypeArguments.Length == 1;
            string definition = isGeneric ? Display(named.OriginalDefinition) : string.Empty;

            if (isGeneric && definition.StartsWith(TaskType + "<", StringComparison.Ordinal))
            {
                return $"{TaskType}.FromResult(default({Display(named.TypeArguments[0])}))";
            }

            if (isGeneric && definition.StartsWith(AwaitableType + "<", StringComparison.Ordinal))
            {
                needsAwaitableResult = true;
                return $"CompletedAwaitable(default({Display(named.TypeArguments[0])}))";
            }

            // Value-type awaitables (ValueTask, UniTask) complete synchronously as default;
            // reference-type awaitables would return null and throw NullReferenceException on await.
            if (returnType.IsReferenceType && IsAwaitable(returnType))
            {
                return null;
            }

            return $"default({displayed})";
        }

        private static bool IsAwaitable(ITypeSymbol type)
        {
            return type.GetMembers("GetAwaiter").OfType<IMethodSymbol>().Any(method => method.Parameters.Length == 0);
        }

        private static string BuildMemberKey(ISymbol member)
        {
            IPropertySymbol property = member as IPropertySymbol;

            if (property != null)
            {
                return BuildPropertyKey(property);
            }

            IEventSymbol eventSymbol = member as IEventSymbol;

            if (eventSymbol != null)
            {
                return BuildEventKey(eventSymbol);
            }

            IMethodSymbol method = member as IMethodSymbol;

            if (method != null && method.MethodKind == MethodKind.Ordinary)
            {
                return BuildMethodKey(method);
            }

            return null;
        }

        private static string BuildEventKey(IEventSymbol eventSymbol)
        {
            return "E:" + eventSymbol.Name;
        }

        private static string BuildPropertyKey(IPropertySymbol property)
        {
            if (property.Parameters.Length == 0)
            {
                return "P:" + property.Name;
            }

            List<string> parameters = new List<string>(property.Parameters.Length);

            foreach (IParameterSymbol parameter in property.Parameters)
            {
                parameters.Add(FormatSignatureParameter(parameter));
            }

            return "I:" + string.Join(",", parameters);
        }

        private static string BuildMethodKey(IMethodSymbol method)
        {
            List<string> parameters = new List<string>(method.Parameters.Length);

            foreach (IParameterSymbol parameter in method.Parameters)
            {
                parameters.Add(FormatSignatureParameter(parameter));
            }

            return $"M:{method.Name}`{method.TypeParameters.Length}({string.Join(",", parameters)})";
        }

        private static string FormatSignatureParameter(IParameterSymbol parameter)
        {
            string prefix = parameter.RefKind == RefKind.None ? string.Empty : parameter.RefKind.ToString() + " ";

            return prefix + FormatSignatureType(parameter.Type);
        }

        private static string FormatSignatureType(ITypeSymbol type)
        {
            ITypeParameterSymbol typeParameter = type as ITypeParameterSymbol;

            if (typeParameter != null && typeParameter.TypeParameterKind == TypeParameterKind.Method)
            {
                return "!!" + typeParameter.Ordinal;
            }

            IArrayTypeSymbol array = type as IArrayTypeSymbol;

            if (array != null)
            {
                return FormatSignatureType(array.ElementType) + "[" + new string(',', array.Rank - 1) + "]";
            }

            INamedTypeSymbol named = type as INamedTypeSymbol;

            if (named != null && named.IsGenericType)
            {
                string definition = named.OriginalDefinition.ToDisplayString(SignatureTypeFormat);
                int angle = definition.IndexOf('<');
                string stem = angle >= 0 ? definition.Substring(0, angle) : definition;
                List<string> args = new List<string>(named.TypeArguments.Length);

                foreach (ITypeSymbol argument in named.TypeArguments)
                {
                    args.Add(FormatSignatureType(argument));
                }

                return stem + "<" + string.Join(",", args) + ">";
            }

            return type.ToDisplayString(SignatureTypeFormat);
        }

        private static ScriptLocation GetAttributeOrNodeLocation(GeneratorAttributeSyntaxContext context)
        {
            if (context.Attributes.Length != 0 && context.Attributes[0].ApplicationSyntaxReference != null)
            {
                return ScriptLocation.From(context.Attributes[0].ApplicationSyntaxReference);
            }

            return ScriptLocation.From(context.TargetNode);
        }

        private static string Display(ITypeSymbol type)
        {
            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
    }
}
