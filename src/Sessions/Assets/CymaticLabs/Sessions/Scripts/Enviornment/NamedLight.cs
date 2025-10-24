using System;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Used to register a light by name with the <see cref="SessionsEnvironment"/>.
    /// </summary>
    [Serializable]
    public class NamedLight
    {
        /// <summary>
        /// The name to register this light with.
        /// </summary>
        [Tooltip("The name to register this light with.")]
        public string Name = "Light";

        /// <summary>
        /// The light to use.
        /// </summary>
        [Tooltip("The light to use.")]
        public Light Light;

        /// <summary>
        /// The minimum intensity of the light.
        /// </summary>
        [Tooltip("The minimum intensity of the light.")]
        public float MinIntensity = 0;

        /// <summary>
        /// The maximum intensity of the light.
        /// </summary>
        [Tooltip("The maximum intensity of the light.")]
        public float MaxIntensity = 1;
    }
}
