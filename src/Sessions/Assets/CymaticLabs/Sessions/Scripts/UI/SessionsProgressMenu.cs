using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Logic for the user confirmation menu.
    /// </summary>
    public class SessionsProgressMenu : SessionsMenuBase
    {
        #region Inspector

        /// <summary>
        /// The progress window title text component.
        /// </summary>
        [Tooltip("The progress window title text component.")]
        public Text Title;

        /// <summary>
        /// The progress slider to use.
        /// </summary>
        [Tooltip("The progress slider to use.")]
        public Slider Progress;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static SessionsProgressMenu Current { get; private set; }

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
            // if (SessionsMainMenu.Current != null) SessionsMainMenu.Current.OnHideAllMenus.AddListener(() => { HideMenu(); });
            HideMenu();
        }

        #endregion Init

        #region Operation

        /// <summary>
        /// Shows the debug menu.
        /// </summary>
        protected override void OnShowMenu()
        {
            if (MenuContainer == null) return;
            MenuContainer.SetActive(true);
            if (SessionsMainMenu.Current != null) SessionsMainMenu.Current.HideAllSubmenus();
            if (SessionsFacilitatorMenu.Current != null) SessionsFacilitatorMenu.Current.HideMenu();
            SessionsKeyboard.Hide();
        }

        #endregion Operation

        #region Progress

        /// <summary>
        /// Sets the current progress.
        /// </summary>
        /// <param name="value">The current progress 0.0 - 1.0.</param>
        public void SetProgress(float value)
        {
            if (Progress != null) Progress.value = value;
        }

        /// <summary>
        /// Gets the current progress.
        /// </summary>
        /// <returns>The progress as a float 0.0 - 1.0.</returns>
        public float GetProgress()
        {
            return Progress != null ? Progress.value : 0;
        }

        #endregion Progress

        #endregion Methods
    }
}