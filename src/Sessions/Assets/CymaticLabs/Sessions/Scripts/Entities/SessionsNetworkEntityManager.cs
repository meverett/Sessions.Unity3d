using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CymaticLabs.Logging;
using CymaticLabs.Sessions.Core;
using LiteNetLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Mananges the registration and synchronization of network objects/entities.
    /// </summary>
    public class SessionsNetworkEntityManager : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The sessions networking reference to use.
        /// </summary>
        [Tooltip("The sessions networking reference to use.")]
        public SessionsUdpNetworking SessionsNetworking;

        /// <summary>
        /// The optional transform that acts as the root/parent transform for created network instances.
        /// </summary>
        [Tooltip("The optional transform that acts as the root/parent transform for created network instances.")]
        public Transform InstanceContainer;

        /// <summary>
        /// A list of player spawn points.
        /// </summary>
        [Tooltip("A list of player spawn points.")]
        public SpawnPoint[] SpawnPoints;

        /// <summary>
        /// The current network entity configuration.
        /// </summary>
        //[HideInInspector]
        public SessionsEntitiesConfiguration Configuration;

        /// <summary>
        /// A list of all network entity configurations.
        /// </summary>
        //[HideInInspector]
        public List<SessionsEntitiesConfiguration> AllConfigurations;

        #endregion Inspector

        #region Fields

        // A list of registered network entities by their network name.
        private Dictionary<string, SessionsNetworkEntityInfo> entitiesByName;

        // A list of registered network entity instances by their network name.
        private Dictionary<string, List<SessionsNetworkEntity>> instancesByName;

        // A list of registered network entity instances by their network ID.
        private Dictionary<long, SessionsNetworkEntity> instancesById;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static SessionsNetworkEntityManager Current { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            Current = this;
            entitiesByName = new Dictionary<string, SessionsNetworkEntityInfo>();
            instancesByName = new Dictionary<string, List<SessionsNetworkEntity>>();
            instancesById = new Dictionary<long, SessionsNetworkEntity>();

            if (SessionsNetworking == null) SessionsNetworking = SessionsUdpNetworking.Current;

            if (SessionsNetworking == null)
            {
                Debug.LogWarning("Sessions Networking reference is not assigned.");
                return;
            }

            // Load current configuration
            LoadConfiguration();

            // Register to network events
            SessionsNetworking.OnAgentConnected.AddListener(HandleAgentConnected);
            SessionsNetworking.OnAgentDisconnected.AddListener(HandleAgentDisconnected);
            SessionsNetworking.OnNetworkInstanceCreated.AddListener(HandleNetworkInstaceCreated);
            SessionsNetworking.OnNetworkTransformReceived.AddListener(HandleNetworkTransformUpdated);
            SessionsNetworking.OnNetworkStateEntered.AddListener(HandleNetworkStateChanged);
            SessionsNetworking.OnNetworkStateExited.AddListener(HandleNetworkStateChanged);
            SessionsNetworking.OnSessionHosted.AddListener(HandleSessionHosted);
            SessionsNetworking.OnRpcCommandExecuted.AddListener(HandleRpcCommand);
        }

        #endregion Init

        #region Load Configuration

        /// <summary>
        /// Loads the curerent network entities configuration.
        /// </summary>
        public void LoadConfiguration()
        {
            if (Configuration == null || Configuration.Entities == null || Configuration.Entities.Length == 0) return;

            // Register entities
            var totalRegistered = 0;
            if (entitiesByName == null) entitiesByName = new Dictionary<string, SessionsNetworkEntityInfo>();
            //else entitiesByName.Clear(); // don't clear for additive/overwrite loading for now

            for (var i = 0; i < Configuration.Entities.Length; i++)
            {
                var entityInfo = Configuration.Entities[i];

                if (entityInfo == null || !entityInfo.Enabled) continue;

                // No registration name
                if (string.IsNullOrEmpty(entityInfo.Name))
                {
                    Debug.LogWarningFormat("Network Entity registration has no name and will be ignored. Index:{0}", i);
                    continue;
                }

                // Ensure at least one prefab
                if (entityInfo.Prefab == null && entityInfo.NonXrPrefab == null)
                {
                    Debug.LogWarningFormat("Network Entity registration has no prefabs. Name:{0}, Index:{0}", entityInfo.Name, i);
                    //continue;
                }

                // Ensure unique key and register
                if (entitiesByName.ContainsKey(entityInfo.Name))
                {
                    Debug.LogWarningFormat("Duplicate Network Entity name during registration: {0}", entityInfo.Name);
                    entitiesByName[entityInfo.Name] = entityInfo;
                }
                else
                {
                    entitiesByName.Add(entityInfo.Name, entityInfo);
                }

                totalRegistered++;
            }

            //Debug.LogFormat("{0} network entities registered.", totalRegistered);
        }

        #endregion Load Configuration

        #region Update

        #endregion Update

        #region Registration

        /// <summary>
        /// Registers an instance of a network entity with the network manager.
        /// </summary>
        /// <param name="networkName">The network name that the entity is an instance of.</param>
        /// <param name="instance">The instance to register.</param>
        /// <param name="owner">The session agent who owns/created the instance.</param>
        public void RegisterNetworkInstance(string networkName, SessionsNetworkEntity instance, SessionAgent owner)
        {
            if (string.IsNullOrEmpty(networkName)) throw new ArgumentNullException("networkName");
            if (instance == null) throw new ArgumentException("instance");
            if (instance.IsRegisteredWithNetwork) return; // already registered
            if (!entitiesByName.ContainsKey(networkName)) throw new ArgumentException("No Network Entity is registered with network name: " + networkName);

            // Get the entry
            var entityInfo = entitiesByName[networkName];

            // Ensure a list for this type of entity exists
            if (!instancesByName.ContainsKey(networkName)) instancesByName.Add(networkName, new List<SessionsNetworkEntity>());
            var list = instancesByName[networkName];

            // Ensure there are not too many instances already registered
            if (list.Count >= entityInfo.MaxInstances)
                throw new InvalidOperationException("Maximum number of instances already exists for: " + networkName);

            // Add the instance
            list.Add(instance);
            instancesById.Add(instance.Id, instance);
            instance.EntityInfo = entityInfo;
            instance.Owner = owner;
            instance.IsMine = false;
            instance.EntityManager = this;

            // Next figure out who the instance belongs too
            // If we are the owner by ID of this instance...
            if (owner.Id == SessionsNetworking.AgentId)
            {
                var isHost = SessionsNetworking.IsHost;

                // If the instance registers in both modes, its my copy, or if host mode and I'm the host, or in peer mode and I a peer, also my copy
                if  (instance.RegistrationMode == NetworkRegistrationModes.Both ||
                    (instance.RegistrationMode == NetworkRegistrationModes.HostOnly && isHost) ||
                    (instance.RegistrationMode == NetworkRegistrationModes.PeerOnly && !isHost))
                {
                    instance.IsMine = true;
                }
                else
                {
                    CyLog.LogInfoFormat("{0} {1} instance registered and waiting for synchronization", networkName, instance.RegistrationMode);
                }
            }

            // Complete registration
            instance.IsRegisteredWithNetwork = true;

            // If this is a player, move them to the appropriate spawn point if available
            // HACK for now using the positional voice behavior works, but seems a little wonky
            //if (SpawnPoints != null && SpawnPoints.Length > 0 && instance.GetComponentInChildren<SessionsPositionalVoice>() != null)
            //{

            //    // Get the total number of connected agents/players
            //    var totalPlayers = SessionsNetworking.GetAllAgents().Count();

            //    // If there is a spawn point for this "index" of player, use it, otherwise just use the first one
            //    var spawn = totalPlayers < SpawnPoints.Length ? SpawnPoints[totalPlayers] : SpawnPoints[0];
            //    instance.transform.position = spawn.Position;
            //    instance.transform.eulerAngles = spawn.Rotation;
            //}

            CyLog.LogInfoFormat("Network Instance registered with ID: {0}", instance.Id);
        }

        /// <summary>
        /// Unregisters a network instance from the network.
        /// </summary>
        /// <param name="instance">The instance to unregister.</param>
        public void UnregisterNetworkInstance(SessionsNetworkEntity instance)
        {
            if (!instance.IsRegisteredWithNetwork) throw new ArgumentException("instance is not registered");

            // Otherwise find the instance
            var list = instancesByName[instance.EntityInfo.Name];
            list.Remove(instance);
            instancesById.Remove(instance.Id);
        }

        #endregion Registration

        #region Instances

        #region Get

        /// <summary>
        /// Gets a network entity instance given its ID.
        /// </summary>
        /// <param name="id">The ID of the instance to get.</param>
        /// <returns>The instance if it is found, otherwise NULL.</returns>
        public SessionsNetworkEntity GetInstanceById(long id)
        {
            return instancesById.ContainsKey(id) ? instancesById[id] : null;
        }

        /// <summary>
        /// Gets all current network entity instances for a given network entity name.
        /// </summary>
        /// <param name="name">The name of the network entity to get instances for.</param>
        /// <returns>An array of instances if any are found, otherwise an empty array.</returns>
        public SessionsNetworkEntity[] GetInstancesByName(string name)
        {
            return instancesByName.ContainsKey(name) ? instancesByName[name].ToArray() : new SessionsNetworkEntity[0];
        }

        /// <summary>
        /// Gets all current network entity instances.
        /// </summary>
        /// <returns>An array with the list of current network entity instances.</returns>
        public SessionsNetworkEntity[] GetAllInstances()
        {
            var list = new SessionsNetworkEntity[instancesById.Count];
            instancesById.Values.CopyTo(list, 0);
            return list;
        }

        /// <summary>
        /// Gets all current network entity instances that belong to the current agent.
        /// </summary>
        /// <returns>An array with the list of current network entity instances.</returns>
        public IList<SessionsNetworkEntity> GetMyInstances()
        {
            var list = new List<SessionsNetworkEntity>();
            var agentId = SessionsNetworking.AgentId;

            foreach (var pair in instancesById)
            {
                if (pair.Value.Owner.Id == agentId && pair.Value.IsMine)
                    list.Add(pair.Value);
            }

            return list;
        }

        /// <summary>
        /// Gets all current network entity instances that do not belong to the current agent.
        /// </summary>
        /// <returns>An array with the list of current network entity instances.</returns>
        public IList<SessionsNetworkEntity> GetOthersInstances()
        {
            var list = new List<SessionsNetworkEntity>();
            var agentId = SessionsNetworking.AgentId;

            foreach (var pair in instancesById)
            {
                if (pair.Value.Owner.Id != agentId)
                    list.Add(pair.Value);
            }

            return list;
        }

        /// <summary>
        /// Gets all current network entity instances that do not belong to the current agent.
        /// </summary>
        /// <param name="agentId">The agent ID of the owner to get instances for.</param>
        /// <returns>An array with the list of current network entity instances.</returns>
        public IList<SessionsNetworkEntity> GetInstancesByOwner(Guid agentId)
        {
            var list = new List<SessionsNetworkEntity>();

            foreach (var pair in instancesById)
            {
                if (pair.Value.Owner.Id == agentId)
                    list.Add(pair.Value);
            }

            return list;
        }

        #endregion Get

        #region Create

        /// <summary>
        /// Creates a new network entity instance.
        /// </summary>
        /// <param name="name">The registered network name of the entity to create an instance of.</param>
        /// <param name="id">The instance ID to assign.</param>
        /// <param name="parentId">The ID of the parent entity instance, if any (0 = none).</param>
        /// <param name="owner">The owner of the instance.</param>
        /// <param name="localOnly">Whether or not to only create the instannce locally and not across connected agents.</param>
        /// <param name="position">The position of the instance.</param>
        /// <param name="rotation">The rotation of the instance.</param>
        /// <param name="scale">The scale of the instance.</param>
        public void CreateNetworkInstance(string name, SessionAgent owner, long id, long parentId, bool localOnly = false, 
            Vector3? position = null, Vector3? rotation = null, Vector3? scale = null)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");
            if (!entitiesByName.ContainsKey(name)) throw new ArgumentException("No registered network entities found by name: " + name);
            if (owner == null) throw new ArgumentNullException("owner");

            // Get the entity info
            var entityInfo = entitiesByName[name];
            SessionsNetworkEntity instance = null;

            // First check to see if any unclaimed/waiting network instances of the right type are available to bind to
            if (instancesByName.ContainsKey(name))
            {
                var selfId = SessionsNetworking.AgentId;

                foreach (var i in instancesByName[name])
                {
                    // If we currently have the owner ID for creating this instance, but it is not ours, it is available for binding...
                    if (i.Owner.Id == selfId && !i.IsMine)
                    {
                        CyLog.LogInfoFormat("Found available instance for binding {0} {1}:{2}", name, owner.Id, owner.Name);

                        // Bind the current instance reference to the available instance and take ownership of it
                        instance = i;
                        instancesById.Remove(instance.Id); // remove by old ID
                        instance.Id = id; // update to new ID
                        instancesById.Add(instance.Id, instance);
                        instance.Owner = owner; // take new ownership
                        StartCoroutine(DoAttachToParent(instance, parentId));
                        return;
                    }
                }
            }

            // Ensure we have not hit the maximum instance count
            if (instancesByName.ContainsKey(name) && instancesByName[name].Count >= entityInfo.MaxInstances)
            {
                CyLog.LogErrorFormat("Maximum amount of network instances already created for entity: {0}", name);
                return;
            }

            // Try to select a non-VR prefab if available if this is not VR, but if only the main prefab exists, use that
            var prefab = !SessionsUdpNetworking.IsXR && entityInfo.NonXrPrefab != null ? entityInfo.NonXrPrefab : entityInfo.Prefab;

            // Create the instance
            var pos = position != null ? position.Value : Vector3.zero;
            var rot = Quaternion.identity;
            if (rotation != null) rot.eulerAngles = rotation.Value;
            var scl = scale != null ? scale.Value : Vector3.one;
            var gObj = Instantiate(prefab, pos, rot);
            gObj.transform.localScale = scl;
            instance = gObj.GetComponentInChildren<SessionsNetworkEntity>();

            // Add to the instance parent container if configured
            if (InstanceContainer != null) gObj.transform.SetParent(InstanceContainer, true);

            // local instances that were created remotely should provide an ID, otherwise we will provide one
            if (localOnly) instance.Id = id;

            // Register the instance
            RegisterNetworkInstance(entityInfo.Name, instance, owner);

            // Enable positional voice tracking if this is an agent with positional tracking enabled
            var voiceTracking = instance.GetComponentInChildren<SessionsPositionalVoice>();
            if (voiceTracking != null) voiceTracking.StartTracking();

            // Broadcast the creation of this instance to the network
            if (!localOnly)
            {
                foreach (var agent in SessionsNetworking.GetAllAgents())
                {
                    // Ignore self
                    if (agent.Id == SessionsNetworking.AgentId) continue;

                    // Send the create message
                    var msg = GetCreateMessageForInstance(instance);
                    SessionsNetworking.SendToAgent(agent, msg, SendOptions.ReliableUnordered);
                }
            }

            // Attach to parent if specified
            StartCoroutine(DoAttachToParent(instance, parentId));
        }

        // Returns a network instance creation message for the supplied instance
        private JsonArgsSessionMessage GetCreateMessageForInstance(SessionsNetworkEntity instance, MessageFlags? flags = MessageFlags.None)
        {
            var pos = instance.transform.position;
            var rot = instance.transform.eulerAngles;
            var scl = instance.transform.localScale;
            if (flags == null) flags = MessageFlags.None;

            // Send a create instance message
            var parentId = instance.ParentEntity != null ? instance.ParentEntity.Id : (long)0;
            var jsonArgs = string.Format("{{\"name\":\"{0}\",\"id\":{1},\"parentId\":{2},\"pos\":[{3},{4},{5}],\"rot\":[{6},{7},{8}],\"scl\":[{9},{10},{11}]}}",
                instance.EntityInfo.Name, instance.Id, parentId, pos.x, pos.y, pos.z, rot.x, rot.y, rot.z, scl.x, scl.y, scl.z);

            return new JsonArgsSessionMessage(MessageTypes.Entity, flags.Value, "Create", jsonArgs);
        }

        // Groups a instance with its parent instance if it has one
        private IEnumerator DoAttachToParent(SessionsNetworkEntity instance, long parentId)
        {
            // No parent...
            if (parentId == 0) yield break;

            // Otherwise attempt to find the parent by ID
            SessionsNetworkEntity parent = null;

            // Wait for parent to register
            while (parent == null)
            {
                parent = GetInstanceById(parentId);
                yield return 0;
            }

            // If the parent does not currently have a parent, create one to group all instances under
            if (parent.transform.parent == null || parent.transform.parent == InstanceContainer)
            {
                var root = new GameObject();
                if (InstanceContainer != null) root.transform.SetParent(InstanceContainer);
                root.name = "[root]-" + parent.EntityInfo.Name + "-" + parent.Id.ToString();
                root.transform.position = parent.transform.position;
                parent.transform.SetParent(root.transform);
                root.transform.localPosition = Vector3.zero;
            }

            // Set parent to the parent instance's "root group"
            instance.transform.SetParent(parent.transform.parent);
        }

        #endregion Create

        #region Destroy

        /// <summary>
        /// Destroys an existing, registered network entity instance.
        /// </summary>
        /// <param name="instance">The instance to destroy.</param>
        /// <param name="localOnly">Whether or not to only destroy it locally or across the network.</param>
        public void DestroyNetworkInstance(SessionsNetworkEntity instance, bool localOnly = false)
        {
            if (instance == null) throw new ArgumentNullException("instance");
            if (!instance.IsRegisteredWithNetwork) throw new InvalidOperationException("Network Entity instance is not registered with the network");

            // Unregister first
            UnregisterNetworkInstance(instance);

            // If this entity has a parent/root group transform/game object, destroy that too
            if (instance.transform.parent.name.StartsWith("[root]"))
            {
                // Destroy group
                Destroy(instance.transform.parent.gameObject);
            }
            else
            {
                // Destroy
                Destroy(instance.gameObject);
            }

            // Broadcast to the network
            if (!localOnly)
            {
                // Send the create message
                var msg = GetDestroyMessageForInstance(instance);

                foreach (var agent in SessionsNetworking.GetAllAgents())
                {
                    // Ignore self
                    if (agent.Id == SessionsNetworking.AgentId) continue;
                    SessionsNetworking.SendToAgent(agent, msg, SendOptions.ReliableUnordered);
                }
            }
        }

        // Returns a network instance creation message for the supplied instance
        private JsonArgsSessionMessage GetDestroyMessageForInstance(SessionsNetworkEntity instance)
        {
            // Send a create instance message
            var jsonArgs = string.Format("{{\"name\":\"{0}\",\"id\":{1}}}", instance.EntityInfo.Name, instance.Id);
            return new JsonArgsSessionMessage(MessageTypes.Entity, MessageFlags.None, "Destroy", jsonArgs);
        }

        /// <summary>
        /// Destroys all network instances for a given agent.
        /// </summary>
        /// <param name="agent">The agent to destroy all network instances for.</param>
        public void DestroyAllInstancesByAgent(SessionAgent agent, bool localOnly = true)
        {
            if (agent == null) throw new ArgumentNullException("agent");

            var agentInstances = new List<SessionsNetworkEntity>();

            foreach (var pair in instancesById)
            {
                // Find all instances owned by the agent
                if (pair.Value.Owner.Id == agent.Id && !pair.Value.IsMine)
                {
                    agentInstances.Add(pair.Value);
                }
            }

            // Go through and destroy each instance
            foreach (var instance in agentInstances)
                DestroyNetworkInstance(instance, localOnly);
        }

        #endregion Destroy

        #endregion Instances

        #region Transforms

        // Handles updates of network transforms
        private void HandleNetworkTransformUpdated(TransformMessage msg)
        {
            // Get the instance by its ID
            var instance = GetInstanceById(msg.NetworkEntityId);

            if (instance == null)
            {
                CyLog.LogWarnFormat("Cannot update network transform of missing instance ID {0}", msg.NetworkEntityId);
                return;
            }

            var netTrans = instance.GetNetworkTransform(msg.Name); // we use the message name as the network name of the tranform target

            // TODO Log/Handle?
            if (netTrans == null) return;

            // Update the transform from the new message values.
            netTrans.BindToTransformMessage(msg);
        }

        #endregion Transforms

        #region States

        // Handles updates of network transforms
        private void HandleNetworkStateChanged(StateMessage msg)
        {
            //CyLog.LogInfoFormat("Getting network state entity by ID {0}...", msg.NetworkEntityId);

            // Get the instance by its ID
            var instance = GetInstanceById(msg.NetworkEntityId);

            if (instance == null)
            {
                CyLog.LogWarnFormat("Cannot update network state machine of missing instance ID {0}", msg.NetworkEntityId);
                return;
            }

            //CyLog.LogInfoFormat("Getting the network state machine for network name {0}...", msg.NetworkName);
            var netState = instance.GetNetworkStateMachine(msg.NetworkName); // we use the message name as the network name of the target

            // TODO Log/Handle?
            if (netState == null) return;

            // Update the transform from the new message values.
            //CyLog.LogInfoFormat("Binding to received network state change {0} @ {1}!", msg.StateName, msg.Value);
            netState.BindToStateMessage(msg);
        }

        #endregion States

        #region RPC

        // Handles network entity RPC command messages
        private void HandleRpcCommand(RpcMessage msg)
        {
            // Get the instance by its ID
            var instance = GetInstanceById(msg.NetworkEntityId);

            if (instance == null)
            {
                CyLog.LogWarnFormat("Cannot call RPC of missing instance ID {0}", msg.NetworkEntityId);
                return;
            }

            // Call the RPC on the instance and pass in the arguments from the message
            instance.CallRpc(msg.Owner.Id, msg.Name, true, msg.Args, msg.Value);
        }

        #endregion RPC

        #region Agents

        // Handle the connection of peer
        private void HandleAgentConnected(SessionAgent agent)
        {
            // Synchronize all network entity instances with the connecting agent.
            foreach (var instance in GetMyInstances())
            {
                // Send the create message
                var msg = GetCreateMessageForInstance(instance, MessageFlags.Request);

                // Create a completion handler that will synchronize all of the instances states
                msg.CompleteHandler = (response) =>
                {
                    // If this network instance has any network states, capture them
                    var netStates = instance.GetAllNetworkStates();

                    foreach (var netState in netStates)
                    {
                        var owner = netState.NetworkEntity.Owner;

                        // Get all of the presently active states
                        foreach (var state in netState.States.GetAllActiveStates())
                        {
                            // Send a state message of all presently active network states to enter the active states at the current times
                            //CyLog.LogInfoFormat("SYNC STATES OF {0}:{1}:{2}:{3} @ {4}", owner.Name, netState.NetworkEntity.Id, netState.NetworkName, state.Name, state.Time);
                            var stateMsg = new StateMessage(MessageFlags.None, "StateEnter", state.Time, null, netState.NetworkName, state.Name);
                            stateMsg.NetworkEntityId = netState.NetworkEntity.Id; // important: assign the network entity instance ID
                            SessionsNetworking.SendToAgent(agent, stateMsg, SendOptions.ReliableOrdered);
                        }
                    }
                };

                SessionsNetworking.SendToAgent(agent, msg, SendOptions.ReliableOrdered);
            }
        }

        // Handle the disconnection of peer
        private void HandleAgentDisconnected(SessionAgent agent)
        {
            if (agent == null) return;

            // Destroy all instances locally that belonged to the agent that disconnected
            foreach (var instance in GetInstancesByOwner(agent.Id))
            {
                DestroyNetworkInstance(instance, true); // only destroy locally
            }
        }

        // Handles the creation of a network instance as requested by a remote agent
        private void HandleNetworkInstaceCreated(JsonArgsSessionMessage msg)
        {
            // Get the network entity name to create
            var name = msg.GetArgs<string>("name");

            // Get the instance ID
            var id = msg.GetArgs<long>("id");

            // Get the parent ID
            var parentId = msg.GetArgs<long>("parentId");

            // Get the position
            var rawPos = msg.GetArgs<JArray>("pos");
            Vector3? pos = null;
            if (rawPos != null) pos = new Vector3() { x = rawPos[0].Value<float>(), y = rawPos[1].Value<float>(), z = rawPos[2].Value<float>() };

            // Get the rotation
            var rawRot = msg.GetArgs<JArray>("rot");
            Vector3? rot = null;
            if (rawRot != null) rot = new Vector3() { x = rawRot[0].Value<float>(), y = rawRot[1].Value<float>(), z = rawRot[2].Value<float>() };

            // Get the scale
            var rawScl = msg.GetArgs<JArray>("scl");
            Vector3? scl = null;
            if (rawScl != null) scl = new Vector3() { x = rawScl[0].Value<float>(), y = rawScl[1].Value<float>(), z = rawScl[2].Value<float>() };

            // Create the instance locally
            CreateNetworkInstance(name, msg.Owner, id, parentId, true, pos, rot, scl);
        }

        #endregion Agents

        #region Session

        // Handles the successful hosting of a new session
        private void HandleSessionHosted(string name, string info)
        {
            // Go through all current instances and if they don't yet belong to the current agent as host, assign them full ownership
            var agentId = SessionsNetworking.AgentId;
            var self = SessionsNetworking.Self;
            var total = 0;

            foreach (var instance in instancesById.Values.ToArray())
            {
                if (instance.Owner == null || (instance.Owner.Id == agentId && !instance.IsMine))
                {
                    instance.Owner = self;
                    instance.IsMine = true;
                    total++;
                }
            }

            CyLog.LogInfoFormat("{0} instance(s) inherited ownership from scene as host", total);
        }

        #endregion Session

        #endregion Methods
    }

    /// <summary>
    /// Class used to store data about player spawn points.
    /// </summary>
    [Serializable]
    public class SpawnPoint
    {
        /// <summary>
        /// The name of the spawn point.
        /// </summary>
        [Tooltip("The name of the spawn point.")]
        public string Name = "Spawn1";

        /// <summary>
        /// The spawn position.
        /// </summary>
        [Tooltip("The spawn position.")]
        public Vector3 Position;

        /// <summary>
        /// The spawn orientation.
        /// </summary>
        [Tooltip("The spawn orientation.")]
        public Vector3 Rotation;
    }
}
