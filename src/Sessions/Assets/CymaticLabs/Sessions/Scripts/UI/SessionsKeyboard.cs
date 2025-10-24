using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VRKeyboard;
using VRKeyboard.Utils;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// A VR enabled keyboard.
    /// </summary>
    public class SessionsKeyboard : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The udnerlying The keyboard manager to use.
        /// </summary>
        public KeyboardManager KeyboardManager;

        /// <summary>
        /// The text component to bind to.
        /// </summary>
        public Text Text;

        /// <summary>
        /// The input field to use when not using XR.
        /// </summary>
        public InputField NonXrInputField;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static SessionsKeyboard Current { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            Current = this;
        }

        private void Start()
        {
            // Automatically look for reference as necessary
            if (KeyboardManager == null) KeyboardManager = GetComponentInChildren<KeyboardManager>();
            if (KeyboardManager == null) return;

            // Initial bind of the text component to edit
            KeyboardManager.inputText = Text;

            // Get all of the keyboard buttons
            var buttons = KeyboardManager.gameObject.GetComponentsInChildren<Button>();

            // Add a component to each one to add sound interactivity
            foreach (var button in buttons) button.gameObject.AddComponent<SoundOnInteractionUI>();
        }

        #endregion Init

        #region Operation

        /// <summary>
        /// Shows the keyboard.
        /// </summary>
        public void ShowKeyboard()
        {
            if (KeyboardManager == null) return;
            KeyboardManager.gameObject.SetActive(true);
        }

        /// <summary>
        /// Shows the keyboard.
        /// </summary>
        public static void Show()
        {
            if (Current == null) return;
            Current.ShowKeyboard();
        }

        /// <summary>
        /// Hides the keyboard.
        /// </summary>
        public void HideKeyboard()
        {
            if (KeyboardManager == null) return;
            KeyboardManager.gameObject.SetActive(false);
        }

        /// <summary>
        /// Hides the keyboard.
        /// </summary>
        public static void Hide()
        {
            if (Current == null) return;
            Current.HideKeyboard();
        }

        /// <summary>
        /// Binds the keyboard to a text field.
        /// </summary>
        /// <param name="text">The text component to bind to.</param>
        public void BindText(Text text)
        {
            if (KeyboardManager == null) return;
            Text = text;
            KeyboardManager.inputText = text;
        }

        /// <summary>
        /// Binds the keyboard to a text field.
        /// </summary>
        /// <param name="text">The text component to bind to.</param>
        public static void Bind(Text text)
        {
            if (Current == null) return;
            Current.BindText(text);
        }

        #endregion Operation

        #region Update

        private void Update()
        {
            // If this is non-XR, use the simulated VR keyboard input to bind to the text input instead
            if (!SessionsUdpNetworking.IsXR)
            {
                if (Text != null && NonXrInputField != null && !string.IsNullOrEmpty(NonXrInputField.text))
                {
                    Text.text = NonXrInputField.text;
                }
            }
        }

        #endregion Update

        #endregion Methods
    }
}
