using System;
using UnityEngine;

namespace CymaticLabs.xAPI.Unity3d
{
    /// <summary>
    /// Represents an xAPI context definition.
    /// </summary>
    [Serializable]
    public class XapiContext : ScriptableObject
    {
        /// <summary>
        /// The context's platform string.
        /// </summary>
        [Tooltip("The context's platform string.")]
        public string Platform = "Cymatic Sessions Client";

        /// <summary>
        /// The context's language
        /// </summary>
        [Tooltip("The context's language")]
        public string Language = "en";
    }
}
