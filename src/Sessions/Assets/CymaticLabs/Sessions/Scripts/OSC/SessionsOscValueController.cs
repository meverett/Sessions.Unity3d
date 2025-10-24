using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Newtonsoft.Json;
using CymaticLabs.Logging;

namespace CymaticLabs.Protocols.Osc.Unity3d
{
    /// <summary>
    /// Custom Unity event class to capture OSC value events.
    /// </summary>
    [System.Serializable]
    public class OscFloatValueEvent : UnityEvent<string, float, object>
    { }

    /// <summary>
    /// Manages OSC control signals for the session.
    /// </summary>
    public class SessionsOscValueController : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The port to use when listening for OSC messages via UDP.
        /// </summary>
        [Header("Network Configuration")]
        [Tooltip("The port to use when listening for OSC messages via UDP.")]
        [Range(0, 65535)]
        public int UdpPort = 8000;

        /// <summary>
        /// Whether or not the OSC value controller should start automatically or not.
        /// </summary>
        [Header("Behavior")]
        [Tooltip("Whether or not to start the OSC reciever/controller automatically on start.")]
        public bool StartAutomatically = true;

        /// <summary>
        /// Whether or not to log received messages.
        /// </summary>
        [Tooltip("Whether or not to log received messages.")]
        public bool LogReceived = false;

        /// <summary>
        /// The OSC settings file to apply at runtime.
        /// </summary>
        [Header("OSC Value Mapping")]
        //[Tooltip("The OSC settings file to apply at runtime.")]
        //[HideInInspector]
        public TextAsset Configuration;

        /// <summary>
        /// Occurs when a value has been updated via an OSC mapping.
        /// </summary>
        [Header("Events")]
        public OscFloatValueEvent OnValueReceived;

        #endregion Inspector

        #region Fields

        // The underlying OSC message receiver
        private OscReceiver oscReceiver;

        // The underlying OSC message sender
        //private OscSender oscSender;

        // A collection used as a thread-safe message queue
        private List<OscMessage> msgQueue;
        private object msgQueueLock;

        // A look-up table of allowed values and their range mappings by address
        private Dictionary<string, List<DataOscRangeMapFloat>> valuesByAddress;

        #endregion Fields

        #region Properties

        /// <summary>
        /// The parsed and applied OSC configuration.
        /// </summary>
        public DataSessionsOscConfiguration RuntimeConfiguration { get; private set; }

        #endregion Properties
        
        #region Methods

        #region Init

        private void Awake()
        {
            // Index allowed OSC addresses/values
            valuesByAddress = new Dictionary<string, List<DataOscRangeMapFloat>>();

            // Load the current configuration
            LoadConfiguration();

            // Listen for OSC messages
            msgQueueLock = new object();
            msgQueue = new List<OscMessage>();
            //oscSender = new OscSender();
        }

        private void Start()
        {
            if (StartAutomatically)
                StartOscReceiver();
        }

        private void OnDestroy()
        {
            StopOscReceiver();
        }

        private void OnApplicationQuit()
        {
            StopOscReceiver();
        }

        #endregion Init

        #region Operation

        /// <summary>
        /// Starts the OSC receiver on the configured port.
        /// </summary>
        public void StartOscReceiver()
        {
            oscReceiver = new OscReceiver(UdpPort);
            oscReceiver.MessageReceived += OscReceiver_MessageReceived;
            oscReceiver.Start();
        }

        /// <summary>
        /// Stops the OSC receiver on the configured port.
        /// </summary>
        public void StopOscReceiver()
        {
            oscReceiver.MessageReceived -= OscReceiver_MessageReceived;
            oscReceiver.Stop();
        }

        #endregion Operation

        #region Update

        private void Update()
        {
            // If there are received messages in the queue, process them
            if (msgQueue.Count > 0)
            {
                OscMessage[] messages = new OscMessage[0];

                // Extract the current message queue
                lock (msgQueueLock)
                {
                    messages = msgQueue.ToArray();
                    msgQueue.Clear();
                }

                // Process each message
                foreach (var msg in messages)
                {
                    // Ensure a mapping still exists for this address
                    if (!valuesByAddress.ContainsKey(msg.Address)) continue;

                    var list = valuesByAddress[msg.Address];

                    foreach (var map in list)
                    {
                        // Get the value
                        float value = (float)msg.Arguments[map.ArgumentIndex];

                        // Clamp input
                        if (map.ClampInput)
                        {
                            if (value < map.MinInputValue) value = map.MinInputValue;
                            else if (value > map.MaxInputValue) value = map.MaxInputValue;
                        }

                        // Scale output
                        if (map.ScaleOutput)
                        {
                            value = (value - map.MinInputValue) / (map.MaxInputValue - map.MinInputValue) * (map.MaxOutputValue - map.MinOutputValue) + map.MinOutputValue;
                        }

                        // Mark up the message
                        msg.Reliable = map.Reliable;
                        msg.NoBroadcast = map.NoBroadcast;

                        // Broadcast
                        if (!map.NoBroadcast)
                            if (OnValueReceived != null) OnValueReceived.Invoke(map.Name, value, msg);
                    }
                }
            }
        }

        #endregion Update

        #region Event Handlers

        // Handle received OSC message
        private void OscReceiver_MessageReceived(OscMessage message)
        {
            if (message.Arguments != null && message.Arguments.Length > 0)
            {
                if (LogReceived)
                    Debug.LogFormat("[OSC] {0} = {1}", message.Address, message.Arguments[0]);

                // If this is an allowed message, add it to the queue
                if (valuesByAddress.ContainsKey(message.Address))
                {
                    lock (msgQueueLock) msgQueue.Add(message);
                }
                else
                {
                    //CyLog.WriteLine("Unallowed OSC address: " + message.Address);
                }
            }
        }

        #endregion Event Handlers

        #region Configuration

        /// <summary>
        /// Loads and applies the current OSC configuration settings.
        /// </summary>
        public void LoadConfiguration()
        {
            if (Configuration == null)
            {
                CyLog.LogWarn("OSC value controller has no configuration file applied and no configuration will be loaded. OSC disabled.");
                return;
            }

            if (valuesByAddress != null) valuesByAddress.Clear();
            else valuesByAddress = new Dictionary<string, List<DataOscRangeMapFloat>>();

            // If a settings file was supplied, use that
            if (Configuration != null)
            {
                RuntimeConfiguration = JsonConvert.DeserializeObject<DataSessionsOscConfiguration>(Configuration.text);
            }

            // Load in allowed value mappings
            if (RuntimeConfiguration != null && RuntimeConfiguration.AllowedFloats != null && RuntimeConfiguration.AllowedFloats.Length > 0)
            {
                foreach (var value in RuntimeConfiguration.AllowedFloats)
                {
                    if (value == null) continue;

                    // Null address == ignore
                    if (string.IsNullOrEmpty(value.Address))
                    {
                        Debug.LogWarningFormat("Value Receiver: OSC value '{0}' has a blank address and will be ignored.", value.Name);
                        continue;
                    }

                    // Null name == use address as name
                    if (string.IsNullOrEmpty(value.Name))
                    {
                        value.Name = value.Address;
                    }

                    // Ensure a map list for this address
                    if (!valuesByAddress.ContainsKey(value.Address)) valuesByAddress.Add(value.Address, new List<DataOscRangeMapFloat>());

                    // Add this entry
                    valuesByAddress[value.Address].Add(value);
                }
            }
        }

        #endregion Configuration

        #endregion Methods
    }
}


