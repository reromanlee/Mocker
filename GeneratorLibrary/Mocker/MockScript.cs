using GeneratorLibrary.Common;
using System;

namespace GeneratorLibrary.Mocker
{
    /// <summary>
    /// A generated mock, or the reason one could not be generated. Carried out of the search so the
    /// problem can be reported against the component rather than surfacing inside generated code.
    /// </summary>
    public readonly struct MockScript : IEquatable<MockScript>
    {
        private readonly string _name;
        private readonly string _content;
        private readonly string _unsupportedMember;

        public MockScript(string name, string content, string unsupportedMember, ScriptLocation location)
        {
            _name = name;
            _content = content;
            _unsupportedMember = unsupportedMember;
            Location = location;
        }

        /// <summary>
        /// Name the script is written under.
        /// </summary>
        public string Name
        {
            get { return _name ?? string.Empty; }
        }

        /// <summary>
        /// Complete content of the mock.
        /// </summary>
        public string Content
        {
            get { return _content ?? string.Empty; }
        }

        /// <summary>
        /// Member that could not be mocked, or an empty string when the mock is complete.
        /// </summary>
        public string UnsupportedMember
        {
            get { return _unsupportedMember ?? string.Empty; }
        }

        /// <summary>
        /// Where the component was declared.
        /// </summary>
        public ScriptLocation Location { get; }

        /// <summary>
        /// Whether a mock could be written for this component.
        /// </summary>
        public bool IsSupported
        {
            get { return UnsupportedMember.Length == 0; }
        }

        public bool Equals(MockScript other)
        {
            return string.Equals(Name, other.Name, StringComparison.Ordinal)
                && string.Equals(Content, other.Content, StringComparison.Ordinal)
                && string.Equals(UnsupportedMember, other.UnsupportedMember, StringComparison.Ordinal)
                && Location.Equals(other.Location);
        }

        public override bool Equals(object obj)
        {
            return obj is MockScript other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Name.GetHashCode();
                hash = (hash * 397) ^ Content.GetHashCode();
                hash = (hash * 397) ^ UnsupportedMember.GetHashCode();
                return hash;
            }
        }
    }
}
