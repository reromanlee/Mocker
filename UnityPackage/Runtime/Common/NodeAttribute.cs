using System;

namespace reromanlee.Mocker
{
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