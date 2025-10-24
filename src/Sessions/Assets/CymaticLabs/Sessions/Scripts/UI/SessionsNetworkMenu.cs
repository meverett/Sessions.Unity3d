using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

using CymaticLabs.Sessions.Core;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Used to display connection information in VR to help debug.
    /// </summary>
    public class SessionsNetworkMenu : SessionsMenuBase
    {
        #region Inspector

        /// <summary>
        /// The Sessions networking component to get debug information from.
        /// </summary>
        [Tooltip("The Sessions networking component to get debug information from.")]
        public SessionsUdpNetworking SessionsNetworking;

        /// <summary>
        /// The text UI element to print debug information into.
        /// </summary>
        [Tooltip("The text UI element to print debug information into.")]
        public Text Text;

        /// <summary>
        /// The image used to indicate send activity.
        /// </summary>
        [Tooltip("The image used to indicate send activity.")]
        public Image Send;

        /// <summary>
        /// The image used to indicate receive activity.
        /// </summary>
        [Tooltip("The image used to indicate receive activity.")]
        public Image Receive;

        #endregion Inspector

        #region Fields

        // Used to build the final output debug string
        private StringBuilder sb;

        // Track the last send and receive end points
        private string lastRx = "";
        private string lastRxKey = "";
        private string lastTx = "";
        private string lastTxKey = "";

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static SessionsNetworkMenu Current { get; private set; }

        /// <summary>
        /// An optional custom debug message to display per frame.
        /// </summary>
        public string CustomOutput { get; set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            Current = this;
            sb = new StringBuilder();
        }

        private void Start()
        {
            // Register with main menu events
            if (SessionsMainMenu.Current != null) SessionsMainMenu.Current.OnHideAllMenus.AddListener(() => { HideMenu(); });

            if (Text == null) Text = GetComponentInChildren<Text>();
            if (SessionsNetworking == null) SessionsNetworking = SessionsUdpNetworking.Current;
            HideMenu();
        }

        #endregion Init

        #region Update

        private void LateUpdate()
        {
            if (Text == null || SessionsNetworking == null) return;

            // Nothing more to do if the menu is not active
            if (!MenuContainer.activeSelf) return;

            #region Update Activity Monitors

            // Update TX activity
            if (Send != null && SessionsNetworking.SendActivity)
            {
                var c = Send.color;
                c.a = 0.5f;
                Send.color = c;
                lastTx = SessionsNetworking.LastSendEndPoint;

                if (SessionsNetworking.LastSentMessage != null)
                {
                    var msg = SessionsNetworking.LastSentMessage;
                    lastTxKey = string.Format("{0}:{1}:{2}:{3}", msg.Type, msg.Flags, msg.Name, msg.Value);
                }
            }
            else
            {
                var c = Send.color;
                c.a -= Time.deltaTime * 3f;
                Mathf.Clamp01(c.a);
                Send.color = c;
            }

            // Update RX activity
            if (Receive != null && SessionsNetworking.ReceiveActivity)
            {
                var c = Receive.color;
                c.a = 0.5f;
                Receive.color = c;
                lastRx = SessionsNetworking.LastReceiveEndPoint;

                if (SessionsNetworking.LastReceivedMessage != null)
                {
                    var msg = SessionsNetworking.LastReceivedMessage;
                    lastRxKey = string.Format("{0}:{1}:{2}:{3}", msg.Type, msg.Flags, msg.Name, msg.Value);
                }
            }
            else
            {
                var c = Receive.color;
                c.a -= Time.deltaTime * 3f;
                Mathf.Clamp01(c.a);
                Receive.color = c;
            }

            #endregion Update Activity Monitors

            #region Update Text Stats

            sb.Length = 0; // clear out current contents

            // Get framerate
            float framerate = OVRPlugin.GetAppFramerate();

            // Get the local IP
            string localIP = SessionsNetworking.LocalIP != null ? SessionsNetworking.LocalIP.ToString() : "???";

            string privateIP = "???";
            string publicIP = "???";
            string privatePort = "???";
            string publicPort = "???";
            string isRegistered = "False";
            string isConnected = "False";

            var agent = SessionsNetworking.Self;

            if (agent != null)
            {
                privateIP = agent.PrivateIP.ToString();
                publicIP = agent.PublicIP.ToString();
                privatePort = agent.PrivatePort.ToString();
                publicPort = agent.PublicPort.ToString();
                isRegistered = string.Format("{0}", SessionsNetworking.AgentId != System.Guid.Empty);

                foreach (var a in SessionsNetworking.GetAllAgents())
                {
                    if (a.IsConnected)
                    {
                        isConnected = "True";
                        break;
                    }
                }

                sb.AppendFormat("Frame Rate: {0:F2}\n", framerate);
                sb.AppendFormat("Local IP: {0}\n", localIP);
                sb.AppendFormat("Private IP: {0}\n", privateIP);
                sb.AppendFormat("Public IP: {0}\n", publicIP);
                sb.AppendFormat("Private Port: {0}\n", privatePort);
                sb.AppendFormat("Public Port: {0}\n", publicPort);
                sb.AppendFormat("Is Registered: {0}\n", isRegistered);
                sb.AppendFormat("Is Connected: {0}\n", isConnected);
                sb.AppendFormat("\nLast TX: {0}\n", lastTx);
                sb.AppendFormat("{0}\n", lastTxKey);
                sb.AppendFormat("\nLast RX: {0}\n", lastRx);
                sb.AppendFormat("{0}\n", lastRxKey);
                sb.AppendFormat("\n{0}", CustomOutput);

                Text.text = sb.ToString();
            }

            #endregion Update Text Stats
        }

        #endregion Update

        #endregion Methods
    }
}

