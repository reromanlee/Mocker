using System;
using System.Text;

namespace reromanlee.Mocker.Generator.Common
{
    /// <summary>
    /// Emits indented C# source text with deterministic LF line endings and zero-allocation
    /// struct brace scopes.
    /// </summary>
    public sealed class ScriptEmitter
    {
        private const string Step = "    ";

        private readonly StringBuilder _builder = new StringBuilder();

        private int _depth;

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

            _builder.Append('\n');

            return this;
        }

        public ScriptEmitter Line()
        {
            return Line(string.Empty);
        }

        public Scope Block(string header)
        {
            Line(header);
            Line("{");
            _depth++;

            return new Scope(this, true);
        }

        public Scope Block(string header, string continuation)
        {
            Line(header);
            _depth++;
            Line(continuation);
            _depth--;
            Line("{");
            _depth++;

            return new Scope(this, true);
        }

        public Scope Namespace(string namespaceName)
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

        public readonly struct Scope : IDisposable
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
                if (!_isOpen || _emitter == null)
                {
                    return;
                }

                _emitter._depth--;
                _emitter.Line("}");
            }
        }
    }
}
