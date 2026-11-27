using reromanlee.Mocker.Generator.Common;
using System;

namespace reromanlee.Mocker.Generator.Mocker
{
    public enum MockMemberKind
    {
        Method = 0,
        Property = 1,
        Event = 2
    }

    /// <summary>
    /// Equatable description of a single interface or abstract member that a generated mock must implement.
    /// </summary>
    public readonly struct MockMemberModel : IEquatable<MockMemberModel>
    {
        private readonly string _memberKey;
        private readonly string _memberName;
        private readonly string _declaration;
        private readonly string _bodyLines;

        public MockMemberModel(
            MockMemberKind kind,
            string memberKey,
            string memberName,
            string declaration,
            string bodyLines,
            bool isUnsupported,
            bool needsAwaitable,
            bool needsAwaitableResult)
        {
            Kind = kind;
            _memberKey = memberKey;
            _memberName = memberName;
            _declaration = declaration;
            _bodyLines = bodyLines;
            IsUnsupported = isUnsupported;
            NeedsAwaitable = needsAwaitable;
            NeedsAwaitableResult = needsAwaitableResult;
        }

        public MockMemberKind Kind { get; }

        public string MemberKey
        {
            get { return _memberKey ?? string.Empty; }
        }

        public string MemberName
        {
            get { return _memberName ?? string.Empty; }
        }

        public string Declaration
        {
            get { return _declaration ?? string.Empty; }
        }

        public string BodyLines
        {
            get { return _bodyLines ?? string.Empty; }
        }

        public bool IsUnsupported { get; }

        public bool NeedsAwaitable { get; }

        public bool NeedsAwaitableResult { get; }

        public bool Equals(MockMemberModel other)
        {
            return Kind == other.Kind
                && IsUnsupported == other.IsUnsupported
                && NeedsAwaitable == other.NeedsAwaitable
                && NeedsAwaitableResult == other.NeedsAwaitableResult
                && string.Equals(MemberKey, other.MemberKey, StringComparison.Ordinal)
                && string.Equals(MemberName, other.MemberName, StringComparison.Ordinal)
                && string.Equals(Declaration, other.Declaration, StringComparison.Ordinal)
                && string.Equals(BodyLines, other.BodyLines, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is MockMemberModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ MemberKey.GetHashCode();
                hash = (hash * 397) ^ MemberName.GetHashCode();
                hash = (hash * 397) ^ Declaration.GetHashCode();
                hash = (hash * 397) ^ BodyLines.GetHashCode();
                hash = (hash * 397) ^ (IsUnsupported ? 1 : 0);
                hash = (hash * 397) ^ (NeedsAwaitable ? 1 : 0);
                hash = (hash * 397) ^ (NeedsAwaitableResult ? 1 : 0);
                return hash;
            }
        }
    }

    /// <summary>
    /// Equatable description of a component ready for mock emission, isolated from Roslyn symbols
    /// so incremental steps cache cleanly across edits.
    /// </summary>
    public readonly struct ComponentMockModel : IEquatable<ComponentMockModel>
    {
        private readonly string _mockName;
        private readonly string _mockFullName;

        public ComponentMockModel(
            TypeTarget type,
            string mockName,
            string mockFullName,
            bool isInterface,
            bool isValidForMock,
            bool hasAccessibleParameterlessConstructor,
            EquatableArray<MockMemberModel> members,
            ScriptLocation location)
        {
            Type = type;
            _mockName = mockName;
            _mockFullName = mockFullName;
            IsInterface = isInterface;
            IsValidForMock = isValidForMock;
            HasAccessibleParameterlessConstructor = hasAccessibleParameterlessConstructor;
            Members = members;
            Location = location;
        }

        public TypeTarget Type { get; }

        public string MockName
        {
            get { return _mockName ?? string.Empty; }
        }

        public string MockFullName
        {
            get { return _mockFullName ?? string.Empty; }
        }

        public bool IsInterface { get; }

        public bool IsValidForMock { get; }

        public bool HasAccessibleParameterlessConstructor { get; }

        public EquatableArray<MockMemberModel> Members { get; }

        public ScriptLocation Location { get; }

        public bool Equals(ComponentMockModel other)
        {
            return Type.Equals(other.Type)
                && IsInterface == other.IsInterface
                && IsValidForMock == other.IsValidForMock
                && HasAccessibleParameterlessConstructor == other.HasAccessibleParameterlessConstructor
                && string.Equals(MockName, other.MockName, StringComparison.Ordinal)
                && string.Equals(MockFullName, other.MockFullName, StringComparison.Ordinal)
                && Members.Equals(other.Members)
                && Location.Equals(other.Location);
        }

        public override bool Equals(object obj)
        {
            return obj is ComponentMockModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Type.GetHashCode();
                hash = (hash * 397) ^ MockName.GetHashCode();
                hash = (hash * 397) ^ MockFullName.GetHashCode();
                hash = (hash * 397) ^ (IsInterface ? 1 : 0);
                hash = (hash * 397) ^ (IsValidForMock ? 1 : 0);
                hash = (hash * 397) ^ (HasAccessibleParameterlessConstructor ? 1 : 0);
                hash = (hash * 397) ^ Members.GetHashCode();
                hash = (hash * 397) ^ Location.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>
    /// Equatable summary of members and constructors hand-written by the user inside a
    /// <c>partial class *Mock</c> declaration so generated mocks skip user-provided members without CS0111.
    /// </summary>
    public readonly struct UserPartialMock : IEquatable<UserPartialMock>
    {
        private readonly string _fullName;

        public UserPartialMock(string fullName, bool hasPublicParameterlessConstructor, EquatableArray<string> memberKeys)
        {
            _fullName = fullName;
            HasPublicParameterlessConstructor = hasPublicParameterlessConstructor;
            MemberKeys = memberKeys;
        }

        public string FullName
        {
            get { return _fullName ?? string.Empty; }
        }

        public bool HasPublicParameterlessConstructor { get; }

        public EquatableArray<string> MemberKeys { get; }

        public bool Equals(UserPartialMock other)
        {
            return HasPublicParameterlessConstructor == other.HasPublicParameterlessConstructor
                && string.Equals(FullName, other.FullName, StringComparison.Ordinal)
                && MemberKeys.Equals(other.MemberKeys);
        }

        public override bool Equals(object obj)
        {
            return obj is UserPartialMock other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = FullName.GetHashCode();
                hash = (hash * 397) ^ (HasPublicParameterlessConstructor ? 1 : 0);
                hash = (hash * 397) ^ MemberKeys.GetHashCode();
                return hash;
            }
        }
    }
}
