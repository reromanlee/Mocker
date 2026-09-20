using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;

namespace GeneratorLibrary.Common
{
    /// <summary>
    /// Where something was written in a script, kept as plain values rather than as a Roslyn location,
    /// so that pointing at code does not keep whole syntax trees alive in the generator cache.
    /// </summary>
    public readonly struct ScriptLocation : IEquatable<ScriptLocation>
    {
        private readonly string _filePath;

        public ScriptLocation(string filePath, TextSpan span, LinePositionSpan lineSpan)
        {
            _filePath = filePath;
            Span = span;
            LineSpan = lineSpan;
        }

        /// <summary>
        /// Script the code was written in.
        /// </summary>
        public string FilePath
        {
            get { return _filePath ?? string.Empty; }
        }

        /// <summary>
        /// Character range the code covers.
        /// </summary>
        public TextSpan Span { get; }

        /// <summary>
        /// Line and column range the code covers, which is what an editor jumps to.
        /// </summary>
        public LinePositionSpan LineSpan { get; }

        /// <summary>
        /// Whether a place in code is known, so a report can fall back to no location instead of a wrong one.
        /// </summary>
        public bool IsEmpty
        {
            get { return FilePath.Length == 0; }
        }

        /// <summary>
        /// Takes the place a piece of syntax was written, such as the attribute itself.
        /// </summary>
        /// <param name="node">Syntax to point at.</param>
        /// <returns>The place it was written, or an empty location when there is no syntax.</returns>
        public static ScriptLocation From(SyntaxNode node)
        {
            if (node == null)
            {
                return default(ScriptLocation);
            }

            Location location = node.GetLocation();

            return new ScriptLocation(location.SourceTree == null ? string.Empty : location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
        }

        /// <summary>
        /// Takes the place a piece of syntax was written from a reference to it, such as an attribute application.
        /// </summary>
        /// <param name="reference">Reference to the syntax to point at.</param>
        /// <returns>The place it was written, or an empty location when there is no reference.</returns>
        public static ScriptLocation From(SyntaxReference reference)
        {
            return reference == null ? default(ScriptLocation) : From(reference.GetSyntax());
        }

        /// <summary>
        /// Rebuilds the Roslyn location a report needs.
        /// </summary>
        /// <returns>The location, or none when the place is unknown.</returns>
        public Location ToLocation()
        {
            return IsEmpty ? Location.None : Location.Create(FilePath, Span, LineSpan);
        }

        public bool Equals(ScriptLocation other)
        {
            return string.Equals(FilePath, other.FilePath, StringComparison.Ordinal)
                && Span.Equals(other.Span)
                && LineSpan.Equals(other.LineSpan);
        }

        public override bool Equals(object obj)
        {
            return obj is ScriptLocation other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = FilePath.GetHashCode();
                hash = (hash * 397) ^ Span.GetHashCode();
                hash = (hash * 397) ^ LineSpan.GetHashCode();
                return hash;
            }
        }
    }
}
