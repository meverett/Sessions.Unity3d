using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Diagnostics;

using CymaticLabs.Logging;

using Rug.Osc;
using RugOscSender = Rug.Osc.OscSender;
using RugOscMessage = Rug.Osc.OscMessage;
using RugOscPacket = Rug.Osc.OscPacket;

namespace CymaticLabs.Protocols.Osc
{
    /// <summary>
    /// Sends OSC messages on a particular network device and port.
    /// </summary>
    public class OscSender
    {
        #region Fields

        // The sender's name
        string name;

        // The IP address of the computer to send OSC messages to
        IPAddress ipAddress;

        // The port to use for sending OSC messages
        int port;

        // The underlying OSC message sender
        RugOscSender sender;

        // The thread used to send OSC messages
        Thread thread;

        /// <summary>
        /// OSC messages waiting to be send out.
        /// </summary>
        Queue<RugOscMessage> messages;

        // For thread sync
        object messageLock = new object();
        //object sendLock = new object();

        // Used to time operations
        Stopwatch stopWatch;

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
        /// Gets or sets the IP address of the sender.
        /// </summary>
        public string IpAddress
        {
            get { return ipAddress.ToString(); }

            set
            {
                if (IsStarted) throw new InvalidOperationException("cannot change IpAddress while running");
                if (string.IsNullOrEmpty(value)) throw new ArgumentNullException("IpAddress");

                if (value == "127.0.0.1" || value == "localhost")
                {
                    ipAddress = IPAddress.Loopback;
                }
                else
                {
                    ipAddress = System.Net.IPAddress.Parse(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the port number that will be used when sending for OSC messages.
        /// </summary>
        public int Port
        {
            get { return port; }

            set
            {
                if (port != value)
                {
                    if (value < 1) throw new ArgumentException("Port must be greater than 0");
                    port = value;
                }
            }
        }

        /// <summary>
        /// Gets whether or not the receiver has started.
        /// </summary>
        public bool IsStarted { get; private set; }

        #endregion Properties

        #region Constructors

        public OscSender()
            : this("OSC Sender " + Guid.NewGuid().ToString(), "127.0.0.1", 2345)
        { }

        public OscSender(string name)
            : this(name, "127.0.0.1", 2345)
        { }

        public OscSender(string name, string ipAddress, int port)
        {
            Name = name;
            IpAddress = ipAddress;
            Port = port;
            messages = new Queue<RugOscMessage>();
            stopWatch = new Stopwatch();
        }

        #endregion Constructors

        #region Methods

        #region Start/Stop

        /// <summary>
        /// Starts the OSC message sender.
        /// </summary>
        /// <returns>True if the operation succeedes, False if not.</returns>
        public bool Start()
        {
            if (IsStarted) return true;

            // Create and connect the sender
            sender = new RugOscSender(ipAddress, 0, port); // 0 for local port so it will self-assign an available port

            // Create a thread to do the listening
            thread = new Thread(new ThreadStart(SendLoop));
            thread.Priority = ThreadPriority.Highest;

            try
            {
                sender.Connect();
            }
            catch (Exception ex)
            {
                CyLog.LogInfo(ex.Message);
                return false;
            }

            // Start the sending thread
            thread.Start();

            IsStarted = true;
            CyLog.LogInfoFormat("{0} started", Name);
            return true;
        }

        /// <summary>
        /// Stops the OSC message sender.
        /// </summary>
        /// <returns>True if the operation succeedes, False if not.</returns>
        public bool Stop()
        {
            if (!IsStarted) return true;

            try
            {
                sender.Close();
                sender.Dispose();
            }
            finally
            {
                sender = null;
            }

            // Wait for the listen thread to exit
            thread.Join();

            IsStarted = false;
            CyLog.LogInfoFormat("{0} stopped", Name);
            return false;
        }

        #endregion Start/Stop

        #region Send

        /// <summary>
        /// Sends a signal to a specific OSC channel given its index.
        /// </summary>
        /// <param name="channel">The index of the channel to send to.</param>
        /// <param name="value">The value to send to the channel.</param>
        public void Send(int channel, float value)
        {
            //CyLog.WriteLine("OSC {0} - {1}", channel, value);
            //messages.Enqueue(new RugOscMessage(Name + "/" + channel.ToString(), value));
            //sender.Send(new RugOscMessage(Name + "/" + channel.ToString(), value));
        }

        /// <summary>
        /// Sends a signal to a specific OSC channel given its name.
        /// </summary>
        /// <param name="channel">The name of the channel to send to.</param>
        /// <param name="value">The value to send to the channel.</param>
        public void Send(string channel, float value)
        {
            lock (messageLock)
            {
                messages.Enqueue(new RugOscMessage("/" + Name.Replace(" ", "") + "/" + channel, value));
            }
        }

        // Main receiving thread message loop
        void SendLoop()
        {
            try
            {
                while (sender.State != OscSocketState.Closed)
                {
                    // Reset the timer and time the send operation
                    stopWatch.Reset();
                    stopWatch.Start();

                    // if we are in a state to recieve
                    if (sender.State == OscSocketState.Connected)
                    {
                        lock (messageLock)
                        {
                            while (messages.Count > 0)
                            {
                                var message = messages.Dequeue();
                                sender.Send(message);
                            }
                        }
                    }

                    // Finish timing
                    stopWatch.Stop();
                    
                    // If the elappsed time is less than a millisecond, sleep for 1 millsecond
                    if (stopWatch.Elapsed.TotalMilliseconds < 1.0)
                    {
                        // Go with 1000hz refresh
                        Thread.Sleep(1);
                    }
                }
            }
            catch (Exception ex)
            {
                // if the socket was connected when this happens
                // then tell the user
                if (sender != null && sender.State == OscSocketState.Connected)
                {
                    CyLog.LogInfo("Exception in OSC sender loop");
                    CyLog.LogInfo(ex.Message);
                }
                else
                {
                    CyLog.LogInfo(ex.Message);
                }
            }
        }

        #endregion Send

        #endregion Methods
    }
}
