using System;
using System.Collections.Generic;
using System.Threading;

using CymaticLabs.Logging;

using Rug.Osc;
using RugOscReceiver = Rug.Osc.OscReceiver;
using RugOscMessage = Rug.Osc.OscMessage;
using RugOscPacket = Rug.Osc.OscPacket;

namespace CymaticLabs.Protocols.Osc
{
    /// <summary>
    /// Receives OSC messages on a given port and raises events based on
    /// the received messages.
    /// </summary>
    public class OscReceiver
    {
        #region Fields

        // The receiver's name
        string name;

        // The underlying OSC message receiver
        RugOscReceiver receiver;

        // The thread used to listen for OSC messages on
        Thread thread;

        // A list of subscribed handlers that want to be notified for messages for a given address.
        Dictionary<string, List<OscMessageReceivedEventHandler>> subscriptions;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets or sets the name of the DMX sender.
        /// </summary>
        public string Name
        {
            get { return name; }

            set
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentNullException("Name");
                name = value;
            }
        }

        /// <summary>
        /// Gets the port number that will be used when listening for OSC messages.
        /// </summary>
        public int Port { get; private set; }

        /// <summary>
        /// Gets whether or not the receiver has started.
        /// </summary>
        public bool IsStarted { get; private set; }

        #endregion Properties

        #region Events

        /// <summary>
        /// Occurs when any OSC message is received.
        /// </summary>
        public event OscMessageReceivedEventHandler MessageReceived;

        #endregion Events

        #region Constructors

        /// <summary>
        /// Creates a new OSC receiver with a random name and the default port of 1337.
        /// </summary>
        /// <param name="port">The port to listen for OSC messages on.</param>
        public OscReceiver()
            : this("OSC Receiver " + Guid.NewGuid().ToString(), 1337)
        {
        }

        /// <summary>
        /// Creates a new OSC receiver listening locally on the given port number.
        /// </summary>
        /// <param name="port">The port to listen for OSC messages on.</param>
        public OscReceiver(int port) 
            : this("OSC Receiver " + Guid.NewGuid().ToString(), port)
        {
        }

        /// <summary>
        /// Creates a new OSC named receiver listening locally on the given port number.
        /// </summary>
        /// <param name="name">The receiver's name.</param>
        /// <param name="port">The port to listen for OSC messages on.</param>
        public OscReceiver(string name, int port)
        {
            Name = name;
            Port = port;
            subscriptions = new Dictionary<string, List<OscMessageReceivedEventHandler>>();
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Starts OSC services.
        /// </summary>
        /// <returns>True if the operation succeedes, False if not.</returns>
        public bool Start()
        {
            if (IsStarted) return true;

            // Create the underlying OSC receiver
            receiver = new RugOscReceiver(Port);

            // Create a thread to do the listening
            thread = new Thread(new ThreadStart(ListenLoop));
            //thread.Priority = ThreadPriority.Highest;
            thread.IsBackground = true;

            // Connect the receiver
            receiver.Connect();

            // Start the listen thread
            thread.Start();

            IsStarted = true;

            CyLog.LogInfo("Started OSC receiver on port " + Port);

            return true;
        }

        /// <summary>
        /// Stops OSC services.
        /// </summary>
        /// <returns>True if the operation succeedes, False if not.</returns>
        public bool Stop()
        {
            if (!IsStarted) return true;

            // close the Reciver 
            try
            {
                receiver.Close();
                receiver.Dispose();
            }
            finally
            {
                receiver = null;
            }

            // Wait for the listen thread to exit
            thread.Join();

            IsStarted = false;

            CyLog.LogInfo("Stopped OSC receiver on port " + Port);

            return true;
        }

        // Main receiving thread message loop
        void ListenLoop()
        {
            try
            {
                while (receiver.State != OscSocketState.Closed)
                {
                    // if we are in a state to recieve
                    if (receiver.State == OscSocketState.Connected)
                    {
                        // get the next message 
                        // this will block until one arrives or the socket is closed
                        RugOscPacket packet = receiver.Receive();

                        // Write the packet to the console 
                        //CyLog.WriteLine(packet.ToString());

                        // Get the message
                        if (!(packet is RugOscMessage)) continue;
                        var rugMsg = (RugOscMessage)packet;
                        var message = new OscMessage(rugMsg.Address, rugMsg.ToArray(), rugMsg.IsEmpty, rugMsg.Count, rugMsg.Origin, rugMsg.SizeInBytes, rugMsg);

                        // Notify globally
                        if (MessageReceived != null) MessageReceived(message);

                        // Notify subscriptions
                        if (subscriptions.ContainsKey(message.Address))
                        {
                            var handlers = subscriptions[message.Address];
                            for (int i = 0; i < handlers.Count; i++) handlers[i](message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // if the socket was connected when this happens
                // then tell the user
                if (receiver != null && receiver.State == OscSocketState.Connected)
                {
                    CyLog.LogInfo("Exception in OSC receiving loop");
                    CyLog.LogInfo(ex.Message);
                }
            }
        }

        /// <summary>
        /// Subscribes an OSC message handler to be notified when an OSC message on a particular address
        /// has been received.
        /// </summary>
        /// <param name="address">The OSC address to listen for messages on.</param>
        /// <param name="handler">The handler that will handle the message.</param>
        public void Subscribe(string address, OscMessageReceivedEventHandler handler)
        {
            if (string.IsNullOrEmpty(address)) throw new ArgumentNullException("address");
            if (handler == null) throw new ArgumentNullException("handler");

            // Make sure a list for the current address exists
            if (!subscriptions.ContainsKey(address)) subscriptions.Add(address, new List<OscMessageReceivedEventHandler>());
            subscriptions[address].Add(handler);
        }

        /// <summary>
        /// Unsubscribes an OSC message handler from being notified when an OSC message on a particular address
        /// has been received.
        /// </summary>
        /// <param name="address">The OSC address to stop listening for messages on.</param>
        /// <param name="handler">The handler to ubsubscribe from the address.</param>
        public void Unsubscribe(string address, OscMessageReceivedEventHandler handler)
        {
            if (string.IsNullOrEmpty(address)) throw new ArgumentNullException("address");
            if (handler == null) throw new ArgumentNullException("handler");

            // Make sure a list for the current address exists
            subscriptions[address].Remove(handler);

            // Clean up empty list
            if (subscriptions[address].Count == 0) subscriptions.Remove(address);
        }

        #endregion Methods
    }
}
