using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using CymaticLabs.Sessions.Core;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Triggers a network state on a network state machine on collision.
    /// </summary>
    public class NetworkStateCollisionTrigger : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The network state machine instance to trigger events on.
        /// </summary>
        public SessionsNetworkStateMachine NetworkStates;

        /// <summary>
        /// The name of the network state to trigger/enter on collision.
        /// </summary>
        public string StateName = "NetworkState";

        /// <summary>
        /// Whether or not to force the state.
        /// </summary>
        public bool ForceState = true;

        /// <summary>
        /// Whether or not the event occurs on trigger.
        /// </summary>
        public bool TriggerEnabled = true;

        /// <summary>
        /// Whether or not the event occurs on collision.
        /// </summary>
        public bool CollisionEnabled = true;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        #endregion Init

        #region Update

        #endregion Update

        public void OnCollisionEnter(Collision collision)
        {
            if (!CollisionEnabled || NetworkStates == null) return;
            NetworkStates.States.EnterState(StateName, 0, ForceState);
        }

        public void OnTriggerEnter(Collider other)
        {
            if (!TriggerEnabled || NetworkStates == null) return;
            NetworkStates.States.EnterState(StateName, 0, ForceState);
        }

        public void ExitState()
        {
            if (NetworkStates == null) return;
            NetworkStates.States.ExitState(StateName, ForceState);
        }

        #endregion Methods
    }
}
