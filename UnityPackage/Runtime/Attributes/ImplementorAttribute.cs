using System;

namespace reromanlee.Mocker
{
    /// <summary>
    /// Marks a concrete class as a selectable implementation of a component interface or abstract class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ImplementorAttribute : Attribute
    {
        public readonly Type ComponentType;

        public ImplementorAttribute(Type componentType)
        {
            ComponentType = componentType;
        }
    }
}
