using System;

namespace reromanlee.Mocker
{
    /// <summary>
    /// Marks an abstract partial class as the root composite that owns child nodes and components
    /// and coordinates construction, asynchronous initialization, and deterministic disposal.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class CompositeAttribute : Attribute
    {
    }
}
