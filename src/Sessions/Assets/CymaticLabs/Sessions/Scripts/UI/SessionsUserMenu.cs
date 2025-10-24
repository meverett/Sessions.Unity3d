using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CymaticLabs.Sessions.Core;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Logic for the the user menu.
    /// </summary>
    public class SessionsUserMenu : SessionsMenuBase
    {
        #region Inspector

        /// <summary>
        /// The input field used for the agent's name.
        /// </summary>
        public Text NameText;

        /// <summary>
        /// The button used to submit the name
        /// </summary>
        public Button SubmitButton;

        /// <summary>
        /// Occurs when the user submits the form.
        /// </summary>
        public UnityEvent OnFormSubmitted;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static SessionsUserMenu Current { get; private set; }

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

            // Set the intial name value from player preferences
            var name = PlayerPrefs.GetString("SessionsAgentName");

            if (!string.IsNullOrEmpty(name))
                NameText.text = name;
        }

        #endregion Init

        #region Update

        #endregion Update

        #region Operation

        /// <summary>
        /// Shows the menu.
        /// </summary>
        protected override void OnShowMenu()
        {
            EventSystem.current.SetSelectedGameObject(null);
            MenuContainer.SetActive(true);
            SessionsKeyboard.Show();
            SessionsKeyboard.Bind(NameText);
        }

        /// <summary>
        /// Hides the menu.
        /// </summary>
        protected override void OnHideMenu()
        {
            MenuContainer.SetActive(false);
            SessionsKeyboard.Bind(null);
            SessionsKeyboard.Hide();
        }

        #endregion Operation

        #region Submit

        /// <summary>
        /// Accepts the form and updates values.
        /// </summary>
        public void Submit()
        {
            if (NameText != null)
            {
                if (!string.IsNullOrEmpty(NameText.text))
                {
                    // Update the name on self
                    if (SessionsUdpNetworking.Current != null && SessionsUdpNetworking.Current.Self != null)
                        SessionsUdpNetworking.Current.Self.Name = NameText.text;

                    // Save name to player preferences
                    PlayerPrefs.SetString("SessionsAgentName", NameText.text);

                    // Hide self
                    HideMenu();

                    // Notify of submission
                    if (OnFormSubmitted != null) OnFormSubmitted.Invoke();
                }
                else
                {
                    SessionsNotifications.GlobalNotify("Name cannot be blank", "Error");
                }
            }
            else
            {
                Debug.LogWarning("Name text reference is missing");
            }
        }

        #endregion Submit

        #endregion Methods
    }
}
