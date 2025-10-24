using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Base class for Sessions UI menus.
    /// </summary>
    public class SessionsMenuBase : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The game object acting as the menu container.
        /// </summary>
        [Tooltip("The game object acting as the menu container.")]
        public GameObject MenuContainer;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        /// <summary>
        /// Whether or not the menu is currently open.
        /// </summary>
        public bool IsMenuOpen { get { return MenuContainer != null && MenuContainer.activeSelf; } }

        /// <summary>
        /// Returns whether or not any menu is currently open.
        /// </summary>
        public static bool IsAnyMenuOpen
        {
            get
            {
                if (SessionsConfirmMenu.Current != null && SessionsConfirmMenu.Current.IsMenuOpen) return true;
                if (SessionsCustomConfirmMenu.Current != null && SessionsCustomConfirmMenu.Current.IsMenuOpen) return true;
                if (SessionsFacilitatorMenu.Current != null && SessionsFacilitatorMenu.Current.IsMenuOpen) return true;
                if (SessionsMainMenu.Current != null && SessionsMainMenu.Current.IsMenuOpen) return true;
                if (SessionsNetworkMenu.Current != null && SessionsNetworkMenu.Current.IsMenuOpen) return true;
                if (SessionsProgressMenu.Current != null && SessionsProgressMenu.Current.IsMenuOpen) return true;
                if (SessionsUserMenu.Current != null && SessionsUserMenu.Current.IsMenuOpen) return true;
                if (VoiceSettingsMenu.Current != null && VoiceSettingsMenu.Current.IsMenuOpen) return true;
                return false;
            }
        }

        #endregion Properties

        #region Methods

        #region Init

        #endregion Init

        #region Update

        #endregion Update

        #region Operation

        /// <summary>
        /// Shows the menu.
        /// </summary>
        public void ShowMenu()
        {
            OnShowMenu();
        }

        /// <summary>
        /// Shows the menu.
        /// </summary>
        protected virtual void OnShowMenu()
        {
            if (MenuContainer != null) MenuContainer.SetActive(true);
        }

        /// <summary>
        /// Hides the menu.
        /// </summary>
        public void HideMenu()
        {
            OnHideMenu();
        }

        /// <summary>
        /// Hides the menu.
        /// </summary>
        protected virtual void OnHideMenu()
        {
            if (MenuContainer != null) MenuContainer.SetActive(false);
        }

        #endregion Operation

        #endregion Methods
    }
}
