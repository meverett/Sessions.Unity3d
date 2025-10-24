using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using CymaticLabs.Logging;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Registers a network instance already in the scene with the <see cref="SessionsNetworkEntityManager"/>.
    /// </summary>
    //[RequireComponent(typeof(SessionsNetworkEntity))]
    public class RegisterNetworkEntityInstance : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The network entity name to use when registering the instance. This links the instance with its network prefab/entry.
        /// </summary>
        [Tooltip("The network entity name to use when registering the instance. This links the instance with its network prefab/entry.")]
        public string NetworkName;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        private void Start()
        {
            if (string.IsNullOrEmpty(NetworkName))
            {
                Debug.LogWarningFormat("Network entity instance has a missing NetworkName and will not be registered.");
                return;
            }

            StartCoroutine(DoRegisterInstance());
        }

        // Waits until the network manager is properly initialized and then registers the instance.
        private IEnumerator DoRegisterInstance()
        {
            var sessions = SessionsUdpNetworking.Current;
            if (sessions == null) yield break;
            while (sessions.AgentId == Guid.Empty || sessions.LocalIP == null) yield return 0;
            SessionsNetworkEntityManager.Current.RegisterNetworkInstance(NetworkName, GetComponent<SessionsNetworkEntity>(), SessionsUdpNetworking.Current.Self);
            CyLog.LogInfoFormat("Network Entity instance registered: {0} -> {1}", NetworkName, name);
        }

        #endregion Methods
    }

    /// <summary>
    /// Different network instance registration modes.
    /// </summary>
    public enum NetworkRegistrationModes
    {
        /// <summary>
        /// Only register the network instance if operating as session host.
        /// </summary>
        HostOnly,

        /// <summary>
        /// Only register the network instance if not operating as session host.
        /// </summary>
        PeerOnly,

        /// <summary>
        /// Register the network instance when operating as either session host or not.
        /// </summary>
        Both
    }
}
