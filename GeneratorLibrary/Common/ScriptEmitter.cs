using System;
using System.Text;

namespace GeneratorLibrary.Common
{
    /// <summary>
    /// Builds a script while keeping track of how deep it is, so that writers describe the code they want
    /// rather than threading an indent string through every call. Braces are opened and closed by scopes,
    /// which means a block cannot be left unclosed.
    /// </summary>
    public sealed class ScriptEmitter
    {
        private const string Step = "    ";

        private readonly StringBuilder _builder = new StringBuilder();

        private int _depth;

        /// <summary>
        /// Writes a line at the current depth.
        /// </summary>
        /// <param name="text">Line to write.</param>
        /// <returns>This, so calls can be chained.</returns>
        public ScriptEmitter Line(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                for (int level = 0; level < _depth; level++)
                {
                    _builder.Append(Step);
                }

                _builder.Append(text);
            }

            _builder.AppendLine();

            return this;
        }

        /// <summary>
        /// Writes a blank line.
        /// </summary>
        /// <returns>This, so calls can be chained.</returns>
        public ScriptEmitter Line()
        {
            return Line(string.Empty);
        }

        /// <summary>
        /// Opens a braced block, and closes it when the scope is disposed.
        /// </summary>
        /// <param name="header">What comes before the brace, such as a method signature.</param>
        /// <returns>The scope to dispose when the block ends.</returns>
        public IDisposable Block(string header)
        {
            Line(header);
            Line("{");
            _depth++;

            return new Scope(this, true);
        }

        /// <summary>
        /// Opens a braced block whose header continues onto a second, indented line, which is what a
        /// constructor with a base call needs.
        /// </summary>
        /// <param name="header">First line of the header.</param>
        /// <param name="continuation">Second line, such as a base call.</param>
        /// <returns>The scope to dispose when the block ends.</returns>
        public IDisposable Block(string header, string continuation)
        {
            Line(header);
            _depth++;
            Line(continuation);
            _depth--;
            Line("{");
            _depth++;

            return new Scope(this, true);
        }

        /// <summary>
        /// Opens a namespace block, or nothing at all when the type sits in the global namespace.
        /// </summary>
        /// <param name="namespaceName">Namespace to open, or an empty string for none.</param>
        /// <returns>The scope to dispose when the namespace ends.</returns>
        public IDisposable Namespace(string namespaceName)
        {
            if (string.IsNullOrEmpty(namespaceName))
            {
                return new Scope(this, false);
            }

            return Block("namespace " + namespaceName);
        }

        public override string ToString()
        {
            return _builder.ToString();
        }

        private sealed class Scope : IDisposable
        {
            private readonly ScriptEmitter _emitter;
            private readonly bool _isOpen;

            public Scope(ScriptEmitter emitter, bool isOpen)
            {
                _emitter = emitter;
                _isOpen = isOpen;
            }

            public void Dispose()
            {
                if (!_isOpen)
                {
                    return;
                }

                _emitter._depth--;
                _emitter.Line("}");
            }
        }
    }
}
