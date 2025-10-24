using System;
using System.Collections.Generic;
using UnityEngine;

using CymaticLabs.Sessions.Core;
using CymaticLabs.Logging;

using Dissonance;
using Dissonance.Datastructures;
using Dissonance.Networking;
using Dissonance.Extensions;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Integration of Dissonance communications server on top of Sessions networking.
    /// </summary>
    public class SessionsVoiceCommsNetwork : BaseCommsNetwork<SessionsVoiceServer, SessionsVoiceClient, Guid, ClientConnectionDetails, ServerConnectionDetails>
    {
        #region Inspector

        /// <summary>
        /// The Sessions UDP networking instance to use.
        /// </summary>
        public SessionsUdpNetworking SessionNetworking;

        /// <summary>
        /// The server address to use for voice chat.
        /// </summary>
        public string ServerAddress = "127.0.0.1";

        /// <summary>
        /// The port to use for voice chat.
        /// </summary>
        public int Port = 9000;

        #endregion Inspector

        #region Fields

        // An internal queue of waiting client voice packets wrapped in <see cref="VoiceMessage">voice messages</see>.
        private Queue<VoiceMessage> queuedClientMessages;

        // An internal queue of waiting server voice packets wrapped in <see cref="VoiceMessage">voice messages</see>.
        private Queue<VoiceMessage> queuedServerMessages;

        // Used to pool/queue loopback packets
        private readonly ConcurrentPool<byte[]> inProcBuffers = new ConcurrentPool<byte[]>(8, () => new byte[1024]);
        private readonly List<ArraySegment<byte>> inProcQueue = new List<ArraySegment<byte>>();

        // The underlying Dissonance communications network
        private DissonanceComms dissonance;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static SessionsVoiceCommsNetwork Current { get; private set; }

        /// <summary>
        /// Gets the local Dissonance player ID of the current player.
        /// </summary>
        public static string LocalPlayerId
        {
            get
            {
                if (Current == null) return null;
                if (Current.dissonance == null) Current.dissonance = FindObjectOfType<DissonanceComms>();
                return Current.dissonance != null ? Current.dissonance.LocalPlayerName : null;
            }
        }

        /// <summary>
        /// Whether or not the voice client is connected.
        /// </summary>
        public bool IsClientConnected
        {
            get
            {
                return Client != null ? Client.IsConnected : false;
            }
        }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            Current = this;
            queuedClientMessages = new Queue<VoiceMessage>();
            queuedServerMessages = new Queue<VoiceMessage>();
        }

        private void Start()
        {
            if (SessionNetworking == null) Debug.LogWarning("Sessions Networking reference is NULL; voice comms will not work properly.");
        }

        #endregion Init

        #region Operation

        //protected override void Initialize()
        //{
        //}

        //private void OnDestroy()
        //{
        //}

        /// <summary>
        /// Launches Dissonance in dedicated server mode.
        /// </summary>
        /// <remarks>In this mode Dissonance has no local client and only acts as a voice server.</remarks>
        public void InitializeAsDedicatedServer()
        {
            RunAsDedicatedServer(new ServerConnectionDetails
            {
                Port = Port
            });
        }

        /// <summary>
        /// Launches Dissonance in non-dedicated server mode.
        /// </summary>
        /// <remarks>
        /// In this mode Dissonance launches as a server, but also a local client.
        /// This means that it can both act as the voice server, but also a participating
        /// voice client from the same instance/process.
        /// </remarks>
        public void InitializeAsServer()
        {
            RunAsHost(
                new ServerConnectionDetails
                {
                    Port = Port
                },
                new ClientConnectionDetails
                {
                    Address = ServerAddress,
                    Port = Port
                }
            );
        }

        /// <summary>
        /// Launches Dissonance in client mode.
        /// </summary>
        /// <remarks>This launches a voice client only, and no matching server is launched.</remarks>
        /// <param name="serverAddress">The address of the voice server to connect to.</param>
        public void InitializeAsClient(string serverAddress)
        {
            if (serverAddress.Equals("localhost", StringComparison.InvariantCultureIgnoreCase))
                serverAddress = "127.0.0.1";

            ServerAddress = serverAddress;

            RunAsClient(new ClientConnectionDetails
            {
                Address = ServerAddress,
                Port = Port
            });
        }

        /// <summary>
        /// Disconnects the client.
        /// </summary>
        public void DisableClient()
        {
            if (Client != null && Client.IsConnected) Client.Disconnect();
            CyLog.LogInfo("Voice client diabled");
        }

        /// <summary>
        /// Enables the client.
        /// </summary>
        public void EnableClient()
        {
            if (Client != null && !Client.IsConnected) Client.Connect();
            CyLog.LogInfo("Voice client enabled");
        }

        #endregion Operation

        #region Update

        protected override void Update()
        {
            // Send over queued client in-proc messages
            for (var i = 0; i < inProcQueue.Count; i++)
            {
                if (Client != null)
                    Client.NetworkReceivedPacket(inProcQueue[i]);

                // Recycle the packet into the pool of byte buffers
                inProcBuffers.Put(inProcQueue[i].Array);
            }

            // Clear the queue
            inProcQueue.Clear();

            base.Update();
        }

        #endregion Update

        #region Queuing

        /// <summary>
        /// Queues client voice messages for processing.
        /// </summary>
        /// <param name="messages">The messages to process.</param>
        public void QueueClientVoiceMessages(VoiceMessage message)
        {
            queuedClientMessages.Enqueue(message);
        }

        /// <summary>
        /// Processes a client voice message originating from in-process.
        /// </summary>
        /// <param name="agentId">The ID of the agent to queue the client message for.</param>
        /// <param name="packet">The message to queue.</param>
        internal void ProcessInProcClientVoiceMessage(Guid agentId, ArraySegment<byte> packet)
        {
            // Queue the client message for processing
            inProcQueue.Add(packet.CopyTo(inProcBuffers.Get()));
        }

        /// <summary>
        /// Queues server voice messages for processing.
        /// </summary>
        /// <param name="messages">The messages to process.</param>
        public void QueueServerVoiceMessages(VoiceMessage message)
        {
            queuedServerMessages.Enqueue(message);
        }

        /// <summary>
        /// Processes a server voice message originating from in-process.
        /// </summary>
        /// <param name="packet">The message to queue.</param>
        internal void ProcessInProcServerVoiceMessage(ArraySegment<byte> packet)
        {
            if (Server == null) return;

            // Pass directly to the server for processing
            Server.NetworkReceivedPacket(SessionNetworking.AgentId, packet);
        }

        /// <summary>
        /// Consumes the waiting client voice messages from the queue.
        /// </summary>
        /// <returns></returns>
        public ICollection<VoiceMessage> ConsumeWaitingClientMessages()
        {
            var list = queuedClientMessages.ToArray();
            queuedClientMessages.Clear();
            return list;  
        }

        /// <summary>
        /// Consumes the waiting server voice messages from the queue.
        /// </summary>
        /// <returns></returns>
        public ICollection<VoiceMessage> ConsumeWaitingServerMessages()
        {
            var list = queuedServerMessages.ToArray();
            queuedServerMessages.Clear();
            return list;
        }

        #endregion Queuing

        #region Client

        /// <summary>
        /// Creates an instance of a voice client.
        /// </summary>
        /// <param name="connectionParameters">The connection details to use when creating the client.</param>
        /// <returns>The client instance using the specified connection details.</returns>
        protected override SessionsVoiceClient CreateClient([CanBeNull] ClientConnectionDetails connectionParameters)
        {
            return new SessionsVoiceClient(this, connectionParameters);
        }

        #endregion Client

        #region Server

        /// <summary>
        /// Creates an instance of a voice server.
        /// </summary>
        /// <param name="connectionParameters">The connection details to use when creating the server.</param>
        /// <returns>The server instance using the specified connection details.</returns>
        protected override SessionsVoiceServer CreateServer([CanBeNull] ServerConnectionDetails connectionParameters)
        {
            return new SessionsVoiceServer(this, connectionParameters);
        }

        #endregion Server

        #endregion Methods
    }

    /// <summary>
    /// Custom class used to store client connection details.
    /// </summary>
    public struct ClientConnectionDetails
    {
        public string Address { get; set; }
        public int Port { get; set; }
    }

    /// <summary>
    /// Custom class used to store server connection details.
    /// </summary>
    public struct ServerConnectionDetails
    {
        public int Port { get; set; }
    }
}
