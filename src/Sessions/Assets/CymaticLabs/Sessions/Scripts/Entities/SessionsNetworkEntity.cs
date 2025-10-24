using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using CymaticLabs.Logging;
using CymaticLabs.Sessions.Core;
using LiteNetLib;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Registers an entity/prefab for network synchronization and instantiation.
    /// </summary>
    public class SessionsNetworkEntity : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// When True, disables synchronization of the network entity.
        /// </summary>
        [Tooltip("When True, disables synchronization of the network entity.")]
        public bool DisableNetworkSync = false;

        /// <summary>
        /// Whether or not this entity represents a player.
        /// </summary>
        [Tooltip("Whether or not this entity represents a player.")]
        public bool IsPlayer = false;

        /// <summary>
        /// Network instance registration will only be carried out if the settings match the selected mode.
        /// </summary>
        [Tooltip("Network instance registration will only be carried out if the settings match the selected mode.")]
        public NetworkRegistrationModes RegistrationMode = NetworkRegistrationModes.HostOnly;

        /// <summary>
        /// Optional reference to the parent entity instance.
        /// </summary>
        [Tooltip("Optional reference to the parent entity instance.")]
        public SessionsNetworkEntity ParentEntity;

        /// <summary>
        /// Occurs when the network instance has registered with the network entity manager.
        /// </summary>
        public SessionNetworkEntityEvent OnRegistered;

        #endregion Inspector

        #region Fields

        // A list of network transforms that belong to the entity, indexed by their network name
        private Dictionary<string, SessionsNetworkTransform> networkTransforms;

        // A list of network state machines that belong to the entity, indexed by their network name
        private Dictionary<string, SessionsNetworkStateMachine> networkStates;

        // A list of RPC commands by their RPC name
        private Dictionary<string, RpcCommand> rpcCommands;

        // Whether or not the instance is currently registered with network activity through the entity network manager.
        private bool isRegisteredWithNetwork = false;

        #endregion Fields

        #region Properties

        /// <summary>
        /// The ID of the instance.
        /// </summary>
        public long Id { get; internal set; }

        /// <summary>
        /// The owner of the object.
        /// </summary>
        public SessionAgent Owner { get; internal set; }

        /// <summary>
        /// Whether or not the current copy of the object belongs to the current agent or a remote agent.
        /// </summary>
        public bool IsMine { get; internal set; }

        /// <summary>
        /// Whether or not this network entity instance is currently registered with the <see cref="SessionsNetworkEntityManager"/>.
        /// </summary>
        public bool IsRegisteredWithNetwork
        {
            get { return isRegisteredWithNetwork; }

            set
            {
                if (isRegisteredWithNetwork == value) return;
                isRegisteredWithNetwork = value;

                // If this instance just registered, notify
                if (isRegisteredWithNetwork) OnRegistered.Invoke(this);
            }
        }

        /// <summary>
        /// Gets the network entity registration information for the instance.
        /// </summary>
        public SessionsNetworkEntityInfo EntityInfo { get; internal set; }

        /// <summary>
        /// The entity manager managing this entity.
        /// </summary>
        public SessionsNetworkEntityManager EntityManager { get; internal set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            Id = IdUtilities.NewUniqeId64(6); // we only need 6 bytes for a sufficiently unique ID

            rpcCommands = new Dictionary<string, RpcCommand>();

            networkTransforms = new Dictionary<string, SessionsNetworkTransform>();

            // Search for all child network transforms and register them with this entity
            foreach (var netTrans in GetComponentsInChildren<SessionsNetworkTransform>())
            {
                if (networkTransforms.ContainsKey(netTrans.NetworkName))
                {
                    Debug.LogWarningFormat("Network Entity instance {0} already has a network transform named {1} and will be overwritten.");
                    networkTransforms[netTrans.NetworkName] = netTrans;
                }
                else
                {
                    networkTransforms.Add(netTrans.NetworkName, netTrans);
                }
            }

            networkStates = new Dictionary<string, SessionsNetworkStateMachine>();

            // Search for all child network transforms and register them with this entity
            foreach (var netState in GetComponentsInChildren<SessionsNetworkStateMachine>())
            {
                if (networkStates.ContainsKey(netState.NetworkName))
                {
                    Debug.LogWarningFormat("Network Entity instance {0} already has a network state machine named {1} and will be overwritten.");
                    networkStates[netState.NetworkName] = netState;
                }
                else
                {
                    networkStates.Add(netState.NetworkName, netState);
                }
            }
        }

        #endregion Init

        #region Clean Up

        private void OnDestroy()
        {
            // Unregister self while being destroyed
            if (IsRegisteredWithNetwork)
            {
                Debug.LogWarningFormat("Network Entity instance was destroyed without properly unregistering from the network first: {0}:{1}", EntityInfo.Name, Id);
                SessionsNetworkEntityManager.Current.UnregisterNetworkInstance(this);
            }
        }

        #endregion Clean Up

        #region Update

        private void Update()
        {
            var sessions = SessionsUdpNetworking.Current;

            // If we are not registered or the network is not operation, don't do anything            
            if (DisableNetworkSync || !IsRegisteredWithNetwork || sessions == null || !sessions.IsStarted) return;

            // Make sure this is transform is belongs to this client and if so broadcast its transform data
            if (IsMine)
            {
                // Send updates for all network transforms that belong to this entity
                foreach (var pair in networkTransforms)
                {
                    var msg = pair.Value.AsTransformMessage();

                    foreach (var agent in sessions.GetAllAgents())
                    {
                        // Ensure there is a peer to send data too
                        if (agent.Peer == null && !agent.IsRelayed) continue;

                        // Send the message
                        sessions.SendToAgent(agent, msg, SendOptions.Unreliable);
                    }
                }

                // Update all network states
                foreach (var pair in networkStates)
                {
                    pair.Value.States.Update(Time.time);
                }
            }
        }

        #endregion Update

        #region Transform

        /// <summary>
        /// Gets a network transform component that belongs to this instance by name.
        /// </summary>
        /// <param name="name">The network name of the network transform to get.</param>
        /// <returns>The network transform if it is found, otherwise NULL.</returns>
        public SessionsNetworkTransform GetNetworkTransform(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new System.ArgumentNullException("name");
            return networkTransforms.ContainsKey(name) ? networkTransforms[name] : null;
        }

        #endregion Transform

        #region States

        /// <summary>
        /// Gets all of the current network state machines for the entity.
        /// </summary>
        /// <returns></returns>
        public ICollection<SessionsNetworkStateMachine>GetAllNetworkStates()
        {
            return networkStates.Values.ToArray();
        }

        /// <summary>
        /// Gets a network state machine component that belongs to this instance by name.
        /// </summary>
        /// <param name="name">The network name of the network state machine to get.</param>
        /// <returns>The network state machine if it is found, otherwise NULL.</returns>
        public SessionsNetworkStateMachine GetNetworkStateMachine(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new System.ArgumentNullException("name");
            return networkStates.ContainsKey(name) ? networkStates[name] : null;
        }

        #endregion States

        #region RPC

        #region Get

        /// <summary>
        /// Gets an RPC command handler given its command name.
        /// </summary>
        /// <param name="name">The RPC name.</param>
        /// <returns>The command handler if its found, otherwise NULL.</returns>
        public RpcCommand GetRpc(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new System.ArgumentNullException("name");
            return rpcCommands.ContainsKey(name) ? rpcCommands[name] : null;
        }

        #endregion Get

        #region Call

        /// <summary>
        /// Calls a registered RPC.
        /// </summary>
        /// <param name="agentId">The ID of the agent calling the command.</param>
        /// <param name="name">The name of the RPC to call.</param>
        /// <param name="isLocal">Whether or not it is only being called locally, or network-wide.</param>
        /// <param name="args">The RPC's string argument.</param>
        /// <param name="value">The RPC's value argument.</param>
        public void CallRpc(Guid agentId, string name, bool isLocal, string args = null, float? value = null)
        {
            if (string.IsNullOrEmpty(name)) throw new System.ArgumentNullException("name");
            var command = GetRpc(name);
            if (command == null) return;
            float numericVal = value != null ? value.Value : 0;
            command(agentId, isLocal, args, numericVal);

            // If this isn't just local playback, broadcast
            if (!isLocal)
            {
                // Create the RPC message
                var msg = new RpcMessage(MessageFlags.None, name, numericVal, args, null);
                msg.NetworkEntityId = Id; // tag this message with this entity instance's ID
                EntityManager.SessionsNetworking.SendToAll(msg, SendOptions.ReliableOrdered);
            }
        }

        #endregion Call

        #region Register

        /// <summary>
        /// Registers a remote procedure call command with its command name and the network.
        /// </summary>
        /// <param name="name">The name of the RPC command.</param>
        /// <param name="command">The command handler that will handle the command call.</param>
        public void RegisterRpcCommand(string name, RpcCommand command)
        {
            if (string.IsNullOrEmpty(name)) throw new System.ArgumentNullException("name");
            if (command == null) throw new System.ArgumentNullException("command");

            if (rpcCommands.ContainsKey(name))
            {
                CyLog.LogWarnFormat("RPC command already registered and being overwritten with name: {0}", name);
                rpcCommands[name] = command;
            }
            else
            {
                rpcCommands.Add(name, command);
            }
        }

        #endregion Register

        #region Unregister

        /// <summary>
        /// Unregisters a remote procedure call command from its command name and the network.
        /// </summary>
        /// <param name="name">The name of the RPC command.</param>
        public void UnregisterRpcCommand(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new System.ArgumentNullException("name");

            if (!rpcCommands.ContainsKey(name))
            {
                CyLog.LogWarnFormat("RPC command was not already registered and cannot unregister: {0}", name);
                return;
            }
            else
            {
                rpcCommands.Remove(name);
            }
        }

        #endregion Unregister

        #endregion RPC

        #endregion Methods
    }

    /// <summary>
    /// Sessions network entity related events.
    /// </summary>
    [System.Serializable]
    public class SessionNetworkEntityEvent : UnityEvent<SessionsNetworkEntity> { }
}
