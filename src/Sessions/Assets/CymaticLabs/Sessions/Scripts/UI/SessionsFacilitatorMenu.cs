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
using CymaticLabs.Logging;
using CymaticLabs.Sessions.Core;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Logic for the Facilitator service menu.
    /// </summary>
    public class SessionsFacilitatorMenu : SessionsMenuBase
    {
        #region Inspector

        /// <summary>
        /// The sessions networking manager.
        /// </summary>
        public SessionsUdpNetworking SessionsNetworking;

        /// <summary>
        /// The status text UI element.
        /// </summary>
        public Text Status;

        /// <summary>
        /// The element used as the root/parent of the peer list and lower menu content.
        /// </summary>
        public GameObject ContentContainer;

        /// <summary>
        /// The Text component that displays the current session name.
        /// </summary>
        public Text SessionName;

        /// <summary>
        /// The image used as a loading spinner.
        /// </summary>
        public Image Spinner;

        /// <summary>
        /// The progress slider used to show loading progress.
        /// </summary>
        public Slider Progress;

        /// <summary>
        /// The button used to join a session.
        /// </summary>
        public Button JoinButton;

        /// <summary>
        /// The button used to leave a session.
        /// </summary>
        public Button LeaveButton;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static SessionsFacilitatorMenu Current { get; private set; }

        /// <summary>
        /// Whether or not the current user is joining or hosting.
        /// </summary>
        public bool IsHosting { get; private set; }

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
            if (SessionsNetworking == null) SessionsNetworking = SessionsUdpNetworking.Current;
            HideMenu();
        }

        #endregion Init

        #region Update

        private void Update()
        {
            if (MenuContainer != null && MenuContainer.activeSelf)
            {
                // Keep the keyboard open while this menu is open
                SessionsKeyboard.Show();
            }
        }

        #endregion Update

        #region Operation

        /// <summary>
        /// Shows the menu.
        /// </summary>
        /// <param name="reset">Whether or not to force a reset of the menu when showing.</param>
        public void ShowMenu(bool reset = false)
        {
            if (SessionsNetworking == null) return;
            if (MenuContainer != null) MenuContainer.SetActive(true);
            EventSystem.current.SetSelectedGameObject(null);

            if (reset)
            {
                if (ContentContainer != null) ContentContainer.SetActive(true);
                if (Spinner != null) Spinner.gameObject.SetActive(false);
                if (Progress != null) Progress.gameObject.SetActive(false);
                if (Status) Status.text = "Enter Session Name";
            }

            // See if there was an previous session name saved
            var lastSession = PlayerPrefs.GetString("SessionsLastSessionName");

            // Use the last save session name if available, otherwise a default value
            SessionName.text = !string.IsNullOrEmpty(lastSession) ? lastSession : "My Session";

            // Bind to session name text "input"
            SessionsKeyboard.Bind(SessionName);
            SessionsKeyboard.Show();
        }

        protected override void OnShowMenu()
        {
            base.OnShowMenu();
            ShowMenu(false);
        }

        #endregion Operation

        #region Status

        /// <summary>
        /// Sets the menu status text.
        /// </summary>
        /// <param name="text">The text to set.</param>
        public void SetStatus(string text)
        {
            if (Status != null) Status.text = text;
        }

        #endregion Status

        #region Session

        /// <summary>
        /// Sets whether or not the current user will be joining or hosting a session.
        /// </summary>
        /// <param name="isHosting">Whether or not the current user will be hosting the session.</param>
        public void SetHosting(bool isHosting)
        {
            IsHosting = isHosting;
        }
        
        /// <summary>
        /// Queries the Facilitator for the latest list of peers and displays them.
        /// </summary>
        public void LaunchSession()
        {
            // TODO Set status text

            // Show loading
            if (ContentContainer != null) ContentContainer.SetActive(false);
            if (Spinner != null) Spinner.gameObject.SetActive(true);
            if (Progress != null) Progress.gameObject.SetActive(false);

            // Hide Keyboard
            SessionsKeyboard.Hide();

            var text = SessionName.text;

            // Validate
            if (string.IsNullOrEmpty(text))
            {
                SessionsNotifications.GlobalNotify("Session name cannot be blank", "Error");
                SetStatus("Name cannot be blank");
                if (ContentContainer != null) ContentContainer.SetActive(true);
                if (Spinner != null) Spinner.gameObject.SetActive(false);
                return;
            }

            // If there is no current scene, load one first
            var sceneManager = SessionsSceneManager.Current;
            FinishLaunchSession();
        }

        // Finishes launching the session after the scene has loaded
        private void FinishLaunchSession()
        {
            // If we are not currently registered with the Facilitator, begin a connection attempt
            if (!SessionsNetworking.IsRegistered)
            {
                SetStatus("Connecting to Session Facilitator Service...");
                BeginConnect();
            }
            // Now that the client is connected and registered, ask for a list of peers
            else
            {
                FinalizeLaunchSession();
            }
        }

        // Finishes launching a session
        private void FinalizeLaunchSession()
        {
            var text = SessionName.text;

            // Validate
            if (string.IsNullOrEmpty(text))
            {
                SessionsNotifications.GlobalNotify("Session name cannot be blank", "Error");
                SetStatus("Name cannot be blank");
                if (ContentContainer != null) ContentContainer.SetActive(true);
                if (Spinner != null) Spinner.gameObject.SetActive(false);
                return;
            }

            // Save this session in player preferences
            PlayerPrefs.SetString("SessionsLastSessionName", text);

            // If acting as host, host a new session
            if (IsHosting)
            {
                // Get the current scene's info
                SessionsSceneInfo sceneInfo = null;
                if (SessionsSceneManager.Current != null) sceneInfo = SessionsSceneManager.Current.CurrentSceneInfo;

                // Send the session host request to the Facilitator
                if (sceneInfo != null && sceneInfo.Url != "app://SessionsLobby")
                {
                    CyLog.LogInfoFormat("Hosting session: {0} -> {1}", text, sceneInfo.name);
                    SessionsNetworking.HostNewSession(text, sceneInfo.Url, sceneInfo.ImageUrl, sceneInfo.Info);// TODO Add max agents?

                    // Start loading the associated scene
                    if (string.IsNullOrEmpty(sceneInfo.Url) || !Uri.IsWellFormedUriString(sceneInfo.Url, UriKind.RelativeOrAbsolute))
                    {
                        CyLog.LogErrorFormat("Scene URL is missing or incorrectly formatted: {0}", sceneInfo.Name);
                    }
                    else
                    {
                        // Load scene
                        if (SessionsSceneManager.Current != null) SessionsSceneManager.Current.LoadScene(sceneInfo.Url);
                    }
                }
                else
                {
                    SessionsNetworking.HostNewSession(text, "app://SessionsLobby");
                }
            }
            // Otherwise just join an existing session
            else
            {
                // Send the join request to the Facilitator
                SessionsNetworking.JoinSession(text);
            }
        }

        // Fades out the spinner image and then deactivates it.
        private IEnumerator DoFadeOutSpinner()
        {
            if (Spinner == null) yield break;

            float timer = 0;
            float duration = 0.5f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                var c = Spinner.color;
                c.a = Mathf.Lerp(1, 0, timer / duration);
                Spinner.color = c;
                yield return 0;
            }

            Spinner.gameObject.SetActive(false);
            var cf = Spinner.color;
            cf.a = 1;
            Spinner.color = cf; // restore alpha after invisible
        }

        #endregion Session

        #region Connect

        /// <summary>
        /// Attempts to connect to and register with the Facilitator service.
        /// </summary>
        public void BeginConnect()
        {
            if (SessionsNetworking == null) return;
            SessionsNetworking.ConnectToFacilitator();
        }

        /// <summary>
        /// Ends/finalizes the connection to the Facilitator.
        /// </summary>
        public void AfterConnect()
        {
            if (SessionsNetworking == null) return;

            // Otherwise if the session is valid, launch it
            FinalizeLaunchSession();
        }

        #endregion Connect

        #region Event Handlers

        /// <summary>
        /// Handles the loading of a scene.
        /// </summary>
        /// <param name="name">The name of the loaded scene.</param>
        public void HandleSceneLoaded(string name)
        {
            FinishLaunchSession();
        }

        /// <summary>
        /// Handles the progress of a newly loading scene.
        /// </summary>
        /// <param name="progress">The scene's load progress.</param>
        public void HandleSceneLoadProgress(float progress)
        {
            if (Progress != null) Progress.value = progress;
        }

        /// <summary>
        /// Handles the joining of a session.
        /// </summary>
        /// <param name="name">The name of the session that was joined.</param>
        /// <param name="info">Additional information about the session, if any.</param>
        public void HandleSessionJoined(string name, string info)
        {
            if (JoinButton != null) JoinButton.gameObject.SetActive(false);
            if (LeaveButton != null) LeaveButton.gameObject.SetActive(true);
            SessionsNotifications.GlobalNotify("Now joined: " + name, "Agent Connected");
            HideMenu();
            SessionsKeyboard.Bind(null);
            SessionsKeyboard.Hide();
        }

        /// <summary>
        /// Handles the errors when joining a session.
        /// </summary>
        /// <param name="name">The name of the session that was joined.</param>
        /// <param name="info">Additional information about the error, if any.</param>
        public void HandleSessionJoinedError(string name, string error)
        {
            if (JoinButton != null) JoinButton.gameObject.SetActive(true);
            if (LeaveButton != null) LeaveButton.gameObject.SetActive(false);
            ShowMenu();
            SetStatus(error);
            SessionsNotifications.GlobalNotify(error, "Error");
            if (ContentContainer != null) ContentContainer.SetActive(true);
            if (Spinner != null) Spinner.gameObject.SetActive(false);
            if (Progress != null) Progress.gameObject.SetActive(false);
            SessionsKeyboard.Show();
        }

        /// <summary>
        /// Handles the hosting of a session.
        /// </summary>
        /// <param name="name">The name of the session that was joined.</param>
        /// <param name="info">Additional information about the session, if any.</param>
        public void HandleSessionHosted(string name, string info)
        {
            if (JoinButton != null) JoinButton.gameObject.SetActive(false);
            if (LeaveButton != null) LeaveButton.gameObject.SetActive(true);
            SessionsNotifications.GlobalNotify("Now hosting: " + name, "Host Connected");
            HideMenu();
            SessionsKeyboard.Bind(null);
            SessionsKeyboard.Hide();
        }

        /// <summary>
        /// Handles the errors when hosting a session.
        /// </summary>
        /// <param name="name">The name of the session that was joined.</param>
        /// <param name="info">Additional information about the error, if any.</param>
        public void HandleSessionHostingError(string name, string error)
        {
            if (JoinButton != null) JoinButton.gameObject.SetActive(true);
            if (LeaveButton != null) LeaveButton.gameObject.SetActive(false);
            ShowMenu();
            SetStatus(error);
            SessionsNotifications.GlobalNotify(error, "Error");
            if (ContentContainer != null) ContentContainer.SetActive(true);
            if (Spinner != null) Spinner.gameObject.SetActive(false);
            if (Progress != null) Progress.gameObject.SetActive(false);
            SessionsKeyboard.Show();
        }

        /// <summary>
        /// Handles the ending of the current session.
        /// </summary>
        /// <param name="name">The name of the session that ended.</param>
        /// <param name="info">Information about the session ending.</param>
        public void HandleSessionEnded(string name, string info)
        {
            if (JoinButton != null) JoinButton.gameObject.SetActive(true);
            if (LeaveButton != null) LeaveButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// Handles agent connections.
        /// </summary>
        /// <param name="agent">The agent that connected.</param>
        public void HandleAgentConnect(SessionAgent agent)
        {
            // Hide the menu, we're now connected in a session
            HideMenu();
        }

        /// <summary>
        /// Handles agent disconnection.
        /// </summary>
        public void HandleAgentDisconnect(SessionAgent agent)
        {
            if (SessionsNetworking.IsHost) return; // if we're hosting we don't care about disconnects...

            // Relist peers and change status
            ShowMenu();

            if (ContentContainer != null) ContentContainer.SetActive(true);
            if (Spinner != null) Spinner.gameObject.SetActive(false);
            if (Progress != null) Progress.gameObject.SetActive(false);

            if (agent != null)
            {
                SetStatus("Connection with " + agent.Name + " failed");
            }
            else
            {
                SetStatus("Connection failed");
            }
        }

        // Handles connection attempts where the agent has unregistered
        public void HandleAgentNotFound(Guid agentId, string agentName)
        {
            if (ContentContainer != null) ContentContainer.SetActive(true);
            if (Spinner != null) Spinner.gameObject.SetActive(false);
            if (Progress != null) Progress.gameObject.SetActive(false);

            if (!string.IsNullOrEmpty(agentName))
            {
                SetStatus("Connection with " + agentName + " failed");
            }
            else
            {
                SetStatus("Connection failed");
            }
        }

        // Handles connection drops with the Facilitator
        public void HandleFacilitatorDisconnect()
        {
            SetStatus("Error connecting to Facilitator");
            ShowMenu(true);
        }

        #endregion Event Handlers

        #endregion Methods
    }
}
