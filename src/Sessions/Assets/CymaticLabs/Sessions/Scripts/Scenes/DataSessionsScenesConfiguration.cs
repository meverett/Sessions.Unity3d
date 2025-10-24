using System;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Represents a collection of sessions scenes.
    /// </summary>
    [Serializable]
    public class DataSessionsScenesConfiguration
    {
        /// <summary>
        /// The name of the configuration.
        /// </summary>
        public string Name;

        /// <summary>
        /// The version of the settings.
        /// </summary>
        public float Version;

        /// <summary>
        /// The list of available scenes.
        /// </summary>
        public DataSessionsSceneInfo[] Scenes;
    }
}
