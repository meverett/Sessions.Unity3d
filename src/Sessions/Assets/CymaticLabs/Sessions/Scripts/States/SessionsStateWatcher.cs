using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using CymaticLabs.Sessions.Core;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Watches the states of one or more <see cref="SessionStateMachine">state machines</see> and
    /// triggers an event when the one or more states are active or inactive. Typically used to trigger
    /// a state when two or more composite states being watched are activated.
    /// </summary>
    public class SessionsStateWatcher : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// When enabled, monitoring of state triggers will occur every frame and not just during the enter/exit of monitored states.
        /// </summary>
        [Tooltip("When enabled, monitoring of state triggers will occur every frame and not just during the enter/exit of monitored states.")]
        public bool MonitorOnUpdate = false;

        /// <summary>
        /// Occurs when the states being monitored on the state machine match the active/inactive states list.
        /// </summary>
        public UnityEvent OnWatcherTriggerEntered;

        /// <summary>
        /// Occurs every frame when the states being monititored on the state machine match the active/inactive states list.
        /// </summary>
        public UnityEvent OnWatcherTriggered;

        /// <summary>
        /// Occurs when the states being monitored on the state machine no longer match the active/inactive states list.
        /// </summary>
        public UnityEvent OnWatcherTriggerExited;

        /// <summary>
        /// A list of state machines and states to monitor.
        /// </summary>
        public SessionsStateMachineMonitor[] StateMachineMonitors;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        /// <summary>
        /// Whether or not the state watcher is currently triggered where all watched states currently match the monitoring criteria.
        /// </summary>
        public bool IsTriggered { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Start()
        {
            IsTriggered = false;

            // Register to listen to supplied state machines to monitor state updates
            if (StateMachineMonitors != null && StateMachineMonitors.Length > 0)
            {
                foreach (var sm in StateMachineMonitors)
                {
                    if (sm == null || sm.StateMachine == null) continue;
                    sm.StateMachine.OnNetworkStateEntered.AddListener(HandleStateEntered);
                    sm.StateMachine.OnNetworkStateExited.AddListener(HandleStateExited);
                }
            }
        }

        #endregion Init

        #region Clean Up

        private void OnDestroy()
        {
            // Unregister from supplied state machines
            if (StateMachineMonitors != null && StateMachineMonitors.Length > 0)
            {
                foreach (var sm in StateMachineMonitors)
                {
                    if (sm == null || sm.StateMachine == null) continue;
                    sm.StateMachine.OnNetworkStateEntered.RemoveListener(HandleStateEntered);
                    sm.StateMachine.OnNetworkStateExited.RemoveListener(HandleStateExited);
                }
            }
        }

        #endregion Clean Up

        #region Update

        private void Update()
        {
            if (!MonitorOnUpdate) return;
            ProcessStateMonitors();
        }

        #endregion Update

        #region States

        /// <summary>
        /// Whether or not the state watchers monitoring criteria is currently satisfied/triggered.
        /// </summary>
        /// <returns>True if all state monitors are satisfied, otherwise False.</returns>
        public bool AreStatesTriggered()
        {
            // Go through the current list of state machines being monitored.
            if (StateMachineMonitors != null && StateMachineMonitors.Length > 0)
            {
                foreach (var sm in StateMachineMonitors)
                {
                    if (sm == null || sm.StateMachine == null) continue;
                    
                    // Check active states
                    foreach (var state in sm.ActiveStateNames)
                    {
                        if (!sm.StateMachine.States.IsActive(state)) return false;
                        if (sm.ActiveStateTimer > 0)
                        {
                            // If this is a timed activation state and the timer is still elapsing, nothing is triggered yet...
                            var s = sm.StateMachine.States.GetState(state);
                            if (s.IsActive && s.Time < sm.ActiveStateTimer) return false;
                        }
                    }
                        
                    // Check inactive states
                    foreach (var state in sm.InactiveStateNames)
                    {
                        if (sm.StateMachine.States.IsActive(state)) return false;
                    }
                }
            }

            // If we made it here, all monitoring criteria was satisifed
            return true;
        }

        // Process state triggers and fires the corresponding detected events
        private void ProcessStateMonitors()
        {
            var isTriggered = AreStatesTriggered();

            if (!IsTriggered && isTriggered)
            {
                IsTriggered = true;
                OnWatcherTriggerEntered.Invoke();
            }
            else if (IsTriggered && !isTriggered)
            {
                IsTriggered = false;
                OnWatcherTriggerExited.Invoke();
            }

            IsTriggered = IsTriggered;
            if (isTriggered) OnWatcherTriggered.Invoke();
        }

        #endregion States

        #region Event Handlers

        // Handles state entry for a monitored state machine
        private void HandleStateEntered(SessionStateMachine stateMachine, string stateName)
        {
            ProcessStateMonitors();
        }

        // Handles state exit for a monitored state machine
        private void HandleStateExited(SessionStateMachine stateMachine, string stateName)
        {
            ProcessStateMonitors();
        }

        #endregion Event Handlers

        #endregion Methods
    }

    /// <summary>
    /// Utility class used to register composite state monitoring of a state machine.
    /// </summary>
    [Serializable]
    public class SessionsStateMachineMonitor
    {
        /// <summary>
        /// The optional name of the state monitoring.
        /// </summary>
        [Tooltip("The optional name of the state monitoring.")]
        public string Name;

        /// <summary>
        /// The state machine to monitor.
        /// </summary>
        [Tooltip("The state machine to monitor.")]
        public SessionsNetworkStateMachine StateMachine;

        /// <summary>
        /// Optional time in seconds that the state must be enabled before the monitor is triggered for active states.
        /// </summary>
        [Tooltip("Optional time in seconds that the state must be enabled before the monitor is triggered for active states.")]
        public float ActiveStateTimer = 0;

        /// <summary>
        /// The names of the active states to monitor.
        /// </summary>
        [Tooltip("The names of the active states to monitor.")]
        public string[] ActiveStateNames;

          /// <summary>
        /// The names of the active states to monitor.
        /// </summary>
        [Tooltip("The names of the inactive states to monitor.")]
        public string[] InactiveStateNames;
    }
}
