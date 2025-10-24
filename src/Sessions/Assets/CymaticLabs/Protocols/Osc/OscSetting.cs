namespace CymaticLabs.Protocols.Osc.Unity3d
{
    /// <summary>
    /// Maps an expected input range to a desired output range for OSC messages
    /// with floating point message arguments.
    /// </summary>
    [System.Serializable]
    public class OscSettings
    {
        /// <summary>
        /// The port number to listen for OSC messages on.
        /// </summary>
        public int ReceivePort;

        /// <summary>
        /// A collection of value in/out range mappings for 
        /// particular OSC addresses.
        /// </summary>
        public OscRangeMapFloat[] FloatRangeMappings;

        /// <summary>
        /// A collection of numeric effect ID to effect name
        /// mappings. Used to map numeric ID values to a named effect.  
        /// </summary>
        public OscEffectIdToNameMap[] EffectIdToNameMappings;
    }
}
