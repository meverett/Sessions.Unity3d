using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using CymaticLabs.Logging;
using CymaticLabs.Sessions.Core;
using LiteNetLib;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Synchronizes a state machine's states over the network for an associated network entity.
    /// </summary>
    public class SessionsNetworkStateMachine : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The network entity this transform belongs to.
        /// </summary>
        [Header("Registration")]
        [Tooltip("The network entity this transform belongs to.")]
        public SessionsNetworkEntity NetworkEntity;

        /// <summary>
        /// The network name of the tracked object as part of its parent entity.
        /// </summary>
        [Tooltip("The network name of the tracked object as part of its parent entity.")]
        public string NetworkName = "States1";

        /// <summary>
        /// Whether or not this is entity has shared states that everyone on the network can change.
        /// </summary>
        [Tooltip("Whether or not this is entity has shared states that everyone on the network can change.")]
        //public bool SharedStates = false;

        /// <summary>
        /// Occurs when an entity network state is entered.
        /// </summary>
        public SessionStateChangeEvent OnNetworkStateEntered;

        /// <summary>
        /// Occurs when an entity network state is exited.
        /// </summary>
        public SessionStateChangeEvent OnNetworkStateExited;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        /// <summary>
        /// The underlying state machine being synchronized on the network.
        /// </summary>
        public SessionStateMachine States { get; private set; }

        /// <summary>
        /// Whether or not this instance belongs to the current network peer.
        /// </summary>
        public bool IsMine { get { return NetworkEntity != null && NetworkEntity.IsMine; } }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            if (NetworkEntity == null) GetComponentInChildren<SessionsNetworkEntity>();
            States = new SessionStateMachine();
            States.Owner = this; // bind to the state machine as its owning object

            // Register to state machine events
            States.OnStateEntered += States_OnStateEntered;
            States.OnStateExited += States_OnStateExited;
        }

        #endregion Init

        #region Update

        private void Update()
        {
            // Update own states
            if (States == null) return;
            States.Update(Time.time);
        }

        #endregion Update

        #region States

        /// <summary>
        /// Enters a state on the state machine.
        /// </summary>
        /// <param name="name">The name of the state to enter.</param>
        public void EnterState(string name)
        {
            States.EnterState(name, 0, false);
        }

        /// <summary>
        /// Enters a state on the state machine.
        /// </summary>
        /// <param name="name">The name of the state to enter.</param>
        public void EnterStateForced(string name)
        {
            States.EnterState(name, 0, true);
        }

        /// <summary>
        /// Enters a state on the state machine.
        /// </summary>
        /// <param name="name">The name of the state to enter.</param>
        /// <param name="time">The time of the state to enter.</param>
        /// <param name="force">Whether or not to force entry.</param>
        public void EnterState(string name, float time = 0, bool force = false)
        {
            States.EnterState(name, time, force);
        }

        /// <summary>
        /// Exits a state on the state machine.
        /// </summary>
        /// <param name="name">The name of the state to exit.</param>
        public void ExitState(string name)
        {
            States.ExitState(name, false);
        }

        /// <summary>
        /// Exits a state on the state machine.
        /// </summary>
        /// <param name="name">The name of the state to exit.</param>
        public void ExitStateForced(string name)
        {
            States.ExitState(name, true);
        }

        /// <summary>
        /// Exits a state on the state machine.
        /// </summary>
        /// <param name="name">The name of the state to exit.</param>
        /// <param name="force">Whether or not to force exit.</param>
        public void ExitState(string name, bool force = false)
        {
            States.ExitState(name, force);
        }

        /// <summary>
        /// Updates transform information from a network message.
        /// </summary>
        public void BindToStateMessage(StateMessage message)
        {
            if (message == null) throw new System.ArgumentException("message");
            if (message.NetworkEntityId != NetworkEntity.Id) throw new System.Exception("Wrong instance ID: " + message.Id.ToString());
            if (NetworkEntity.IsMine) return; // don't bind on own instance

            // If this is a state enter message...
            if (message.Name == "StateEnter")
            {
                // Parse the state machine's network name, and the state
                if (message.NetworkName != NetworkName) return; // not for this network state machine
                States.EnterState(message.StateName, message.Value, true);
            }
            else if (message.Name == "StateExit")
            {
                // Parse the state machine's network name, and the state
                if (message.NetworkName != NetworkName) return; // not for this network state machine
                States.ExitState(message.StateName, true);
            }
        }

        // Handle the entering of a network state
        private void States_OnStateEntered(SessionStateMachine stateMachine, ISessionState state, float time)
        {
            // Get the network state for the state machine
            var netState = (SessionsNetworkStateMachine)stateMachine.Owner;

            // Notify
            OnNetworkStateEntered.Invoke(stateMachine, state.Name);

            // If this is not mine, ignore
            if (!netState.NetworkEntity.IsMine) return;

            // TODO Make this my dynamically injectable for network reference?
            var msg = new StateMessage(MessageFlags.None, "StateEnter", time, null, NetworkName, state.Name);
            msg.NetworkEntityId = NetworkEntity.Id;

             // Send to all connected clients
            var sessions = SessionsUdpNetworking.Current;

            foreach (var agent in sessions.GetAllAgents())
            {
                sessions.SendToAgent(agent, msg, SendOptions.ReliableOrdered);
            }
        }

        // Handle the exiting of a network state
        private void States_OnStateExited(SessionStateMachine stateMachine, ISessionState state, float time)
        {
            // Get the network state for the state machine
            var netState = (SessionsNetworkStateMachine)stateMachine.Owner;

            // Notify
            OnNetworkStateExited.Invoke(stateMachine, state.Name);

            // If this is not mine, ignore
            if (!netState.NetworkEntity.IsMine) return;

            // TODO Make this my dynamically injectable for network reference?
            var msg = new StateMessage(MessageFlags.None, "StateExit", time, null, NetworkName, state.Name);
            msg.NetworkEntityId = NetworkEntity.Id;

            // Send to all connected clients
            var sessions = SessionsUdpNetworking.Current;

            foreach (var agent in sessions.GetAllAgents())
            {
                sessions.SendToAgent(agent, msg, SendOptions.ReliableOrdered);
            }
        }

        #endregion States

        #endregion Methods
    }

    /// <summary>
    /// Unity event related to changes of a state machine.
    /// </summary>
    [Serializable]
    public class SessionStateChangeEvent : UnityEvent<SessionStateMachine, string> { }
}
