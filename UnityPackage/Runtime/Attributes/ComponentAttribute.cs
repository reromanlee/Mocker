using System;

namespace reromanlee.Mocker
{
    /// <summary>
    /// Marks a partial interface or abstract partial class as a swappable component exposed under
    /// <see cref="Name"/> on its parent composite or node.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ComponentAttribute : Attribute
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
