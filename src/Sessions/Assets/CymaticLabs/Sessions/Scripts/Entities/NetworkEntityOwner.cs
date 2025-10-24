namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Different ownership relationships for network entity instnaces.
    /// </summary>
    public enum NetworkEntityOwner
    {
        /// <summary>
        /// The instance is not mine.
        /// </summary>
        NotMine,

        /// <summary>
        /// The instance is mine.
        /// </summary>
        IsMine,

        /// <summary>
        /// The instance is everyone's.
        /// </summary>
        Everyone
    }
}
