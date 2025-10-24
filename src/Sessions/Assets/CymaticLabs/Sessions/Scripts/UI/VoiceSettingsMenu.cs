using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using CymaticLabs.Logging;
using CymaticLabs.Language.Unity3d;
using CymaticLabs.Sessions.Core;

namespace CymaticLabs.Sessions.Unity3d
{
    public class VoiceSettingsMenu : SessionsMenuBase
    {
        #region Inspector

        /// <summary>
        /// The positional voice toggle menu to use.
        /// </summary>
        [Tooltip("The positional voice toggle menu to use.")]
        public Toggle PositionalVoiceToggle;

        /// <summary>
        /// The voice chat drop down menu to use.
        /// </summary>
        [Tooltip("The voice chat drop down menu to use.")]
        public Dropdown ModeDropdown;

        /// <summary>
        /// The user's language preference.
        /// </summary>
        [Tooltip("The users language preference.")]
        public Dropdown LanguageDropdown;

        /// <summary>
        /// The speaker voice drop down menu to use.
        /// </summary>
        [Tooltip("The speaker voice drop down menu to use.")]
        public Dropdown SpeakerVoiceDropdown;

        #endregion Inspector

        #region Fields

        private LanguageInfo[] languages;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static VoiceSettingsMenu Current { get; private set; }

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

            #region Register UI Event Handlers

            // Update changes to positional audio
            if (PositionalVoiceToggle != null) PositionalVoiceToggle.onValueChanged.AddListener((enabled) =>
            {
                if (SessionsUser.Current == null) return;
                SessionsUser.Current.UpdateSpatialVoice(PositionalVoiceToggle.isOn, true);
            });

            // Update the user setting based on the drop down
            if (ModeDropdown != null) ModeDropdown.onValueChanged.AddListener((index) =>
            {
                if (SessionsUser.Current == null) return;
                SessionsUser.Current.UpdateVoiceMode((VoiceChatModes)ModeDropdown.value, true);

                // Enable/disable voice selection based on mode selection
                if (SpeakerVoiceDropdown != null)
                    SpeakerVoiceDropdown.interactable = ModeDropdown != null && ((VoiceChatModes)ModeDropdown.value) == VoiceChatModes.AutoTranslated;
            });

            // Updates changes to user language
            if (LanguageDropdown != null) LanguageDropdown.onValueChanged.AddListener((index) =>
            {
                if (SessionsUser.Current == null) return;

                // Get the language code from the selected item
                var item = LanguageDropdown.options[LanguageDropdown.value];
                var parsed = item.text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var language = parsed[0].Replace("[", "").Replace("]", "");
                SessionsUser.Current.UpdateLanguage(language);
            });

            // Updates changes ot user translation voice type
            if (SpeakerVoiceDropdown != null) SpeakerVoiceDropdown.onValueChanged.AddListener((index) =>
            {
                if (SessionsUser.Current == null) return;
                SessionsUser.Current.UpdateTranslationVoice((TranslationVoices)SpeakerVoiceDropdown.value, true);
            });

            #endregion Register UI Event Handlers

            #region Populate Language Dropdown

            if (LanguageDropdown != null && LanguageServices.Current != null)
            {
                languages = LanguageServices.Current.GetLanguages();
                
                var selectedIndex = 0;

                if (languages.Length > 0)
                {
                    var items = new List<Dropdown.OptionData>();

                    for (var i = 0; i < languages.Length; i++)
                    {
                        var language = languages[i];
                        var item = new Dropdown.OptionData(string.Format("[{0}] {1}", language.Code, language.Label));
                        items.Add(item);

                        // Is this the current language? If so automatically select it
                        if (Application.systemLanguage == language.SystemLanguage) selectedIndex = i;
                    }

                    LanguageDropdown.options = items;
                    LanguageDropdown.value = selectedIndex;
                }
            }

            #endregion Populate Language Dropdown

            // Select translation voice
            if (SpeakerVoiceDropdown != null && SessionsUser.Current != null)
                SpeakerVoiceDropdown.value = (int)SessionsUser.Current.TranslationVoice;

            // Disable the voice dropdown unless auto-translate is selected
            if (SpeakerVoiceDropdown != null) SpeakerVoiceDropdown.interactable = false;

            // Enable/disable voice selection based on mode selection
            if (SpeakerVoiceDropdown != null)
                SpeakerVoiceDropdown.interactable = ModeDropdown != null && ((VoiceChatModes)ModeDropdown.value) == VoiceChatModes.AutoTranslated;

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
            Bind();
        }

        // Data bind the UI control
        private void Bind()
        {
            var user = SessionsUser.Current;
            if (user == null) return;
            if (PositionalVoiceToggle != null) PositionalVoiceToggle.isOn = user.SpatialVoice;
            if (ModeDropdown != null) ModeDropdown.value = (int)user.VoiceMode;
        }

        #endregion Operation

        #region Update

        private void Update()
        {
            if (MenuContainer != null && !MenuContainer.activeSelf) return; // nothing to do; hidden

            // HACK Dropdown lists create items at runtime with a normal canvas and graphics raycaster, we need to use OVR
            if (SessionsUdpNetworking.IsXR)
            {
                if (ModeDropdown != null)
                {
                    // Foreach found graphics ray caster...
                    var found = ModeDropdown.GetComponentsInChildren<GraphicRaycaster>();

                    foreach (var gr in found)
                    {
                        // Create an OVRRaycaster and replace it
                        var ovr = gr.GetComponent<OVRRaycaster>();

                        if (ovr == null)
                        {
                            gr.enabled = false;
                            ovr = gr.gameObject.AddComponent<OVRRaycaster>();
                            ovr.blockingObjects = GraphicRaycaster.BlockingObjects.All;
                            ovr.sortOrder = 20;
                        }
                    }
                }

                if (LanguageDropdown != null)
                {
                    // Foreach found graphics ray caster...
                    var found = LanguageDropdown.GetComponentsInChildren<GraphicRaycaster>();

                    foreach (var gr in found)
                    {
                        // Create an OVRRaycaster and replace it
                        var ovr = gr.GetComponent<OVRRaycaster>();

                        if (ovr == null)
                        {
                            gr.enabled = false;
                            ovr = gr.gameObject.AddComponent<OVRRaycaster>();
                            ovr.blockingObjects = GraphicRaycaster.BlockingObjects.All;
                            ovr.sortOrder = 20;
                        }
                    }
                }

                if (SpeakerVoiceDropdown != null)
                {
                    // Foreach found graphics ray caster...
                    var found = SpeakerVoiceDropdown.GetComponentsInChildren<GraphicRaycaster>();

                    foreach (var gr in found)
                    {
                        // Create an OVRRaycaster and replace it
                        var ovr = gr.GetComponent<OVRRaycaster>();

                        if (ovr == null)
                        {
                            gr.enabled = false;
                            ovr = gr.gameObject.AddComponent<OVRRaycaster>();
                            ovr.blockingObjects = GraphicRaycaster.BlockingObjects.All;
                            ovr.sortOrder = 20;
                        }
                    }
                }
            }
        }

        #endregion Update

        #endregion Methods
    }
}
