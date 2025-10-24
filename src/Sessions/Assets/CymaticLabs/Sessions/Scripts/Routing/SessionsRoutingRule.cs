using System;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Serializable asset that contains session value routing rules and event triggers.
    /// </summary>
    public class SessionsRoutingRule : ScriptableObject
    {
        /// <summary>
        /// Whether or not the rule is currently enabled.
        /// </summary>
        public bool Enabled = true;

        /// <summary>
        /// The name of the session value.
        /// </summary>
        public string Name = "Value1";

        /// <summary>
        /// The type of value comparison used for the rule.
        /// </summary>
        public ValueCompare Comparison = ValueCompare.EqualTo;

        /// <summary>
        /// The rule specified comparison value.
        /// </summary>
        public float Value = 1;

        /// <summary>
        /// Occurs when the rule is satisified by the incoming session value event.
        /// </summary>
        public SessionValueEvent OnPassed;

        /// <summary>
        /// Used as a work around to allow edits compatibility with Unity Editor's Undo/Redo stack.
        /// </summary>
        static internal Action<SessionsRoutingRule> OnCreated;

        /// <summary>
        /// Gets the current position of the mapping in the editor's list.
        /// </summary>
        [HideInInspector]
        public int EditIndex;

        /// <summary>
        /// Whether or not the rule is expanded in the UI.
        /// </summary>
        [HideInInspector]
        public bool IsExpanded;

        public SessionsRoutingRule()
        {
            // Notify we were created
            EditIndex = -1;
        }
    }
}
