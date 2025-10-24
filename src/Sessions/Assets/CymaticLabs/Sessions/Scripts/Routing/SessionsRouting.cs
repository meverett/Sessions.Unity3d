using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.Events;
using CymaticLabs.Logging;
using CymaticLabs.Sessions.Core;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Manages connections between session values and scene event triggers.
    /// </summary>
    public class SessionsRouting : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The sessions manager instance to use.
        /// </summary>
        [Tooltip("The sessions manager instance to use.")]
        public SessionsSceneManager SessionsManager;

        /// <summary>
        /// The sessions networking instance to use.
        /// </summary>
        [Tooltip("The sessions networking instance to use.")]
        public SessionsUdpNetworking SessionsNetworking;

        /// <summary>
        /// The current routing configuration file to use.
        /// </summary>
        [HideInInspector]
        public SessionsRoutingConfiguration Configuration;

        /// <summary>
        /// A list of all of the current configurations within the config.
        /// </summary>
        [HideInInspector]
        public List<SessionsRoutingConfiguration> AllConfigurations;

        #endregion Inspector

        #region Fields

        // A list of value triggers given name of the session value
        private Dictionary<string, List<SessionsRoutingRule>> rulesByValueName;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static SessionsRouting Current { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            Current = this;
            rulesByValueName = new Dictionary<string, List<SessionsRoutingRule>>();
        }

        private void Start()
        {
            if (SessionsNetworking == null) SessionsNetworking = SessionsUdpNetworking.Current;
            if (SessionsManager == null) SessionsManager = SessionsSceneManager.Current;
            LoadConfiguration();           
        }

        #endregion Init

        #region Update

        #endregion Update

        #region Sessions

        #region Values

        /// <summary>
        /// Handles a session value being updated.
        /// </summary>
        /// <param name="name">The name of the value.</param>
        /// <param name="value">The value.</param>
        /// <param name="raw">The optional raw source object for the value update.</param>
        public void HandleSessionValueChange(string name, float value, object raw)
        {
            if (string.IsNullOrEmpty(name)) throw new System.ArgumentNullException("name");

            // CymaticLabs.Logging.CyLog.LogInfoFormat("{0} = {1}", name, value);

            // Ensure a trigger is registered for this value name
            if (!rulesByValueName.ContainsKey(name)) return;

            // Go through and evaluate triggers
            foreach (var item in rulesByValueName[name])
            {
                // Start out assuming failure
                bool passed = false;

                #region Compare Values

                switch (item.Comparison)
                {
                    case ValueCompare.Any:
                        passed = true;
                        break;

                    case ValueCompare.EqualTo:
                        passed = item.Value == value;
                        break;

                    case ValueCompare.GreaterThan:
                        passed = value > item.Value;
                        break;

                    case ValueCompare.GreatherThanEqualTo:
                        passed = value >= item.Value;
                        break;

                    case ValueCompare.LessThan:
                        passed = value < item.Value;
                        break;

                    case ValueCompare.LessThanEqualTo:
                        passed = value <= item.Value;
                        break;

                    case ValueCompare.NotEqualTo:
                        passed = value != item.Value;
                        break;
                }

                #endregion Compare Values

                // If a trigger occured, notify/invoke trigger event handlers
                if (passed) item.OnPassed.Invoke(name, value, raw);
            }
        }

        #endregion Values

        #endregion Sessions

        #region Configuration

        /// <summary>
        /// Loads the current configuration into memory.
        /// </summary>
        public void LoadConfiguration()
        {
            if (Configuration == null || Configuration.Rules == null || Configuration.Rules.Length == 0) return;

            if (rulesByValueName != null) rulesByValueName.Clear();
            else rulesByValueName = new Dictionary<string, List<SessionsRoutingRule>>();

            for (var i = 0; i < Configuration.Rules.Length; i++)
            {
                var rule = Configuration.Rules[i];
                if (rule == null || !rule.Enabled) continue;

                // Ensure the list for this name
                if (!rulesByValueName.ContainsKey(rule.Name)) rulesByValueName.Add(rule.Name, new List<SessionsRoutingRule>());

                // Add the trigger to the list for this value name
                rulesByValueName[rule.Name].Add(rule);
            }
        }

        /// <summary>
        /// Updates a rule during runtime.
        /// </summary>
        /// <param name="rule">The rule to update.</param>
        public void UpdateRule(SessionsRoutingRule rule, bool copyListeners = false)
        {
            if (rule == null) throw new ArgumentNullException("rule");
            var match = rulesByValueName.SelectMany(rl => rl.Value).FirstOrDefault(r => r.EditIndex == rule.EditIndex);

            if (match != null)
            {
                Debug.Log("Found a match!");
                match.Name = rule.Name;
                match.Comparison = rule.Comparison;
                if (copyListeners) match.OnPassed = rule.OnPassed;
                match.Enabled = rule.Enabled;
            }
        }

        #endregion Configuration

        #endregion Methods
    }

    /// <summary>
    /// Different type of value oper
    /// </summary>
    public enum ValueCompare
    {
        /// <summary>
        /// Any value satisifies.
        /// </summary>
        Any,

        /// <summary>
        /// A less-than-or-equal-to comparison: <![CDATA[<=]]>
        /// </summary>
        LessThanEqualTo,

        /// <summary>
        /// A less-than comparison: <![CDATA[<]]>
        /// </summary>
        LessThan,

        /// <summary>
        /// An equal-to comparison: <![CDATA[==]]>
        /// </summary>
        EqualTo,

        /// <summary>
        /// A not-equal-to comparison: <![CDATA[!=]]>
        /// </summary>
        NotEqualTo,

        /// <summary>
        /// A greater-than comparison: <![CDATA[>]]>
        /// </summary>
        GreaterThan,

        /// <summary>
        /// A greater-than-or-equal-to comparison: <![CDATA[>=]]>
        /// </summary>
        GreatherThanEqualTo
    }
}
