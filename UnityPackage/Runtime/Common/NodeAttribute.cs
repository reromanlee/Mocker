using System;

namespace reromanlee.Mocker
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class NodeAttribute : Attribute
    {
        public readonly string Name;
        public readonly Type ParentType;

        public NodeAttribute(string name, Type parentType)
        {
            Name = name;
            ParentType = parentType;
        }
    }
}