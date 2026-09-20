namespace GeneratorLibrary.Mocker
{
    /// <summary>
    /// Metadata names of the attributes shipped in Runtime/Common, which is how the generator looks them up.
    /// </summary>
    public static class MockerAttributes
    {
        private const string AttributeNamespace = "reromanlee.Mocker.";

        /// <summary>
        /// Base class that owns the modules and manages their creation, injection and disposal.
        /// </summary>
        public const string Composite = AttributeNamespace + "CompositeAttribute";

        /// <summary>
        /// Class that groups submodules under a name and can be nested into another node.
        /// </summary>
        public const string Node = AttributeNamespace + "NodeAttribute";

        /// <summary>
        /// Interface that is mocked at runtime, exposed under a name on its parent.
        /// </summary>
        public const string Component = AttributeNamespace + "ComponentAttribute";

        /// <summary>
        /// Concrete class that implements a component and can be selected as its provider.
        /// </summary>
        public const string Implementor = AttributeNamespace + "ImplementorAttribute";
    }
}
