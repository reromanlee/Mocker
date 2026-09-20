using System;

namespace reromanlee.Mocker
{
    public class ComponentAttribute
    {
        public readonly string Name;
        public readonly Type ParentType;

        public ComponentAttribute(string name, Type parentType)
        {
            Name = name;
            ParentType = parentType;
        }
    }
}
