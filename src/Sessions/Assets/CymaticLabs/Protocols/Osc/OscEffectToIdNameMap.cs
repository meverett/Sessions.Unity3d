namespace CymaticLabs.Protocols.Osc
{
    /// <summary>
    /// Maps an incoming OSC effect ID to its matching effect name.
    /// </summary>
    [System.Serializable]
    public class OscEffectIdToNameMap
    {
        /// <summary>
        /// The effect ID.
        /// </summary>
        public int Id;

        /// <summary>
        /// The effect name to map to.
        /// </summary>
        public string Name;
    }
}
