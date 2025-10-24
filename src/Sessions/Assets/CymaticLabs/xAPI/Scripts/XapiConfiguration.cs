using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CymaticLabs.xAPI.Unity3d
{
    /// <summary>
    /// Stores a configuration for <see href="https://xapi.com">xAPI</see>.
    /// </summary>
    public class XapiConfiguration : ScriptableObject
    {
        #region Inspector

        /// <summary>
        /// The name of the configuration.
        /// </summary>
        [Tooltip("The name of the configuration.")]
        public string Name = "Default xAPI Config";

        /// <summary>
        /// The API version to use with the client.
        /// </summary>
        [Tooltip("The API version to use with the client.")]
        public string ApiVersion = "1.0.3";

        /// <summary>
        /// The base xAPI URL.
        /// </summary>
        [Tooltip("The base xAPI URL.")]
        public string BaseURL = "http://xapi.cymaticlabs.net/data/xAPI/";

        /// <summary>
        /// The HTTP BasicAuth client token to use to authenticate the client with the API.
        /// </summary>
        [Tooltip("The HTTP BasicAuth client token to use to authenticate the client with the API.")]
        public string BasicAuth = "";

        /// <summary>
        /// The default statement to use. This acts as the "additive" basis for common fields when creating statements.
        /// </summary>
        [Tooltip("The default statement to use. This acts as the \"additive\" basis for common fields when creating statements.")]
        public XapiStatement DefaultStatement;

        /// <summary>
        /// Used as a work around to allow edits compatibility with Unity Editor's Undo/Redo stack.
        /// </summary>
        static internal Action<XapiConfiguration> OnCreated;

        /// <summary>
        /// Used as a work around to allow edits compatibility with Unity Editor's Undo/Redo stack.
        /// </summary>
        static internal Action<XapiConfiguration> OnDestroyed;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        public XapiConfiguration() : this("1.0.3")
        {
        }

        public XapiConfiguration(string apiVersion = "1.0.3")
        {
            ApiVersion = apiVersion;
        }

        #endregion Properties

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
