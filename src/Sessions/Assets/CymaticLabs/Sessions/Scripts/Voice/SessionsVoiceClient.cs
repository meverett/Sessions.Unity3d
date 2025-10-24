using System;
using System.Collections.Generic;
using CymaticLabs.Sessions.Core;
using Dissonance;
using Dissonance.Networking;

namespace CymaticLabs.Sessions.Unity3d
{
    public class SessionsVoiceClient : BaseClient<SessionsVoiceServer, SessionsVoiceClient, Guid>
    {
        #region Fields

        private List<Guid> tmpDestinations;

        #endregion Fields

        #region Properties

        /// <summary>
        /// The sessions voice communications used by the client.
        /// </summary>
        public SessionsVoiceCommsNetwork Network { get; private set; }

        /// <summary>
        /// The connection information associated with this client.
        /// </summary>
        public ClientConnectionDetails Connection { get; private set; }

        /// <summary>
        /// The static singleton instance of the client.
        /// </summary>
        public static SessionsVoiceClient Current { get; private set; }

        #endregion Properties

        #region Constructors

        public SessionsVoiceClient(SessionsVoiceCommsNetwork network, ClientConnectionDetails connection)
            : base(network)
        {
            Current = this;
            Network = network;
            Connection = connection;
            tmpDestinations = new List<Guid>();
            UnityEngine.Debug.Log("SessionsVoiceClient created");
        }

        #endregion Constructors

        #region Methods

        public override void Connect()
        {
            Connected();
        }

        public override void Disconnect()
        {
            base.Disconnect();
        }

        /// <summary>
        /// Consumes all waiting client messages to read.
        /// </summary>
        protected override void ReadMessages()
        {
            if (Network == null) return;

            // Get the current queue of voice data
            var messages = Network.ConsumeWaitingClientMessages();
            if (messages == null || messages.Count == 0) return;

            // Process each packet
            foreach (var msg in messages)
            {
                // If the packet buffer or owner are missing...
                if (msg.PacketBuffer == null || msg.Owner == null)
                {
                    UnityEngine.Debug.LogFormat("Something was null... {0} vs. {1}", msg.PacketBuffer != null, msg.Owner != null);
                    continue; // no packet buffer for some reason, so skip
                }

                var data = new ArraySegment<byte>(msg.PacketBuffer);
                var id = NetworkReceivedPacket(data); // send into Dissonance for processing

                // Attempt to connect peer-to-peer when possible
                if (id.HasValue)
                    ReceiveHandshakeP2P(id.Value, msg.Owner.Id);
            }
        }

        /// <summary>
        /// Sends an unreliable packet.
        /// </summary>
        /// <param name="packet">The packet to send.</param>
        protected override void SendUnreliable(ArraySegment<byte> packet)
        {
            // If this is an inner-process server/client, pass the messages directly to the server and avoid loopback
            if (SessionsVoiceServer.Current != null)
            {
                Network.ProcessInProcServerVoiceMessage(packet);
                return;
            }

            // Get the voice server agent
            var voiceAgent = Network.SessionNetworking.VoiceHost;

            // Create a new voice data message
            var msg = new VoiceMessage(MessageFlags.None, "Data", VoiceMessage.SERVER_VALUE); // destination: server
            msg.Channel = UdpChannels.Voice;

            // Copy the raw data into the packet buffer of the message
            msg.PacketBuffer = new byte[packet.Count];
            Array.Copy(packet.Array, packet.Offset, msg.PacketBuffer, 0, packet.Count);
            Network.SessionNetworking.SendToAgent(voiceAgent, msg, LiteNetLib.SendOptions.Unreliable);
        }

        /// <summary>
        /// Sends a reliable packet.
        /// </summary>
        /// <param name="packet">The packet to send.</param>
        protected override void SendReliable(ArraySegment<byte> packet)
        {
            // If this is an inner-process server/client, pass the messages directly to the server and avoid loopback
            if (SessionsVoiceServer.Current != null)
            {
                Network.ProcessInProcServerVoiceMessage(packet);
                return;
            }

            // Otherwise create and send the network message
            // Get the voice server agent
            var voiceAgent = Network.SessionNetworking.VoiceHost;

            // Read the type of packet
            DisonnanceMessageTypes header;
            var reader = new PacketReader(packet);
            var cmdName = "Command";

            // Base the message name on this packet name
            if (reader.ReadPacketHeader(out header)) cmdName = header.ToString();

            // Create a new voice data message
            var msg = new VoiceMessage(MessageFlags.None, cmdName, VoiceMessage.SERVER_VALUE); // destination: server
            msg.Channel = UdpChannels.Voice;

            // Copy the raw data into the packet buffer of the message
            msg.PacketBuffer = new byte[packet.Count];
            Array.Copy(packet.Array, packet.Offset, msg.PacketBuffer, 0, packet.Count);
            Network.SessionNetworking.SendToAgent(voiceAgent, msg, LiteNetLib.SendOptions.ReliableOrdered);
        }

        private void SendP2P([NotNull] IList<ClientInfo<Guid?>> destinations, ArraySegment<byte> packet, bool reliable)
        {
            //Build list of destinations and remove peers we can send to directly from the list
            for (var i = destinations.Count - 1; i >= 0; i--)
            {
                if (destinations[i].Connection.HasValue)
                {
                    tmpDestinations.Add(destinations[i].Connection.Value);
                    destinations.RemoveAt(i);
                }
            }

            if (tmpDestinations.Count > 0)
            {
                // Read the type of packet
                DisonnanceMessageTypes header;
                var reader = new PacketReader(packet);
                var cmdName = reliable ? "Command" : "Data";

                // Base the message name on this packet name
                if (reader.ReadPacketHeader(out header)) cmdName = header.ToString();

                // Create a new voice data message
                var msg = new VoiceMessage(MessageFlags.None, cmdName, VoiceMessage.CLIENT_VALUE); // destination: client/peer
                msg.Channel = UdpChannels.Voice;
                var sendOptions = reliable ? LiteNetLib.SendOptions.ReliableOrdered : LiteNetLib.SendOptions.Unreliable;

                // Copy the raw data into the packet buffer of the message
                msg.PacketBuffer = new byte[packet.Count];
                Array.Copy(packet.Array, packet.Offset, msg.PacketBuffer, 0, packet.Count);

                // Go through each peer and send directly to them
                foreach (var destination in tmpDestinations.ToArray())
                {
                    var agent = Network.SessionNetworking.GetAgentById(destination);
                    if (agent == null) continue; // ? bad agent ID ?
                    Network.SessionNetworking.SendToAgent(agent, msg, sendOptions);
                }

                tmpDestinations.Clear();
            }
        }

        protected override void SendReliableP2P(List<ClientInfo<Guid?>> destinations, ArraySegment<byte> packet)
        {
            SendP2P(destinations, packet, true);

            //Call base to send to all the rest of the peers via server relay
            base.SendReliableP2P(destinations, packet);
        }

        protected override void SendUnreliableP2P(List<ClientInfo<Guid?>> destinations, ArraySegment<byte> packet)
        {
            SendP2P(destinations, packet, false);

            //Call base to send to all the rest of the peers via server relay
            base.SendReliableP2P(destinations, packet);
        }

        protected override void OnServerAssignedSessionId(uint session, ushort id)
        {
            base.OnServerAssignedSessionId(session, id);

            //Broadcast a handshake to everyone else
            var buffer = WriteHandshakeP2P(session, id);
            var sessions = Network.SessionNetworking;

            // Read the type of packet
            DisonnanceMessageTypes header;
            var reader = new PacketReader(buffer);
            var cmdName = "Command";

            // Base the message name on this packet name
            if (reader.ReadPacketHeader(out header)) cmdName = header.ToString();

            // Create a new voice data message
            var msg = new VoiceMessage(MessageFlags.None, cmdName, VoiceMessage.CLIENT_VALUE); // destination: client/peer
            msg.Channel = UdpChannels.Voice;
            var sendOptions = LiteNetLib.SendOptions.ReliableOrdered;

            // Copy the raw data into the packet buffer of the message
            msg.PacketBuffer = new byte[buffer.Length];
            Array.Copy(buffer, 0, msg.PacketBuffer, 0, buffer.Length);

            // Send a hand shake to all agents
            foreach (var agent in sessions.GetAllAgents())
            {
                sessions.SendToAgent(agent, msg, sendOptions);
            }
        }

        #endregion Methods
    }
}
