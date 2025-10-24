using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Dissonance;
using Dissonance.Audio.Playback;
using Dissonance.Audio.Capture;
using CymaticLabs.Logging;
using CymaticLabs.Language.Unity3d;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Class for managing voice chat sessions using Sessions.
    /// </summary>
    public class SessionsVoiceChat : MonoBehaviour
    {
        #region Constants

        /// <summary>
        /// The default port for voice chat data.
        /// </summary>
        public const int DEFAULT_VOICE_PORT = 9000;

        #endregion Constants

        #region Inspector

        /// <summary>
        /// The Sessions networking instance to use.
        /// </summary>
        public SessionsUdpNetworking SessionsNetworking;

        /// <summary>
        /// The Dissonance communications network to use.
        /// </summary>
        public SessionsVoiceCommsNetwork CommunicationsNetwork;

        /// <summary>
        /// The target voice server/host.
        /// </summary>
        public string Host = "127.0.0.1";

        /// <summary>
        /// The port to use for voice communications.
        /// </summary>
        [Range(0, 65535)]
        public int Port = DEFAULT_VOICE_PORT;

        /// <summary>
        /// The voice broadcast trigger being used by Dissonance.
        /// </summary>
        public SessionsVoiceBroadcastTrigger BroadcastTrigger;

        /// <summary>
        /// The Sessions translator reference to use.
        /// </summary>
        public LanguageServices Translator;

        /// <summary>
        /// Whether or not to hear translated speech locally.
        /// </summary>
        public bool EchoTranslationLocally = false;

        #endregion Inspector

        #region Fields

        private bool lastPushToTalk = false;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Whether or not voice services have been started.
        /// </summary>
        public bool IsVoiceStarted { get; private set; }

        /// <summary>
        /// Whether or not voice chat is currently operating as a server.
        /// </summary>
        public bool IsVoiceServer { get; private set; }

        /// <summary>
        /// Whether or not voice chat is currently operating as a client.
        /// </summary>
        public bool IsVoiceClient { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Start()
        {
            if (SessionsNetworking == null) SessionsNetworking = GetComponent<SessionsUdpNetworking>();
            CommunicationsNetwork.Port = (ushort)Port; // configure underlying port

            if (SessionsNetworking == null)
                Debug.LogWarning("Sessions voice chat has no assigned Sessions Networking reference and will not function correctly.");

            if (Translator == null) Translator = LanguageServices.Current;

            // Register to translator playback events
            if (Translator != null)
            {
                // Manually activate and deactivate speaker's indicator for auto-translate events
                Translator.OnVoicePlaybackStarted.AddListener((agentId) =>
                {
                    var positional = SessionsPositionalVoice.GetVoiceByAgentId(new Guid(agentId));
                    if (positional != null) positional.IsSpeaking = true;
                });

                // Manually activate and deactivate speaker's indicator for auto-translate events
                Translator.OnVoicePlaybackStopped.AddListener((agentId) =>
                {
                    var positional = SessionsPositionalVoice.GetVoiceByAgentId(new Guid(agentId));
                    if (positional != null) positional.IsSpeaking = false;
                });
            }

            // Register auto-translate RPC
            if (SessionsNetworking != null)
            {
                SessionsNetworking.RegisterRpcCommand("VoiceChat.AutoTranslate", RpcAutoTranslate);
            }
        }

        #endregion Init

        #region Update

        #endregion Update

        #region Opertation

        /// <summary>
        /// Starts a voice server.
        /// </summary>
        public void StartVoiceServer()
        {
            if (IsVoiceServer || SessionsNetworking == null) return;
            IsVoiceStarted = true;
            CommunicationsNetwork.ServerAddress = Host;
            CommunicationsNetwork.Port = (ushort)Port;
            CommunicationsNetwork.InitializeAsServer();
            IsVoiceServer = true;

            // If auto-translated is selected, disable local micrphone recording since the translator will use it
            if (SessionsUser.Current != null && SessionsUser.Current.VoiceMode == VoiceChatModes.AutoTranslated)
            {
                CyLog.LogInfoFormat("'{0}' selected so streaming voice will be disabled.", VoiceChatModes.AutoTranslated);
                SetMicrophoneCapture(false, true);
            }
        }

        /// <summary>
        /// Starts a voice server.
        /// </summary>
        public void StartVoiceClient(string hostname = null)
        {
            if (IsVoiceClient) return;

            if (string.IsNullOrEmpty(hostname))
            {
                hostname = Host;
                //hostname = Networking.DirectoryServiceHostname;

                // Ensure a known directory services host exists...
                if (string.IsNullOrEmpty(hostname))
                {
                    CyLog.LogInfo("[ERROR] Directory service host unknown; cannot start voice chat client");
                    return;
                }
            }

            IsVoiceStarted = true;
            CommunicationsNetwork.ServerAddress = hostname;
            CommunicationsNetwork.Port = (ushort)Port;
            CommunicationsNetwork.InitializeAsClient(hostname);
            IsVoiceClient = true;

            // If auto-translated is selected, disable local micrphone recording since the translator will use it
            if (SessionsUser.Current != null && SessionsUser.Current.VoiceMode == VoiceChatModes.AutoTranslated)
            {
                CyLog.LogInfoFormat("'{0}' selected so streaming voice will be disabled.", VoiceChatModes.AutoTranslated);
                SetMicrophoneCapture(false, true);
            }
        }

        /// <summary>
        /// Starts up voice services.
        /// </summary>
        public void StartVoiceChat()
        {
            if (IsVoiceStarted || SessionsNetworking == null) return;

            IsVoiceStarted = true;

            // Automatically detect server/client roll based on directory services roll
            if (SessionsNetworking.IsHost)
            {
                StartVoiceServer();
                CyLog.LogInfo("Sessions voice chat server started in host mode");
            }
            // Not directory services so run voice chat in client mode
            else
            {
                StartVoiceClient();
                CyLog.LogInfo("Sessions voice chat server started in client mode");
            }
        }

        /// <summary>
        /// Stops voice services.
        /// </summary>
        public void StopVoiceChat()
        {
            if (!IsVoiceStarted || SessionsNetworking == null) return;
            CommunicationsNetwork.Stop();
            IsVoiceStarted = false;
            IsVoiceClient = false;
            IsVoiceServer = false;
            CyLog.LogInfo("Sessions voice chat stopped");
        }

        /// <summary>
        /// Sets the microphone recording/capture status.
        /// </summary>
        /// <param name="enabled">Whether or not VOIP microphone capture is enabled.</param>
        public void SetMicrophoneCapture(bool enabled, bool force = false)
        {
            var micCapture = GetComponentInChildren<IMicrophoneCapture>();

            // Stop any current related coroutines
            StopCoroutine(DoStopMicrophoneCapture(micCapture));

            if (enabled && !micCapture.IsRecording)
            {
                var comms = GetComponentInChildren<DissonanceComms>();
                comms.IsMuted = false;
                micCapture.StartCapture(comms.MicrophoneName);
            }
            else if (!enabled)
            {
                if (!force && micCapture.IsRecording)
                {
                    GetComponentInChildren<DissonanceComms>().IsMuted = true;
                    micCapture.StopCapture();
                }
                else
                {
                    StartCoroutine(DoStopMicrophoneCapture(micCapture));
                }
            }
        }

        // Waits until the microphone is recording, then stops it
        private IEnumerator DoStopMicrophoneCapture(IMicrophoneCapture micCapture)
        {
            while (!micCapture.IsRecording) yield return new WaitForSeconds(0.25f);
            GetComponentInChildren<DissonanceComms>().IsMuted = true;
            micCapture.StopCapture();
        }

        #endregion Opertation

        #region Push To Talk

        /// <summary>
        /// Handles a push-to-talk input event.
        /// </summary>
        /// <param name="input">The input name.</param>
        /// <param name="value">The input value.</param>
        public void HandlePushToTalk(string name, float value)
        {
            // If there's no trigger reference or the mode is auto translated, ignore events....
            if (BroadcastTrigger == null || name != "PushToTalk") return;

            // Get the current session user
            var user = SessionsUser.Current;
            var enabled = value == 1;
            var hasChanged = lastPushToTalk != enabled;
            

            // If auto-translate is on, route push to talk to that
            if (user.VoiceMode == VoiceChatModes.AutoTranslated)
            {
                if (Translator != null && hasChanged)
                {
                    if (enabled)
                    {
                        Translator.StartVoiceCapture(user.Language, BroadcastTranslation);
                    }
                    else
                    {
                        Translator.StopVoiceCapture();
                    }
                }
            }
            else
            {
                // Otherwise update local push-to-talk from user input event
                BroadcastTrigger.SetPushToTalk(enabled);
            }

            // Record current value
            lastPushToTalk = enabled;
        }

        // Broadcasts a translated message to all connected clients for auto-translation client-side
        private void BroadcastTranslation(string language, string message)
        {
            if (SessionsNetworking == null || SessionsUser.Current == null) return;

            if (string.IsNullOrEmpty(language))
            {
                CyLog.LogWarn("Empty translation language, nothing will be sent.");
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                CyLog.LogWarn("Empty translation message, nothing will be sent.");
                return;
            }

            // Construct the arguments
            var user = SessionsUser.Current;
            var agentId = SessionsNetworking.AgentId;
            var voiceId = ((int)user.TranslationVoice).ToString();
            var args = language + "\t" + voiceId + "\t" + message;
            var spatial = user.SpatialVoice ? 1 : 0;

            // Broadcast
            SessionsNetworking.CallRpc(agentId, "VoiceChat.AutoTranslate", false, args, spatial);

            if (EchoTranslationLocally)
                SessionsNetworking.CallRpc(agentId, "VoiceChat.AutoTranslate", true, args, spatial);
        }

        #endregion Push To Talk

        #region Auto Translate

        /// <summary>
        /// RPC command that translates transcribed speech as text into translated, spoken speech based on local
        /// user language settings.
        /// </summary>
        /// <param name="agentId">The ID of the agent who made the call.</param>
        /// <param name="isLocal">Whether or not this call is localy only.</param>
        /// <param name="args">Message arguments.</param>
        /// <param name="value">The spatial blend for the audio source.</param>
        public void RpcAutoTranslate(Guid agentId, bool isLocal, string args, float spatial)
        {
            // If this is a local translate, we don't need to play it because its from the current agent
            if (string.IsNullOrEmpty(args) || !isLocal || LanguageServices.Current == null || SessionsUser.Current == null)
                return; // nothing to do...

            // Parse the arguments
            var languageIndex = -1;
            var voiceIndex = -1;
            var c = 0;

            // Parse out the source language code
            while ((languageIndex == -1 || voiceIndex == -1) && c < args.Length)
            {
                // The first tab in the arguments marks the end of the language code
                var isMarker = args[c] == '\t';

                if (isMarker)
                {
                    if (languageIndex == -1) languageIndex = c;
                    else if (voiceIndex == -1) voiceIndex = c;
                    if (languageIndex != -1 && voiceIndex != -1) break;
                }

                c++;
            }

            var language = args.Substring(0, languageIndex);
            var rawVoice = args.Substring(languageIndex + 1, voiceIndex - languageIndex);
            var message = args.Substring(voiceIndex + 1);
            var voice = (TranslationVoices)int.Parse(rawVoice);

            // Translate the message to the current user's language settings and play
            var user = SessionsUser.Current;
            var agent = SessionsNetworking.GetAgentById(agentId);

            // Attempt to get the audio source of this user
            // Get the current list of voices
            var voices = GetComponentsInChildren<VoicePlayback>();
            TranslationAudioSource audioSource = (from vp in voices where vp.PlayerName == agent.VoiceId select vp.GetComponentInChildren<TranslationAudioSource>()).FirstOrDefault();
            if (audioSource != null) audioSource.Audio.spatialBlend = spatial;
            LanguageServices.Current.TranslateAndSay(message, language, user.Language, voice, agentId.ToString(), audioSource != null ? audioSource.Audio : null);
        }

        #endregion Auto Translate

        #endregion Methods
    }
}
