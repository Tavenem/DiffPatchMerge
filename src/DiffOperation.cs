namespace Tavenem.DiffPatchMerge
{
    /// <summary>
    /// The operation represented by a particular change in a text.
    /// </summary>
    public enum DiffOperation
    {
        /// <summary>
        /// A removal from the original text.
        /// </summary>
        Deleted = -1,

        /// <summary>
        /// The original text is unchanged.
        /// </summary>
        Unchanged = 0,

        /// <summary>
        /// An insertion into the original text.
        /// </summary>
        Inserted = 1,
    }
}
