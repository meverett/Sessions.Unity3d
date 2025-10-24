using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CymaticLabs.Logging;

namespace CymaticLabs.xAPI.Unity3d
{
    /// <summary>
    /// Exposes a configured xAPI statement on a behaviour so it can be interacted with from Unity.
    /// </summary>
    public class XapiStatementBehaviour : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The <see cref="XapiManager">xAPI Manager</see> instance to use.
        /// </summary>
        [Tooltip("The xAPI Manager instance to use.")]
        public XapiManager XapiManager;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
        }

        private void Start()
        {
            if (XapiManager == null) XapiManager = XapiManager.Current;

            if (XapiManager == null)
            {
                CyLog.LogWarn("No XapiManager instance was found and xAPI statement will be disabled.");
                return;
            }
        }

        #endregion Init

        #region xAPI

        /// <summary>
        /// Handles an xAPI statement event.
        /// </summary>
        /// <param name="actor">The statement actor.</param>
        /// <param name="verb">The statement verb.</param>
        /// <param name="obj">The statement object.</param>
        public void HandleStatementEvent(string actor, string verb, string obj)
        {
            var xapi = XapiManager;
            if (xapi == null) return;
            xapi.CreateStatement(actor, verb, obj);
        }

        #endregion xAPI

        #endregion Methods
    }
}
