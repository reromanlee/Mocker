using System;

namespace reromanlee.Mocker
{
    /// <summary>
    /// Marks a partial class as a composite: the base class that holds the modules and manages
    /// their creation, dependency injection and disposal. The generated part is created in the
    /// same assembly as the marked class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class CompositeAttribute : Attribute
    {
    }
}
