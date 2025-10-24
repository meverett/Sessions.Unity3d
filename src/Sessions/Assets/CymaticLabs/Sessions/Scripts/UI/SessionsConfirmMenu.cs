using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Logic for the user confirmation menu.
    /// </summary>
    public class SessionsConfirmMenu : SessionsMenuBase
    {
        #region Inspector

        /// <summary>
        /// The message text component.
        /// </summary>
        [Tooltip("The message text component.")]
        public Text Message;     

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static SessionsConfirmMenu Current { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            Current = this;    
        }

        private void Start()
        {
            // Register with main menu events
            if (SessionsMainMenu.Current != null) SessionsMainMenu.Current.OnHideAllMenus.AddListener(() => { HideMenu(); });
            HideMenu();
        }

        #endregion Init

        #endregion Methods
    }
}