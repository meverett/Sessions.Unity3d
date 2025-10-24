using System;
using UnityEngine;

namespace CymaticLabs.xAPI.Unity3d
{
    /// <summary>
    /// Represents an xAPI actor definition.
    /// </summary>
    [Serializable]
    public class XapiActor : ScriptableObject
    {
        /// <summary>
        /// The actor's name.
        /// </summary>
        [Tooltip("The actor's name.")]
        public string Name;

        /// <summary>
        /// The actor's account URL (account.homePage).
        /// </summary>
        [Tooltip("The actor's account URL (account.homePage).")]
        public string AccoutURL;

        /// <summary>
        /// The actor's account name (account.name).
        /// </summary>
        [Tooltip("The actor's account name (account.name).")]
        public string AccountName;
    }
}
