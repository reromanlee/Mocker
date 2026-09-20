namespace GeneratorLibrary.Mocker
{
    /// <summary>
    /// Outcome of following a type up its parent chain towards the composite at the root.
    /// </summary>
    public enum MockerStatus
    {
        /// <summary>
        /// The chain reaches a composite, so the type has a place in the generated API.
        /// </summary>
        Resolved,

        /// <summary>
        /// No parent was written at all.
        /// </summary>
        MissingParent,

        /// <summary>
        /// The parent that was written carries no Mocker attribute.
        /// </summary>
        UnknownParent,

        /// <summary>
        /// The chain loops back on itself and never reaches a composite.
        /// </summary>
        Cycle
    }
}
