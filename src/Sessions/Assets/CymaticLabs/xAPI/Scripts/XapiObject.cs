using System;
using UnityEngine;

namespace CymaticLabs.xAPI.Unity3d
{
    /// <summary>
    /// Represents an xAPI verb definition.
    /// </summary>
    [Serializable]
    public class XapiObject : ScriptableObject
    {
        /// <summary>
        /// The objects's ID.
        /// </summary>
        [Tooltip("The objects's ID.")]
        public string Id;

        /// <summary>
        /// The object's language
        /// </summary>
        [Tooltip("The object's language")]
        public string Language = "en";

        /// <summary>
        /// The object's name.
        /// </summary>
        [Tooltip("The object's name.")]
        public string Name;

        /// <summary>
        /// The object's type.
        /// </summary>
        [Tooltip("The object's type.")]
        public string Type;
    }
}
