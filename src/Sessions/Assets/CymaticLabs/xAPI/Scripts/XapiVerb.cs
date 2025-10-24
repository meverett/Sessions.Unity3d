using System;
using UnityEngine;

namespace CymaticLabs.xAPI.Unity3d
{
    /// <summary>
    /// Represents an xAPI verb definition.
    /// </summary>
    [Serializable]
    public class XapiVerb : ScriptableObject
    {
        /// <summary>
        /// The verb's ID.
        /// </summary>
        [Tooltip("The verb's ID.")]
        public string Id;

        /// <summary>
        /// The verb's language
        /// </summary>
        [Tooltip("The verb's language")]
        public string Language = "en";

        /// <summary>
        /// The name of the verb.
        /// </summary>
        [Tooltip("The name of the verb.")]
        public string Name;
    }
}
