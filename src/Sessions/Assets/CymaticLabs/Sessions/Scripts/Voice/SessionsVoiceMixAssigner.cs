using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Audio;
using CymaticLabs.Logging;
using CymaticLabs.Language.Unity3d;
using CymaticLabs.Sessions.Core;
using Dissonance.Audio.Playback;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Helps a Dissonance vocal object automatically assigned itself a mixer output channel.
    /// </summary>
    [RequireComponent(typeof(VoicePlayback))]
    [RequireComponent(typeof(AudioSource))]
    public class SessionsVoiceMixAssigner : MonoBehaviour
    {
        #region Inspector

        #endregion Inspector

        #region Fields

        // The local voice playback reference
        private VoicePlayback voicePlayback;

        // The local voice audio source
        private AudioSource audioSource;

        #endregion Fields

        #region Properties

        /// <summary>
        /// The voice playback component being used for voice chat.
        /// </summary>
        public VoicePlayback VoicePlayback { get { return voicePlayback; } }

        /// <summary>
        /// The audio source component being used for voice chat.
        /// </summary>
        public AudioSource AudioSource { get { return audioSource; } }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            voicePlayback = GetComponentInChildren<VoicePlayback>();
            audioSource = GetComponentInChildren<AudioSource>();
            StartCoroutine(DoWaitForVocalIdMatch());
        }

        #endregion Init

        #region Update

        // Waits until the voice client has an assigned ID and then connects it to the correct audio mix group
        private IEnumerator DoWaitForVocalIdMatch()
        {
            // Wait until we have a player name for this vocal
            while (string.IsNullOrEmpty(voicePlayback.PlayerName)) yield return 0;

            // Get the positional voice for this ID
            var posVoice = SessionsPositionalVoice.GetVoiceByPlayerId(voicePlayback.PlayerName);

            if (posVoice != null && posVoice.NetworkTransform != null && posVoice.NetworkTransform.NetworkEntity != null && posVoice.NetworkTransform.NetworkEntity.Owner != null)
            {
                var owner = posVoice.NetworkTransform.NetworkEntity.Owner;

                // Register the voice audio source for this agent with its own dedicated vocal submix
                SessionsSound.Current.GetNextAvailableVoiceMix(owner.Id, audioSource);

                // Copy this mix group to the auto-translate audio source if one is present
                var translateSource = GetComponentInChildren<TranslationAudioSource>();
                if (translateSource != null && translateSource.Audio != null) translateSource.Audio.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;

                CyLog.LogInfoFormat("[VOICE] new voice mix group assigned for {0}", owner.Name);
            }
        }

        #endregion Update

        #endregion Methods
    }
}
