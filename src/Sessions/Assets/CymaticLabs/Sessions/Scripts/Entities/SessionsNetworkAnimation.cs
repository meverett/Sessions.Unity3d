using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using CymaticLabs.Logging;
using CymaticLabs.Sessions.Core;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Used to synchronize animation states over the network.
    /// </summary>
    public class SessionsNetworkAnimation : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The network state machine to use for the animation states.
        /// </summary>
        public SessionsNetworkStateMachine NetworkStates;

        /// <summary>
        /// A list of animators and their trigger state name.
        /// </summary>
        public AnimatorNetworkState[] NetworkAnimators;

        #endregion Inspector

        #region Fields

        /// <summary>
        /// A list of triggerable network animators.
        /// </summary>
        private Dictionary<string, List<AnimatorNetworkState>> networkAnimators;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Whether or not this instance belongs to the current network peer.
        /// </summary>
        public bool IsMine { get { return NetworkStates != null && NetworkStates.IsMine; } }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            networkAnimators = new Dictionary<string, List<AnimatorNetworkState>>();
        }

        private void Start()
        {
            if (NetworkStates == null) NetworkStates = GetComponentInChildren<SessionsNetworkStateMachine>();

            // Process network animators
            if (NetworkAnimators != null && NetworkAnimators.Length > 0)
            {
                foreach (var netAnim in NetworkAnimators)
                {
                    if (string.IsNullOrEmpty(netAnim.StateName))
                    {
                        CyLog.LogWarnFormat("Network Animator has no state name and will be ignored");
                        continue;
                    }

                    // Get the list of network animation triggers for this state
                    if (!networkAnimators.ContainsKey(netAnim.StateName)) networkAnimators.Add(netAnim.StateName, new List<AnimatorNetworkState>());
                    var list = networkAnimators[netAnim.StateName];

                    // Add this trigger
                    list.Add(netAnim);

                    // Install the state into the state machine
                    var animState = new CustomSessionState(netAnim.StateName, true);

                    // For now we only need to set the trigger on enter
                    animState.EnterHandler = (time, force) =>
                    {
                        // Get the duration of the animation
                        float percent = time / netAnim.Duration;
                        netAnim.Animator.Play(netAnim.StateName, 0, percent);
                        return true;
                    };

                    animState.UpdateHandler = (time) =>
                    {
                        // If the animation is finished, exit the state
                        if (animState.Time >= netAnim.Duration) return false;
                        return true;
                    };

                    // Register the animation state with the network state machine
                    NetworkStates.States.Add(animState);
                }
            }
        }

        #endregion Init

        #region Update

        #endregion Update

        #endregion Methods
    }

    /// <summary>
    /// Utility class to be able to register animators to network animator states.
    /// </summary>
    [System.Serializable]
    public class AnimatorNetworkState
    {
        /// <summary>
        /// The name of the state/trigger assigned to the animator.
        /// </summary>
        public string StateName;

        /// <summary>
        /// The animator to synchronize states for the assigned trigger.
        /// </summary>
        public Animator Animator;

        /// <summary>
        /// The duration of the animation state in seconds.
        /// </summary>
        public float Duration;
    }
}
