using Microsoft.CodeAnalysis.CSharp;

namespace GeneratorLibrary.Common
{
    /// <summary>
    /// Turns names read from user code into the identifiers and type references generated code can use.
    /// </summary>
    public static class ScriptNames
    {
        /// <summary>
        /// Fully qualified reference to a type, so generated code never depends on what is in scope.
        /// </summary>
        /// <param name="fullName">Namespace and name of the type.</param>
        /// <returns>The reference, such as "global::Game.Tools".</returns>
        public static string Qualified(string fullName)
        {
            return string.IsNullOrEmpty(fullName) ? string.Empty : "global::" + fullName;
        }

        /// <summary>
        /// Turns a name into the form used for parameters and fields, escaping it when it collides
        /// with a keyword so that a node called "Object" or "Event" still generates valid code.
        /// </summary>
        /// <param name="name">Name to convert.</param>
        /// <returns>A usable identifier in camel case.</returns>
        public static string ToParameter(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "value";
            }

            string camelCase = char.ToLowerInvariant(name[0]) + name.Substring(1);

            return Escape(camelCase);
        }

        /// <summary>
        /// Turns a name into the form used for private fields.
        /// </summary>
        /// <param name="name">Name to convert.</param>
        /// <returns>A usable field name, such as "_ads".</returns>
        public static string ToField(string name)
        {
            return string.IsNullOrEmpty(name) ? "_value" : "_" + char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        /// <summary>
        /// Escapes an identifier that would otherwise be read as a keyword.
        /// </summary>
        /// <param name="identifier">Identifier to escape.</param>
        /// <returns>The identifier, prefixed with @ when it needs it.</returns>
        public static string Escape(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return identifier;
            }

            bool isKeyword = SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None
                || SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None;

            return isKeyword ? "@" + identifier : identifier;
        }

        /// <summary>
        /// Drops the leading I from an interface name, which is how a component name becomes a type name.
        /// </summary>
        /// <param name="typeName">Name to trim.</param>
        /// <returns>The name without its interface prefix.</returns>
        public static string TrimInterfacePrefix(string typeName)
        {
            bool looksLikeInterface = typeName != null && typeName.Length > 1 && typeName[0] == 'I' && char.IsUpper(typeName[1]);

            return looksLikeInterface ? typeName.Substring(1) : typeName;
        }
    }
}
