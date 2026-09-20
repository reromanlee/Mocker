using System;

namespace reromanlee.Mocker
{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public class ComponentAttribute : Attribute
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
