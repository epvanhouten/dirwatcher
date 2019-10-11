namespace DirWatcher
{
    /// <summary>
    /// Represents an observed changed.
    /// </summary>
    public enum StateChangeActionEnum
    {
        /// <summary>
        /// A new file was observed.
        /// </summary>
        New,

        /// <summary>
        /// A file was deleted.
        /// </summary>
        Deleted,

        /// <summary>
        /// Last write time was updated.
        /// </summary>
        Changed,

        /// <summary>
        /// No changed was observed.
        /// </summary>
        None,
    }
}