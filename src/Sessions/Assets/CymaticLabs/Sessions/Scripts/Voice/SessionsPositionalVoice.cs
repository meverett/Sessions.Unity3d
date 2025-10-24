using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Dissonance;
using CymaticLabs.Logging;
using CymaticLabs.Sessions.Core;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Implements the <see cref="IDissonancePlayer"/> interface so that Dissonance
    /// audio can implement positional tracking of players.
    /// </summary>
    public class SessionsPositionalVoice : MonoBehaviour, IDissonancePlayer
    {
        #region Inspector

        /// <summary>
        /// The network transform to use for the player's position.
        /// </summary>
        [Tooltip("The network transform to use for the player's position.")]
        public SessionsNetworkTransform NetworkTransform;

        /// <summary>
        /// The sessions networking instance to use.
        /// </summary>
        [Tooltip("The sessions networking instance to use.")]
        public SessionsUdpNetworking SessionsNetworking;

        /// <summary>
        /// The optional voice acitivity image indicator to use.
        /// </summary>
        [Tooltip("The optional voice acitivity image indicator to use.")]
        public Image VoiceIndicator;

        #endregion Inspector

        #region Fields

        // The underlying Dissonance communications network
        private DissonanceComms dissonance;

        // Singleton list of 
        private static List<SessionsPositionalVoice> voices = new List<SessionsPositionalVoice>();

        #endregion Fields

        #region Properties

        /// <summary>
        /// The Dissonance player ID of this player.
        /// </summary>
        public string PlayerId { get; private set; }
        
        /// <summary>
        /// Gets the position of the audio.
        /// </summary>
        public Vector3 Position
        {
            get { return transform.position; }
        }

        /// <summary>
        /// Gets the rotation of the audio.
        /// </summary>
        public Quaternion Rotation
        {
            get { return transform.rotation; }
        }

        /// <summary>
        /// Gets the type of network player the instance is.
        /// </summary>
        public NetworkPlayerType Type
        {
            get { return NetworkTransform != null && NetworkTransform.IsMine ? NetworkPlayerType.Local : NetworkPlayerType.Remote; }
        }

        /// <summary>
        /// Whether or not position is currently being tracked.
        /// </summary>
        public bool IsTracking { get; private set; }

        /// <summary>
        /// Gets the current voice activation level for the player.
        /// </summary>
        public float VoiceLevel { get; private set; }

        /// <summary>
        /// Whether or not the user's voice indicator will currently indicate if they are speaking or not.
        /// </summary>
        public bool IsSpeaking { get; set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            // Clear all current voices
            //voices.Clear();
        }

        private void Start()
        {
            if (SessionsNetworking == null) SessionsNetworking = SessionsUdpNetworking.Current;
            if (NetworkTransform == null) NetworkTransform = GetComponentInChildren<SessionsNetworkTransform>();
            dissonance = FindObjectOfType<DissonanceComms>();
            if (NetworkTransform == null || NetworkTransform.NetworkEntity == null || SessionsNetworking == null) return;

            // Once a new session begins, start positional tracking
            SessionsNetworking.OnSessionHosted.AddListener(HandleSessionStarted);
            SessionsNetworking.OnSessionJoined.AddListener(HandleSessionStarted);

            // Update the voice indicator image if one is present
            if (VoiceIndicator != null)
            {
                var c = VoiceIndicator.color;
                c.a = 0; // hide it on start
                VoiceIndicator.color = c;
            }

            // Register this voice with the global total
            voices.Add(this);
        }

        public void OnEnable()
        {
            dissonance = FindObjectOfType<DissonanceComms>();
        }

        public void OnDisable()
        {
            StopTracking();
        }

        #endregion Init

        #region Operation

        /// <summary>
        /// Starts positional tracking.
        /// </summary>
        public void StartTracking()
        {
            if (dissonance == null) dissonance = FindObjectOfType<DissonanceComms>();
            if (IsTracking || dissonance == null || NetworkTransform == null || NetworkTransform.NetworkEntity == null) return;
            var instance = NetworkTransform.NetworkEntity;
            if (string.IsNullOrEmpty(PlayerId)) PlayerId = instance.Owner.VoiceId;
            CyLog.LogInfoFormat("Starting voice positional tracking for {0}:{1}:{2} = {3}", instance.Owner.Name, instance.Id, NetworkTransform.NetworkName, PlayerId);
            dissonance.TrackPlayerPosition(this);
            IsTracking = true;
        }

        /// <summary>
        /// Stops positional tracking.
        /// </summary>
        public void StopTracking()
        {
            if (dissonance == null) dissonance = FindObjectOfType<DissonanceComms>();
            if (!IsTracking || dissonance == null) return;
            dissonance.StopTracking(this);
            IsTracking = false;
        }

        // Handles the start of a new session
        private void HandleSessionStarted(string name, string info)
        {
            StartTracking();
        }

        #endregion Operation

        #region Update

        private void Update()
        {
            if (dissonance == null) dissonance = FindObjectOfType<DissonanceComms>();
            if (dissonance == null || NetworkTransform == null || NetworkTransform.NetworkEntity == null) return;

            // Make sure this object has its voice ID assigned
            var entity = NetworkTransform.NetworkEntity;
            if (entity.Owner == null) return;
            if (!string.IsNullOrEmpty(entity.Owner.VoiceId) && PlayerId != entity.Owner.VoiceId) PlayerId = entity.Owner.VoiceId;
            if (string.IsNullOrEmpty(PlayerId)) return;

            // Assign the current user this positional voice, if it belongs to them and is unassigned.
            var user = SessionsUser.Current;
            var isUserVoice = entity.Owner.Id == SessionsNetworking.AgentId;
            if (isUserVoice && user != null && user.PositionalVoice == null) user.PositionalVoice = this;

            // Get the player state for this player
            var state = dissonance.FindPlayer(PlayerId);
            if (state == null) return; // no state information for this player yet

            // Current speaking status based on either Disonnance or manual override
            var isSpeaking = state.IsSpeaking || IsSpeaking;

            // If this player is currently speaking, reset the voice level back to 100%
            if (isSpeaking) VoiceLevel = 0.5f + (UnityEngine.Random.value * 0.5f);
            VoiceLevel -= (VoiceLevel * Time.deltaTime) * 10f;
            if (Mathf.Abs(VoiceLevel) < 0.1f) VoiceLevel = 0;

            // Update the voice indicator image if one is present
            if (VoiceIndicator != null)
            {
                var c = VoiceIndicator.color;
                c.a = VoiceLevel;
                VoiceIndicator.color = c;
            }
        }

        #endregion Update

        #region Voices

        /// <summary>
        /// Gets all of the currently available positional voices.
        /// </summary>
        /// <returns>All current positional voices.</returns>
        public static SessionsPositionalVoice[] GetAllVoices()
        {
            return voices.ToArray();
        }

        /// <summary>
        /// Gets the positional voice for a given Dissonance player ID.
        /// </summary>
        /// <param name="playerId">The Dissonance ID of the player to get positional voice for.</param>
        /// <returns>The positional voice if found, otherwise NULL.</returns>
        public static SessionsPositionalVoice GetVoiceByPlayerId(string playerId)
        {
            return voices.FirstOrDefault(v => v.PlayerId == playerId);
        }

        /// <summary>
        /// Gets the positional voice for an agent given their agent ID.
        /// </summary>
        /// <param name="agentId">The agent ID of the agent to get positional voice for.</param>
        /// <returns>The positional voice if found, otherwise NULL.</returns>
        public static SessionsPositionalVoice GetVoiceByAgentId(Guid agentId)
        {
            return voices.FirstOrDefault(v => v.NetworkTransform != null && v.NetworkTransform.NetworkEntity != null && v.NetworkTransform.NetworkEntity.Owner != null && v.NetworkTransform.NetworkEntity.Owner.Id == agentId);
        }

        #endregion Voices

        #endregion Methods
    }
}
