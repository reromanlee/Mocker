using System;

namespace reromanlee.Mocker
{
    /// <summary>
    /// Marks this assembly as the composition root: the one place where the selection enums and the
    /// wiring are generated. Put it in exactly one assembly, the one that references both the assembly
    /// declaring the composite and every assembly holding implementors.
    /// </summary>
    /// <remarks>
    /// This has to be said out loud rather than guessed. An implementor must reference the assembly that
    /// declares the component it implements, so that assembly can never reference the implementors back
    /// and can never see them. Only a third assembly, downstream of both, sees everything at once.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public sealed class MockerRootAttribute : Attribute
    {
    }
}
