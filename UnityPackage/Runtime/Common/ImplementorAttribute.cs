using System;

namespace reromanlee.Mocker
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class ImplementorAttribute : Attribute
    {
        public readonly Type InterfaceType;

        public ImplementorAttribute(Type interfaceType)
        {
            InterfaceType = interfaceType;
        }
    }
}
