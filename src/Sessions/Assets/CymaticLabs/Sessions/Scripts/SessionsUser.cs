using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using CymaticLabs.Logging;
using CymaticLabs.Sessions.Core;
using CymaticLabs.Language.Unity3d;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Represents the current user agent for the session.
    /// </summary>
    public class SessionsUser : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// Whether or not to enable 3D spatial audio with voice.
        /// </summary>
        [Header("Voice")]
        [Tooltip("Whether or not to enable 3D spatial audio with voice.")]
        public bool SpatialVoice = true;

        /// <summary>
        /// The user's configured voice mode.
        /// </summary>
        [Tooltip("The user's configured voice mode.")]
        public VoiceChatModes VoiceMode = VoiceChatModes.PushToTalk;

        /// <summary>
        /// The input axis name for the trigger for push to talk.
        /// </summary>
        [Tooltip("The input axis name for the trigger for push to talk.")]
        public string PushToTalkTrigger = "Voice";

        /// <summary>
        /// A list of one or more push to talk buttons.
        /// </summary>
        [Tooltip("A list of one or more push to talk buttons.")]
        public OVRInput.Button[] PushToTalkButtonsVR;

        /// <summary>
        /// The reference to the positional voice component used by the user.
        /// </summary>
        [Tooltip("The reference to the positional voice component used by the user.")]
        public SessionsPositionalVoice PositionalVoice;

        /// <summary>
        /// The player controller script that is currently being used to control player movement.
        /// </summary>
        [Tooltip("The player controller script that is currently being used to control player movement.")]
        public OVRPlayerController OVRPlayerController;

        /// <summary>
        /// Occurs when some user input is pressed down.
        /// </summary>
        [Header("Events")]
        public SessionUserInputEvent OnUserInputDown;

        /// <summary>
        /// Occurs when some user input is released up
        /// </summary>
        public SessionUserInputEvent OnUserInputUp;

        /// <summary>
        /// Occurs when some user input is held down.
        /// </summary>
        public SessionUserInputEvent OnUserInput;

        #endregion Inspector

        #region Fields

        // Whether or not user prerferences have been loaded/initialized yet
        private bool arePrefsInitialized = false;

        // The last push-to-talk input value state
        private float lastPushToTalk = 0;

        // Used to time input holding to hide/show the menu
        private float tpadTimer = float.MaxValue;
        private int tpadTotal = 5;
        private int tpadCount = 0;
        private float tpadDuration = 3;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Whether or not the client is currently running in XR mode.
        /// </summary>
        public bool IsXR { get; private set; }

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static SessionsUser Current { get; private set; }

        /// <summary>
        /// The language preference of the current user: en-US, etc.
        /// </summary>
        public string Language { get; private set; }

        /// <summary>
        /// The voice type to use when auto-translated voice chat is enabled.
        /// </summary>
        public TranslationVoices TranslationVoice { get; private set; }

        /// <summary>
        /// Whether or not push-to-talk is currently on/open.
        /// </summary>
        public bool IsPushToTalkOpen = false;// { get; private set; }

        /// <summary>
        /// Gets whether or not the primary interaction button is currently held down.
        /// </summary>
        public bool PrimaryButton { get; private set; }

        /// <summary>
        /// Gets whether or not the primary interaction button has just been pressed down.
        /// </summary>
        public bool PrimaryButtonDown { get; private set; }

        /// <summary>
        /// Gets whether or not the primary interaction button has just been released.
        /// </summary>
        public bool PrimaryButtonUp { get; private set; }

        /// <summary>
        /// Gets whether or not the secondary interaction button is currently held down.
        /// </summary>
        public bool SecondaryButton { get; private set; }

        /// <summary>
        /// Gets whether or not the secondary interaction button has just been pressed down.
        /// </summary>
        public bool SecondaryButtonDown { get; private set; }

        /// <summary>
        /// Gets whether or not the secondary interaction button has just been released.
        /// </summary>
        public bool SecondaryButtonUp { get; private set; }

        /// <summary>
        /// Gets whether or not the back button is currently held down.
        /// </summary>
        public bool BackButton { get; private set; }

        /// <summary>
        /// Gets whether or not the back button has just been pressed down.
        /// </summary>
        public bool BackButtonDown { get; private set; }

        /// <summary>
        /// Gets whether or not the back button has just been released.
        /// </summary>
        public bool BackButtonUp { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            Current = this;
            IsXR = UnityEngine.XR.XRSettings.enabled;
            LoadPreferences();
        }

        private void Start()
        {
            // Set the current language preference based on platform-detected language
            if (LanguageServices.Current != null)
            {
                var languageInfo = LanguageServices.Current.GetPlatformLanguage();
                if (languageInfo != null) UpdateLanguage(languageInfo.Code, true);
            }

            // Attempt to locate player controller if not assigned
            if (OVRPlayerController == null) OVRPlayerController = FindObjectOfType<OVRPlayerController>();
        }

        #endregion Init

        #region Update

        private void Update()
        {
            #region Check for Open Menus

            // If any menus are open, temporarily disable movement
            if (OVRPlayerController != null)
                OVRPlayerController.EnableLinearMovement = !SessionsMenuBase.IsAnyMenuOpen;

            #endregion Check for Open Menus

            #region Primary & Secondary Buttons

            UpdateButtonInputStates();

            // Perform events
            if (PrimaryButton) OnUserInput.Invoke("Primary", 1);
            if (PrimaryButtonDown) OnUserInputDown.Invoke("Primary", 1);
            if (PrimaryButtonUp) OnUserInputUp.Invoke("Primary", 0);

            if (SecondaryButton) OnUserInput.Invoke("Secondary", 1);
            if (SecondaryButtonDown) OnUserInputDown.Invoke("Secondary", 1);
            if (SecondaryButtonUp) OnUserInputUp.Invoke("Secondary", 0);

            if (BackButton) OnUserInput.Invoke("Back", 1);
            if (BackButtonDown) OnUserInputDown.Invoke("Back", 1);
            if (BackButtonUp) OnUserInputUp.Invoke("Back", 0);

            #endregion Primary & Secondary Buttons

            #region Left & Right Clicks

            // If no menus are open, detech turns and turn the user
            if (IsXR && OVRPlayerController != null && !SessionsMenuBase.IsAnyMenuOpen && OVRInput.GetDown(OVRInput.Button.PrimaryTouchpad))
            {
                var primaryTouchpad = OVRInput.Get(OVRInput.Axis2D.PrimaryTouchpad);

                // Turn left
                if (primaryTouchpad.x < -0.25f)
                {
                    var rot = OVRPlayerController.transform.eulerAngles;
                    rot.y -= OVRPlayerController.RotationRatchet;
                    OVRPlayerController.transform.eulerAngles = rot;
                }
                // Turn right
                else if (primaryTouchpad.x > 0.25f)
                {
                    var rot = OVRPlayerController.transform.eulerAngles;
                    rot.y += OVRPlayerController.RotationRatchet;
                    OVRPlayerController.transform.eulerAngles = rot;
                }
            }

            #endregion Left & Right Clicks

            #region Push-to-Talk

            // Otherwise if this is XR/VR...
            if (IsXR && PushToTalkButtonsVR != null && PushToTalkButtonsVR.Length > 0)
            {
                // See if all of the configured buttons for push-to-talk are down
                var c = 0;
                foreach (var button in PushToTalkButtonsVR) if (OVRInput.Get(button)) c++;
                var value = c == PushToTalkButtonsVR.Length ? 1 : 0;
                if (VoiceMode == VoiceChatModes.PushToTalk || VoiceMode == VoiceChatModes.AutoTranslated) IsPushToTalkOpen = value == 1;

                if (lastPushToTalk != value)
                {
                    lastPushToTalk = value;

                    if (value == 1)
                    {
                        if (VoiceMode == VoiceChatModes.PushToTalkToggle)
                        {
                            // Toggle push-to-talk state
                            IsPushToTalkOpen = !IsPushToTalkOpen;
                        }
                    }

                    if (IsPushToTalkOpen) OnUserInputDown.Invoke("PushToTalk", 1);
                    else OnUserInputUp.Invoke("PushToTalk", 0);
                }
            }
            // If the push-to-talk input axis/trigger is configured, see if it is in use
            else if (!string.IsNullOrEmpty(PushToTalkTrigger) && Input.GetAxis(PushToTalkTrigger) > 0.5f)
            {
                if (lastPushToTalk != 1)
                {
                    lastPushToTalk = 1;

                    if (VoiceMode == VoiceChatModes.PushToTalkToggle)
                    {
                        // Toggle push-to-talk state
                        IsPushToTalkOpen = !IsPushToTalkOpen;

                        if (IsPushToTalkOpen) OnUserInputDown.Invoke("PushToTalk", 1);
                        else OnUserInputUp.Invoke("PushToTalk", 0);
                    }
                    else if (VoiceMode == VoiceChatModes.PushToTalk || VoiceMode == VoiceChatModes.AutoTranslated)
                    {
                        IsPushToTalkOpen = true;
                        OnUserInputDown.Invoke("PushToTalk", 1);
                    }
                }

                // Currently held down
                if (VoiceMode == VoiceChatModes.PushToTalk || VoiceMode == VoiceChatModes.AutoTranslated) OnUserInput.Invoke("PushToTalk", 1);
            }
            else
            {
                if (lastPushToTalk != 0)
                {
                    lastPushToTalk = 0;
                    if (VoiceMode == VoiceChatModes.PushToTalk || VoiceMode == VoiceChatModes.AutoTranslated) IsPushToTalkOpen = false;
                    if (VoiceMode == VoiceChatModes.PushToTalk || VoiceMode == VoiceChatModes.AutoTranslated) OnUserInputUp.Invoke("PushToTalk", 0);
                }
            }

            if (IsPushToTalkOpen) OnUserInput.Invoke("PushToTalk", 1);
            else OnUserInput.Invoke("PushToTalk", 0);

            #endregion Push-to-Talk

            #region Touch Pad Multiclick

            tpadTimer += Time.deltaTime;

        // Toggle the menu open or closed when the the input button is held for a duration
        if ((IsXR && OVRInput.GetDown(OVRInput.Button.PrimaryTouchpad))
            || (!IsXR && Input.GetKeyDown(KeyCode.F1)))
        {
            // If the timer ran out, start again
            if (tpadTimer >= tpadDuration)
            {
                tpadTimer = 0;
                tpadCount = tpadTotal - 1;
            }
            else if (tpadTimer < tpadDuration)
            {
                tpadTimer = 0;
                tpadCount--;

                // Send out touch pad press events
                if (tpadCount == 2)
                {
                    OnUserInputDown.Invoke("TouchPadPress3", 1);
                }
                else if (tpadCount == 1)
                {
                    OnUserInputDown.Invoke("TouchPadPress4", 1);
                }
                if (tpadCount == 0)
                {
                    OnUserInputDown.Invoke("TouchPadPress5", 1);
                    tpadTimer = float.MaxValue;
                }
            }
        }

            #endregion Touch Pad Multiclick
        }

        #endregion Update

        #region Input

        /// <summary>
        /// Updates the primary and secondary button input states.
        /// </summary>
        public void UpdateButtonInputStates()
        {
            if (!IsXR)
            {
                PrimaryButton = Input.GetButton("Primary");
                PrimaryButtonDown = Input.GetButtonDown("Primary");
                PrimaryButtonUp = Input.GetButtonUp("Primary");
                SecondaryButton = Input.GetButton("Secondary");
                SecondaryButtonDown = Input.GetButtonDown("Secondary");
                SecondaryButtonUp = Input.GetButtonUp("Secondary");
                BackButton = Input.GetButton("Back");
                BackButtonDown = Input.GetButtonDown("Back");
                BackButtonUp = Input.GetButtonUp("Back");
            }
            else
            {
                PrimaryButton = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger);
                PrimaryButtonDown = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger);
                PrimaryButtonUp = OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger);
                SecondaryButton = OVRInput.Get(OVRInput.Button.PrimaryTouchpad);
                SecondaryButtonDown = OVRInput.GetDown(OVRInput.Button.PrimaryTouchpad);
                SecondaryButtonUp = OVRInput.GetUp(OVRInput.Button.PrimaryTouchpad);
                BackButton = OVRInput.Get(OVRInput.Button.Back);
                BackButtonDown = OVRInput.GetDown(OVRInput.Button.Back);
                BackButtonUp = OVRInput.GetUp(OVRInput.Button.Back);
            }
        }

        #endregion Input

        #region Preferences

        #region Load

        /// <summary>
        /// Loads user preferences if available.
        /// </summary>
        public void LoadPreferences()
        {
            if (PlayerPrefs.HasKey("SessionsVoiceSpatial")) SpatialVoice = PlayerPrefs.GetInt("SessionsVoiceSpatial") > 0;
            if (PlayerPrefs.HasKey("SessionsVoiceMode")) VoiceMode = (VoiceChatModes)PlayerPrefs.GetInt("SessionsVoiceMode");
            if (PlayerPrefs.HasKey("SessionsTranslationVoice")) TranslationVoice = (TranslationVoices)PlayerPrefs.GetInt("SessionsTranslationVoice");
            UpdateSpatialVoice(SpatialVoice, false, true);
            UpdateVoiceMode(VoiceMode, false, true);
            UpdateTranslationVoice(TranslationVoice, false, true);
            CyLog.LogInfo("User preferences loaded");
            arePrefsInitialized = true;
        }

        #endregion Load

        #region Save

        /// <summary>
        /// Saves the current user preferences.
        /// </summary>
        public void SavePreferences()
        {
            PlayerPrefs.SetInt("SessionsVoiceSpatial", SpatialVoice ? 1 : 0);
            PlayerPrefs.SetInt("SessionsVoiceMode", (int)VoiceMode);
            PlayerPrefs.SetInt("SessionsTranslationVoice", (int)TranslationVoice);
            CyLog.LogInfo("User preferences saved");
        }

        #endregion Save

        #region Spatial/Positional Voice

        /// <summary>
        /// Updates the 3D positional/spatial voice preference.
        /// </summary>
        /// <param name="enabled">Whether or not 3D spatial voice is enabled.</param>
        /// <param name="save">Whether or not to save the new preference.</param>
        /// <param name="force">Whether or not to force the update.</param>
        public void UpdateSpatialVoice(bool enabled, bool save = false, bool force = false)
        {
            if (SpatialVoice == enabled && !force) return;
            SpatialVoice = enabled;
            if (save) SavePreferences();

            // Update the current settings in voice chat
            if (SessionsUdpNetworking.Current == null || SessionsUdpNetworking.Current.VoiceChat == null ||
                !SessionsUdpNetworking.Current.UseVoice || SessionsUdpNetworking.Current.VoiceChat.BroadcastTrigger == null) return;

            SessionsUdpNetworking.Current.VoiceChat.BroadcastTrigger.BroadcastPosition = enabled;
        }

        #endregion Spatial/Positional Voice

        #region Voice Chat Mode

        /// <summary>
        /// Updates the voice chat mode preference.
        /// </summary>
        /// <param name="mode">The voice chat mode to use..</param>
        /// <param name="save">Whether or not to save the new preference.</param>
        /// <param name="force">Whether or not to force the update.</param>
        public void UpdateVoiceMode(VoiceChatModes mode, bool save = false, bool force = false)
        {
            if (VoiceMode == mode && !force) return;

            // Get the current voice mode
            var lastMode = VoiceMode;

            VoiceMode = mode;
            if (save) SavePreferences();

            // Update the current settings in voice chat
            var networking = SessionsUdpNetworking.Current;

            if (networking == null || networking.VoiceChat == null || !networking.UseVoice || networking.VoiceChat.BroadcastTrigger == null)
                return;

            var trigger = SessionsUdpNetworking.Current.VoiceChat.BroadcastTrigger;

            switch (mode)
            {
                case VoiceChatModes.Off:
                case VoiceChatModes.AutoTranslated:
                    trigger.Mode = Dissonance.CommActivationMode.None;
                    break;

                case VoiceChatModes.PushToTalk:
                case VoiceChatModes.PushToTalkToggle:
                    trigger.Mode = Dissonance.CommActivationMode.PushToTalk;
                    break;

                case VoiceChatModes.VoiceActivated:
                    trigger.Mode = Dissonance.CommActivationMode.VoiceActivation;
                    break;
            }

            // If switching to auto-translate, disable VOIP (after initial preferences have been loaded)
            if (arePrefsInitialized)
            {
                if (mode == VoiceChatModes.AutoTranslated)
                {
                    networking.VoiceChat.SetMicrophoneCapture(false, true);
                }
                // Otherwise enable it
                else
                {
                    networking.VoiceChat.SetMicrophoneCapture(true);
                }
            }
        }
                
        #endregion Voice Chat Mode

        #region Language

        /// <summary>
        /// Updates the user's language preference.
        /// </summary>
        /// <param name="language">The language code of the language to update to (en-US, etc.)</param>
        /// <param name="force">Whether or not to force the update.</param>
        public void UpdateLanguage(string language, bool force = false)
        {
            if (string.IsNullOrEmpty(language)) throw new ArgumentNullException("language");
            var currentLanguage = Language;

            // If there is no change in value, don't update...
            if (currentLanguage == language && !force) return;

            // Otherwise update to the new language
            Language = language;

            CyLog.LogInfoFormat("Updated language preference to '{0}'", language);
        }

        #endregion Language

        #region Translation Voice

        /// <summary>
        /// Updates the user's auto-translation voice type.
        /// </summary>
        /// <param name="voice">The auto-translated spoken voice preference.</param>
        /// <param name="save">Whether or not to save the new preference.</param>
        /// <param name="force">Whether or not to force the update.</param>
        public void UpdateTranslationVoice(TranslationVoices voice, bool save = false, bool force = false)
        {
            // If there is no change in value, don't update...
            if (TranslationVoice == voice && !force) return;

            // Otherwise update to the new voice
            TranslationVoice = voice;
            if (save) SavePreferences();
            CyLog.LogInfoFormat("Updated auto-translated voice preference to '{0}'", voice);
        }

        #endregion Translation Voice

        #endregion Preference

        #endregion Methods
    }

    /// <summary>
    /// Events related to user input.
    /// </summary>
    [System.Serializable]
    public class SessionUserInputEvent : UnityEvent<string, float> { };
}
