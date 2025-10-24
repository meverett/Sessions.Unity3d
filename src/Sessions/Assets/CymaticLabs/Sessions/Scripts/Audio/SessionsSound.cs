using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Audio;
using CymaticLabs.Logging;
using CymaticLabs.Sessions.Core;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// A sound effect and audio track mananger for sessions.
    /// </summary>
    public class SessionsSound : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The sessions manager instance to use.
        /// </summary>
        [Tooltip("The sessions manager instance to use.")]
        public SessionsSceneManager SessionsManager;

        /// <summary>
        /// The sessions networking instance to use.
        /// </summary>
        [Tooltip("The sessions networking instance to use.")]
        public SessionsUdpNetworking SessionsNetworking;

        /// <summary>
        /// Optional name of the starting audio clip to play.
        /// </summary>
        [Tooltip("Optional name of the starting audio clip to play.")]
        public string StartClipName;

        /// <summary>
        /// The volume of the optional starting clip to play.
        /// </summary>
        [Tooltip("The volume of the optional starting clip to play.")]
        public float StartClipVolume = 1;

        /// <summary>
        /// Different submixes available for voice chat.
        /// </summary>
        [Tooltip("Different submixes available for voice chat.")]
        public AudioMixerGroup[] VoiceMixes;

        /// <summary>
        /// A list of audio sources to use in a pool for playing sound effects.
        /// </summary>
        [Tooltip("A list of audio sources to use in a pool for playing sound effects.")]
        public AudioSource[] EfxAudioSources;

        /// <summary>
        /// The list of audio EFX clips that can be triggered by name.
        /// </summary>
        [Tooltip("The list of audio EFX clips that can be triggered by name.")]
        public NamedAudioClip[] EfxClips;

        /// <summary>
        /// A list of audio sources to use for playing music tracks.
        /// </summary>
        [Tooltip("A list of audio sources to use for playing music tracks.")]
        public AudioSource[] MusicAudioSources;

        /// <summary>
        /// The list of audio music clips that can be triggered by name.
        /// </summary>
        [Tooltip("The list of audio music clips that can be triggered by name.")]
        public NamedAudioClip[] MusicClips;

        /// <summary>
        /// Occurs when a music clip finishes playing.
        /// </summary>
        [Header("Events")]
        public UnityAudioClipEvent OnMusicFinishedPlaying;

        #endregion Inspector

        #region Fields

        // A list of EFX audio clips by name
        private Dictionary<string, NamedAudioClip> efxClipsByName;

        // The index of the next/current audio source in the audio source pool
        private int efxIndex = 0;

        // Audio sources that have been pegged to playing a particular clip only
        private Dictionary<string, AudioSource> audioSourcesByClipName;

        // The index of the next available vocal mix group.
        private int voiceIndex = 0;

        // A list of voice audio source by their agent ID
        private Dictionary<Guid, AudioSource> voiceAudioSourcesByAgentId;

        // A list of voice submix groups by their agent ID
        private Dictionary<Guid, AudioMixerGroup> voiceMixesByAgentId;

        // A list of music audio clips by name
        private Dictionary<string, NamedAudioClip> musicClipsByName;

        // A "playing" state of music tracks by track ID.
        private bool[] playingMusicTracks;

        #endregion Fields

        #region Properties

        /// <summary>
        /// The static singleton instance of the behavior.
        /// </summary>
        public static SessionsSound Current { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            Current = this;
            efxClipsByName = new Dictionary<string, NamedAudioClip>();
            audioSourcesByClipName = new Dictionary<string, AudioSource>();
            voiceAudioSourcesByAgentId = new Dictionary<Guid, AudioSource>();
            voiceMixesByAgentId = new Dictionary<Guid, AudioMixerGroup>();
            musicClipsByName = new Dictionary<string, NamedAudioClip>();

            // Store all efx audio clips by name into look up
            if (EfxClips != null && EfxClips.Length > 0)
            {
                foreach (var item in EfxClips)
                {
                    if (string.IsNullOrEmpty(item.Name)) continue;

                    if (efxClipsByName.ContainsKey(item.Name))
                    {
                        Debug.LogWarningFormat("Duplicate EFX audio clip registered with name: {0}", item.Name);
                        efxClipsByName[item.Name] = item; // overwrite
                    }
                    else
                    {
                        efxClipsByName.Add(item.Name, item);
                    }
                }
            }

            // Store all music audio clips by name into look up
            if (MusicClips != null && MusicClips.Length > 0)
            {
                foreach (var item in MusicClips)
                {
                    if (string.IsNullOrEmpty(item.Name)) continue;

                    if (musicClipsByName.ContainsKey(item.Name))
                    {
                        Debug.LogWarningFormat("Duplicate music audio clip registered with name: {0}", item.Name);
                        musicClipsByName[item.Name] = item; // overwrite
                    }
                    else
                    {
                        musicClipsByName.Add(item.Name, item);
                    }
                }
            }

            // Create an internal list of playing music track states used to detect a finish event
            if (MusicAudioSources != null && MusicAudioSources.Length > 0)
            {
                playingMusicTracks = new bool[MusicAudioSources.Length];
                for (var i = 0; i < playingMusicTracks.Length; i++) playingMusicTracks[i] = false;
            }
            else
            {
                playingMusicTracks = new bool[0];
            }
        }

        private void Start()
        {
            if (SessionsNetworking == null) SessionsNetworking = SessionsUdpNetworking.Current;
            if (SessionsManager == null) SessionsManager = SessionsSceneManager.Current;

            // Register global sound RPCs
            if (SessionsNetworking != null)
            {
                // Register the play sound clip RPC
                SessionsNetworking.RegisterRpcCommand("Sound.PlayEfx", RpcPlayEfx);
                SessionsNetworking.RegisterRpcCommand("Sound.PlayMusic", RpcPlayMusic);
            }

            #region Register Value Handlers

            // Register Session Value Handlers
            if (SessionsManager != null)
            {
                // Create dynamic handlers for all configured tracks
                if (MusicAudioSources != null)
                {
                    for (var i = 0; i < MusicAudioSources.Length; i++)
                    {
                        var trackId = i;

                        // Create the volume handler
                        var valueName = string.Format("Sound/Track/{0}/Volume", trackId);

                        // Register volume handler
                        SessionsManager.RegisterValueHandler(valueName, (name, value) =>
                        {
                            if (MusicAudioSources == null || MusicAudioSources.Length - 1 < trackId) return;
                            MusicAudioSources[trackId].volume = value;
                        });

                        // Create the track play handler
                        valueName = string.Format("Sound/Track/{0}/Play", trackId);

                        // Register play handler
                        SessionsManager.RegisterValueHandler(valueName, (name, value) =>
                        {
                            if (MusicClips == null || MusicClips.Length == 0) return;

                            // If the value is less than 0, it's a track stop
                            if (value < 0)
                            {
                                StopMusic(trackId);
                                return;
                            }

                            // Otherwise Get the music clip ID
                            var musicId = (int)value;

                            // Make sure its in range
                            if (musicId >= 0 && musicId > MusicClips.Length - 1) return;

                            // Find the clip to play
                            var item = MusicClips[musicId];

                            // Play the music
                            PlayMusic(item, trackId);
                        });

                        // Track: Lowpass Cutoff
                        valueName = string.Format("Sound/Track/{0}/LP/CF", trackId);

                        SessionsManager.RegisterValueHandler(valueName, (name, value) =>
                        {
                            var audioGroup = MusicAudioSources[trackId].outputAudioMixerGroup;
                            value = Mathf.Lerp(-1, 1, value);
                            float a = Mathf.Sqrt(0.5f * (1f + value));
                            var v = Mathf.Pow(a, 10);
                            float cf = Mathf.Lerp(10, 22000, v);
                            audioGroup.audioMixer.SetFloat("Track" + trackId.ToString() + "_LP_CF", cf);
                        });

                        // Track: Highpass Cutoff
                        valueName = string.Format("Sound/Track/{0}/HP/CF", trackId);

                        SessionsManager.RegisterValueHandler(valueName, (name, value) =>
                        {
                            var audioGroup = MusicAudioSources[trackId].outputAudioMixerGroup;
                            value = Mathf.Lerp(-1, 1, value);
                            float a = Mathf.Sqrt(0.5f * (1f + value));
                            var v = Mathf.Pow(a, 10);
                            float cf = Mathf.Lerp(10, 22000, v);
                            audioGroup.audioMixer.SetFloat("Track" + trackId.ToString() + "_HP_CF", cf);
                        });

                        // Track: Send A
                        valueName = string.Format("Sound/Track/{0}/Send/A", trackId);

                        SessionsManager.RegisterValueHandler(valueName, (name, value) =>
                        {
                            var audioGroup = MusicAudioSources[trackId].outputAudioMixerGroup;
                            var level = LinearToDecibel(value);
                            audioGroup.audioMixer.SetFloat("Track" + trackId.ToString() + "_SendA", level);
                        });
                    }
                }
            }

            #endregion Register Value Handlers

            // Play start clip
            if (!string.IsNullOrEmpty(StartClipName))
            {
                PlayEfx(StartClipName, false, StartClipVolume, true);
            }
        }

        #endregion Init

        #region Update

        private void Update()
        {
            // Go through and monitor the state of each playing track to identify when one finishes playing.
            if (MusicAudioSources != null && MusicAudioSources.Length > 0)
            {
                for (var i = 0; i < MusicAudioSources.Length; i++)
                {
                    var audio = MusicAudioSources[i];
                    var isPlaying = playingMusicTracks[i];

                    // Compare the current playing state to the last known state
                    if (!audio.isPlaying && isPlaying)
                    {
                        // The track stopped playing, so change state and notify
                        playingMusicTracks[i] = false;

                        CyLog.LogInfoFormat("Track finished playing: {0}", i);

                        // Notify
                        OnMusicFinishedPlaying.Invoke(i, audio.name, audio, audio.clip);
                    }
                }
            }
        }

        #endregion Update

        #region Audio

        #region EFX

        /// <summary>
        /// RPC command that plays an audio clip across the network.
        /// </summary>
        /// <param name="agentId">The ID of the agent who made the call.</param>
        /// <param name="isLocal">Whether or not this call to the clip is localy only.</param>
        /// <param name="name">The name of the audio clip to play.</param>
        /// <param name="volume">The volume of the clip to play.</param>
        public void RpcPlayEfx(Guid agentId, bool isLocal, string name, float volume)
        {
            PlayEfx(name); // for now ignore volume
        }

        /// <summary>
        /// Command that plays an audio clip.
        /// </summary>
        /// <param name="name">The name of the audio clip to play.</param>
        public void PlayEfxSimple(string name)
        {
            PlayEfx(name); // for now ignore volume
        }

        /// <summary>
        /// Plays a registered audio clip given its name.
        /// </summary>
        /// <param name="name">The name of the audio clip to play.</param>
        /// <param name="sameSource">Whether or not the same audio source should be used to play the clip.</param>
        /// <param name="volume">The volume of the clip to play.</param>
        /// <param name="oneShot">Whether or not to play it as a one shot.</param>
        public static void PlayEfx(string name, bool sameSource = false, float? volume = null, bool oneShot = false)
        {
            if (Current == null) return;
            var item = Current.efxClipsByName.ContainsKey(name) ? Current.efxClipsByName[name] : null;
            if (item == null) return;
            if (volume == null) volume = item.Volume;

            PlayEfx(item, sameSource, volume.Value, oneShot);
        }

        // Plays the specified audio clip
        private static void PlayEfx(NamedAudioClip item, bool sameSource = false, float? volume = null, bool oneShot = false)
        {
            if (Current == null || Current.EfxAudioSources == null) return;

            AudioSource audio = null;

            // If we're trying to use the same source
            if (sameSource)
            {
                // See if the audio already exists by this source
                audio = Current.audioSourcesByClipName.ContainsKey(item.Name) ? Current.audioSourcesByClipName[item.Name] : null;
            }

            // If nothing existed by the same source, get the next audio source in the pool
            if (audio == null)
            {
                if (Current.efxIndex > Current.EfxAudioSources.Length - 1) Current.efxIndex = 0;
                else if (Current.efxIndex < 0) Current.efxIndex = 0;

                // Get the current/next audio source
                audio = Current.EfxAudioSources[Current.efxIndex];

                // If this audio source was previously dedicated to a clip, remove its dedication
                if (Current.audioSourcesByClipName.ContainsKey(audio.name)) Current.audioSourcesByClipName.Remove(audio.name);
            }

            // If using same source, put this audio source away dedicated to this clip for now
            if (sameSource && !Current.audioSourcesByClipName.ContainsKey(item.Name))
            {
                Current.audioSourcesByClipName.Add(item.Name, audio);
                audio.name = item.Name;
            }

            // Stop any currently playing audio
            audio.Stop();
            audio.loop = false;
            if (volume != null) audio.volume = volume.Value;

            if (oneShot)
            {
                if (volume != null)
                {
                    audio.PlayOneShot(item.Clip, volume.Value);
                }
                else
                {
                    audio.PlayOneShot(item.Clip);
                }
            }
            else
            {
                audio.clip = item.Clip;
                audio.Stop();
                audio.Play();
            }

            // Update the audio source index in the pool
            audio.name = item.Name;
            Current.efxIndex++;
        }

        #endregion EFX

        #region Music

        /// <summary>
        /// RPC command that plays a music audio clip across the network.
        /// </summary>
        /// <param name="agentId">The ID of the agent who made the call.</param>
        /// <param name="isLocal">Whether or not this call to the clip is localy only.</param>
        /// <param name="name">The name of the audio clip to play.</param>
        /// <param name="trackId">The track number to use to play the audio.</param>
        public void RpcPlayMusic(Guid agentId, bool isLocal, string name, float trackId)
        {
            PlayMusic(name, (int)trackId); // for now ignore volume
        }

        /// <summary>
        /// Plays a registered music audio clip given its name.
        /// </summary>
        /// <param name="name">The name of the audio clip to play.</param>
        /// <param name="trackId">The track number to use to play the music.</param>
        /// <param name="volume">The volume of the clip to play.</param>
        public static void PlayMusic(string name, int trackId, float? volume = null)
        {
            if (Current == null) return;
            var item = Current.musicClipsByName.ContainsKey(name) ? Current.musicClipsByName[name] : null;
            if (item == null) return;
            if (volume == null) volume = item.Volume;
            PlayMusic(item, trackId, volume.Value);
        }

        // Plays the specified audio clip
        private static void PlayMusic(NamedAudioClip item, int trackId, float? volume = null)
        {
            if (Current == null || Current.MusicAudioSources == null) return;

            if (trackId < 0 || trackId > Current.MusicAudioSources.Length - 1)
            {
                CyLog.LogErrorFormat("Cannot play music on missing track ID {0}", trackId);
                return;
            }

            // Get the audio source for the selected track
            AudioSource audio = Current.MusicAudioSources[trackId];

            // Stop any currently playing audio
            audio.Stop();
            audio.clip = item.Clip;
            audio.Stop();
            audio.loop = item.Loop;
            if (volume != null) audio.volume = volume.Value;
            audio.Play();

            // Update playing state
            Current.playingMusicTracks[trackId] = true;

            // Update the audio item name
            audio.name = item.Name;
        }

        /// <summary>
        /// RPC command that stops playing music audio clip across the network.
        /// </summary>
        /// <param name="isLocal">Whether or not this call to the clip is localy only.</param>
        /// <param name="args">Optional arguments.</param>
        /// <param name="trackId">The track ID to stop playing audio on..</param>
        public void RpcStopMusic(bool isLocal, string args, float trackId)
        {
            StopMusic((int)trackId); // for now ignore volume
        }

        /// <summary>
        /// Stops music playing on the current track ID.
        /// </summary>
        /// <param name="trackId">The ID of the track to stop playing music on.</param>
        public static void StopMusic(int trackId)
        {
            if (Current == null || Current.MusicAudioSources == null) return;

            if (trackId < 0 || trackId > Current.MusicAudioSources.Length - 1)
            {
                CyLog.LogErrorFormat("Cannot stop music on missing track ID {0}", trackId);
                return;
            }

            // Get the audio source for the selected track
            AudioSource audio = Current.MusicAudioSources[trackId];
            audio.Stop();

            Current.playingMusicTracks[trackId] = false;
        }

        #endregion Music

        #endregion Audio

        #region Voice

        /// <summary>
        /// Gets the voice audio source for an agent given the agent's ID.
        /// </summary>
        /// <param name="agentId">The ID of the agent to get the voice audio source for.</param>
        /// <returns>The agent's voice audio source if found, otherwise NULL.</returns>
        public AudioSource GetVoiceAudioSource(Guid agentId)
        {
            return voiceAudioSourcesByAgentId.ContainsKey(agentId) ? voiceAudioSourcesByAgentId[agentId] : null;
        }

        /// <summary>
        /// Gets the voice submix group for an agent given the agent's ID.
        /// </summary>
        /// <param name="agentId">The ID of the agent to get the voice submix group for.</param>
        /// <returns>The agent's voice audio submix group if found, otherwise NULL.</returns>
        public AudioMixerGroup GetVoiceMix(Guid agentId)
        {
            return voiceMixesByAgentId.ContainsKey(agentId) ? voiceMixesByAgentId[agentId] : null;
        }

        /// <summary>
        /// Gets the next available voice submix group.
        /// </summary>
        /// <param name="agentId">The ID of the agent the voice mix is for.</param>
        /// <param name="audioSource">The voice audio source to use.</param>
        /// <returns>The next available mix group if available, otherwise NULL.</returns>
        public bool GetNextAvailableVoiceMix(Guid agentId, AudioSource audioSource)
        {
            if (VoiceMixes == null || VoiceMixes.Length == 0) return false;

            if (voiceIndex + 1 > VoiceMixes.Length - 1)
            {
                CyLog.LogError("[VOICE] no more dedicated vocal submixes are left");
                return false;
            }

            // TODO A more sophisticated pool with recycling that ties to agent disconnect up to whatever set limit

            // Get the next available voice mix
            var voiceMix = VoiceMixes[voiceIndex++];

            // Assign the new dedicated vocal submix group to the audio source
            audioSource.outputAudioMixerGroup = voiceMix;

            // Store the audio source for this agent
            if (voiceAudioSourcesByAgentId.ContainsKey(agentId)) voiceAudioSourcesByAgentId.Add(agentId, audioSource);
            else voiceAudioSourcesByAgentId[agentId] = audioSource;

            // Store the voice submix group for this agent
            if (!voiceMixesByAgentId.ContainsKey(agentId)) voiceMixesByAgentId.Add(agentId, voiceMix);
            else voiceMixesByAgentId[agentId] = voiceMix;

            return true;
        }

        #endregion Voice

        #region Utility

        /// <summary>
        /// Converts a linear value to decibel level.
        /// </summary>
        /// <param name="linear">The linear level.</param>
        /// <returns>The equivalent decivel level.</returns>
        public float LinearToDecibel(float linear)
        {
            float dB;

            if (linear != 0)
                dB = 20.0f * Mathf.Log10(linear);
            else
                dB = -144.0f;

            return dB;
        }

        #endregion Utility

        #endregion Methods
    }

    /// <summary>
    /// Event class used to capture events related to audio clips.
    /// </summary>
    [System.Serializable]
    public class UnityAudioClipEvent : UnityEvent<int, string, AudioSource, AudioClip> { };
}

