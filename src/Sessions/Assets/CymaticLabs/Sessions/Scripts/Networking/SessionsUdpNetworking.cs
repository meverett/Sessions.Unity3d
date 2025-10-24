using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using CymaticLabs.Sessions.Core;
using CymaticLabs.Logging;
using CymaticLabs.Protocols.Osc;
using CymaticLabs.Protocols.Osc.Unity3d;
using Newtonsoft.Json.Linq;
using LiteNetLib;
using LiteNetLib.Utils;
//using Open.Nat;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Class used to manage the network-oriented aspect of sessions.
    /// </summary>
    public class SessionsUdpNetworking : MonoBehaviour
    {
        #region Constants

        /// <summary>
        /// The maximum number of active connections the network layer will accept.
        /// </summary>
        public const int MAX_CONNECTIONS = 9;

        /// <summary>
        /// The default port for unicast communications.
        /// </summary>
        public const int DEFAULT_UNICAST_PORT = 9000;

        /// <summary>
        /// The default port for facilitator service communications.
        /// </summary>
        public const int DEFAULT_FACILITATOR_PORT = 9009;

        /// <summary>
        /// The default remote logging port of the remote logging host.
        /// </summary>
        public const int DEFAULT_REMOTE_LOGGING_PORT = 9008;

        /// <summary>
        /// The default port for voice communications.
        /// </summary>
        public const int DEFAULT_VOICE_PORT = 9000;

        /// <summary>
        /// The keep-alive message loop timeout threshold in seconds
        /// </summary>
        public const int KEEP_ALIVE_TIMEOUT = 30;

        /// <summary>
        /// The keep-alive message send interval in seconds.
        /// </summary>
        public const int KEEP_ALIVE_INTERVAL = 10;

        /// <summary>
        /// The special lifetime value for UPnP that signifies a auto-renewed/auto-released session
        /// versus a manual timeout (any value between 1 and int.MavValue - 1) or infinite (value of 0).
        /// </summary>
        internal const int UPNP_SESSION = int.MaxValue;

        #endregion Constants

        #region Inspector

        #region References

        /// <summary>
        /// The sessions manager.
        /// </summary>
        [Header("References")]
        [Tooltip("The sessions manager reference to coordinate with.")]
        public SessionsSceneManager Manager;

        /// <summary>
        /// The OSC value controller that will listen for and map OSC control values.
        /// </summary>
        [Tooltip("The OSC value controller reference to use to enable OSC control.")]
        public SessionsOscValueController OscController;

        #endregion References

        #region Network Configuration

        /// <summary>
        /// The port number to use for unicast UDP messages.
        /// </summary>
        [Header("Network Configuration")]
        [Tooltip("The unicast port to use for the network transport layer.")]
        [Range(0, 65535)]
        public int UnicastPort = DEFAULT_UNICAST_PORT;

        /// <summary>
        /// The interval between network updates in milliseconds.
        /// </summary>
        [Range(10, 100)]
        [Tooltip("The interval between network updates in milliseconds.")]
        public int NetworkUpdateInterval = 15;

        /// <summary>
        /// Whether or not to attempt to use UPnP to do a session-based port-forwarding of this client.
        /// </summary>
        //[Tooltip("Whether or not to attempt to use UPnP to do a session-based port-forwarding of this client.")]
        //public bool UseUPNP = true;

        /// <summary>
        /// Whether or not to force the connected to always relay through the Facilitator instead of peer-to-peer. Mostly useful for testing.
        /// </summary>
        [Tooltip("Whether or not to force the connected to always relay through the Facilitator instead of peer-to-peer. Mostly useful for testing.")]
        public bool ForceRelay = false;

        /// <summary>
        /// The address of the Facilitator service to connect to.
        /// </summary>
        [Tooltip("The address of the Facilitator service to connect to.")]
        public string FacilitatorAddress;

        /// <summary>
        /// The port of the Facilitator service to connect to.
        /// </summary>
        [Tooltip("The port of the Facilitator service to connect to.")]
        [Range(0, 65535)]
        public int FacilitatorPort = DEFAULT_FACILITATOR_PORT;

        #endregion Network Configuration

        #region Behavior

        /// <summary>
        /// Whether or not to start networking on scene start.
        /// </summary>
        [Header("Behavior")]
        [Tooltip("Whether or not to automatically initialize networking on start.")]
        public bool StartAutomatically = true;

        /// <summary>
        /// "When running in the Editor port numbers will be offset by +1. Useful for running a client/host on the same machine."
        /// </summary>
        [Tooltip("When running in the Editor port numbers will be offset by +1. Useful for running a client/host on the same machine.")]
        public bool OffsetPortsOnEditor = true;

        #endregion Behavior

        #region Voice

        /// <summary>
        /// Whether or not to use voice chat.
        /// </summary>
        [Header("Voice")]
        [Tooltip("Whether or not to enable voice chat during sessions.")]
        public bool UseVoice = true;

        /// <summary>
        /// The voice chat prefab instance script for VOIP communication.
        /// </summary>
        [Tooltip("The voice chat reference to use to enable session VOIP.")]
        public SessionsVoiceChat VoiceChat;

        #endregion Voice

        #region Debugging

        /// <summary>
        /// The sets the logging level for what types of log events will be logged and output.
        /// </summary>
        [Header("Debugging")]
        [Tooltip("The sets the logging level for what types of log events will be logged and output.")]
        public LogLevels LogLevel = LogLevels.Info;

        /// <summary>
        /// Whether or not to enable remote debugging where debug logs will be sent over TCP to a remote logging host.
        /// </summary>
        [Tooltip("Whether or not to enable remote debugging where debug logs will be sent over TCP to a remote logging host.")]
        public bool EnableRemoteDebugging = false;

        /// <summary>
        /// When remote logging is enabled, the address/IP of the remote logging host to connect to.
        /// </summary>
        [Tooltip("When remote logging is enabled, the address/IP of the remote logging host to connect to.")]
        public string RemoteLoggingHost;

        /// <summary>
        /// When remote logging is enabled, the port of the remote logging host to connect to.
        /// </summary>
        [Tooltip("When remote logging is enabled, the port of the remote logging host to connect to.")]
        [Range(0, 65535)]
        public int RemoteLoggingPort = DEFAULT_REMOTE_LOGGING_PORT;

        /// <summary>
        /// The UI text to use for debugging.
        /// </summary>
        [Tooltip("Optional Text component to use to debug output to the UI.")]
        public UnityEngine.UI.Text DebugText;

        #endregion Debugging

        #region Network Events

        /// <summary>
        /// Occurs when the networking layer has initialized and is operational.
        /// </summary>
        [Header("Network Events")]
        public UnityEvent OnNetworkingStarted;

        /// <summary>
        /// Occurs when the networking layer has initialized and is operational.
        /// </summary>
        public UnityEvent OnNetworkingStopped;

        #endregion Network Events

        #region Session Events

        /// <summary>
        /// Occurs when a value has been updated via a network agent.
        /// </summary>
        [Header("Session Events")]
        public SessionValueEvent OnSessionValueChanged;

        /// <summary>
        /// Occurs when a new session is created as the host.
        /// </summary>
        public SessionStatusEvent OnSessionHosted;

        /// <summary>
        /// Occurs when an error results from trying to host a new session.
        /// </summary>
        public SessionStatusEvent OnSessionHostError;

        /// <summary>
        /// Occurs when joining an existing session.
        /// </summary>
        public SessionStatusEvent OnSessionJoined;

        /// <summary>
        /// Occurs when an error results from trying to join an existing session.
        /// </summary>
        public SessionStatusEvent OnSessionJoinedError;

        /// <summary>
        /// Occurs when a new session is hosted or joined.
        /// </summary>
        public SessionStatusEvent OnSessionStarted;

        /// <summary>
        /// Occurs for errors during either hosted or joined sessions.
        /// </summary>
        public SessionStatusEvent OnSessionError;

        /// <summary>
        /// Occurs when the current session ends.
        /// </summary>
        public SessionStatusEvent OnSessionEnded;

        #endregion Session Events

        #region Agent Events

        /// <summary>
        /// Occurs when an connected to a remote agent.
        /// </summary>
        [Header("Agent Events")]
        public SessionAgentEvent OnAgentConnected;

        /// <summary>
        /// Occurs when an disconnected from a remote agent.
        /// </summary>
        public SessionAgentEvent OnAgentDisconnected;

        /// <summary>
        /// Occurs when a connection attempt fails with the target agent not found by ID on the Facilitator.
        /// </summary>
        public SessionAgentNotFoundEvent OnAgentNotFound;

        #endregion Agent Events

        #region Entity Events

        /// <summary>
        /// Occurs when a new network instance has been created from a remote peer.
        /// </summary>
        [Header("Entity Events")]
        public NetworkInstanceCreatedEvent OnNetworkInstanceCreated;

        /// <summary>
        /// Occurs when a new network tranform update message was received.
        /// </summary>
        public NetworkTransformEvent OnNetworkTransformReceived;

        /// <summary>
        /// Occurs when the network state of a network instance has been entered.
        /// </summary>
        public NetworkStateChangeEvent OnNetworkStateEntered;

        /// <summary>
        /// Occurs when the network state of a network instance has been exited.
        /// </summary>
        public NetworkStateChangeEvent OnNetworkStateExited;

        /// <summary>
        /// Occurs when an entity RPC command is received/executed.
        /// </summary>
        public NetworkRpcCommandEvent OnRpcCommandExecuted;

        #endregion Entity Events

        #region Facilitator Events

        /// <summary>
        /// Occurs when this agent successfully registers with the facilitator.
        /// </summary>
        [Header("Facilitator Events")]
        public FacilitatorEvent OnConnectedToFacilitator;

        /// <summary>
        /// Occurs when this agent disconnects or unregisters from the facilitator.
        /// </summary>
        public FacilitatorEvent OnDisconnectedFromFacilitator;

        /// <summary>
        /// Occurs after a successful registration with the Facilitator.
        /// </summary>
        public FacilitatorEvent OnRegisteredWithFaciliator;

        #endregion Facilitator Events

        #endregion Inspector

        #region Fields

        // Represents the Facilitator service
        private SessionFacilitator facilitator;

        // The local IP address
        private IPAddress localIP;

        // The current agent's information
        private SessionAgent self;

        // Whether or not this client is running as host
        private bool isHost = false;

        // The underlying UDP network transport listener
        private EventBasedNetListener listener;

        // The underlying UDP network transport manager
        private NetManager netManager;

        // Used for NAT UPnP
        //private NatDiscoverer natDiscoverer;
        //private NatDevice natDevice;

        // The message queue lock
        private object queueLock = new object();

        // A queue for received messages
        private Queue<SessionMessage> msgQueue;

        // A queue for messages waiting for a response
        private Dictionary<string, SessionMessage> requestMsgQueue;

        // A list of queued actions to execute (typically queued in another thread)
        private ConcurrentBag<Action> actionQueue;

        // A list of known registered agents indexed by their unique ID
        private Dictionary<Guid, SessionAgent> agentsById;

        // A list of known registered agents indexed by pivate host:port combo
        private Dictionary<string, SessionAgent> agentsByPrivateDataKey;

        // A list of known registered agents indexed by public host:port combo
        private Dictionary<string, SessionAgent> agentsByPublicDataKey;

        // List of unique packet order IDs used for sequencing reliable packets, per agent
        private Dictionary<string, ushort> txPacketIdsByAgent;

        // A list of agents who's we are currently attempting to facilitat a connection with
        private Dictionary<Guid, SessionAgent> connectingAgentsById;

        // A list of agent connection attempts by the agent ID
        private Dictionary<Guid, NatConnectionAttempt> connectionAttemptsByAgentId;

        // A list of RPC commands by their RPC name
        private Dictionary<string, RpcCommand> rpcCommands;

        // Used for message processing hooks
        //private SessionMessageProcessor sortPreProcessor;
        //private SessionMessageProcessor sortPostProcessor;
        //private SessionMessageProcessor transientPreProcessor;
        //private SessionMessageProcessor transientPostProcessor;
        //private SessionMessageProcessor requestPreProcessor;
        //private SessionMessageProcessor requestPostProcessor;
        //private SessionMessageProcessor responsePreProcessor;
        //private SessionMessageProcessor responsePostProcessor;

        private string logFilePath;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static SessionsUdpNetworking Current { get; private set; }

        /// <summary>
        /// Whether or not the VR/XR is enabled.
        /// </summary>
        public static bool IsXR { get { return UnityEngine.XR.XRSettings.enabled; } }

        /// <summary>
        /// The facilitator service keep alive timeout.
        /// </summary>
        public static TimeSpan FacilitatorTimeout = new TimeSpan(0, 0, KEEP_ALIVE_TIMEOUT);

        /// <summary>
        /// The facilitator service keep alive message interval.
        /// </summary>
        public static TimeSpan FacilitatorKeepAliveInterval = new TimeSpan(0, 0, KEEP_ALIVE_INTERVAL);

        /// <summary>
        /// Whether or not the networking component of the session is currently started.
        /// </summary>
        public bool IsStarted { get; private set; }

        /// <summary>
        /// Whether or not the current network session is operating in host mode.
        /// </summary>
        public bool IsHost
        {
            get { return isHost; }
            
            private set
            {
                isHost = value;
                if (self != null) self.IsHost = value;
            }
        }

        /// <summary>
        /// Whether or not the client is currently in a network session.
        /// </summary>
        public bool IsInSession { get; private set; }

        /// <summary>
        /// Whether or not this agent is currently registered with the facilitator.
        /// </summary>
        public bool IsRegistered { get { return facilitator != null && facilitator.IsRegistered; } }

        /// <summary>
        /// The IP end point that unicast UDP listening is occuring on, if any.
        /// </summary>
        public IPEndPoint LocalUnicastEP { get; protected set; }

        /// <summary>
        /// Gets the current local/LAN/private IP address.
        /// </summary>
        public IPAddress LocalIP { get { return localIP; } }

        /// <summary>
        /// Whether or not UPnP was successfully configured on the local NAT/router.
        /// </summary>
        public bool IsUPnPConfigured { get; private set; }

        /// <summary>
        /// Whether or not the the underlying UDP client is ready to receive data.
        /// </summary>
        public bool IsReceiving { get; private set; }

        /// <summary>
        /// The unique agent ID of this agent given by the facilitator service upon agent registration.
        /// </summary>
        public Guid AgentId { get; private set; }

        /// <summary>
        /// A cached version of the current agent's information.
        /// </summary>
        public SessionAgent Self
        {
            get
            {
                if (self == null) self = GetSelfAsAgent();

                // Ensure references are up to date
                if (self.Id != AgentId && AgentId != Guid.Empty) self.Id = AgentId;
                if (self.PrivateIP == null) self.PrivateIP = localIP;
                if (self.PublicIP == null) self.PublicIP = localIP;
                if (self.ConnectedIP == null) self.ConnectedIP = localIP;

                return self;
            }
        }

        /// <summary>
        /// The agent that is the voice host for the session.
        /// </summary>
        public SessionAgent VoiceHost { get; internal set; }

        /// <summary>
        /// The current time as the network process sees it.
        /// </summary>
        public DateTime Now { get; private set; }

        /// <summary>
        /// Whether or not the voice port is currently being registered with the facilitator service.
        /// </summary>
        public bool IsRegisteringVoice { get; private set; }

        /// <summary>
        /// Whether or not the voice port is currently registered with the facilitator service for this agent.
        /// </summary>
        public bool IsVoiceRegistered { get; private set; }

        /// <summary>
        /// The name of the current session, if any.
        /// </summary>
        public string SessionName { get; private set; }

        /// <summary>
        /// Whether or not there has been any message sending activity this frame.
        /// </summary>
        public bool SendActivity { get; private set; }

        /// <summary>
        /// The IP:port combination to which the last message was sent (if any).
        /// </summary>
        public string LastSendEndPoint { get; private set; }

        /// <summary>
        /// The last sent message, if any.
        /// </summary>
        public SessionMessage LastSentMessage { get; private set; }

        /// <summary>
        /// Whether or not there has been any message receiving activity this frame.
        /// </summary>
        public bool ReceiveActivity { get; private set; }

        /// <summary>
        /// The IP:port combination from which the last message was received (if any).
        /// </summary>
        public string LastReceiveEndPoint { get; private set; }

        /// <summary>
        /// The last received message, if any.
        /// </summary>
        public SessionMessage LastReceivedMessage { get; private set; }

        /// <summary>
        /// Gets the total number of messages sent.
        /// </summary>
        public ulong TotalMessagesSent { get; private set; }

        /// <summary>
        /// Gets the total number of messages received.
        /// </summary>
        public ulong TotalMessagesReceived { get; private set; }

        /// <summary>
        /// Gets the total number of messages dequeued and processed.
        /// </summary>
        public ulong TotalMessagesDequeued { get; private set; }

        /// <summary>
        /// The total number of transient messages received.
        /// </summary>
        public ulong TotalTransientsReceived { get; private set; }

        /// <summary>
        /// The total number of message requests received.
        /// </summary>
        public ulong TotalRequestsReceived { get; private set; }

        /// <summary>
        /// The total number of message requests processed.
        /// </summary>
        public ulong TotalRequestsProcessed { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        #region Awake

        private void Awake()
        {
            IsRegisteringVoice = false;
            IsVoiceRegistered = false;
            Now = DateTime.Now;
            Current = this;
            AgentId = Guid.Empty;
            msgQueue = new Queue<SessionMessage>();
            requestMsgQueue = new Dictionary<string, SessionMessage>();
            actionQueue = new ConcurrentBag<Action>();
            agentsById = new Dictionary<Guid, SessionAgent>();
            agentsByPrivateDataKey = new Dictionary<string, SessionAgent>();
            agentsByPublicDataKey = new Dictionary<string, SessionAgent>();
            txPacketIdsByAgent = new Dictionary<string, ushort>();
            connectingAgentsById = new Dictionary<Guid, SessionAgent>();
            connectionAttemptsByAgentId = new Dictionary<Guid, NatConnectionAttempt>();
            rpcCommands = new Dictionary<string, RpcCommand>();
            SendActivity = false;
            ReceiveActivity = false;

            // Configure logging
            CyLog.LogLevel = LogLevel;
            CyLog.OnRefreshed += CyLog_OnRefreshed;
            CyLog.OnEventLogged += CyLog_OnEventLogged;
            //CyLog.UseUtc = true;

            // If remote logging is enabled, connect to the remote logging host
            if (EnableRemoteDebugging)
            {
                if (string.IsNullOrEmpty(RemoteLoggingHost))
                {
                    CyLog.LogWarn("Remote debug logging is enabled but no remote logging host address was provided.");
                }
                else
                {
                    try
                    {
                        // Get the remote logging host's IP
                        IPAddress address = null;

                        if (!IPAddress.TryParse(RemoteLoggingHost, out address))
                            address = Dns.GetHostAddresses(RemoteLoggingHost)[0];

                        CyLog.EnableRemoteLogging(new IPEndPoint(address, RemoteLoggingPort));
                    }
                    catch (Exception ex)
                    {
                        CyLog.LogError(ex);
                    }
                }
            }

            // Apply any sort of client port adjustment
            if (OffsetPortsOnEditor && Application.isEditor)
            {
                UnicastPort++; // avoid port conflicts if on same machine
                if (VoiceChat != null) VoiceChat.Port++;
            }

            // Turn on V-Sync when not on a mobile build
            if (Application.platform != RuntimePlatform.Android || Application.isEditor || Application.platform == RuntimePlatform.WindowsPlayer)
            {
                CyLog.LogInfo("V-Sync: On");
                QualitySettings.vSyncCount = 1;
            }

            // Create a representation to manage interaction with the Facilitator service
            facilitator = new SessionFacilitator(!string.IsNullOrEmpty(FacilitatorAddress) ? FacilitatorAddress : null, FacilitatorPort);

            // Register to Facilitator state change events
            facilitator.States.OnStateEntered += Facilitator_OnStateEntered;
            facilitator.States.OnStateExited += Facilitator_OnStateExited;

            // Register for proper VR shutdown
            if (IsXR)
            {
                
            }
        }

        #endregion Awake

        #region Start

        private void Start()
        {
            // Attempt to autodetect voice chat component
            if (VoiceChat == null) GetComponent<SessionsVoiceChat>();
            if (Manager == null) GetComponent<SessionsSceneManager>();

            // Automatically start networking if configured...
            if (StartAutomatically) StartNetworking();
        }

        #endregion Start

        #endregion Init

        #region Clean Up

        /// <summary>
        /// Cleans up the networking for shutdown.
        /// </summary>
        public void CleanUp()
        {
            EndSession();
            StopReceiving();
            StopNetworking();
        }

        private void OnDestroy()
        {
            CleanUp();
        }

        private void OnApplicationQuit()
        {
            CleanUp();
        }

        #endregion Clean Up

        #region Update

        private void Update()
        {
            // Set log level
            CyLog.LogLevel = LogLevel;

            // Process queues
            ProcessQueue();

            // Update the Facilitator state machine
            facilitator.States.Update(Time.time);

            // Update agent state machines
            foreach (var pair in agentsById) pair.Value.States.Update(Time.time);
        }

        #endregion Update

        #region Operation

        /// <summary>
        /// Creates a UDP client and starts listening for messages on the configured port.
        /// </summary>
        public void StartNetworking()
        {
            if (IsStarted) return;

            CyLog.LogInfo("Sessions networking starting...");

            // Close any current network layer
            if (netManager != null) netManager.Stop();

            // Set local listening end point
            LocalUnicastEP = new IPEndPoint(IPAddress.Any, UnicastPort);

            #region Setup Net Manager

            // Create the UDP network transport layer
            listener = new EventBasedNetListener();
            netManager = new NetManager(listener, MAX_CONNECTIONS, "Sessions");
            netManager.UpdateTime = NetworkUpdateInterval;
            netManager.MergeEnabled = true;
            listener.NetworkErrorEvent += Listener_NetworkErrorEvent;
            listener.NetworkLatencyUpdateEvent += Listener_NetworkLatencyUpdateEvent;
            listener.NetworkReceiveEvent += Listener_NetworkReceiveEvent;
            listener.NetworkReceiveUnconnectedEvent += Listener_NetworkReceiveUnconnectedEvent;
            listener.PeerConnectedEvent += Listener_PeerConnectedEvent;
            listener.PeerDisconnectedEvent += Listener_PeerDisconnectedEvent;
            netManager.UnconnectedMessagesEnabled = true; // needed for NAT punch-through
            netManager.Start(UnicastPort);

            #endregion Setup Net Manager

            // Start receiving messages
            StartReceiving();

            // Start OSC services
            if (OscController != null)
            {
                if (OffsetPortsOnEditor && Application.isEditor) OscController.UdpPort++; // TODO Remove this?
                OscController.StartOscReceiver();
            }

            IsStarted = true;

            CyLog.LogInfoFormat("Sessions networking started on {0}", LocalUnicastEP);

            #region Assign Local IP

            // Autodetect best local IP address
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    CyLog.LogInfoFormat("Initial local IP selected: {0}", ip);
                    localIP = ip;
                    break;
                }
            }

            // Now pass through network interfaces and ensure the selected IP is connected/enabled
            foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Check the gateway, this is a way to filter out VM network interfaces like Docker
                var gateway = netInterface.GetIPProperties().GatewayAddresses.FirstOrDefault();

                // Ensure that this network interface is connected and not a VM software NIC driver
                if (netInterface.OperationalStatus != OperationalStatus.Up || gateway == null || gateway.Address.ToString().Equals("0.0.0.0"))
                {
                    continue;
                }

                // Get network interface addresses
                var properties = netInterface.GetIPProperties();

                // Get the addresses
                foreach (var address in properties.UnicastAddresses)
                {
                    // TODO Support IPv6
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                    // Assign the address
                    var addr = address.Address.ToString();
                    CyLog.LogInfoFormat("Autoselected local IP: {0}", addr);
                    localIP = address.Address;
                    break;
                }
            }

            // Finally, don't be very picky
            if (localIP == null)
            {
                foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    // Get network interface addresses
                    var properties = netInterface.GetIPProperties();

                    // Get the addresses
                    foreach (var address in properties.UnicastAddresses)
                    {
                        // TODO Support IPv6
                        if (address.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                        CyLog.LogInfoFormat("Final autoselected local IP: {0}", address.Address.ToString());
                        localIP = address.Address;
                        break;
                    }
                }
            }

            #endregion Assign Local IP

            // Notify
            if (OnNetworkingStarted != null) OnNetworkingStarted.Invoke();

            // Attempt to open a port forward for this client via UPnP on local NAT/router
            //if (UseUPNP && !IsUPnPConfigured) ConfigureUPnP();

            // Broadcast a discovery request to see if there is a Facilitator running locally
            var discoverMsg = new JsonArgsSessionMessage(MessageTypes.Facilitate, MessageFlags.Request, "Discover");
            netManager.SendDiscoveryRequest(discoverMsg.Serialize(), facilitator != null ? facilitator.EndPoint.Port : DEFAULT_FACILITATOR_PORT);
        }

        /// <summary>
        /// Stops listening for messages and closes the current UDP client.
        /// </summary>
        public void StopNetworking()
        {
            if (!IsStarted) return;

            // Stop OSC services
            if (OscController != null) OscController.StopOscReceiver();

            // Stop voice chat
            if (VoiceChat != null) VoiceChat.StopVoiceChat();

            // If registered with the facilitator, unregister
            if (IsRegistered) UnregisterFromFacilitator();

            // Attempt to cleanly disconnect from connected agents
            foreach (var pair in agentsById)
            {
                var agent = pair.Value;
                if (agent.IsConnected) DisconnectFromAgent(agent);
            }

            CyLog.LogInfo("Sessions networking stopping...");

            // Stop receiving messages
            if (IsReceiving) StopReceiving();

            // Close the current clients
            LocalUnicastEP = null;
            if (netManager != null) netManager.Stop();

            listener.NetworkErrorEvent -= Listener_NetworkErrorEvent;
            listener.NetworkLatencyUpdateEvent -= Listener_NetworkLatencyUpdateEvent;
            listener.NetworkReceiveEvent -= Listener_NetworkReceiveEvent;
            listener.NetworkReceiveUnconnectedEvent -= Listener_NetworkReceiveUnconnectedEvent;
            listener.PeerConnectedEvent -= Listener_PeerConnectedEvent;
            listener.PeerDisconnectedEvent -= Listener_PeerDisconnectedEvent;

            IsStarted = false;

            CyLog.LogInfo("Sessions networking stopped");

            // Notify
            if (OnNetworkingStopped != null) OnNetworkingStopped.Invoke();
        }

        #endregion Operation

        #region Logging

        // Handles the log entry written event
        private void CyLog_OnEventLogged(string content, LogLevels type)
        {
            // Debug to Unity's debug logger
            switch (type)
            {
                case LogLevels.Info:
                case LogLevels.Verbose:
                    Debug.Log(content);
                    break;

                case LogLevels.Warning:
                    Debug.LogWarning(content);
                    break;

                case LogLevels.Error:
                    Debug.LogError(content);
                    break;
            }
        }

        // Handles refresh events from the logging system
        private void CyLog_OnRefreshed()
        {
            if (DebugText == null) return;
            DebugText.text = CyLog.GetBuffer(); // get internal rolling window log buffer and render to UI.
        }

        #endregion Logging

        #region Net Manager

        #region Data Received

        // Handle receiving a new network message
        private void Listener_NetworkReceiveEvent(NetPeer peer, NetDataReader reader)
        {
            // Deserialize the session message
            ReceiveData(peer, reader);
        }

        #endregion Data Received

        #region Unconnected Receive Data

        // Handle unconnected event
        private void Listener_NetworkReceiveUnconnectedEvent(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
        {
            CyLog.LogVerboseFormat("Unconnected message received from {0}:{1}", remoteEndPoint.Host, remoteEndPoint.Port);

            try
            {
                if (messageType == UnconnectedMessageType.Default)
                {
                    int offset = reader.Position;
                    var msg = new SessionMessage(reader.Data, ref offset);
                    msg.EndPoint = new IPEndPoint(IPAddress.Parse(remoteEndPoint.Host), remoteEndPoint.Port);

                    // IMPORTANT! During NAT connection attempts the agent/owner of this message will not exist yet
                    msg.Owner = GetAgentyByKey(remoteEndPoint.Host + ":" + remoteEndPoint.Port);

                    lock (msgQueue) msgQueue.Enqueue(msg);
                }
                else if (messageType == UnconnectedMessageType.DiscoveryResponse)
                {
                    int offset = reader.Position;
                    var msg = SessionMessage.Deserialize(reader.Data, ref offset);

                    // Ensure this is a proper discovery response message
                    if (msg.Type == MessageTypes.Facilitate && msg.Name == "Discover" && msg.Flags == MessageFlags.Response)
                    {
                        // If so update the Facilitator endpoint to the discovered endpoint
                        if (facilitator != null)
                        {
                            IPAddress ipAddress;                          

                            // Try to parse the passed hostname as an IP address...
                            if (!IPAddress.TryParse(remoteEndPoint.Host, out ipAddress))
                            {
                                CyLog.LogWarnFormat("Parsing discovered Facilitator address failed: {0}", remoteEndPoint.Host);
                                
                            }
                            else
                            {
                                // TODO support IPv6
                                if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
                                {
                                    // Update the Facilitator endpoint
                                    FacilitatorAddress = remoteEndPoint.Host;
                                    FacilitatorPort = remoteEndPoint.Port;
                                    facilitator.Hostname = remoteEndPoint.Host;
                                    facilitator.EndPoint = new IPEndPoint(ipAddress, remoteEndPoint.Port);
                                    CyLog.LogInfoFormat("Facilitator discovered at {0}:{1}", remoteEndPoint.Host, remoteEndPoint.Port);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CyLog.LogError(ex);
            }
        }

        #endregion Unconnected Receive Data

        #region Peer Connected

        // Handle the disconnection of a peer
        private void Listener_PeerConnectedEvent(NetPeer peer)
        {
            CyLog.LogInfoFormat("Network peer connected {0}", peer.EndPoint);

            // Check to see if there is currently a connection attempt for this peer
            var agentKey = peer.EndPoint.Host + ":" + peer.EndPoint.Port.ToString();
            var agent = GetAgentyByKey(agentKey);

            // If there is no agent found for this endpoint, and this is not the Facilitator, something went wrong...
            if (agent == null && (peer.EndPoint.Host != facilitator.EndPoint.Address.ToString() || peer.EndPoint.Port != facilitator.EndPoint.Port))
            {
                CyLog.LogErrorFormat("A network peer connected but no agent was found for its endpoint {0}", agentKey);
                return;
            }
            else if (agent != null)
            {
                agent.Peer = peer; // assign the agent to its underlying network peer
                FinalizeConnectToAgent(agent, false, peer.EndPoint.Host, peer.EndPoint.Port);
            }
        }

        #endregion Peer Connected

        #region Peer Disconnected

        // Handle the connection of a peer
        private void Listener_PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            CyLog.LogInfoFormat("Network peer disconnected {0}", peer.EndPoint);

            // Figure out which peer this was and finalize the action
            if (peer.EndPoint.Host == facilitator.Hostname && peer.EndPoint.Port == facilitator.EndPoint.Port)
            {
                // This was the Facilitator
                facilitator.States.EnterState("Disconnected");
                SessionsNotifications.GlobalNotify("Disconnected from Facilitator service", "Facilitator Disconnected");

                // Notify
                if (OnDisconnectedFromFacilitator != null) OnDisconnectedFromFacilitator.Invoke();
            }
            else
            {
                // Find the agent for this peer
                var agentName = "peer";
                var agent = GetAgentyByKey(peer.EndPoint.Host + ":" + peer.EndPoint.Port.ToString());
                if (agent != null) agentName = agent.Name;
                var message = string.Format("Disconnected from {0}", agentName);
                SessionsNotifications.GlobalNotify(message, "Agent Disconnected");

                // Remove agent from local lists
                if (agent != null)
                {
                    if (agentsById.ContainsKey(agent.Id)) agentsById.Remove(agent.Id);
                    if (agentsByPrivateDataKey.ContainsKey(agent.PrivateAgentKey)) agentsByPrivateDataKey.Remove(agent.PrivateAgentKey);
                    if (agentsByPublicDataKey.ContainsKey(agent.PublicAgentKey)) agentsByPrivateDataKey.Remove(agent.PublicAgentKey);
                    if (connectingAgentsById.ContainsKey(agent.Id)) connectingAgentsById.Remove(agent.Id);

                    // Go through each instance that was created by the disconnected agent and destroy it
                    SessionsNetworkEntityManager.Current.DestroyAllInstancesByAgent(agent, true);

                    // Disconnect from voice
                    if (UseVoice && IsHost && SessionsVoiceServer.Current != null)
                    {
                        SessionsVoiceServer.Current.DisconnectClient(agent.Id);
                        CyLog.LogInfoFormat("Disconnected agent from voice server {0}:{1}", agent.Id, agent.Name);
                    }
                }

                // Notify
                OnAgentDisconnected.Invoke(agent);
            }
        }

        #endregion Peer Disconnected

        #region Network Error

        // Handle network error events
        private void Listener_NetworkErrorEvent(NetEndPoint endPoint, int socketErrorCode)
        {
            CyLog.LogInfoFormat("[NET] [ERROR] {0} socket error: {1}", endPoint, socketErrorCode);
        }

        #endregion Network Error

        #region Network Latency

        // Handle client latency update
        private void Listener_NetworkLatencyUpdateEvent(NetPeer peer, int latency)
        {
            //CyLog.LogVerboseFormat("[NET] [LATENCY] {0} {1} ms", peer.EndPoint, latency);

            // Figure out if this is an agent or the facilitator
            var agentKey = peer.EndPoint.Host + ":" + peer.EndPoint.Port.ToString();
            var agent = GetAgentyByKey(agentKey);

            // If this is an agent, udpate latency information
            if (agent != null)
            {
                agent.AddLatencySample(latency);
            }
            // Otherwise this is the facilitator update latency information
            else
            {
                facilitator.AddLatencySample(latency);
            }
        }

        #endregion Network Latency

        #endregion Net Manager

        #region NAT

        #region UPnP

        // Attempts to install local port-forwarding for network traffic via UPnP on local NAT/relay.
        //private void ConfigureUPnP(Action success = null, Action failure = null)
        //{
        //    // Start NAT test
        //    CyLog.LogInfo("Attempting UPnP port forwarding...");
        //    if (natDiscoverer == null) natDiscoverer = new NatDiscoverer();

        //    var cancelToken = new CancellationTokenSource();
        //    cancelToken.CancelAfter((int)(10 * 1000.0));

        //    natDiscoverer.DiscoverDeviceAsync(PortMapper.Upnp, cancelToken).ContinueWith((discoverTask =>
        //    {
        //        natDevice = discoverTask.IsFaulted || discoverTask.IsCanceled || discoverTask.Result == null ? null : discoverTask.Result;

        //        // If no device was found, the traversal failed...
        //        if (natDevice == null)
        //        {
        //            CyLog.LogInfoSafe("UPnP failed to configure: no compatible devices found.");
        //            IsUPnPConfigured = false;
        //            if (failure != null) actionQueue.Add(failure); // can't execute here because of thread safety
        //            return;
        //        }

        //        if (natDevice != null)
        //        {
        //            natDevice.GetExternalIPAsync().ContinueWith((deviceTask =>
        //            {
        //                IPAddress externalIP = deviceTask.IsFaulted || deviceTask.IsCanceled || deviceTask.Result == null ? null : deviceTask.Result;

        //                // If no external IP was found, the traversal failed...
        //                if (externalIP == null)
        //                {
        //                    CyLog.LogInfoSafe(LogLevels.Info, "UPnP failed to configure: no compatible devices found.");
        //                    IsUPnPConfigured = false;
        //                    if (failure != null) actionQueue.Add(failure); // can't execute here because of thread safety
        //                    return;
        //                }

        //                if (externalIP != null)
        //                {
        //                    // Otherwise attempt to create the port forward
        //                    var mapping = new Mapping(Protocol.Udp, UnicastPort, UnicastPort, UPNP_SESSION, "Cymatic Sessions Data & Voice");

        //                    natDevice.CreatePortMapAsync(mapping).ContinueWith((Action<Task>)(mapTask =>
        //                    {
        //                        CyLog.LogInfoSafe(LogLevels.Info, "UPnP successfully configured for local host.");
        //                        IsUPnPConfigured = true;
        //                        if (success != null) actionQueue.Add(success); // can't execute here because of thread safety
        //                    }));
        //                }
        //            }));
        //        }
        //    }));
        //}

        #endregion UPnP

        #endregion NAT

        #region Receiving Messages

        /// <summary>
        /// Starts receiving data.
        /// </summary>
        public void StartReceiving()
        {
            if (IsReceiving) return;
            IsReceiving = true;
        }

        /// <summary>
        /// Stops receiving data.
        /// </summary>
        public void StopReceiving()
        {
            if (!IsReceiving) return;
            IsReceiving = false;
        }

        // Receives UDP data serially
        void ReceiveData(NetPeer peer, NetDataReader reader)
        {
            try
            {
                // Receiving was cancelled so don't process this message and don't continue receiving
                // Or if this message is coming in on the voice port and the voice port is now registered with the facilitator ignore incoming data (could be voice packets)
                if (!IsReceiving || !IsStarted) return;

                // Deserialize the session message
                int offset = reader.Position;
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(peer.EndPoint.Host), peer.EndPoint.Port);
                byte[] buffer = reader.Data;

                // Bad buffer size
                // TODO Log this?
                if (buffer.Length <= SessionMessage.NON_VARIABLE_BASE_PACKET_SIZE)
                {
                    return;
                }

                // Read the packet marker (first 2 bytes)
                var marker = BitConverter.ToUInt16(buffer, offset);
                offset += 2;

                // If this packet has the incorrect marker, ignore it
                if (marker != SessionMessage.PACKET_HEADER_MARKER)
                {
                    // Continue receiving
                    return;
                }

                // Get the type message type
                var type = (MessageTypes)BitConverter.ToUInt16(buffer, offset);

                // Reset the read offset
                offset = 0;

                // Deserialize the message
                var message = SessionMessage.Deserialize(buffer, ref offset);

                // Apply the origin to the message
                message.EndPoint = remoteEP;

                // Assign the message its network peer
                message.Peer = peer;

                // Queue the message
                lock (queueLock)
                {
                    msgQueue.Enqueue(message);
                    TotalMessagesReceived++;

                    // TODO Remove?
                    LastReceiveEndPoint = message.AgentKey;
                    LastReceivedMessage = message;
                    ReceiveActivity = true;
                }

                // Don't log voice as it's too many packets
                if ((type != MessageTypes.Transform) &&
                    (type != MessageTypes.Voice || (message.Name != "VoiceData" && CyLog.LogLevel.HasFlag(LogLevels.Verbose))))
                {
                    var args = message.Args != null ? "\nargs -> " + message.Args : "";
                    CyLog.LogVerboseSafe("[RX] [" + message.AgentKey + "] [" + message.Flags.ToString() + "] [" + message.Channel.ToString() + "] [" + message.Id.ToString() + "] [" + type.ToString() + "] " + message.Name + "=" + message.Value.ToString() + args, null, CyLog.Now);
                    //Log(LogTypes.Info, "RX " + message.AgentKey + ":" + message.Type.ToString() + ":" + message.Flags.ToString() + ":" + message.Name + "=" + message.Value.ToString(), DateTime.Now);
                }
            }
            catch (SocketException sex)
            {
                /* When sending UDP packets to unreachable hosts, unreachable host packets 
                 * returned to this client will trigger this socket exception whenever the next receive handler is called.
                 * Mono doesn't have proper implementation to disable the exception, so we have to catch it
                 */
                if (sex.SocketErrorCode != SocketError.ConnectionReset) throw sex; // pass the exception if it wasn't connection reset
            }
            catch (System.ObjectDisposedException)
            {
                // This is fine, the voice data client was closed to start voice chat, ignore
                return;
            }
            catch (Exception ex)
            {
                // Set the log entry into the queue so it can be consumed later by the main game thread
                CyLog.LogErrorSafe(ex);
            }
        }

        /// <summary>
        /// Returns the current list of received and waiting messages.
        /// </summary>
        /// <returns>A list of the current messages waiting in the received queue.</returns>
        public ICollection<SessionMessage> ConsumeWaitingMessages()
        {
            List<SessionMessage> messages = null;

            lock (queueLock)
            {
                messages = new List<SessionMessage>(msgQueue);
                msgQueue.Clear();
            }

            return messages != null ? messages : new List<SessionMessage>();
        }

        #endregion Receiving Messages

        #region Sending Messages

        /// <summary>
        /// Sends a message to all connected agents (excluding self).
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="options">The options to use when sending the message.</param>
        public void SendToAll(SessionMessage message, SendOptions? options = null)
        {
            foreach (var agent in GetAllAgents())
            {
                SendToAgent(agent, message, options);
            }
        }

        /// <summary>
        /// Sends a message to a connected agent.
        /// </summary>
        /// <param name="agent">The agent to send the message to.</param>
        /// <param name="message">The message to send.</param>
        /// <param name="options">The options to use when sending the message.</param>
        public void SendToAgent(SessionAgent agent, SessionMessage message, SendOptions? options = null)
        {
            if (agent == null) throw new ArgumentNullException("agent");
            if (agent.Peer == null && !agent.IsRelayed) throw new ArgumentException("SessionAgent has no connected peer to send to");
            if (message == null) throw new ArgumentNullException("message");
            Send(agent.Peer, message, options, agent.IsRelayed, agent.PrivateAgentKey, agent.PublicAgentKey);
        }

        /// <summary>
        /// Sends a message to the Facilitator service.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="options">The options to use when sending the message.</param>
        public void SendToFacilitator(SessionMessage message, SendOptions? options = null)
        {
            if (message == null) throw new ArgumentNullException("message");
            Send(facilitator.Peer, message);
        }

        // Sends a message to a connected network peer
        private void Send(NetPeer peer, SessionMessage message, SendOptions? options = null, bool isRelayed = false, string privateKey = null, string publicKey = null)
        {
            // Create an agent key based on the unique endpoint
            // If this is a relayed message, use the facilitator, but use the original source agent key
            var hostname = isRelayed ? facilitator.Hostname : peer.EndPoint.Host;
            var port = isRelayed ? facilitator.EndPoint.Port : peer.EndPoint.Port;
            var agentKey = isRelayed ? publicKey : hostname + ":" + port.ToString();

            // If this message is a request, queue it in the request queue
            if (message.Flags == MessageFlags.Request)
            {
                // Check to see if the current message has an unassigned ID
                if (message.Id == SessionMessage.START_ID)
                {
                    // Consider this request to be "reliable/ordered" delivery so give it an ID for ordering
                    var requestKey = agentKey + (message.Type == MessageTypes.Facilitate ? ":" + (((byte)message.Channel).ToString()) : "");
                    if (!txPacketIdsByAgent.ContainsKey(requestKey)) txPacketIdsByAgent.Add(requestKey, SessionMessage.START_ID); // start new packet ID for this agent
                    message.Id = txPacketIdsByAgent[requestKey]++;
                }

                // Create the unique key for this host/port/message type/message name
                var msgKey = GetMessageKey(message, agentKey);

                lock (requestMsgQueue)
                {
                    // If this request is not currently being handled, add it to the list waiting for responses by its unique message key
                    if (!requestMsgQueue.ContainsKey(msgKey) && message.QueuedTime == null)
                    {
                        message.QueuedTime = Now; // record the initial request time to deteremine timeout later
                        requestMsgQueue.Add(msgKey, message);
                        CyLog.LogInfoFormat("REQ to -> {0} >>> {1}", message.Id, msgKey);
                    }
                }
            }

            // Check to see if this message needs to be relayed through the facilitator
            if (message.Type != MessageTypes.Facilitate)
            {
                // Try to get the target agent for this message
                var target = GetAgentyByKey(agentKey);

                // If the target is found and this is a relayed connection...
                if (target != null && target.IsRelayed)
                {
                    // If so, reroute the message through the facilitator service for delivery
                    hostname = facilitator.Hostname;
                    port = facilitator.EndPoint.Port;
                    message.RelayIds = new Guid[] { AgentId, target.Id }; // from:this agent, to:target agent
                    isRelayed = true;
                }
            }

            // Don't log voice as it's too many packets
            if ((message.Type != MessageTypes.Transform) &&
                (message.Type != MessageTypes.Voice || (message.Name != "VoiceData" && CyLog.LogLevel.HasFlag(LogLevels.Verbose))))
            {
                LastSendEndPoint = hostname + ":" + port.ToString();
                LastSentMessage = message;

                // Set send flag
                SendActivity = true;

                var args = message.Args != null ? "\nargs -> " + message.Args : "";
                CyLog.LogVerbose("[TX] [" + LastSendEndPoint + "] [" + message.Flags.ToString() + "] [" + message.Channel.ToString() + "] [" + message.Id.ToString() + "] [" + message.Type.ToString() + "] " + message.Name + "=" + message.Value.ToString() + args, null, CyLog.Now);
            }

            // This is a relayed message, relay it through the Facilitator
            if (isRelayed)
            {
                SendToFacilitator(message);
                return;
            }

            // Determine the send style based on message type and flags
            var sendOptions = SendOptions.Unreliable;

            // If send options were provided use those...
            if (options != null)
            {
                sendOptions = options.Value;
            }
            // Otherwise look at the message flags to determine send options
            else
            {
                // If this is a request or response, use reliable delivery
                if (message.Flags == MessageFlags.Request || message.Flags == MessageFlags.Response)
                    sendOptions = SendOptions.ReliableOrdered;
            }

            var buffer = message.Serialize();
            peer.Send(buffer, 0, buffer.Length, sendOptions);
        }

        #endregion Sending Messages

        #region Processing

        // Process the current network log and message queues
        private void ProcessQueue()
        {
            try
            {
                // Update underlying network transport layer
                if (netManager != null) netManager.PollEvents();

                // Update current time stamp
                Now = DateTime.Now;
                var now = Now;

                // Clear activity flags
                SendActivity = ReceiveActivity = false;

                #region Process Action Queue

                // Get the current actions from the queue
                foreach (var action in ConsumeWaitingActions()) action();

                #endregion Process Action Queue

                #region Process Log Queue

                CyLog.UpdateQueue();
                CyLog.RemoteFlush(); // flush remote log queue if connecting to remote logging

                #endregion Process Log Queue

                if (!IsStarted) return;

                #region Process Received Message Queue

                if (msgQueue.Count > 0)
                {
                    // Get the current message queue
                    var messages = ConsumeWaitingMessages();
                    TotalMessagesDequeued += (ulong)messages.Count;

                    // Create a new sorted list of requests
                    var transients = new List<SessionMessage>();
                    var requests = new List<SessionMessage>();
                    var responses = new List<SessionMessage>();

                    #region Sort & Preprocess Queue

                    // Filter messages into sublists based on flags
                    foreach (var msg in messages)
                    {
                        // There is no remote endpoint on this message, skip...
                        if (msg.EndPoint == null) continue;

                        // Get the agent for this message
                        var agent = GetAgentFromMessage(msg);

                        // If an agent was found for the message, assign it and update the last activity time stamp
                        if (agent != null)
                        {
                            msg.Owner = agent;
                            if (agent.LastActivityTime < now || agent.LastActivityTime == null) agent.LastActivityTime = now;
                        }

                        // If this was a relayed message, replace the "from" agent
                        if (msg.RelayIds != null && msg.RelayIds.Length > 1)
                        {
                            // Get the agent from the "from" relay ID
                            agent = GetAgentById(msg.RelayIds[0]);

                            // If it was found, replace this messages endpoint with the "from" agent
                            if (agent != null)
                            {
                                msg.EndPoint = new IPEndPoint(agent.PublicIP, agent.PublicPort);
                                msg.Owner = agent;
                                if (agent.LastActivityTime < now || agent.LastActivityTime == null) agent.LastActivityTime = now;
                            }
                        }

                        // Sort the message into its proper sublist
                        switch (msg.Flags)
                        {
                            case MessageFlags.None:
                                transients.Add(msg);
                                break;

                            case MessageFlags.Request:
                                requests.Add(msg);
                                TotalRequestsReceived++;
                                break;

                            case MessageFlags.Response:
                                responses.Add(msg);
                                break;
                        }
                    }

                    // If a sort message post-processor is in place, call it
                    //if (sortPostProcessor != null) sortPostProcessor(messages);

                    #endregion Sort & Preprocess Queue

                    #region Process Transients

                    // If a transient message pre-processor is in place, call it
                    //if (transientPreProcessor != null) transientPreProcessor(transients);

                    // Process transient messages - these are non request/reply/reliable messages
                    foreach (var msg in transients)
                    {
                        try
                        {
                            #region Handle Transients

                            TotalTransientsReceived++;

                            // If this is a transient message with a valid endpoint...
                            var type = msg.Type;

                            // If this is a session value update message...
                            if (type == MessageTypes.Value)
                            {
                                ReceiveActivity = true;

                                // Notify
                                if (OnSessionValueChanged != null) OnSessionValueChanged.Invoke(msg.Name, msg.Value, msg);
                            }
                            // If this is transient voice data...
                            else if (type == MessageTypes.Voice)
                            {
                                HandleVoiceMessage((VoiceMessage)msg);
                            }
                            else if (type == MessageTypes.Transform)
                            {
                                HandleNetworkTransformMessage((TransformMessage)msg);
                            }
                            // Handle state changes...
                            else if (type == MessageTypes.State)
                            {
                                HandleEntityStateMessage((StateMessage)msg);
                            }
                            else if (type == MessageTypes.Rpc)
                            {
                                HandleRpcMessage((RpcMessage)msg);
                            }
                            // Handle network entity messages
                            else if (type == MessageTypes.Entity)
                            {
                                switch (msg.Name)
                                {
                                    case "Create":
                                        HandleNetworkInstanceCreated((JsonArgsSessionMessage)msg);
                                        break;
                                }
                            }

                            #endregion Handle Transients
                        }
                        catch (Exception mex)
                        {
                            CyLog.LogInfoFormat("{0}\n{1}", mex.Message, mex.StackTrace);
                        }
                    }

                    // If a transient message post-processor is in place, call it
                    //if (transientPostProcessor!= null) transientPostProcessor(transients);

                    #endregion Process Transients

                    #region Process Requests

                    // If a request message pre-processor is in place, call it
                    //if (requestPreProcessor != null) requestPreProcessor(totalRequests);

                    // Process Request Messages
                    foreach (var msg in requests)
                    {
                        try
                        {
                            // Increement the number of actual requests processed
                            TotalRequestsProcessed++;
                            ReceiveActivity = true;

                            #region Handle By Type

                            var type = msg.Type;

                            // Handle based on message type
                            if (type == MessageTypes.Connection)
                            {
                                switch (msg.Name)
                                {
                                    // Handle NAT punch-through requests
                                    case "New":
                                        HandleAgentConnectionRequest(msg);
                                        break;
                                }
                            }
                            else if (type == MessageTypes.Facilitate)
                            {
                                switch (msg.Name)
                                {
                                    // Handle a connection request via facilitator
                                    case "Connect":
                                        HandleFacilitatorConnect((JsonArgsSessionMessage)msg, true);
                                        break;
                                }
                            }
                            // Handle voice requests
                            else if (type == MessageTypes.Voice)
                            {
                                HandleVoiceMessage((VoiceMessage)msg);
                            }
                            // Handle state changes...
                            else if (type == MessageTypes.State)
                            {
                                HandleEntityStateMessage((StateMessage)msg);
                            }
                            // Handle network entity messages
                            else if (type == MessageTypes.Entity)
                            {
                                switch (msg.Name)
                                {
                                    case "Create":
                                        HandleNetworkInstanceCreated((JsonArgsSessionMessage)msg);
                                        break;
                                }
                            }

                            #endregion Handle By Type
                        }
                        catch (Exception mex)
                        {
                            CyLog.LogInfoFormat("{0}\n{1}", mex.Message, mex.StackTrace);
                        }
                    }

                    // If a request message post-processor is in place, call it
                    //if (requestPostProcessor != null) requestPostProcessor(totalRequests);

                    #endregion Process Requests

                    #region Process Responses

                    // If a response message pre-processor is in place, call it
                    //if (responsePreProcessor != null) responsePreProcessor(responses);

                    // Process Response Messages
                    foreach (SessionMessage msg in responses)
                    {
                        try
                        {
                            #region Handle Responses

                            // Create the unique key for this host/port/message type/message name
                            var msgKey = GetMessageKey(msg);

                            // Prepare a message request to be found...
                            SessionMessage requestMsg = null;

                            // Check to see if there is a matching request...
                            lock (requestMsgQueue)
                            {
                                // If there is a request waiting for this response, remove it
                                if (requestMsgQueue.ContainsKey(msgKey))
                                {
                                    requestMsg = requestMsgQueue[msgKey];

                                    // If this is not a keep alive request marked for removal...
                                    requestMsgQueue.Remove(msgKey);
                                }
                            }

                            // If this was a response to a valid request, handle the response
                            if (requestMsg != null)
                            {
                                CyLog.LogInfoFormat("RES: {0}", msgKey);

                                if (msg.Type == MessageTypes.Connection)
                                {
                                    //switch (msg.Name)
                                    //{

                                    //}
                                }
                                else if (msg.Type == MessageTypes.Facilitate)
                                {
                                    // Get the facilitate message arguments
                                    var fMsg = (JsonArgsSessionMessage)msg;

                                    switch (msg.Name)
                                    {
                                        case "Add":
                                            HandleFacilitatorAddResponse(fMsg);
                                            break;

                                        case "Remove":
                                            HandleFacilitatorRemoveResponse(fMsg);
                                            break;

                                        case "List":
                                            HandleFacilitatorListResponse(fMsg);
                                            break;

                                        case "Connect":
                                            HandleFacilitatorConnect(fMsg, false);
                                            break;
                                    }
                                }
                                // Handle voice responses
                                else if (msg.Type == MessageTypes.Voice)
                                {
                                    HandleVoiceMessage((VoiceMessage)msg);
                                }

                                // If this request had a completed handler, call it, its response was received and it is being removed
                                if (requestMsg.CompleteHandler != null) requestMsg.CompleteHandler(msg);
                            }

                            // messages received; set flag
                            ReceiveActivity = responses.Count > 0;

                            #endregion Handle Responses
                        }
                        catch (Exception mex)
                        {
                            CyLog.LogInfoFormat("{0}\n{1}", mex.Message, mex.StackTrace);
                        }
                    }

                    // If a response message post-processor is in place, call it
                    //if (responsePostProcessor != null) responsePostProcessor(responses);

                    #endregion Process Repsonses
                }

                #endregion Process Received Message Queue

                #region Proces Requests Awaiting Responses

                if (requestMsgQueue.Count > 0)
                {
                    // Copy a list of request message keys
                    var requestKeys = requestMsgQueue.Keys.ToArray();

                    // Create a list for requests that have timed out to be removed
                    var staleRequests = new List<string>();

                    // Go through and send out follow up requests
                    foreach (var msgKey in requestKeys)
                    {
                        try
                        {
                            // Get the request message
                            var msg = requestMsgQueue[msgKey];
                            var queueTime = msg.QueuedTime;

                            // If this request has timed out, mark it for removal
                            if (msg.IsCancelled || now - queueTime > msg.Timeout) // normal timeout
                            {
                                // Add the key to mark the message for clean up
                                staleRequests.Add(msgKey);
                                continue;
                            }
                        }
                        catch (Exception rex)
                        {
                            CyLog.LogInfoFormat("{0}\n{1}", rex.Message, rex.StackTrace);
                        }
                    }

                    // Clean-up old requests
                    var removed = 0;

                    foreach (var msgKey in staleRequests)
                    {
                        var reqMsg = requestMsgQueue[msgKey];
                        removed++;
                        requestMsgQueue.Remove(msgKey);
                        if (reqMsg.TimeoutHandler != null) reqMsg.TimeoutHandler(); // call timeout handler if assigned
                        reqMsg.TimeoutHandler = null;
                    }

                    if (removed > 0) CyLog.LogInfoFormat("{0} requests timed out", removed);
                }

                #endregion Proces Requests Awaiting Responses

                #region Process Connection Attempts

                var attemptIds = connectionAttemptsByAgentId.Keys.ToArray();

                foreach (var attemptId in attemptIds)
                {
                    // Get the attempt
                    var attempt = connectionAttemptsByAgentId[attemptId];
                    var agent = attempt.Agent;

                    // If still attempting to connect, resend the messages
                    if (attempt.PrivateConnectionState == ConnectionStates.Connecting && now - attempt.LastPrivateAttempt >= attempt.RetryInterval)
                    {
                        CyLog.LogVerboseFormat("Resending private connection attempt to {0}:{1}", agent.PrivateIP, agent.PrivatePort);
                        netManager.SendUnconnectedMessage(attempt.PrivateConnectionMessage.Serialize(), new NetEndPoint(agent.PrivateIP.ToString(), agent.PrivatePort));
                        attempt.LastPrivateAttempt = now;
                    }

                    if (attempt.PublicConnectionState == ConnectionStates.Connecting && now - attempt.LastPublicAttempt >= attempt.RetryInterval)
                    {
                        CyLog.LogVerboseFormat("Resending public connection attempt to {0}:{1}", agent.PrivateIP, agent.PrivatePort);
                        netManager.SendUnconnectedMessage(attempt.PublicConnectionMessage.Serialize(), new NetEndPoint(agent.PublicIP.ToString(), agent.PublicPort));
                        attempt.LastPublicAttempt = now;
                    }
                }

                #endregion Process Connection Attempts
            }
            catch (Exception ex)
            {
                CyLog.LogInfoFormat("{0}\n{1}", ex.Message, ex.StackTrace);
            }
        }

        #endregion Processing

        #region Facilitator

        #region Message Handlers

        #region Add Response

        // Handle "Add" agent response messages
        private void HandleFacilitatorAddResponse(JsonArgsSessionMessage msg)
        {
            // Validate
            if (msg == null) throw new ArgumentNullException("msg");
            if (msg.Type != MessageTypes.Facilitate) throw new ArgumentException("Unexpected message type:" + msg.Type);
            if (msg.Name != "Add") throw new ArgumentException("Unexpected message name:" + msg.Name);

            // Get the agent ID from the response
            var agentIdRaw = msg.GetArgs<string>("agentId");
            var agentId = new Guid(agentIdRaw);
            var agentName = msg.GetArgs<string>("name");
            var voiceId = msg.GetArgs<string>("voiceId");

            // Get the connection details
            var privateIP = msg.GetArgs<string>("privateIP");
            var privatePort = msg.GetArgs<int>("privatePort");
            var publicIP = msg.GetArgs<string>("publicIP");
            var publicPort = msg.GetArgs<int>("publicPort");

            // Assign self details
            AgentId = agentId;
            var self = Self;
            self.Name = agentName;
            self.VoiceId = voiceId;
            self.PrivateIP = IPAddress.Parse(privateIP);
            self.PrivatePort = privatePort;
            self.PublicIP = IPAddress.Parse(publicIP);
            self.PublicPort = publicPort;

            // Get the agent who has accepted the connection request
            FinalizeRegisterWithFacilitator();
        }

        #endregion Add Response

        #region Remove Response

        // Handles "Remove" agent response messages
        private void HandleFacilitatorRemoveResponse(JsonArgsSessionMessage msg)
        {
            // Validate
            if (msg == null) throw new ArgumentNullException("msg");
            if (msg.Type != MessageTypes.Facilitate) throw new ArgumentException("Unexpected message type:" + msg.Type);
            if (msg.Name != "Remove") throw new ArgumentException("Unexpected message name:" + msg.Name);

            // Check to see if there was an error message
            if (msg.Value <= -1)
            {
                var errorMsg = msg.GetArgs<string>("error");
                CyLog.LogInfoFormat("Error unregistering from facilitator: {0}", errorMsg);
            }
            else
            {
                // This agent has now been confirmed removed from the facilitator
                FinalizeDisconnectFromFacilitator();
            }
        }

        #endregion Remove Response

        #region List Response

        // Handle "List" agent response messages
        private void HandleFacilitatorListResponse(JsonArgsSessionMessage msg)
        {
            // Validate
            if (msg == null) throw new ArgumentNullException("msg");
            if (msg.Type != MessageTypes.Facilitate) throw new ArgumentException("Unexpected message type:" + msg.Type);
            if (msg.Name != "List") throw new ArgumentException("Unexpected message name:" + msg.Name);

            // Get the list of agents
            var list = msg.GetArgs<JArray>("agents");

            if (list == null)
            {
                Debug.LogWarningFormat("List of agents came back NULL from facilitator");
            }
            else
            {
                // Go through each listed agent...
                foreach (var item in list)
                {
                    // Make sure the item is the expected type
                    if (!(item is JObject))
                    {
                        Debug.LogWarningFormat("Agent list item has unexpected type: {0}", item.GetType().Name);
                        continue;
                    }

                    // Get the name and ID of the agent
                    var rawId = item["id"] != null ? item["id"].Value<string>() : null;
                    var name = item["name"] != null ? item["name"].Value<string>() : null;

                    // Validate values
                    if (string.IsNullOrEmpty(rawId))
                    {
                        Debug.LogWarningFormat("Agent list item has missing ID.");
                        continue;
                    }
                    else if (string.IsNullOrEmpty(name))
                    {
                        Debug.LogWarningFormat("Agent list item has missing name.");
                        continue;
                    }

                    // Parse the ID
                    var id = new Guid(rawId);

                    // Ignore self...
                    if (id == AgentId) continue;

                    // Create a new internal entry for this registered agent
                    var regAgent = new SessionAgent(id, name);
                    if (!agentsById.ContainsKey(id)) agentsById.Add(id, regAgent);
                    else agentsById[id] = regAgent;
                    CyLog.LogInfoFormat("Added new registered agent: {0} {1}", id, name);
                }
            }
        }

        #endregion List Response

        #region Connect Request/Response

        // Handle "Connect" to agent request/response messages
        private void HandleFacilitatorConnect(JsonArgsSessionMessage msg, bool isRequest)
        {
            // Validate
            if (msg == null) throw new ArgumentNullException("msg");
            if (msg.Type != MessageTypes.Facilitate) throw new ArgumentException("Unexpected message type:" + msg.Type);
            if (msg.Name != "Connect") throw new ArgumentException("Unexpected message name:" + msg.Name);

            #region Load Agent Values

            // Get the agent ID from the response
            var agentIdRaw = msg.GetArgs<string>("agentId");
            var agentId = new Guid(agentIdRaw);
            var agentName = msg.GetArgs<string>("name");
            var platform = (SessionsPlatforms)msg.GetArgs<int>("platform");
            var voiceId = msg.GetArgs<string>("voiceId");
            var isHost = msg.GetArgs<bool>("isHost");

            // Was it a failure?
            if (msg.Value <= -1)
            {
                Debug.LogWarningFormat("Facilitator returned agent not found: {0}", agentId);

                // Notify
                OnAgentNotFound.Invoke(agentId, agentName);
                return;
            }

            // Get the connection details
            var privateIP = msg.GetArgs<string>("privateIP");
            var privatePort = msg.GetArgs<int>("privatePort");
            var publicIP = msg.GetArgs<string>("publicIP");
            var publicPort = msg.GetArgs<int>("publicPort");

            #endregion Load Agent Values

            CyLog.LogInfoFormat("Connecting to agent {0}:{1}, platform:{2}, voiceId:{3}, privateIP:{4}, privatePort:{5}, publicIP:{6}, publicPort:{7}",
                agentId, agentName, platform, voiceId, privateIP, privatePort, publicIP, publicPort);

            #region Add or Update Agent

            // Create a registered agent entry for this agent
            var agent = new SessionAgent(agentId, agentName, platform, IPAddress.Parse(privateIP), privatePort, IPAddress.Parse(publicIP), publicPort);
            agent.IsHost = isHost;
            agent.VoiceId = voiceId;
            var privateKey = agent.PrivateIP.ToString() + ":" + agent.PrivatePort.ToString();
            var publicKey = agent.PublicIP.ToString() + ":" + agent.PublicPort.ToString();

            // Check to see if there is already a local agent registered with this information and if not register this agent locally
            if (!agentsById.ContainsKey(agentId))
            {
                agentsById.Add(agentId, agent); // index by ID

                // Index by private/public key
                agentsByPrivateDataKey.Add(privateKey, agent);
                agentsByPublicDataKey.Add(publicKey, agent);
            }
            else
            {
                // Get the current/old agent
                var oldAgent = agentsById[agentId];

                // Make sure something actually changed
                if (oldAgent.PublicIP != agent.PublicIP ||
                    oldAgent.PublicPort != agent.PublicPort ||
                    oldAgent.PrivateIP != agent.PrivateIP ||
                    oldAgent.PrivatePort != agent.PrivatePort || 
                    oldAgent.Name != agent.Name || 
                    oldAgent.IsHost != agent.IsHost || 
                    oldAgent.VoiceId != agent.VoiceId ||
                    oldAgent.Platform != agent.Platform)
                {
                    // Update to the new information
                    oldAgent.Name = agent.Name;
                    oldAgent.Platform = agent.Platform;
                    oldAgent.VoiceId = agent.VoiceId;
                    oldAgent.IsHost = agent.IsHost;
                    oldAgent.PrivateIP = agent.PrivateIP;
                    oldAgent.PrivatePort = agent.PrivatePort;
                    oldAgent.PublicIP = agent.PublicIP;
                    oldAgent.PublicPort = agent.PublicPort;

                    // Since the origin/old agent might have the connection peer instance we need, switch over to it instead
                    agent = oldAgent; 
                }
            }

            // Get the latest version of the referenced agent
            agent = agentsById[agentId];

            // Index by private/public key
            agentsByPrivateDataKey[privateKey] = agent;
            agentsByPublicDataKey[publicKey] = agent;

            #endregion Add or Update Agent

            // Don't do anything if already connected...
            if (agent.IsConnected) return;

            #region Negotiate Connection to Agent

            // If set to force the connection to relay, do so now
            if (ForceRelay)
            {
                FinalizeConnectToAgent(agent, true);
                return;
            }

            // If not forcing a relay, attempt a NAT punch-through by sending unsolicited/unreliable connect requests to both public and private end points provided by the Facilitator
            CyLog.LogInfoFormat("Attempting NAT punch-through connection request to private endpoint: {0}:{1}", privateIP, privatePort);
            CyLog.LogInfoFormat("Attempting NAT punch-through connection request to public endpoint: {0}:{1}", publicIP, publicPort);

            // Create a NAT punch-through connection request for both public and private end points
            var selfId = AgentId.ToString();
            var privateMsg = new SessionMessage(MessageTypes.Connection, MessageFlags.Request, "New", selfId);
            var publicMsg = new SessionMessage(MessageTypes.Connection, MessageFlags.Request, "New", selfId);

            // Send to the yet-to-be-connected client's end points
            if (!connectingAgentsById.ContainsKey(agent.Id)) connectingAgentsById.Add(agent.Id, agent); // create a connecting state

            // If a connection attempt for this agent is already under way, ignore and don't try redundantly
            if (connectionAttemptsByAgentId.ContainsKey(agent.Id)) return;

            var attempt = new NatConnectionAttempt(agent, privateMsg, publicMsg);
            attempt.OnConnectionStateChanged += NatConnectionAttempt_OnConnectionStateChanged;

            // Otherwise register the connection attempt and send the public/private request messages
            connectionAttemptsByAgentId.Add(agent.Id, attempt);
            attempt.LastPrivateAttempt = Now;
            attempt.LastPublicAttempt = Now;

            netManager.SendUnconnectedMessage(privateMsg.Serialize(), new NetEndPoint(privateIP, privatePort));
            netManager.SendUnconnectedMessage(publicMsg.Serialize(), new NetEndPoint(publicIP, publicPort));

            // Since we are sending these message requests in a disconnecte state, we will need to manually queue them to receive their respective responses
            var privateMsgKey = GetMessageKey(privateMsg, agent.PrivateAgentKey);
            var publicMsgKey = GetMessageKey(publicMsg, agent.PublicAgentKey);

            if (!requestMsgQueue.ContainsKey(privateMsgKey))
            {
                privateMsg.QueuedTime = Now;
                requestMsgQueue.Add(privateMsgKey, privateMsg);
            }

            // If the public and private message key is the same, the addreses are the same, so only use one, and consider the other "failed" to fall back to just one
            if (privateMsgKey == publicMsgKey)
            {
                // Simulate a timeout to make the public request message "fail"
                publicMsg.TimeoutHandler();
            }
            else
            {
                if (!requestMsgQueue.ContainsKey(publicMsgKey))
                {
                    publicMsg.QueuedTime = Now;
                    requestMsgQueue.Add(publicMsgKey, publicMsg);
                }
            }

            #endregion Negotiate Connection to Agent
        }

        #endregion Connect Request/Response

        #endregion Message Handlers

        #region Event Handlers

        // Handles changes to the Facilitator's state
        private void Facilitator_OnStateEntered(SessionStateMachine stateMachine, ISessionState state, float time)
        {
            switch (state.Name)
            {
                case "Disconnected":
                    FinalizeDisconnectFromFacilitator();
                    break;

                case "Connecting":
                    // TODO anything?
                    break;

                case "Connected":
                    // The 'true' will cause auto registration with Facilitator and keep the connection state moving
                    FinalizeConnectoToFacilitator(true);
                    break;

                case "Registering":

                    break;

                case "Registered":

                    break;

                case "Disconnecting":

                    break;
            }
        }

        // Handles changes to the Facilitator's state
        private void Facilitator_OnStateExited(SessionStateMachine stateMachine, ISessionState state, float time)
        {
            switch (state.Name)
            {
                case "Disconnected":

                    break;

                case "Connecting":

                    break;

                case "Connected":

                    break;

                case "Registering":

                    break;

                case "Registered":

                    break;

                case "Disconnecting":

                    break;
            }
        }

        #endregion Event Handlers

        #region Connect

        /// <summary>
        /// Initiates a connection to the Facilitator.
        /// </summary>
        public void ConnectToFacilitator()
        {
            if (facilitator.IsConnected) return; // already connected
            CyLog.LogInfo("Connecting to the Facilitator...");
            facilitator.States.EnterState("Connecting");
            facilitator.Peer = netManager.Connect(facilitator.Hostname, facilitator.EndPoint.Port);
        }

        // Finalizes a connection to the facilitator service
        private void FinalizeConnectoToFacilitator(bool register = true)
        {
            CyLog.LogInfo("Facilitator connection established");

            // If specified to register next, do so
            if (register) RegisterWithFacilitator();
        }

        #endregion Connect

        #region Disconnect

        // Finalizes a disconnection from the facilitator service
        private void FinalizeDisconnectFromFacilitator()
        {
            CyLog.LogInfo("Disconnected from Facilitator");
            SessionsNotifications.GlobalNotify("Disconnected from Facilitator service", "Facilitator Disconnected");

            // Notify
            if (OnDisconnectedFromFacilitator != null) OnDisconnectedFromFacilitator.Invoke();
        }

        #endregion Disconnect

        #region Facilitate Agent Connection

        /// <summary>
        /// Requests a connection to another agent using the facilitator.
        /// </summary>
        /// <param name="agentId">The ID of the agent to facilitate a connection with.</param>
        public void FacilitateConnection(Guid agentId)
        {
            // Create a connection request to the facilitator for a facilitated connection with this agent
            var jsonArgs = string.Format("{{\"agentId\":\"{0}\"}}", agentId);
            CyLog.LogInfoFormat("Requesting connection request through facilitator: {0}", jsonArgs);
            var connMsg = new JsonArgsSessionMessage(MessageTypes.Facilitate, MessageFlags.Request, "Connect", jsonArgs);
            SendToFacilitator(connMsg);
        }

        #endregion Facilitate Agent Connection

        #region Register

        /// <summary>
        /// Registers this agent with the facilitator service.
        /// </summary>
        public void RegisterWithFacilitator()
        {
            StartCoroutine(DoRegisterWithFacilitator());
        }

        // Registers this agent with the facilitator service at the given port and host
        IEnumerator DoRegisterWithFacilitator()
        {
            while (localIP == null) yield return 0;

            // Build the registration message
            var privateIP = localIP.ToString();
            var privatePort = (ushort)LocalUnicastEP.Port;

            // Ensure a connection before moving forward...
            if (facilitator.Peer.ConnectionState != ConnectionState.Connected)
            {
                CyLog.LogInfo("Facilitator registration failed: not connected");
                yield break;
            }

            // As long as we're connected, register ourselves with the Facilitator
            CyLog.LogInfoFormat("Registering with Facilitator: {0}", facilitator.EndPoint);

            // Attempt to get the agent name from player preferences
            var agentName = PlayerPrefs.GetString("SessionsAgentName");

            // Auto select a relevant name
            if (string.IsNullOrEmpty(agentName))
            {
                agentName = "Win Client";
                if (Application.isEditor) agentName = "Editor";
                else if (Application.platform == RuntimePlatform.Android) agentName = "Oculus Go";
            }

            // Form the arguments and message
            var voiceId = SessionsVoiceCommsNetwork.LocalPlayerId;
            var platform = SessionsSceneManager.GetPlatform();

            var jsonArgs = string.Format("{{\"name\":\"{0}\",\"platform\":{1},\"voiceId\":\"{2}\",\"privateIP\":\"{3}\",\"privatePort\":{4}}}", 
                agentName, (byte)platform, voiceId, privateIP, privatePort);

            var msg = new JsonArgsSessionMessage(MessageTypes.Facilitate, MessageFlags.Request, "Add", jsonArgs);
            SendToFacilitator(msg);
        }

        // Finalizes registration with the Facilitator
        private void FinalizeRegisterWithFacilitator()
        {
            // Ensure we are marked registered now
            facilitator.IsRegistered = true;

            SessionsNotifications.PlayClip("Facilitator Connected");

            // Notify
            if (OnConnectedToFacilitator != null) OnConnectedToFacilitator.Invoke();
        }

        #endregion Register

        #region Unregister

        /// <summary>
        /// Unregisters this agent from the facilitator service.
        /// </summary>
        /// <param name="hostname">The facilitator hostname or IP.</param>
        /// <param name="port">The facilitator port.</param>
        public void UnregisterFromFacilitator()
        {
            // Build the unregister/remove message
            var jsonArgs = string.Format("{{\"agentId\":\"{0}\"}}", AgentId);
            var removeMsg = new SessionMessage(MessageTypes.Facilitate, MessageFlags.Request, "Remove", jsonArgs);
            CyLog.LogInfoFormat("Unregistering from Facilitator: {0}", facilitator.EndPoint);
            SendToFacilitator(removeMsg);
        }

        #endregion Unregister

        #region Sessions

        /// <summary>
        /// Handles a session value being updated by a network agent.
        /// </summary>
        /// <param name="name">The name of the value.</param>
        /// <param name="value">The value.</param>
        /// <param name="raw">The optional raw source object for the value update.</param>
        public void HandleSessionValueChange(string name, float value, object raw)
        {
            if (string.IsNullOrEmpty(name)) throw new System.ArgumentNullException("name");

            // For now only host will rebroadcast
            if (!IsHost) return;

            // If the source was an OSC message...
            if (raw is OscMessage)
            {
                var oscMsg = (OscMessage)raw;

                // See if the underlying raw OSC message is intact, and if so, extract it...
                if (oscMsg.Raw == null) return;
                Rug.Osc.OscMessage rugMsg = (Rug.Osc.OscMessage)oscMsg.Raw;

                // Go through all connected agents and rebroadcast
                foreach (var pair in agentsById)
                {
                    var agent = pair.Value;
                    if (!agent.IsConnected || (!agent.IsRelayed && agent.ConnectedIP == null)) continue; // only rebroadcast to connected agents

                    // Rebroadcast to this other agent
                    var valMsg = new SessionMessage(MessageTypes.Value, MessageFlags.None, name, value);
                    SendToAgent(agent, valMsg, oscMsg.Reliable ? SendOptions.ReliableOrdered : SendOptions.Unreliable);
                }
            }
        }

        // Sends relevant state data to the specified agent
        private void SendStateToAgent(SessionAgent agent)
        {
            // If this is the host...
            if (IsHost)
            {
                // Get a list of all of the current session values
                var valueNames = Manager.GetSessionValueNames();

                foreach (var name in valueNames)
                {
                    // Get the current session value
                    var value = Manager.GetSessionValue(name);

                    // Don't send values if none exists for them yet
                    if (value == null) continue;

                    // Send it to the agent
                    var msg = new SessionMessage(MessageTypes.Value, MessageFlags.None, name, value.Value);
                    SendToAgent(agent, msg);
                }
            }
            // Otherwise if this is the client...
            else
            {
                // TODO sync anything from the client end?
            }
        }

        /// <summary>
        /// Attempts to create a new session as the host with the Facilitator.
        /// </summary>
        /// <param name="name">The name of the session to host.</param>
        /// <param name="url">The URL/URI of the scene to host.</param>
        /// <param name="img">The image to associate with the session.</param>
        /// <param name="info">An optional description to include with the session.</param>
        /// <param name="maxAgents">Optional maximum number of agents that can be in the session at once.</param>
        public void HostNewSession(string name, string url, string img = null, string info = null, int maxAgents = 8)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

            // Create the message
            var jsonArgs = string.Format("{{\"name\":\"{0}\",\"url\":\"{1}\",\"img\":\"{2}\",\"info\":\"{3}\",\"max\":{4}}}", name, url, img, info, maxAgents);
            var msg = new JsonArgsSessionMessage(MessageTypes.Facilitate, MessageFlags.Request, "Host", jsonArgs);

            // Create a completion handler to launch events once the session has been created or failed
            msg.CompleteHandler = (response) =>
            {
                // Check to see if the response failed...
                if (response.Value == -1)
                {
                    IsInSession = false;
                    var res = (JsonArgsSessionMessage)response;
                    var error = res.GetArgs<string>("error");
                    CyLog.LogErrorFormat("Error hosting session: {0} => {1}", name, error);
                    OnSessionHostError.Invoke(name, error);
                    OnSessionError.Invoke(name, error);
                }
                // Otherwise it was successful!
                else
                {
                    CyLog.LogInfoFormat("Session successfully hosted: {0}", name);
                    IsHost = true; // we are now successfully hosting a session
                    IsInSession = true;
                    SessionName = name;
                    OnSessionHosted.Invoke(name, response.Args);
                    OnSessionStarted.Invoke(name, response.Args);
                    
                    // Start voice communications as host/server
                    if (UseVoice) StartVoiceChat(3);
                }
            };

            // Create a timeout handler to handle a timeout with the Facilitator's response
            msg.TimeoutHandler = () =>
            {
                IsInSession = false;
                var error = string.Format("Hosting session timeout");
                CyLog.LogErrorFormat(error);
                OnSessionHostError.Invoke(name, error);
                OnSessionError.Invoke(name, error);
            };

            // Send the message to the Facilitator
            CyLog.LogInfoFormat("Attempting to host new session: {0}", name);
            SendToFacilitator(msg, SendOptions.ReliableUnordered);
        }

        /// <summary>
        /// Attempts to join an existing session as a guest through the Facilitator.
        /// </summary>
        /// <param name="name">The name of the session to join.</param>
        public void JoinSession(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

            // Create the message
            var jsonArgs = string.Format("{{\"name\":\"{0}\"}}", name);
            var msg = new JsonArgsSessionMessage(MessageTypes.Facilitate, MessageFlags.Request, "Join", jsonArgs);

            // Create a completion handler to launch events once the session has been created or failed
            msg.CompleteHandler = (response) =>
            {
                // Check to see if the response failed...
                if (response.Value == -1)
                {
                    IsInSession = false;
                    var res = (JsonArgsSessionMessage)response;
                    var error = res.GetArgs<string>("error");
                    //CyLog.LogErrorFormat("Error joining session: {0} => {1}", name, error);
                    OnSessionJoinedError.Invoke(name, error);
                    OnSessionError.Invoke(name, error);
                }
                // Otherwise it was successful!
                else
                {
                    IsInSession = true;
                    CyLog.LogInfoFormat("Session successfully joined: {0} | {1}", name, response.Args);
                    IsHost = false; // we're a guest
                    SessionName = name;
                    OnSessionJoined.Invoke(name, response.Args);
                    OnSessionStarted.Invoke(name, response.Args);
                }
            };

            // Create a timeout handler to handle a timeout with the Facilitator's response
            msg.TimeoutHandler = () =>
            {
                IsInSession = false;
                var error = string.Format("Joining session timeout");
                //CyLog.LogErrorFormat(error);
                OnSessionHostError.Invoke(name, error);
                OnSessionError.Invoke(name, error);
            };

            // Send the message to the Facilitator
            CyLog.LogInfoFormat("Attempting to join session: {0}", name);
            SendToFacilitator(msg, SendOptions.ReliableUnordered);
        }

        #endregion Sessions

        #endregion Facilitator

        #region Agents

        #region Message Handlers

        #region Connect

        // Handles NAT punch-through connection requests
        private void HandleAgentConnectionRequest(SessionMessage msg)
        {
            // Validate
            if (msg == null) throw new ArgumentNullException("msg");
            if (msg.Type != MessageTypes.Connection) throw new ArgumentException("Unexpected message type:" + msg.Type);
            if (msg.Name != "New") throw new ArgumentException("Unexpected message name:" + msg.Name);

            CyLog.LogInfoFormat("Received a NAT punch-through connection request from {0}:{1}", msg.EndPoint.Address, msg.EndPoint.Port);

            // NAT connection request successfully received, send a response and attempt to connect to the agent formally
            var resMsg = new SessionMessage(msg.Type, MessageFlags.Response, msg.Name);
            resMsg.Id = msg.Id; // needed to match a response to its request
            netManager.SendUnconnectedMessage(resMsg.Serialize(), new NetEndPoint(msg.EndPoint.Address.ToString(), msg.EndPoint.Port));

            // The reference to the owner of the message will likely not exist yet, so get their ID out of the arguments to find them
            if (msg.Owner == null) msg.Owner = GetAgentById(new Guid(msg.Args)); // agent ID should be in the args of the message encoded as a string

            // If we're not already connected to this agent, connect formally
            if (msg.Owner != null && !msg.Owner.IsConnected)
            {
                CyLog.LogInfoFormat("Sending NAT connection response to {0}:{1}", msg.EndPoint.Address, msg.EndPoint.Port);
                msg.Owner.Peer = netManager.Connect(msg.EndPoint.Address.ToString(), msg.EndPoint.Port);
            }
        }

        #endregion Connect

        #region Disconnect

        // Handles disconnection messages
        private void HandleAgentDisconnect(SessionMessage msg)
        {
            CyLog.LogInfo("Got the connection request!");

            // Validate
            if (msg == null) throw new ArgumentNullException("msg");
            if (msg.Type != MessageTypes.Connection) throw new ArgumentException("Unexpected message type:" + msg.Type);
            if (msg.Name != "End") throw new ArgumentException("Unexpected message name:" + msg.Name);

            // Get the agent asking for disconnection
            var agent = GetAgentFromMessage(msg);

            if (agent == null)
            {
                CyLog.LogInfo("An unconnected agent issued a disconnect message; ignoring");
                return;
            }

            // Finish disconnecting from the requesting agent
            FinalizeDisconnectFromAgent(agent);

            // If this is a disconnection request...
            if (msg.Flags == MessageFlags.Request)
            {
                // Send a response
                var resMsg = new SessionMessage(MessageTypes.Connection, MessageFlags.Response, msg.Name);
                resMsg.Id = msg.Id; // important: apply request ID on outgoing response
                SendToAgent(agent, resMsg);
            }
        }

        #endregion Disconnect

        #region Network Entities

        // Handles network instance creation messages from a remote agent
        private void HandleNetworkInstanceCreated(JsonArgsSessionMessage msg)
        {
            // Validate
            if (msg == null) throw new ArgumentNullException("msg");
            if (msg.Type != MessageTypes.Entity) throw new ArgumentException("Unexpected message type:" + msg.Type);
            if (msg.Name != "Create") throw new ArgumentException("Unexpected message name:" + msg.Name);

            // Just pass the message on for the network entity manager to handle
            if (OnNetworkInstanceCreated != null) OnNetworkInstanceCreated.Invoke(msg);

            // If this is a request, send a response
            if (msg.Flags == MessageFlags.Request && msg.Owner != null)
            {
                var resMsg = new JsonArgsSessionMessage(MessageTypes.Entity, MessageFlags.Response, msg.Name, msg.Value);
                resMsg.Id = msg.Id; // important for repsonse to find request
                SendToAgent(msg.Owner, resMsg);
            }
        }

        // Handles agent network entity messages
        private void HandleEntityStateMessage(StateMessage msg)
        {
            // Validate
            if (msg == null) throw new ArgumentNullException("msg");
            if (msg.Type != MessageTypes.State) throw new ArgumentException("Unexpected message type:" + msg.Type);

            // CyLog.LogInfoFormat("Handling state message {0}:{1} {2}.{3}", msg.Name, msg.NetworkEntityId, msg.NetworkName, msg.StateName);

            switch (msg.Name)
            {
                case "StateEnter":
                    OnNetworkStateEntered.Invoke(msg);
                    break;

                case "StateExit":
                    OnNetworkStateExited.Invoke(msg);
                    break;
            }

            // If this is a request, send a response
            if (msg.Flags == MessageFlags.Request && msg.Owner != null)
            {
                var resMsg = new JsonArgsSessionMessage(MessageTypes.State, MessageFlags.Response, msg.Name, msg.Value);
                resMsg.Id = msg.Id; // important for repsonse to find request
                SendToAgent(msg.Owner, resMsg);
            }
        }

        #endregion Network Entities

        #region Network Transform

        // Handles the receiving a network tranform update message
        private void HandleNetworkTransformMessage(TransformMessage msg)
        {
            // Validate
            if (msg == null) throw new ArgumentNullException("msg");
            if (msg.Type != MessageTypes.Transform) throw new ArgumentException("Unexpected message type:" + msg.Type);

            // Just pass the message on for the network entity manager to handle
            if (OnNetworkTransformReceived != null) OnNetworkTransformReceived.Invoke(msg);
        }

        #endregion Network Transform

        #endregion Message Handlers

        #region Event Handlers

        // Handles changes to connection states for an agent connection attempt
        private void NatConnectionAttempt_OnConnectionStateChanged(NatConnectionAttempt attempt)
        {
            try
            {
                // If the private connection attempt succeeded...
                if (attempt.PrivateConnectionState == ConnectionStates.Connected)
                {
                    // Fail the other connection attempt
                    if (attempt.PublicConnectionState != ConnectionStates.Failed)
                    {
                        attempt.PublicConnectionState = ConnectionStates.Failed;
                        attempt.PublicConnectionMessage.IsCancelled = true; // cancel the request
                    }

                    // Issue a proper connection sequence to the private end point
                    attempt.Agent.Peer = netManager.Connect(attempt.Agent.PrivateIP.ToString(), attempt.Agent.PrivatePort);

                    // Remove the NAT connection attempt
                    attempt.OnConnectionStateChanged -= NatConnectionAttempt_OnConnectionStateChanged;
                    connectionAttemptsByAgentId.Remove(attempt.Agent.Id);
                }
                // If the public connection attempt succeeded...
                else if (attempt.PublicConnectionState == ConnectionStates.Connected)
                {
                    // Fail the other connection attempt
                    if (attempt.PrivateConnectionState != ConnectionStates.Failed)
                    {
                        attempt.PrivateConnectionState = ConnectionStates.Failed;
                        attempt.PrivateConnectionMessage.IsCancelled = true; // cancel the request
                    }

                    // Issue a proper connection sequence to the private end point
                    attempt.Agent.Peer = netManager.Connect(attempt.Agent.PublicIP.ToString(), attempt.Agent.PublicPort);

                    // Remove the NAT connection attempt
                    attempt.OnConnectionStateChanged -= NatConnectionAttempt_OnConnectionStateChanged;
                    connectionAttemptsByAgentId.Remove(attempt.Agent.Id);
                }
                // Otherwise if both connection attempts failed, fall back using a relay server
                else if (attempt.PrivateConnectionState == ConnectionStates.Failed && attempt.PublicConnectionState == ConnectionStates.Failed)
                {
                    // Remove the NAT connection attempt
                    attempt.OnConnectionStateChanged -= NatConnectionAttempt_OnConnectionStateChanged;
                    connectionAttemptsByAgentId.Remove(attempt.Agent.Id);

                    // Finalize the connection using the relay server
                    FinalizeConnectToAgent(attempt.Agent, true);
                }
            }
            catch (Exception ex)
            {
                CyLog.LogError(ex);
            }
        }

        #endregion Event Handlers

        #region Connect

        // Handles the connection procedure to an agent
        private void FinalizeConnectToAgent(SessionAgent agent, bool isRelayed, string ip = null, int? port = null)
        {
            if (!isRelayed && (string.IsNullOrEmpty(ip) || port == null))
                throw new ArgumentException("ip and port cannot be nul when isRelayed = false");

            agent.IsRelayed = isRelayed;
            
            if (!isRelayed)
            {
                agent.ConnectedIP = IPAddress.Parse(ip);
                agent.ConnectedPort = (int)port;
            }

            // Ensure this agent exist in local store
            if (!agentsById.ContainsKey(agent.Id)) agentsById.Add(agent.Id, agent);
            if (!agentsByPrivateDataKey.ContainsKey(agent.PrivateAgentKey)) agentsByPrivateDataKey.Add(agent.PrivateAgentKey, agent);
            if (!agentsByPublicDataKey.ContainsKey(agent.PublicAgentKey)) agentsByPublicDataKey.Add(agent.PublicAgentKey, agent);

            CyLog.LogInfoFormat("{0}Connected to agent {1}:{2}, platform:{3}, voiceId:{4}, privateIP:{5}:{6}, publicIP:{7}:{8}",
                        isRelayed ? "[relay] " : "",
                        agent.Id,
                        agent.Name,
                        agent.Platform,
                        agent.VoiceId,
                        agent.PrivateIP,
                        agent.PrivatePort,
                        agent.PublicIP,
                        agent.PublicPort);

            // Remove from "connecting" list
            if (connectionAttemptsByAgentId.ContainsKey(agent.Id)) connectionAttemptsByAgentId.Remove(agent.Id);
            if (connectingAgentsById.ContainsKey(agent.Id)) connectingAgentsById.Remove(agent.Id);

            // If we're not the host and this agent we just connected to is, also connect to them as voice server
            if (UseVoice && !IsHost && agent.IsHost)
            {
                // Assign this agent as the voice host
                VoiceHost = agent;

                // Determine the hostname/port of the voice server to connect to
                var voiceHost = isRelayed ? agent.PublicIP.ToString() : agent.ConnectedIP.ToString();
                var voicePort = isRelayed ? agent.PublicPort : agent.ConnectedPort;
                CyLog.LogInfoFormat("Connecting to voice server {0}:{1}...", voiceHost, voicePort);
                StartVoiceChat(3, voiceHost, voicePort);
            }

            // Notify
            if (OnAgentConnected != null) OnAgentConnected.Invoke(agent);

            SessionsNotifications.GlobalNotify("Connected to Session Host", "Host Connected");

            // Send the current session state to the agent
            SendStateToAgent(agent);
        }

        #endregion Connect

        #region Disconnect

        /// <summary>
        /// Begins the disconnection process from a connected agent.
        /// </summary>
        /// <param name="agent">The agent to disconnect from.</param>
        /// <param name="asRequest">Whether or not to send the disconnection message requiring a response or not.</param>
        public void DisconnectFromAgent(SessionAgent agent, bool asRequest = true)
        {
            if (agent == null) throw new ArgumentNullException("agent");

            if (!agent.IsConnected)
            {
                CyLog.LogInfoFormat("Already disconnected from agent {0}:{1}", agent.Id, agent.Name);
                return;
            }

            // If there is no desire for a response for this disconnection message, mark agent as disconnected
            if (!asRequest) FinalizeDisconnectFromAgent(agent);

            // Send the disconnection message
            var msg = new SessionMessage(MessageTypes.Connection, asRequest ? MessageFlags.Request : MessageFlags.None, "End");
            SendToAgent(agent, msg);
        }

        // Handles the disconnection procedure for an agent
        private void FinalizeDisconnectFromAgent(SessionAgent agent)
        {
            if (!agent.IsConnected)
            {
                CyLog.LogInfoFormat("Already disconnected from agent {0}:{1}", agent.Id, agent.Name);
                return;
            }

            SessionsNotifications.GlobalNotify("Disconnected from " + agent.Name, "Agent Disconnected");
            CyLog.LogInfoFormat("Disconnected from agent {0}:{1}", agent.Id, agent.Name);

            // Remove agent from local lists
            if (agentsById.ContainsKey(agent.Id)) agentsById.Remove(agent.Id);
            if (agentsByPrivateDataKey.ContainsKey(agent.PrivateAgentKey)) agentsByPrivateDataKey.Remove(agent.PrivateAgentKey);
            if (agentsByPublicDataKey.ContainsKey(agent.PublicAgentKey)) agentsByPrivateDataKey.Remove(agent.PublicAgentKey);
            if (connectingAgentsById.ContainsKey(agent.Id)) connectingAgentsById.Remove(agent.Id);

            // Notify
            if (OnAgentDisconnected != null) OnAgentDisconnected.Invoke(agent);
        }

        #endregion Disconnect

        #region Get/Find

        /// <summary>
        /// Gets all agents currently recorded by the client.
        /// </summary>
        /// <returns>A list of available agents.</returns>
        public IEnumerable<SessionAgent> GetAllAgents()
        {
            var list = new List<SessionAgent>();

            foreach (var agent in agentsById.Values)
            {
                if (agent.Id == Self.Id) continue; // ignore self
                list.Add(agent);
            }

            return list;
        }

        /// <summary>
        /// Gets an agent by its ID.
        /// </summary>
        /// <param name="agentId">The ID of the agent to get.</param>
        /// <returns>The agent if found, otherwise NULL.</returns>
        public SessionAgent GetAgentById(Guid agentId)
        {
            if (agentId == AgentId) return Self; // return self if this is own ID
            return agentsById.ContainsKey(agentId) ? agentsById[agentId] : null;
        }

        /// <summary>
        /// Gets an agent representing the current agent.
        /// </summary>
        /// <returns>The current agent.</returns>
        public SessionAgent GetSelfAsAgent()
        {
            var self = new SessionAgent(AgentId, "Self", SessionsSceneManager.GetPlatform(), localIP, UnicastPort, localIP, UnicastPort);
            self.ConnectedIP = localIP;
            self.ConnectedPort = LocalUnicastEP.Port;
            return self;
        }

        /// <summary>
        /// Gets the agent who sent a specific message.
        /// </summary>
        /// <param name="msg">The message to get the agent for.</param>
        /// <returns>The agent if found, otherwise NULL.</returns>
        public SessionAgent GetAgentFromMessage(SessionMessage msg)
        {
            if (msg == null) throw new ArgumentNullException("msg");
            if (msg.Owner != null) return msg.Owner;
            return GetAgentyByKey(msg.AgentKey);
        }

        /// <summary>
        /// Gets an agent given either its public or private agent key.
        /// </summary>
        /// <param name="agentKey">The agent key of the agent to get.</param>
        /// <returns>The agent if it was found, otherwise NULL.</returns>
        public SessionAgent GetAgentyByKey(string agentKey)
        {
            if (agentsByPrivateDataKey.ContainsKey(agentKey)) return agentsByPrivateDataKey[agentKey];
            else if (agentsByPublicDataKey.ContainsKey(agentKey)) return agentsByPublicDataKey[agentKey];
            return null;
        }

        #endregion Get/Find

        #endregion Agents

        #region Sessions

        /// <summary>
        /// Ends the current session, if any.
        /// </summary>
        public void EndSession()
        {
            if (!IsStarted) return;

            if (IsInSession)
            {
                SessionsNotifications.GlobalNotify("Left Session: " + SessionName, "Left Session");
                IsInSession = false;
            }

            // Go through all agents and disconnect
            foreach (var agent in GetAllAgents())
            {
                if (agent.IsConnected || agent.IsRelayed)
                {
                    DisconnectFromAgent(agent);
                }
            }

            // TOOD A proper scene unload/clean-up/voice shutdown

            // Notify
            OnSessionEnded.Invoke(SessionName, "");
        }

        #endregion Sessions

        #region RPC

        #region Message Handlers

        // Handles remote procedure call messages
        private void HandleRpcMessage(RpcMessage msg)
        {
            // If this is a global RPC...
            if (msg.NetworkEntityId == 0)
            {
                // See if an RPC handler has registered against this RPC name and if so call it
                if (rpcCommands.ContainsKey(msg.Name))
                {
                    var command = rpcCommands[msg.Name];
                    var ownerId = msg.Owner != null ? msg.Owner.Id : Guid.Empty;
                    command(ownerId, true, msg.Args, msg.Value);
                }
            }
            // Otherwise this is for the entity manager to deal with...
            else
            {
                OnRpcCommandExecuted.Invoke(msg);
            }
        }

        #endregion Message Handlers

        #region Get

        /// <summary>
        /// Gets an RPC command handler given its command name.
        /// </summary>
        /// <param name="name">The RPC name.</param>
        /// <returns>The command handler if its found, otherwise NULL.</returns>
        public RpcCommand GetRpc(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");
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
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");
            var command = GetRpc(name);
            if (command == null) return;
            float numericVal = value != null ? value.Value : 0;
            command(agentId, isLocal, args, numericVal);

            // If this isn't just local playback, broadcast
            if (!isLocal)
            {
                // Create the RPC message
                var msg = new RpcMessage(MessageFlags.None, name, numericVal, args, null);
                msg.NetworkEntityId = 0; // this is a global RPC
                SendToAll(msg, SendOptions.ReliableOrdered);
            }
        }

        #endregion Call

        #region Register

        /// <summary>
        /// Registers a global remote procedure call command with its command name and the network.
        /// </summary>
        /// <param name="name">The name of the RPC command.</param>
        /// <param name="command">The command handler that will handle the command call.</param>
        public void RegisterRpcCommand(string name, RpcCommand command)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");
            if (command == null) throw new ArgumentNullException("command");

            if (rpcCommands.ContainsKey(name))
            {
                CyLog.LogWarnFormat("RPC command already registered and being overwritten with name: {0}", name);
                rpcCommands[name] = command;
            }
            else
            {
                rpcCommands.Add(name, command);
            }

            CyLog.LogInfoFormat("[RPC] global registered: {0}", name);
        }

        #endregion Register

        #region Unregister

        /// <summary>
        /// Unregisters a global remote procedure call command from its command name and the network.
        /// </summary>
        /// <param name="name">The name of the RPC command.</param>
        public void UnregisterRpcCommand(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

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

        #region Voice

        #region Message Handlers

        // Handles Dissonance voice messages
        private void HandleVoiceMessage(VoiceMessage msg)
        {
            //CyLog.WriteLine("Handling voice messages: {0}", messages.Count);

            if (VoiceChat == null || VoiceChat.CommunicationsNetwork == null) return;

            if (msg.Owner == null)
            {
                var agentKey = msg.AgentKey;

                // Get the agent ID for this key
                var agent = GetAgentyByKey(agentKey);

                // No agent found, assume own packet
                if (agent == null)
                {
                    agent = Self;
                }
                else if (agent.ConnectedIP == null)
                {
                    Debug.LogWarningFormat("No connection IP for: {0}", agentKey);
                    return;
                }

                // Associate the owner who sent this message with it (used by Dissonance)
                msg.Owner = agent;
            }

            // Send to Dissonance for processing
            var destination = (int)msg.Value;

            if (destination == VoiceMessage.CLIENT_VALUE)
            {
                VoiceChat.CommunicationsNetwork.QueueClientVoiceMessages(msg);
            }
            else if (destination == VoiceMessage.SERVER_VALUE)
            {
                VoiceChat.CommunicationsNetwork.QueueServerVoiceMessages(msg);
            }

            // If this is a voice request, send a response
            // TODO Is this necessary?
            if (msg.Flags == MessageFlags.Request && msg.Owner != null)
            {
                float dir = (int)msg.Value == VoiceMessage.CLIENT_VALUE ? VoiceMessage.SERVER_VALUE : VoiceMessage.CLIENT_VALUE;
                var resMsg = new VoiceMessage(MessageFlags.Response, msg.Name, dir);
                resMsg.Id = msg.Id; // important: apply request ID on outgoing response
                resMsg.Channel = UdpChannels.Voice;
                SendToAgent(msg.Owner, resMsg);
            }
        }

        #endregion Message Handlers

        #region Start Voice Chat

        /// <summary>
        /// Starts voice chat service.
        /// </summary>
        /// <param name="delay">An amount of time to delay in seconds, before starting.</param>
        /// <param name="hostname">The host to start voice communications on.</param>
        /// <param name="port">The port to start voice communications on.</param>
        /// <param name="sendPunchThrough">Whether or not to send NAT punch-through data packets to open the connection.</param>
        public void StartVoiceChat(float delay = 0, string hostname = null, int? port = null)
        {
            StartCoroutine(DoStartVoiceChat(delay, hostname, port));
        }

        // Starts voice chat services.
        IEnumerator DoStartVoiceChat(float delay, string hostname, int? port)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);

            // Start voice communication
            if (UseVoice && VoiceChat != null && !VoiceChat.IsVoiceStarted)
            {
                if (IsHost)
                {
                    CyLog.LogInfo("[VOICE] Starting voice host/server...");
                    // If this is the voice chat host server, target self
                    VoiceChat.Host = localIP.ToString();
                    VoiceChat.StartVoiceServer();
                }
                else
                {
                    // Otherwise target the connected machine
                    CyLog.LogInfo("[VOICE] Starting voice client...");
                    if (port != null) VoiceChat.Port = (int)port;
                    VoiceChat.StartVoiceClient(hostname);
                }

                SessionsNotifications.GlobalNotify("Voice Chat Started", "Voice Started");
            }
        }

        #endregion Start Voice Chat

        #region Stop Voice Chat

        /// <summary>
        /// Stops the voice chat service.
        /// </summary>
        public void StopVoiceChat()
        {
            if (!VoiceChat.IsVoiceStarted) return;
            if (VoiceChat != null) VoiceChat.StopVoiceChat();
        }

        #endregion Stop Voice Chat

        #endregion Voice

        #region Utility

        // Returns the current list of waiting actions
        private ICollection<Action> ConsumeWaitingActions()
        {
            var actions = new List<Action>();

            while (actionQueue.Count > 0)
            {
                Action action;

                if (!actionQueue.TryTake(out action))
                {
                    CyLog.LogInfo("[ERROR] while trying to dequeue an action");
                    continue;
                }

                actions.Add(action);
            }

            return actions;
        }

        /// <summary>
        /// Gets the message key for a given message. This is a unique key based on remote endpoint + id + message type + message name.
        /// </summary>
        /// <param name="msg">The message to get the key for.</param>
        /// <param name="agentKey">The optional agent key to apply.</param>
        /// <returns>The message key based on the messages values.</returns>
        public string GetMessageKey(SessionMessage msg, string agentKey = null)
        {
            if (msg == null) throw new ArgumentNullException("msg");
            return (agentKey != null ? agentKey : msg.AgentKey) + ":" + msg.Id.ToString() + ":" + msg.Type.ToString() + ":" + msg.Name;
        }

        #endregion Utility

        #endregion Methods
    }

    /// <summary>
    /// Used to capture changes to sesion values.
    /// </summary>
    [System.Serializable]
    public class SessionValueEvent : UnityEvent<string, float, object>
    { }

    /// <summary>
    /// Custom Unity event class to capture events related to <see cref="SessionAgent">session agents</see>.
    /// </summary>
    [System.Serializable]
    public class SessionAgentEvent : UnityEvent<SessionAgent> { }

    /// <summary>
    /// Custom Unity event class to capture events related to <see cref="SessionAgent">session agents</see>.
    /// </summary>
    [System.Serializable]
    public class SessionAgentNotFoundEvent : UnityEvent<Guid, string> { }

    /// <summary>
    /// Custom Unity event class to capture events related to the Facilitator service.
    /// </summary>
    [System.Serializable]
    public class FacilitatorEvent : UnityEvent { }

    /// <summary>
    /// Custom Unity event class to capture events related to network instantiation.
    /// </summary>
    [System.Serializable]
    public class NetworkInstanceCreatedEvent : UnityEvent<JsonArgsSessionMessage> { }

    /// <summary>
    /// Custom Unity event class to capture events related to network transforms.
    /// </summary>
    [System.Serializable]
    public class NetworkTransformEvent : UnityEvent<TransformMessage> { }

    /// <summary>
    /// Unity event related to changes of a network state machine.
    /// </summary>
    [Serializable]
    public class NetworkStateChangeEvent : UnityEvent<StateMessage> { }

    /// <summary>
    /// Unity event related to RPC commands.
    /// </summary>
    [Serializable]
    public class NetworkRpcCommandEvent : UnityEvent<RpcMessage> { }

    /// <summary>
    /// Unity event related to changes of a network state machine.
    /// </summary>
    [Serializable]
    public class SessionStatusEvent : UnityEvent<string, string> { }

    /// <summary>
    /// Handler for session message processing.
    /// </summary>
    /// <param name="messages">The collection of messages to process.</param>
    public delegate void SessionMessageProcessor(IEnumerable<SessionMessage> messages);
}