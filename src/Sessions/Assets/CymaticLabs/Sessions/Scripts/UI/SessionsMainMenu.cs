using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using CymaticLabs.Logging;
using CymaticLabs.Sessions.Core;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// The main runtime user menu.
    /// </summary>
    public class SessionsMainMenu : SessionsMenuBase
    {
        #region Inspector

        /// <summary>
        /// The number of seconds to delay on the start of the scene before showing the main menu.
        /// </summary>
        [Tooltip("The number of seconds to delay on the start of the scene before showing the main menu.")]
        public float ShowDelay = 0;

        /// <summary>
        /// The number of seconds to fade in the menu.
        /// </summary>
        [Tooltip("The number of seconds to fade in the menu.")]
        public float FadeInDuration = 1;

        /// <summary>
        /// The canvas group to use with the main menu.
        /// </summary>
        [Tooltip("The canvas group to use with the main menu.")]
        public CanvasGroup CanvasGroup;

        /// <summary>
        /// The name of the user input axis to user control the main menu.
        /// </summary>
        [Tooltip("The name of the user input axis to user control the main menu.")]
        public string UserInputAxis = "Back";

        /// <summary>
        /// The UI follower reference to use that is controlling the main menu's canvas.
        /// </summary>
        [Tooltip("The UI follower reference to use that is controlling the main menu's canvas.")]
        public UIFollower Follower;

        /// <summary>
        /// The return to lobby button.
        /// </summary>
        [Tooltip("The return to lobby button.")]
        public Button LobbyButton;

        /// <summary>
        /// Occurs when the main menu gives the signal for all other menus to close.
        /// </summary>
        public UnityEvent OnHideAllMenus;

        #endregion Inspector

        #region Fields

        

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static SessionsMainMenu Current { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            Current = this;   
        }

        private void Start()
        {
            // Register to listen to user back button input to toggle menu state
            if (SessionsUser.Current == null)
            {
                CyLog.LogWarn("No SessionsUser reference was found. SessionsMainMenu will not function correctly.");
            }
            else
            {
                var user = SessionsUser.Current;
                user.OnUserInputDown.AddListener(HandleUserInputDown);
            }

            if (SessionsSceneManager.Current != null)
            {
                if (LobbyButton != null) LobbyButton.interactable = !SessionsSceneManager.Current.IsLobby;
            }

            HideMenu();

            // If this is the lobby scene, open the main menu by default and fade it in
            //if (SessionsSceneManager.Current != null && SessionsSceneManager.Current.IsLobby)
            //{
            //    if (CanvasGroup != null) CanvasGroup.alpha = 0;
            //    StartCoroutine(DoShowMenu(ShowDelay));
            //}
            //else
            //{
            //    HideMenu();
            //}
        }

        // Shows the menu after a delay
        private IEnumerator DoShowMenu(float delay = 0)
        {
            if (delay > 0 && SessionsUdpNetworking.IsXR) yield return new WaitForSeconds(delay);
            if (CanvasGroup != null) CanvasGroup.alpha = 0;
            ShowMenu();

            // Play welcome sound
            var info = SessionsSound.Current.EfxClips.FirstOrDefault(nc => nc.Name == "Welcome");
            SessionsSound.PlayEfx("Welcome", false, info != null ? info.Volume : 1, true);

            // Fade in the canvas group if one is present
            if (CanvasGroup != null)
            {
                float timer = 0;

                while (timer < FadeInDuration)
                {
                    timer += Time.deltaTime;
                    CanvasGroup.alpha = Mathf.SmoothStep(0, 1, timer / FadeInDuration);
                    yield return 0;
                }

                CanvasGroup.alpha = 1;
            }
        }

        #endregion Init

        #region Operation

        /// <summary>
        /// Shows the menu.
        /// </summary>
        protected override void OnShowMenu()
        {
            EventSystem.current.SetSelectedGameObject(null);
            if (MenuContainer != null) MenuContainer.SetActive(true);
            SessionsKeyboard.Hide();

            // Rotate to face user
            //if (Follower != null) Follower.UpdateOrientation(true, true);
        }
   
        /// <summary>
        /// Closes all open submenus.
        /// </summary>
        public void HideAllSubmenus()
        {
            OnHideAllMenus.Invoke();
        }

        #endregion Operation

        #region Update

        #endregion Update

        #region Event Handlers

        // Handles user input "down" events
        private void HandleUserInputDown(string input, float value)
        {
            // For now we only respond to the configured button
            if (input != UserInputAxis || SessionsUser.Current.PrimaryButton) return; // ignore if primary is also down when pressed

            // If the main menu is not currently open, hide all other menus, and open the main menu
            if (!IsMenuOpen)
            {
                HideAllSubmenus();
                ShowMenu();
            }
            // Otherwise hide the main menu
            else
            {
                HideMenu();
            }
        }

        #endregion Event Handlers

        #endregion Methods
    }
}