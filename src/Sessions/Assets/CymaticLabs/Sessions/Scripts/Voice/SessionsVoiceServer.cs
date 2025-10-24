using System;
using System.Collections.Generic;
using CymaticLabs.Sessions.Core;
using Dissonance;
using Dissonance.Networking;

namespace CymaticLabs.Sessions.Unity3d
{ 
    public class SessionsVoiceServer : BaseServer<SessionsVoiceServer, SessionsVoiceClient, Guid>
    {
        #region Fields

        #endregion Fields

        #region Properties

        /// <summary>
        /// The network used by the server.
        /// </summary>
        public SessionsVoiceCommsNetwork Network { get; private set; }

        /// <summary>
        /// The connection information associated with this server.
        /// </summary>
        public ServerConnectionDetails Connection { get; private set; }

        /// <summary>
        /// Gets the static singleton instance of the server.
        /// </summary>
        public static SessionsVoiceServer Current { get; private set; }

        #endregion Properties

        #region Constructors

        public SessionsVoiceServer(SessionsVoiceCommsNetwork network, ServerConnectionDetails connection)
        {
            if (network == null) throw new ArgumentNullException("network");

            Current = this;
            Network = network;
            Connection = Connection;
            UnityEngine.Debug.Log("SessionsVoiceServer created");
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Formally disconnects a client from Dissonance.
        /// </summary>
        /// <param name="agentId">The agent ID of the client to disconnect.</param>
        internal void DisconnectClient(Guid agentId)
        {
            ClientDisconnected(agentId);
        }

        protected override void ReadMessages()
        {
            if (Network == null) return;

            // Get the current queue of voice data
            var messages = Network.ConsumeWaitingServerMessages();

            if (messages == null || messages.Count == 0) return;

            // Process each packet
            foreach (var msg in messages)
            {
                // If the packet buffer or owner are missing...
                if (msg.PacketBuffer == null || msg.Owner == null) continue; // no packet buffer for some reason, so skip

                var data = new ArraySegment<byte>(msg.PacketBuffer);
                NetworkReceivedPacket(msg.Owner.Id, data); // send into Dissonance for processing
            }
        }

        public override void SendUnreliable([NotNull] List<Guid> connections, ArraySegment<byte> packet)
        {
            base.SendUnreliable(connections, packet);
        }

        protected override void SendUnreliable(Guid connection, ArraySegment<byte> packet)
        {
            var sessions = Network.SessionNetworking;
            
            // Check to see if this is an in-process packet and if so avoid sending it over loopback and queue directly in-memory for processing
            if (SessionsVoiceClient.Current != null && connection == sessions.AgentId)
            {
                // Queue directly in-process for consumption by the server
                Network.ProcessInProcClientVoiceMessage(connection, packet);
                return;
            }

            // Get the agent for this message
            var agent = connection == sessions.AgentId || connection == Guid.Empty ? sessions.Self : sessions.GetAgentById(connection);

            // Ensure we are connected before sending any messages
            if (!agent.IsConnected) return;

            // Otherwise send the voice message over the network
            // Create a new voice data message
            var msg = new VoiceMessage(MessageFlags.None, "Data", VoiceMessage.CLIENT_VALUE); // destination: client
            msg.Channel = UdpChannels.Voice;

            // Copy the raw data into the packet buffer of the message
            msg.PacketBuffer = new byte[packet.Count];
            Array.Copy(packet.Array, packet.Offset, msg.PacketBuffer, 0, packet.Count);

            // If this is a client/server combo and this is from the localhost
            Network.SessionNetworking.SendToAgent(agent, msg);
        }

        public override void SendReliable([NotNull] List<Guid> connections, ArraySegment<byte> packet)
        {
            base.SendReliable(connections, packet);
        }

        protected override void SendReliable(Guid connection, ArraySegment<byte> packet)
        {
            var sessions = Network.SessionNetworking;

            // Check to see if this is an in-process packet and if so avoid sending it over loopback and queue directly in-memory for processing
            if (SessionsVoiceClient.Current != null && connection == sessions.AgentId)
            {
                // Queue directly in-process for consumption by the server
                Network.ProcessInProcClientVoiceMessage(connection, packet);
                return;
            }

            var agent = connection == sessions.AgentId || connection == Guid.Empty ? sessions.Self : sessions.GetAgentById(connection);

            // Ensure we are connected before sending any messages
            if (agent == null || !agent.IsConnected) return;

            // Read the type of packet
            DisonnanceMessageTypes header;
            var reader = new PacketReader(packet);
            var cmdName = "Command";

            // Base the message name on this packet name
            if (reader.ReadPacketHeader(out header)) cmdName = header.ToString();

            // Create a new voice data message
            var msg = new VoiceMessage(MessageFlags.None, cmdName, VoiceMessage.CLIENT_VALUE); // destination: client
            msg.Channel = UdpChannels.Voice;

            // Copy the raw data into the packet buffer of the message
            msg.PacketBuffer = new byte[packet.Count];
            Array.Copy(packet.Array, packet.Offset, msg.PacketBuffer, 0, packet.Count);

            Network.SessionNetworking.SendToAgent(agent, msg, LiteNetLib.SendOptions.ReliableOrdered);
        }

        #endregion Methods
    }
}
