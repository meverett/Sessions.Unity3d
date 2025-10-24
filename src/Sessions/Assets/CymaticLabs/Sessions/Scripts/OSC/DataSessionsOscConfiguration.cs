using UnityEngine;

namespace CymaticLabs.Protocols.Osc.Unity3d
{
    /// <summary>
    /// Used to represent the settings/mappings of an <see cref="SessionsOscValueController">OSC value controller instance</see>.
    /// </summary>
    [System.Serializable]
    public class DataSessionsOscConfiguration
    { 
        #region Fields

        #endregion Fields

        #region Properties

        /// <summary>
        /// The version of the settings.
        /// </summary>
        public float Version { get; set; }

        /// <summary>
        /// The name of the configuration.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The list of allowed float value mappings.
        /// </summary>
        public DataOscRangeMapFloat[] AllowedFloats { get; set; }

        #endregion Properties

        #region Constructors

        public DataSessionsOscConfiguration(float version = 1.0f)
        {
            Version = version;
        }

        #endregion Constructors

        #region Methods

        #endregion Methods
    }
}
