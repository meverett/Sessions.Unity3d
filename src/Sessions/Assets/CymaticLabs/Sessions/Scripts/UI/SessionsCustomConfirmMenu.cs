using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Logic for the user confirmation menu.
    /// </summary>
    public class SessionsCustomConfirmMenu : SessionsMenuBase
    {
        #region Inspector

        /// <summary>
        /// The message text component.
        /// </summary>
        [Tooltip("The message text component.")]
        public Text Message;

        /// <summary>
        /// The confirm's OK button.
        /// </summary>
        [Tooltip("The confirm's OK button.")]
        public Button OKButton;

        /// <summary>
        /// The confirm's Cancel button.
        /// </summary>
        [Tooltip("The confirm's Cancel button.")]
        public Button CancelButton;

        #endregion Inspector

        #region Fields

        // Internal button callback references
        private Action okCallback;
        private Action cancelCallback;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static SessionsCustomConfirmMenu Current { get; private set; }

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
            //if (SessionsMainMenu.Current != null) SessionsMainMenu.Current.OnHideAllMenus.AddListener(() => { HideMenu(); });

            // Register OK click callback pass through
            if (OKButton != null)
            {
                OKButton.onClick.AddListener(() =>
                {
                    if (okCallback != null) okCallback();
                    okCallback = null;
                    HideMenu();
                });
            }

            // Register Cancel click callback pass through
            if (CancelButton != null)
            {
                CancelButton.onClick.AddListener(() =>
                {
                    if (cancelCallback != null) cancelCallback();
                    cancelCallback = null;
                    HideMenu();
                });
            }

            HideMenu();
        }

        #endregion Init

        #region Operation

        /// <summary>
        /// Shows the debug menu.
        /// </summary>
        /// <param name="message">The message to display in the dialog window.</param>
        /// <param name="okCallback">The callback to bind to the OK button.</param>
        /// <param name="cancelCallback">The callback to bind to the Cancel button.</param>
        public void ShowMenu(string message = null, Action okCallback = null, Action cancelCallback = null)
        {
            if (MenuContainer == null) return;
            MenuContainer.SetActive(true);
            if (Message != null) Message.text = message;
            this.okCallback = okCallback;
            this.cancelCallback = cancelCallback;
        }

        /// <summary>
        /// Hides the debug menu.
        /// </summary>
        protected override void OnHideMenu()
        {
            base.OnHideMenu();
            okCallback = null;
            cancelCallback = null;
        }

        #endregion Operation

        #endregion Methods
    }
}