using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Binds the local agent network entities to a VR rig.
    /// </summary>
    public class SessionsAgentBindToVR : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The VR screen fade reference to use.
        /// </summary>
        [Header("VR Screen Fade")]
        [Tooltip("The VR screen fade reference to use.")]
        public OVRScreenFade ScreenFade;

        /// <summary>
        /// The voice chat instance to use.
        /// </summary>
        [Header("Non-VR Camera")]
        [Tooltip("The non-VR camera instance to use.")]
        public GameObject NonVrCamera;

        /// <summary>
        /// The parent game object for the sessions non-VR camera prefab.
        /// </summary>
        [Tooltip("The parent game object for the sessions non-VR camera prefab.")]
        public GameObject AttachPointNonVrCamera;

        /// <summary>
        /// The HMD instance to use.
        /// </summary>
        [Header("HMD")]
        [Tooltip("The HMD instance to use.")]
        public GameObject HMD;

        /// <summary>
        /// The parent game object for the HMD prefab.
        /// </summary>
        [Tooltip("The parent game object for the HMD prefab.")]
        public GameObject AttachPointHMD;

        /// <summary>
        /// The agent instance to use.
        /// </summary>
        [Header("Agent (body)")]
        [Tooltip("The agent instance to use.")]
        public GameObject Agent;

        /// <summary>
        /// The parent game object for the agent prefab.
        /// </summary>
        [Tooltip("The parent game object for the agent prefab.")]
        public GameObject AttachPointAgent;

        /// <summary>
        /// The left controller instance to use.
        /// </summary>
        [Header("Left Controller")]
        [Tooltip("The left controller instance to use.")]
        public GameObject LeftController;

        /// <summary>
        /// The parent game object for the left controller prefab.
        /// </summary>
        [Tooltip("The parent game object for the left controller prefab.")]
        public GameObject AttachPointLeftController;

        /// <summary>
        /// The right controller instance to use.
        /// </summary>
        [Header("Right Controller")]
        [Tooltip("The right controller instance to use.")]
        public GameObject RightController;

        /// <summary>
        /// The parent game object for the right controller prefab.
        /// </summary>
        [Tooltip("The parent game object for the right controller prefab.")]
        public GameObject AttachPointRightController;

        /// <summary>
        /// The audio source instance to use.
        /// </summary>
        [Header("Audio Source")]
        [Tooltip("The audio source instance to use.")]
        public GameObject AudioSource;

        /// <summary>
        /// The parent game object for the sessions audio source prefab.
        /// </summary>
        [Tooltip("The parent game object for the audio source prefab.")]
        public GameObject AttachPointAudioSource;

        /// <summary>
        /// The voice chat instance to use.
        /// </summary>
        [Header("Voice Chat")]
        [Tooltip("The voice chat instance to use.")]
        public GameObject VoiceChat;

        /// <summary>
        /// The parent game object for the sessions voice chat prefab.
        /// </summary>
        [Tooltip("The parent game object for the sessions voice chat prefab.")]
        public GameObject AttachPointVoiceChat;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            // Screen Fade
            if (!SessionsUdpNetworking.IsXR && ScreenFade != null) ScreenFade.enabled = false;
        }

        private void Start()
        {
            // Non-VR Camera
            if (NonVrCamera != null && AttachPointNonVrCamera != null)
            {
                NonVrCamera.transform.SetParent(AttachPointNonVrCamera.transform);
                //NonVrCamera.transform.localPosition = Vector3.zero;
            }

            // HMD
            if (HMD != null && AttachPointHMD != null)
            {
                HMD.transform.SetParent(AttachPointHMD.transform);
                HMD.transform.localPosition = Vector3.zero;
            }

            // Agent/Body
            if (Agent != null && AttachPointAgent != null)
            {
                Agent.transform.SetParent(AttachPointAgent.transform);
                //Agent.transform.localPosition = Vector3.zero;
            }

            // Left Controller
            if (LeftController != null && AttachPointLeftController != null)
            {
                LeftController.transform.SetParent(AttachPointLeftController.transform);
                LeftController.transform.localPosition = Vector3.zero;

                // Check for Oculus tracked remote
                var remote = AttachPointLeftController.GetComponent<OVRTrackedRemote>();

                if (remote != null)
                {
                    // Hide current remote model
                    if (remote.m_modelOculusGoController != null) remote.m_modelOculusGoController.SetActive(false);

                    // Replace with sessions prefab
                    remote.m_modelOculusGoController = LeftController;
                }
            }

            // Right Controller
            if (RightController != null && AttachPointRightController != null)
            {
                RightController.transform.SetParent(AttachPointRightController.transform);
                RightController.transform.localPosition = Vector3.zero;

                // Check for Oculus tracked remote
                var remote = AttachPointRightController.GetComponent<OVRTrackedRemote>();

                if (remote != null)
                {
                    // Hide current remote model
                    if (remote.m_modelOculusGoController != null) remote.m_modelOculusGoController.SetActive(false);

                    // Replace with sessions prefab
                    remote.m_modelOculusGoController = RightController;
                }
            }

            // Audio Source
            if (AudioSource != null && AttachPointAudioSource != null)
            {
                AudioSource.transform.SetParent(AttachPointAudioSource.transform);
                AudioSource.transform.localPosition = Vector3.zero;
            }

            // Voice Chat
            if (VoiceChat != null && AttachPointVoiceChat != null)
            {
                VoiceChat.transform.SetParent(AttachPointVoiceChat.transform);
                VoiceChat.transform.localPosition = Vector3.zero;
            }
        }

        #endregion Init

        #region Update

        #endregion Update

        #endregion Methods
    }
}
