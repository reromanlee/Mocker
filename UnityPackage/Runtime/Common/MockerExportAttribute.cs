using System;

namespace reromanlee.Mocker
{
    /// <summary>
    /// Publishes one Mocker type so that assemblies referencing this one can find it by reading a single
    /// attribute list instead of scanning every type. Written by the generator, never by hand.
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
