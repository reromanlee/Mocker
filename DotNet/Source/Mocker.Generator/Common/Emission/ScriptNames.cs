using Microsoft.CodeAnalysis.CSharp;

namespace reromanlee.Mocker.Generator.Common
{
    /// <summary>
    /// Converts symbol names into qualified C# references, escaped identifiers, and component stems.
    /// </summary>
    public static class ScriptNames
    {
        public static string Qualified(string fullName)
        {
            return string.IsNullOrEmpty(fullName) ? string.Empty : "global::" + fullName;
        }

        public static string ToParameter(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "value";
            }

            string camelCase = char.ToLowerInvariant(name[0]) + name.Substring(1);

            return Escape(camelCase);
        }

        public static string ToField(string name)
        {
            return string.IsNullOrEmpty(name) ? "_value" : "_" + char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

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
        /// Strips a leading <c>I</c> prefix from an interface name (such as <c>ISaves</c> -> <c>Saves</c>).
        /// </summary>
        public static string TrimInterfacePrefix(string typeName)
        {
            bool looksLikeInterface = typeName != null && typeName.Length > 1 && typeName[0] == 'I' && char.IsUpper(typeName[1]);

            return looksLikeInterface ? typeName.Substring(1) : typeName;
        }

        /// <summary>
        /// Returns the stem used for generated <c>{Stem}Implementor</c> enums and <c>{Stem}Mock</c> types.
        /// Only interface components drop a leading <c>I</c> so abstract classes like <c>IAPService</c> keep their name.
        /// </summary>
        public static string GetComponentStem(string typeName, bool isInterface)
        {
            return isInterface ? TrimInterfacePrefix(typeName) : (typeName ?? string.Empty);
        }
    }
}
