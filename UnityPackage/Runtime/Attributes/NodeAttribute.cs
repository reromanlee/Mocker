using System;

namespace reromanlee.Mocker
{
    /// <summary>
    /// Marks a partial class as a grouping node exposed under <see cref="Name"/> on its parent composite or node.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class NodeAttribute : Attribute
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
