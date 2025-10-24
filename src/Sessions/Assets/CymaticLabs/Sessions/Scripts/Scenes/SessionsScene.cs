using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CymaticLabs.Logging;
using CymaticLabs.Sessions.Core;
using CymaticLabs.Protocols.Osc.Unity3d;
using CymaticLabs.xAPI.Unity3d;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Captures all of the elements of a sessions scene.
    /// </summary>
    public class SessionsScene : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The optional skybox material to use for the scene.
        /// </summary>
        [Header("Appearance")]
        [Tooltip("The optional skybox material to use for the scene.")]
        public Material SkyboxMaterial;

        /// <summary>
        /// The camera's background color.
        /// </summary>
        [Tooltip("The camera's background color.")]
        public Color BackgroundColor = Color.black;

        /// <summary>
        /// The ambient color to use for the scene.
        /// </summary>
        [Tooltip("The ambient color to use for the scene.")]
        public Color AmbientColor = Color.black;

        /// <summary>
        /// Whether or not to apply the current scene on start.
        /// </summary>
        [Header("Behavior")]
        [Tooltip("Whether or not to apply the current scene on start.")]
        public bool ApplyOnStart = true;

        /// <summary>
        /// Whether or not the scene should force its loading if a current scene is already present.
        /// </summary>
        [Tooltip("Whether or not the scene should force its loading if a current scene is already present.")]
        public bool Force = true;

        /// <summary>
        /// Whether or not the scene sets itself as the current scene automatically after applying itself.
        /// </summary>
        [Tooltip("Whether or not the scene sets itself as the current scene automatically after applying itself.")]
        public bool SetCurrent = true;

        /// <summary>
        /// A list of music clips to use with the scene.
        /// </summary>
        [Header("Audio")]
        [Tooltip("A list of music clips to use with the scene.")]
        public NamedAudioClip[] MusicClips;

        #endregion Inspector

        #region Fields

        /// <summary>
        /// The selected Scenes configuration.
        /// </summary>
        [HideInInspector]
        public TextAsset ScenesConfiguration;

        /// <summary>
        /// The selected OSC configuration.
        /// </summary>
        [HideInInspector]
        public TextAsset OscConfiguration;

        /// <summary>
        /// The selected routing configuration.
        /// </summary>
        [HideInInspector]
        public SessionsRoutingConfiguration RoutingConfiguration;

        /// <summary>
        /// The total list of routing configurations.
        /// </summary>
        [HideInInspector]
        public List<SessionsRoutingConfiguration> AllRoutingConfigurations;

        /// <summary>
        /// The selected entities configuration.
        /// </summary>
        [HideInInspector]
        public SessionsEntitiesConfiguration EntitiesConfiguration;

        /// <summary>
        /// The total list of entities configurations.
        /// </summary>
        [HideInInspector]
        public List<SessionsEntitiesConfiguration> AllEntitiesConfigurations;

        /// <summary>
        /// The selected xAPI configuration. 
        /// </summary>
        [HideInInspector]
        public XapiConfiguration XapiConfiguration;

        /// <summary>
        /// The total list of xAPI configurations
        /// </summary>
        [HideInInspector]
        public List<XapiConfiguration> AllXapiConfigurations;

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        private void Start()
        {
            if (!ApplyOnStart) return;
            ApplyScene();
        }

        #endregion Init

        #region Scenes

        /// <summary>
        /// Applies the current entities configuration to the scene.
        /// </summary>
        public void ApplyScenesConfiguration()
        {
            var sceneManager = FindObjectOfType<SessionsSceneManager>();
            if (ScenesConfiguration == null || sceneManager == null) return;
            sceneManager.Configuration = ScenesConfiguration;
            sceneManager.LoadConfiguration();
        }

        #endregion Scenes

        #region OSC

        /// <summary>
        /// Applies the current OSC configuration to the scene.
        /// </summary>
        public void ApplyOscConfiguration()
        {
            var networking = FindObjectOfType<SessionsUdpNetworking>();
            var oscController = networking != null ? networking.OscController : null;
            if (OscConfiguration == null || oscController == null) return;
            oscController.Configuration = OscConfiguration;
            oscController.LoadConfiguration();
        }

        #endregion OSC

        #region Routing

        /// <summary>
        /// Applies the current routing configuration to the scene.
        /// </summary>
        public void ApplyRoutingConfiguration()
        {
            var routing = FindObjectOfType<SessionsRouting>();
            if (RoutingConfiguration == null || routing == null) return;
            routing.AllConfigurations = AllRoutingConfigurations;
            routing.Configuration = RoutingConfiguration;
            routing.LoadConfiguration();
        }

        #endregion Routing

        #region Entities

        /// <summary>
        /// Applies the current entities configuration to the scene.
        /// </summary>
        public void ApplyEntitiesConfiguration()
        {
            var entitiesManager = FindObjectOfType<SessionsNetworkEntityManager>();
            if (EntitiesConfiguration == null || entitiesManager == null) return;
            entitiesManager.AllConfigurations = AllEntitiesConfigurations;
            entitiesManager.Configuration = EntitiesConfiguration;
            entitiesManager.LoadConfiguration();
        }

        #endregion Entities

        #region xAPI

        /// <summary>
        /// Applies the current xAPI configuration to the scene.
        /// </summary>
        public void ApplyXapiConfiguration()
        {
            var xapiManager = FindObjectOfType<XapiManager>();
            if (XapiConfiguration == null || xapiManager == null) return;
            xapiManager.AllConfigurations = AllXapiConfigurations;
            xapiManager.Configuration = XapiConfiguration;
            xapiManager.LoadConfiguration();
        }

        #endregion xAPI

        #region Utility

        /// <summary>
        /// Applies the entire scene.
        /// </summary>
        /// <param name="setCurrent">Whether or not to update the <see cref="SessionsSceneManager">session scene manager's</see> current scene reference.</param>
        public void ApplyScene()
        {
            var sceneManager = FindObjectOfType<SessionsSceneManager>();

            if (sceneManager != null)
            {
                if (!Force && sceneManager.CurrentScene != null) return; // scene already applied
                if (SetCurrent) sceneManager.CurrentScene = this;
            }

            // Apply configurations
            ApplyOscConfiguration();
            ApplyRoutingConfiguration();
            ApplyEntitiesConfiguration();

            // Apply music
            if (SessionsSound.Current != null && MusicClips != null && MusicClips.Length > 0)
            {
                SessionsSound.Current.MusicClips = MusicClips;

                for (var i = 0; i < MusicClips.Length; i++)
                {
                    var item = MusicClips[i];

                    // TODO Remove (auto assign and play music tracks)
                    var audioSources = SessionsSound.Current.MusicAudioSources;

                    if (audioSources != null && i < audioSources.Length)
                    {
                        audioSources[i].clip = item.Clip;
                        audioSources[i].volume = 0;
                        audioSources[i].Play();
                        if (item.Clip != null) StartCoroutine(DoFadeInAudio(audioSources[i], item.Volume, 2));
                    }
                }
            }

            if (Camera.current != null)
            {
                // Apply background color to camera
                Camera.current.backgroundColor = BackgroundColor;

                // Switch camera clear flags to skybox if one was provided
                if (SkyboxMaterial != null)
                {
                    Camera.current.clearFlags = CameraClearFlags.Skybox;
                }
                // Otherwise revert to color
                else if (SkyboxMaterial == null)
                {
                    Camera.current.clearFlags = CameraClearFlags.Color;
                }
            }

            // Apply skybox/lack of skybox
            RenderSettings.skybox = SkyboxMaterial;

            // Apply ambient color
            RenderSettings.ambientSkyColor = AmbientColor;
        }

        // Fades in audio on an audio source
        private IEnumerator DoFadeInAudio(AudioSource audio, float volume, float duration)
        {
            float timer = 0;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                audio.volume = Mathf.SmoothStep(0, volume, timer / duration);
                yield return 0;
            }

            audio.volume = volume;
        }

        #endregion Utility

        #endregion Methods
    }
}
