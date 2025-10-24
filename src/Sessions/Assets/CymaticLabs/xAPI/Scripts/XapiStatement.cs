using System;
using UnityEngine;

namespace CymaticLabs.xAPI.Unity3d
{
    /// <summary>
    /// Represents an xAPI statement.
    /// </summary>
    [Serializable]
    public class XapiStatement : ScriptableObject
    {
        /// <summary>
        /// The statement's unique ID.
        /// </summary>
        [Tooltip("The statement's unique ID.")]
        public string Id;

        /// <summary>
        /// The statement's context.
        /// </summary>
        [Tooltip("The statement's context.")]
        public XapiContext Context;

        /// <summary>
        /// The statement's actor.
        /// </summary>
        [Tooltip("The statement's actor.")]
        public XapiActor Actor;

        /// <summary>
        /// The statement's verb.
        /// </summary>
        [Tooltip("The statement's verb.")]
        public XapiVerb Verb;

        /// <summary>
        /// The statement's object.
        /// </summary>
        [Tooltip("The statement's object.")]
        public XapiObject Object;

        /// <summary>
        /// The statement's time stamp.
        /// </summary>
        [Tooltip("The statement's time stamp.")]
        public DateTime Timestamp;
    }
}
