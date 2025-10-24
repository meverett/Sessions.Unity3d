using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// A utility window/dialog that can be used to get various input from the user.
    /// </summary>
    public class SessionsUserInputEditorDialog : EditorWindow
    {
        #region Inspector

        #endregion Inspector

        #region Fields

        // The user input text
        private string text = "My OSC Config";
        private GUIStyle normalStyle;
        private GUIStyle errorStyle;
        private GUIStyle currentStyle;

        // Whether or not the dialog has an error
        private bool hasError = false;

        #endregion Fields

        #region Properties

        /// <summary>
        /// The title of the dialog window. 
        /// </summary>
        public GUIContent DialogTitle { get; set; }

        /// <summary>
        /// The title of the dialog window. 
        /// </summary>
        public GUIContent DialogContent { get; set; }

        /// <summary>
        /// The label of dialog's "OK" button.
        /// </summary>
        public GUIContent DialogOKButtonLabel { get; set; }

        /// <summary>
        /// The label of dialog's "Cancel" button.
        /// </summary>
        public GUIContent DialogCancelButtonLabel { get; set; }

        /// <summary>
        /// Gets or sets the current user input from the dialog.
        /// </summary>
        public string UserInput { get { return text; } set { text = value; } }

        /// <summary>
        /// The handler to call when the dialog "OK" button is clicked.
        /// </summary>
        public Func<bool> OnDialogAccepted;

        /// <summary>
        /// The handler to call when the dialog "Cancel" button is clicked.
        /// </summary>
        public Func<bool> OnDialogCancelled;

        /// <summary>
        /// Gets or sets whether or not the dialog currently has a validation error.
        /// </summary>
        public bool HasError
        {
            get { return hasError; }

            set
            {
                hasError = value;
                currentStyle = hasError ? errorStyle : normalStyle;
            }
        }

        #endregion Properties

        #region Methods

        private void OnGUI()
        {
            // Ensure styles
            if (errorStyle == null)
            {
                var errorTexture = Resources.Load<Texture2D>("Images/TextInputErrorBackground");

                errorStyle = new GUIStyle(GUI.skin.textField)
                {
                    active = { background = errorTexture, textColor = Color.black },
                    normal = { background = errorTexture, textColor = Color.black },
                    focused = { background = errorTexture, textColor = Color.black },
                    hover = { background = errorTexture, textColor = Color.black }
                };
            }

            if (normalStyle == null)
            {
                normalStyle = new GUIStyle(GUI.skin.textField)
                {
                };
            }

            if (currentStyle == null) currentStyle = normalStyle;

            // Render the title
            titleContent = DialogTitle;

            // Render the dialog content
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(DialogContent);
            EditorGUILayout.Space();

            // Render the user inptu text field
            text = EditorGUILayout.TextField(text, currentStyle);

            // Assume blank strings are never accepted
            if (string.IsNullOrEmpty(text)) HasError = true;
            else HasError = false;

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();

            // OK Button
            if (GUILayout.Button(DialogOKButtonLabel))
            {
                // No handler, just close
                if (OnDialogAccepted == null)
                {
                    Close();
                    return;
                }

                // Otherwise call the handler to see if we should close
                if (OnDialogAccepted()) Close();
            }

            // Cancel Button
            if (GUILayout.Button(DialogCancelButtonLabel))
            {
                // No handler, just close
                if (OnDialogCancelled == null)
                {
                    Close();
                    return;
                }

                // Otherwise call the handler to see if we should close
                if (OnDialogCancelled()) Close();
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion Methods
    }
}
