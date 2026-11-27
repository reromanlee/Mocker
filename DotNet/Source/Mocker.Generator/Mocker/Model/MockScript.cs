using reromanlee.Mocker.Generator.Common;
using System;

namespace reromanlee.Mocker.Generator.Mocker
{
    /// <summary>
    /// Diagnostic reason preventing automatic mock generation for a component.
    /// </summary>
    public enum MockFailureKind
    {
        None = 0,
        UnsupportedMember = 1,
        MissingParameterlessConstructor = 2
    }

    /// <summary>
    /// Generated mock script or structured failure payload reported against the component attribute.
    /// </summary>
    public readonly struct MockScript : IEquatable<MockScript>
    {
        private readonly string _name;
        private readonly string _content;
        private readonly string _componentName;
        private readonly string _mockTypeName;
        private readonly string _unsupportedMember;

        public MockScript(
            string name,
            string content,
            MockFailureKind failureKind,
            string componentName,
            string mockTypeName,
            string unsupportedMember,
            ScriptLocation location)
        {
            _name = name;
            _content = content;
            FailureKind = failureKind;
            _componentName = componentName;
            _mockTypeName = mockTypeName;
            _unsupportedMember = unsupportedMember;
            Location = location;
        }

        public string Name
        {
            get { return _name ?? string.Empty; }
        }

        public string Content
        {
            get { return _content ?? string.Empty; }
        }

        public MockFailureKind FailureKind { get; }

        public string ComponentName
        {
            get { return _componentName ?? string.Empty; }
        }

        public string MockTypeName
        {
            get { return _mockTypeName ?? string.Empty; }
        }

        public string UnsupportedMember
        {
            get { return _unsupportedMember ?? string.Empty; }
        }

        public ScriptLocation Location { get; }

        public bool IsSupported
        {
            get { return FailureKind == MockFailureKind.None; }
        }

        public bool Equals(MockScript other)
        {
            return FailureKind == other.FailureKind
                && string.Equals(Name, other.Name, StringComparison.Ordinal)
                && string.Equals(Content, other.Content, StringComparison.Ordinal)
                && string.Equals(ComponentName, other.ComponentName, StringComparison.Ordinal)
                && string.Equals(MockTypeName, other.MockTypeName, StringComparison.Ordinal)
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
                int hash = (int)FailureKind;
                hash = (hash * 397) ^ Name.GetHashCode();
                hash = (hash * 397) ^ Content.GetHashCode();
                hash = (hash * 397) ^ ComponentName.GetHashCode();
                hash = (hash * 397) ^ MockTypeName.GetHashCode();
                hash = (hash * 397) ^ UnsupportedMember.GetHashCode();
                hash = (hash * 397) ^ Location.GetHashCode();
                return hash;
            }
        }
    }
}
