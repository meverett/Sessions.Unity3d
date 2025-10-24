using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Logic for the scenes menu.
    /// </summary>
    public class SessionsSceneMenu : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The game object acting as the  menu container.
        /// </summary>
        [Tooltip("The game object acting as the menu container.")]
        public GameObject MenuContainer;

        /// <summary>
        /// The name text component.
        /// </summary>
        [Tooltip("The name text component.")]
        public Text Name;

        /// <summary>
        /// The info text component.
        /// </summary>
        [Tooltip("The info text component.")]
        public Text Info;

        /// <summary>
        /// The image component.
        /// </summary>
        [Tooltip("The image component.")]
        public RawImage Image;

        /// <summary>
        /// The 'previous' scene button.
        /// </summary>
        [Tooltip("The 'previous' scene button.")]
        public Button PreviousButton;

        /// <summary>
        /// The 'next' scene button.
        /// </summary>
        [Tooltip("The 'next' scene button.")]
        public Button NextButton;

        /// <summary>
        /// The texture to use to indicate a loading scene image.
        /// </summary>
        [Tooltip("The texture to use to indicate a loading scene image.")]
        public Texture2D LoadingSceneImage;

        /// <summary>
        /// The texture to use for the default scene image.
        /// </summary>
        [Tooltip("The texture to use for the default scene image.")]
        public Texture2D DefaultSceneImage;

        #endregion Inspector

        #region Fields

        // The scene index
        private int sceneIndex = 0;

        // Whether or not the scene image is currently loading
        private bool isLoadingImage = false;

        // Cached scene images by URL
        private Dictionary<string, Texture2D> imagesByUrl;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static SessionsSceneMenu Current { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            Current = this;
            imagesByUrl = new Dictionary<string, Texture2D>();
        }

        private void Start()
        {
            // Register with main menu events
            //if (SessionsMainMenu.Current != null) SessionsMainMenu.Current.OnHideAllMenus.AddListener(() => { HideMenu(); });
            //HideMenu();

            // Add a listener for when the scenes configuration is loaded
            if (SessionsSceneManager.Current != null) SessionsSceneManager.Current.OnConfigurationLoaded.AddListener(() =>
            {
                // Show the menu/bind to current data
                ShowMenu();
            });
        }

        #endregion Init

        #region Operation

        /// <summary>
        /// Shows the debug menu.
        /// </summary>
        public void ShowMenu()
        {
            if (MenuContainer == null) return;
            MenuContainer.SetActive(true);
            BindToCurrentScene();
            UpdateBrowseState();
        }

        /// <summary>
        /// Hides the debug menu.
        /// </summary>
        public void HideMenu()
        {
            if (MenuContainer == null) return;
            MenuContainer.SetActive(false);
        }

        #endregion Operation

        #region Update

        #endregion Update

        #region Scenes

        /// <summary>
        /// Gets the information for the current scene.
        /// </summary>
        /// <returns>The current scene's information.</returns>
        public SessionsSceneInfo CurrentSceneInfo()
        {
            if (SessionsSceneManager.Current == null || 
                SessionsSceneManager.Current.ScenesConfiguration == null || 
                SessionsSceneManager.Current.ScenesConfiguration.Scenes == null)
                return null;

            var scenes = SessionsSceneManager.Current.ScenesConfiguration.Scenes;
            if (scenes.Length == 0) return null;
            if (sceneIndex >= scenes.Length) sceneIndex = 0;
            return scenes[sceneIndex];
        }

        /// <summary>
        /// Binds the menu to the current scene's info.
        /// </summary>
        public void BindToCurrentScene()
        {
            var sceneInfo = CurrentSceneInfo();

            // Bind to data
            if (Name != null) Name.text = sceneInfo.Name;
            if (Info != null) Info.text = sceneInfo.Info;

            // Load and bind image if present...
            if (Image != null)
            {
                // If the scene provides a custom image, use it
                if (sceneInfo.Image != null)
                {
                    Image.texture = sceneInfo.Image;
                }
                // Otherwise if this is a valid image URL, load it
                else if (!string.IsNullOrEmpty(sceneInfo.ImageUrl) && System.Uri.IsWellFormedUriString(sceneInfo.ImageUrl, System.UriKind.RelativeOrAbsolute))
                {
                    // If this image is cached, just use it immediately
                    if (imagesByUrl.ContainsKey(sceneInfo.ImageUrl))
                    {
                        Image.texture = imagesByUrl[sceneInfo.Url];
                        Image.color = new Color(1, 1, 1, 1);
                        Image.uvRect = new Rect(0, 0, 1, 1);
                    }
                    else
                    {
                        // Apply the loading sprite effect
                        StartCoroutine(DoShowImageLoading());

                        // Load the texture first
                        SessionsLoadedImages.LoadImage(sceneInfo.ImageUrl, (texture) =>
                        {
                            imagesByUrl.Add(sceneInfo.ImageUrl, texture); // cache
                            isLoadingImage = false;
                            Image.texture = texture;
                            Image.color = new Color(1, 1, 1, 1);
                            Image.uvRect = new Rect(0, 0, 1, 1);
                        });
                    }
                    
                }
                // Otherwise fallback to default
                else
                {
                    Image.texture = DefaultSceneImage;
                    Image.color = new Color(1, 1, 1, 1);
                    Image.uvRect = new Rect(0, 0, 1, 1);
                }
            }
        }

        // Displays the scene image "loading" animation
        IEnumerator DoShowImageLoading()
        {
            if (Image == null || isLoadingImage) yield break;
            isLoadingImage = true;
            Image.texture = LoadingSceneImage;
            Image.color = new Color(1, 1, 1, 0.25f);
            Image.uvRect = new Rect(0, 0, 0.5f, 0.5f);

            while (Image != null && isLoadingImage)
            {
                var uvs = Image.uvRect;
                var x = uvs.x + Time.deltaTime * 0.5f;
                if (x > 0.5f) x = 0.5f - x;
                Image.uvRect = new Rect(x, uvs.y, uvs.width, uvs.height);
                yield return 0;
            }
        }

        /// <summary>
        /// Selects and displays the previous scene's information if available.
        /// </summary>
        public void PreviousScene()
        {
            if (sceneIndex == 0) return;
            sceneIndex--;
            BindToCurrentScene();
            UpdateBrowseState();
            if (SessionsSceneManager.Current != null) SessionsSceneManager.Current.SetCurrentScene(sceneIndex);
        }

        /// <summary>
        /// Selects and displays the next scene's information if available.
        /// </summary>
        public void NextScene()
        {
            if (SessionsSceneManager.Current == null) return;
            var config = SessionsSceneManager.Current.ScenesConfiguration;
            if (config == null || config.Scenes == null) return;
            if (sceneIndex >= config.Scenes.Length - 1) return;
            sceneIndex++;
            BindToCurrentScene();
            UpdateBrowseState();
            if (SessionsSceneManager.Current != null) SessionsSceneManager.Current.SetCurrentScene(sceneIndex);
        }

        // Updates the UI state of the previous/next scene browse buttons
        private void UpdateBrowseState()
        {
            if (SessionsSceneManager.Current == null) return;
            var config = SessionsSceneManager.Current.ScenesConfiguration;
            if (config == null || config.Scenes == null) return;

            if (PreviousButton != null)
            {
                // At the beginning of the list so disable
                if (sceneIndex == 0) PreviousButton.interactable = false;
                else PreviousButton.interactable = true;
            }

            if (NextButton != null)
            {
                // At the beginning of the list so disable
                if (sceneIndex >= config.Scenes.Length - 1) NextButton.interactable = false;
                else NextButton.interactable = true;
            }
        }

        /// <summary>
        /// Confirms whether or not to host a new session with the current scene.
        /// </summary>
        public void ConfirmHostSession()
        {
            var sceneInfo = CurrentSceneInfo();
            if (sceneInfo == null) return;

            if (SessionsCustomConfirmMenu.Current != null)
            {
                var confirm = SessionsCustomConfirmMenu.Current;

                confirm.ShowMenu("Host Session:\n" + sceneInfo.Name + "?", () =>
                {
                    HostSession();
                });
            }
        }

        /// <summary>
        /// Starts the process of hosting a new session using the current scene.
        /// </summary>
        public void HostSession()
        {
            if (SessionsUdpNetworking.Current != null)
            {
                var networking = SessionsUdpNetworking.Current;

                // Only host if not currently in a session
                if (networking.IsInSession) return;
            }

            // Mark current user as hosting
            PlayerPrefs.SetInt("SessionsIsHosting", 1);

            // Mark the current scene URL
            var sceneInfo = CurrentSceneInfo();
            PlayerPrefs.SetString("SessionsCurrentUrl", sceneInfo.Url);

            // If this is the lobby scene...
            if (SceneManager.GetActiveScene().name == "SessionsLobby" && sceneInfo.Url == "app://SessionsLobby")
            {
                if (SessionsFacilitatorMenu.Current != null)
                {
                    SessionsFacilitatorMenu.Current.SetHosting(true);
                    SessionsFacilitatorMenu.Current.ShowMenu(true);
                }
            }
            else
            {
                // Load the scene
                if (SessionsSceneManager.Current != null)
                    SessionsSceneManager.Current.LoadScene(sceneInfo.Url);
            }
        }

        /// <summary>
        /// Confirms whether or not to join an existing session with the current scene.
        /// </summary>
        public void ConfirmJoinSession()
        {
            var sceneInfo = CurrentSceneInfo();
            if (sceneInfo == null) return;

            if (SessionsCustomConfirmMenu.Current != null)
            {
                var confirm = SessionsCustomConfirmMenu.Current;

                confirm.ShowMenu("Join Session:\n" + sceneInfo.Name + "?", () =>
                {
                    JoinSession();
                });
            }
        }

        /// <summary>
        /// Starts the process of joining an existing session using the current scene.
        /// </summary>
        public void JoinSession()
        {
            if (SessionsUdpNetworking.Current != null)
            {
                var networking = SessionsUdpNetworking.Current;

                // Only host if not currently in a session
                if (networking.IsInSession) return;
            }

            // Mark current user as joining
            PlayerPrefs.SetInt("SessionsIsHosting", 0);

            // Mark the current scene URL
            var sceneInfo = CurrentSceneInfo();
            PlayerPrefs.SetString("SessionsCurrentUrl", sceneInfo.Url);

            // If this is the lobby scene...
            if (SceneManager.GetActiveScene().name == "SessionsLobby" && sceneInfo.Url == "app://SessionsLobby")
            {
                if (SessionsFacilitatorMenu.Current != null)
                {
                    SessionsFacilitatorMenu.Current.SetHosting(false);
                    SessionsFacilitatorMenu.Current.ShowMenu(true);
                }
            }
            else
            {
                // Load the scene
                if (SessionsSceneManager.Current != null)
                    SessionsSceneManager.Current.LoadScene(sceneInfo.Url);
            }
        }
            
        #endregion Scenes

        #endregion Methods
    }
}