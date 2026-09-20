namespace GeneratorLibrary.Mocker
{
    /// <summary>
    /// What a detected type is, which decides how it is placed in the composite tree.
    /// </summary>
    public enum MockerRole
    {
        /// <summary>
        /// Root of a tree. Has no parent.
        /// </summary>
        Composite,

        /// <summary>
        /// Group of submodules. Its parent is the composite or another node.
        /// </summary>
        Node,

        /// <summary>
        /// Mocked interface. Its parent is the composite or a node.
        /// </summary>
        Component,

        /// <summary>
        /// Concrete provider. Its parent is the component interface it implements.
        /// </summary>
        Implementor
    }
}
