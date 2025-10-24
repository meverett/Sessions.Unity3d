using System;
using System.Collections.Generic;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Used to serialize routing rules into a configured set that can be associated with a scene.
    /// </summary>
    public class SessionsRoutingConfiguration : ScriptableObject
    {
        #region Fields

        /// <summary>
        /// The version of the settings.
        /// </summary>
        [HideInInspector]
        public float Version;

        /// <summary>
        /// The name of the configuration.
        /// </summary>
        [HideInInspector]
        public string Name;

        /// <summary>
        /// The rule set for the configuration.
        /// </summary>
        [HideInInspector]
        public SessionsRoutingRule[] Rules;

        /// <summary>
        /// Used as a work around to allow edits compatibility with Unity Editor's Undo/Redo stack.
        /// </summary>
        static internal Action<SessionsRoutingConfiguration> OnCreated;

        /// <summary>
        /// Used as a work around to allow edits compatibility with Unity Editor's Undo/Redo stack.
        /// </summary>
        static internal Action<SessionsRoutingConfiguration> OnDestroyed;

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Constructors

        public SessionsRoutingConfiguration() : this(1.0f)
        {
        }

        public SessionsRoutingConfiguration(float version = 1.0f)
        {
            Version = version;
        }

        #endregion Constructors

        #region Methods

        private void Awake()
        {
            if (OnCreated != null) OnCreated(this);
        }

        private void OnDestroy()
        {
            if (OnDestroyed != null) OnDestroyed(this);
        }

        #endregion Methods
    }
}
