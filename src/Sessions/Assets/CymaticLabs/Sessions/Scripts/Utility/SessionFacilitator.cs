using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using LiteNetLib;
using CymaticLabs.Sessions.Core;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Represents the sessions Facilitator service.
    /// </summary>
    public class SessionFacilitator
    {
        #region Constants

        /// <summary>
        /// The default number of latency samples to use to create a rolling average of latency.
        /// </summary>
        public const int DEFAULT_LATENCY_SAMPLES = 64;

        #endregion Constants

        #region Fields

        // The IP address of the service endpoint
        private IPEndPoint endpoint;

        // A list of latency samples used to create an average latency to agent
        private int[] latencySamples;
        private int latencyIndex = 0;

        #endregion Fields

        #region Properties

        /// <summary>
        /// The IP end point of the service.
        /// </summary>
        public IPEndPoint EndPoint
        {
            get { return endpoint; }

            internal set
            {
                if (endpoint == value) return;
                endpoint = value;

                // If the hostname is empty, use the IP address as a string
                if (string.IsNullOrEmpty(Hostname))
                    Hostname = endpoint.Address.ToString();
            }
        }

        /// <summary>
        /// The host name of the service.
        /// </summary>
        public string Hostname { get; internal set; }

        /// <summary>
        /// Whether or not the client is currently connected to the service.
        /// </summary>
        public bool IsConnected
        {
            get { return Peer != null && Peer.ConnectionState == ConnectionState.Connected; }
        }

        /// <summary>
        /// Whether or not the client is currently registered with the service.
        /// </summary>
        public bool IsRegistered { get; internal set; }

        /// <summary>
        /// The underlying network peer for the service.
        /// </summary>
        internal NetPeer Peer { get; set; }

        /// <summary>
        /// Gets the last known latency with the agent in milliseconds.
        /// </summary>
        public int LastLatency { get; private set; }

        /// <summary>
        /// Gets the running average latency to the agent in milliseconds.
        /// </summary>
        public int AverageLatency { get; private set; }

        /// <summary>
        /// Gets or sets the number of latency samples used to create a running average latency.
        /// </summary>
        public byte LatencySamples
        {
            get { return (byte)latencySamples.Length; }

            set
            {
                if (value == 0) throw new ArgumentException("Value must be between 1 and 255");
                if ((byte)latencySamples.Length == value) return;
                latencySamples = new int[value];
                for (var i = 0; i < latencySamples.Length; i++) latencySamples[i] = int.MinValue;
            }
        }

        /// <summary>
        /// Gets the state machine for the service.
        /// </summary>
        public SessionStateMachine States { get; private set; }

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Creates a new representation of the Facilitator service at the given hostname and port.
        /// </summary>
        /// <param name="hostname">The hostname where the service is located.</param>
        /// <param name="port">The port of the service.</param>
        public SessionFacilitator(string hostname = "sessions.cymaticlabs.net", int port = SessionsUdpNetworking.DEFAULT_FACILITATOR_PORT)
        {
            IsRegistered = false;
            Hostname = hostname;
            IPAddress ipAddress;

            // Try to parse the passed hostname as an IP address...
            if (!IPAddress.TryParse(hostname, out ipAddress))
            {
                // If that fails use DNS to convert the hostname to an IP address
                ipAddress = Dns.GetHostAddresses(hostname)[0];
            }

            // Initialize the service endpoint
            EndPoint = new IPEndPoint(ipAddress, port);

            // Prepare the latency samples
            latencySamples = new int[DEFAULT_LATENCY_SAMPLES];
            for (var i = 0; i < latencySamples.Length; i++) latencySamples[i] = int.MinValue;

            // Initialize the local state machine and states to manage the connection life cycle with the service
            InitializeStates();
        }

        #endregion Constructors

        #region Methods

        #region States

        // Initializes the local states/state machine
        private void InitializeStates()
        {
            States = new SessionStateMachine();
            States.Owner = this;

            // Build and configure various states associated with the service

            #region Disconnected

            var disconnected = new CustomSessionState("Disconnected");
            States.Add(disconnected);

            // Define the "enter" behavior
            disconnected.EnterHandler = (time, force) =>
            {
                // We are not unregistered with the service
                IsRegistered = false;
                return true;
            };

            #endregion Disconnected

            #region Connecting

            var connecting = new CustomSessionState("Connecting", true);
            States.Add(connecting);

            // Define the "enter" behavior
            connecting.EnterHandler = (time, force) =>
            {
                // We are not unregistered with the service
                IsRegistered = false;
                return true;
            };

            // Handle state update routine: if we lose connection, move to disconnected state, if we establish it, move to connected state
            connecting.UpdateHandler = (time) =>
            {
                if (Peer != null)
                {
                    // If we have successfully connected, enter the connected state
                    if (Peer.ConnectionState == ConnectionState.Connected)
                    {
                        States.ExitState("Connecting");
                        States.EnterState("Connected");
                    }
                    // Otherwise if we disconnected, the attempt failed
                    else if (Peer.ConnectionState == ConnectionState.Disconnected)
                    {
                        States.ExitState("Connecting");
                        States.EnterState("Disconnected");
                    }
                }

                return true;
            };

            #endregion Connecting

            #region Connected

            var connected = new CustomSessionState("Connected", true);
            States.Add(connected);

            // Define the "enter" behavior
            connected.EnterHandler = (time, force) =>
            {
                // We are not registered with the service
                IsRegistered = false;
                return true;
            };

            // Handle state update routine: if we lose connection, exit state
            connected.UpdateHandler = (time) =>
            {
                if (Peer != null && Peer.ConnectionState != ConnectionState.Connected) return false;
                return true;
            };

            #endregion Connected

            #region Registering

            var registering = new CustomSessionState("Registering");
            States.Add(registering);

            // Define the "enter" behavior
            registering.EnterHandler = (time, force) =>
            {
                // We are not unregistered with the service
                IsRegistered = false;
                return true;
            };

            #endregion Registering

            #region Registered

            var registered= new CustomSessionState("Registered", true);
            States.Add(registered);

            // Define the "enter" behavior
            registered.EnterHandler = (time, force) =>
            {
                // We are not unregistered with the service
                IsRegistered = true;
                return true;
            };

            // Handle state update routine: if we lose connection, exit state
            registered.UpdateHandler = (time) =>
            {
                if (Peer != null && Peer.ConnectionState != ConnectionState.Connected) return false;
                return true;
            };

            #endregion Registering

            #region Disconnecting

            var disconnecting = new CustomSessionState("Disconnecting");
            States.Add(disconnecting);

            // Define the "enter" behavior
            disconnecting.EnterHandler = (time, force) =>
            {
                // We are not unregistered with the service
                IsRegistered = false;
                return true;
            };

            #endregion Disconnecting

            // Enter the initial state of "Disconnected"
            States.EnterState("Disconnected");
        }

        #endregion States

        #region Latency

        /// <summary>
        /// Adds a new latency sample to the running latency average.
        /// </summary>
        /// <param name="latencyMs"></param>
        internal void AddLatencySample(int latencyMs)
        {
            // Assign the last known latency sample
            LastLatency = latencyMs;

            // Increment/roll the sample in the sample nuffer
            if (++latencyIndex > latencySamples.Length - 1) latencyIndex = 0;

            // Add the sample
            latencySamples[latencyIndex] = latencyMs;

            // Update the running average
            var totalLatency = 0;
            var totalCounted = 0;

            foreach (var value in latencySamples)
            {
                // Uninitialized samples will be int.MinValue so ignore
                if (value == int.MinValue) continue;
                totalCounted++;
                totalLatency += value;
            }

            AverageLatency = totalLatency / totalCounted;
        }

        #endregion Latency

        #endregion Methods
    }
}
