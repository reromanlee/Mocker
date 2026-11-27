using System;

namespace reromanlee.Mocker
{
    /// <summary>
    /// Marks this assembly as a composition root where implementor enums, choices trees, and
    /// runnable composite wiring are generated for the listed composites (or all visible composites when omitted).
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public sealed class MockerRootAttribute : Attribute
    {
        public readonly Type[] Composites;

        public MockerRootAttribute(params Type[] composites)
        {
            Composites = composites;
        }
    }
}
