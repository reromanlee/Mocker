using System;

namespace reromanlee.Mocker
{
    /// <summary>
    /// Emitted by the source generator to publish a Mocker symbol on its declaring assembly so
    /// downstream assemblies discover cross-assembly graphs without scanning every type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public sealed class MockerExportAttribute : Attribute
    {
        public readonly string Role;
        public readonly Type Type;
        public readonly string Name;
        public readonly Type ParentType;

        public MockerExportAttribute(string role, Type type, string name, Type parentType)
        {
            Role = role;
            Type = type;
            Name = name;
            ParentType = parentType;
        }
    }
}
