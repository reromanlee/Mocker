using System;

namespace reromanlee.Mocker
{
    /// <summary>
    /// Marks this assembly as a composition root: a place where the selection enums and the wiring are
    /// generated. Name the composites it owns to keep it to those, or name none to take everything this
    /// assembly can see.
    /// </summary>
    /// <remarks>
    /// This has to be said out loud rather than guessed. An implementor must reference the assembly that
    /// declares the component it implements, so that assembly can never reference the implementors back
    /// and can never see them. Only a third assembly, downstream of both, sees everything at once.
    /// Two roots that both see the same composite would generate the same enums twice, which only breaks
    /// once something references both of them, so name the composites when more than one root exists.
    /// </remarks>
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
