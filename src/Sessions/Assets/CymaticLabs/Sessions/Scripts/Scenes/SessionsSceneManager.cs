using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using CymaticLabs.Sessions.Core;
using CymaticLabs.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Main manager class for Cymatic Sessions.
    /// </summary>
    public class SessionsSceneManager : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// Whether or not to playback the session from recorded file.
        /// </summary>
        [Tooltip("Whether or not to playback the session from recorded file.")]
        public bool PlaybackFromFile = false;

        /// <summary>
        /// The path to a recorded session file to playback.
        /// </summary>
        [Tooltip("The path to a recorded session file to playback.")]
        public string PlaybackFilePath;

        /// <summary>
        /// The current scenes configuration.
        /// </summary>
        [Tooltip("The current scenes configuration.")]
        //[HideInInspector]
        public TextAsset Configuration;

        /// <summary>
        /// Occurs when the scenes configuration has finished loading.
        /// </summary>
        public UnityEvent OnConfigurationLoaded;

        /// <summary>
        /// Occurs when a session's value changes.
        /// </summary>
        public SessionValueEvent OnSessionValueChanged;

        /// <summary>
        /// Occurs when the progress of a loading scene is updated.
        /// </summary>
        public SessionsSceneProgressEvent OnSceneLoadProgress;

        /// <summary>
        /// Occurs when a new scene has loaded.
        /// </summary>
        public SessionsSceneEvent OnSceneLoaded;

        #endregion Inspector

        #region Fields

        // An internal list of registered value setters given a value's name
        private Dictionary<string, List<SessionsValueHandler>> valueHandlers;

        // Internal list of session values/session state
        private Dictionary<string, float?> sessionValues;

        // An internal buffer of recorded session values
        private List<RecordedSessionValue> recordedValues;

        // A list of recorded names by a normalized integer ID
        private Dictionary<int, string> recordedNamesById;
        private Dictionary<string, int> recordedIdsByName;

        // The time when the last recording was started
        private float recordStartTime = 0;

        // The time when the last recording was ended
        private float recordEndTime = 0;

        // Time for playing back recorded sessions
        private float playTime = 0;

        // The index of the currently playing recoded sample
        private int playIndex = 0;

        // A list of available sessions scenes
        private List<SessionsSceneInfo> scenesList;

        #endregion Fields

        #region Properties

        /// <summary>
        /// The current singleton instance of the manager.
        /// </summary>
        public static SessionsSceneManager Current { get; private set; }

        /// <summary>
        /// Gets the current skybox material of the session.
        /// </summary>
        public Material CurrentSkyboxMaterial { get; private set; }

        /// <summary>
        /// The current audio track that is playing.
        /// </summary>
        public AudioSource CurrentAudioTrack { get; private set; }

        /// <summary>
        /// The current scene's information.
        /// </summary>
        public SessionsSceneInfo CurrentSceneInfo { get; internal set; }

        /// <summary>
        /// The current scene.
        /// </summary>
        public SessionsScene CurrentScene { get; internal set; }

        /// <summary>
        /// Whether or not the session is currently being recorded.
        /// </summary>
        public bool IsRecording { get; private set; }

        /// <summary>
        /// Whether or not the session is currently playing back from a recording.
        /// </summary>
        public bool IsPlaying { get; private set; }

        /// <summary>
        /// Gets whether or not this is the lobby scene.
        /// </summary>
        public bool IsLobby { get; private set; }

        /// <summary>
        /// The runtime scenes configuration data.
        /// </summary>
        public SessionsScenesConfiguration ScenesConfiguration { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            Current = this; // set singleton instance
            IsRecording = false;
            IsPlaying = false;
            sessionValues = new Dictionary<string, float?>();
            valueHandlers = new Dictionary<string, List<SessionsValueHandler>>();
            recordedValues = new List<RecordedSessionValue>();
            recordedNamesById = new Dictionary<int, string>();
            recordedIdsByName = new Dictionary<string, int>();
            scenesList = new List<SessionsSceneInfo>();
            IsLobby = SceneManager.GetActiveScene().name == "SessionsLobby";
        }

        private void Start()
        {
            // TODO Remove
            // Load from recording
            if (PlaybackFromFile) LoadRecording(PlaybackFilePath);

            #region Register Transport Control Handlers            

            // Previous
            RegisterValueHandler("Transport/Prev", (name, value) =>
            {
                CyLog.LogInfo("Implement transport 'previous' control!");
            });

            // Scrub Back
            RegisterValueHandler("Transport/Scrub/Back", (name, value) =>
            {
                CyLog.LogInfo("Implement transport 'scrub back' control!");
            });

            // Record
            RegisterValueHandler("Transport/Rec", (name, value) =>
            {
                if (value == 1) StartRecording();
            });

            // Stop
            RegisterValueHandler("Transport/Stop", (name, value) =>
            {
                if (value != 1) return;

                if (IsRecording) StopRecording();
                else if (IsPlaying) StopPlayback();
            });

            // Play
            RegisterValueHandler("Transport/Play", (name, value) =>
            {
                if (value == 1) StartPlayback();
            });

            // Scrub Forward
            RegisterValueHandler("Transport/Scrub/Fwd", (name, value) =>
            {
                CyLog.LogInfo("Implement transport 'scrub forward' control!");
            });

            // Previous
            RegisterValueHandler("Transport/Next", (name, value) =>
            {
                CyLog.LogInfo("Implement transport 'next' control!");
            });

            #endregion Register Transport Control Handlers

            // Load configuration
            LoadConfiguration();

            // Play welcome if this is the lobby
            if (IsLobby)
            {
                StartCoroutine(DoWelcomeLobby(2f));
            }
            // If this isn't the lobby scene, attempt to connect to a session
            else
            {
                StartCoroutine(DoWelcomeScene(1f));
            }
        }

        // Does a delayed welcome to the lobby
        private IEnumerator DoWelcomeLobby(float delay = 0)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            SessionsSound.PlayEfx("Welcome", false, 0.333f, true);
        }

        // Does a delayed welcome to the scene
        private IEnumerator DoWelcomeScene(float delay = 0)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);

            var isHosting = PlayerPrefs.GetInt("SessionsIsHosting") == 1;
            var isReady = PlayerPrefs.GetString("SessionsCurrentUrl") != null; // TODO Verify it matches this scene?

            // If this client is ready for a session with this scene, open the connection menu
            if (isReady && SessionsFacilitatorMenu.Current != null)
            {
                SessionsFacilitatorMenu.Current.SetHosting(isHosting);
                SessionsFacilitatorMenu.Current.ShowMenu(true);
            }
        }

        #endregion Init

        #region Update

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3))
            {
                // Toggle recording
                if (IsRecording) StopRecording();
                else StartRecording();
            }

            if (Input.GetKeyDown(KeyCode.F4))
            {
                if (IsRecording || recordedValues.Count == 0) return;
                SaveRecording(PlaybackFilePath);
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                // Toggle recording
                if (IsPlaying) StopPlayback();
                else StartPlayback();
            }

            if (IsPlaying)
            {
                // Increase playback timer
                playTime += Time.deltaTime;
                bool stillPlaying = false;

                // Move through the current recorded frames...
                while (playIndex < recordedValues.Count)
                {
                    // Get the current recorded frame...
                    var frame = recordedValues[playIndex];

                    // Play this frame if its time has come
                    if (playTime >= frame.Time)
                    {
                        var name = recordedNamesById[frame.Id];
                        HandleSessionValueChange(name, frame.Value, frame.Raw);
                        playIndex++;
                        stillPlaying = true;
                    }
                    // Otherwise if the frame time has not arrived yet stop...
                    else
                    {
                        stillPlaying = true;
                        break;
                    }
                }

                // Automatically stop playback if we reach the end of playback
                if (!stillPlaying) StopPlayback();
            }
        }

        #endregion Update

        #region Sessions

        #region Values

        /// <summary>
        /// Handles a session value being updated.
        /// </summary>
        /// <param name="name">The name of the value.</param>
        /// <param name="value">The value.</param>
        /// <param name="raw">The optional raw source object for the value update.</param>
        public void HandleSessionValueChange(string name, float value, object raw)
        {
            if (string.IsNullOrEmpty(name)) throw new System.ArgumentNullException("name");

            // Update the internal session value
            if (!sessionValues.ContainsKey(name)) sessionValues.Add(name, value);
            else sessionValues[name] = value;

            // Record the value
            if (IsRecording)
            {
                // Normalize this name to save on space
                if (!recordedIdsByName.ContainsKey(name))
                {
                    recordedIdsByName.Add(name, recordedNamesById.Count);
                    recordedNamesById.Add(recordedNamesById.Count, name);
                }

                var id = recordedIdsByName[name];
                recordedValues.Add(new RecordedSessionValue(id, value, Time.time));
            }

            // Dispatch session value change to handlers
            if (valueHandlers.ContainsKey(name))
                foreach (var handler in valueHandlers[name]) handler(name, value);

            // Rebroadcast
            OnSessionValueChanged.Invoke(name, value, raw);
        }

        /// <summary>
        /// Gets a session value given its name.
        /// </summary>
        /// <param name="name">The name of the session value to get.</param>
        /// <returns>The session value if it is found, otherwise 0.</returns>
        public float? GetSessionValue(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new System.ArgumentNullException("name");
            return sessionValues.ContainsKey(name) ? sessionValues[name] : null;
        }

        /// <summary>
        /// Gets a list of all of the current session values' names.
        /// </summary>
        /// <returns>A list of all value names currently in the session.</returns>
        public string[] GetSessionValueNames()
        {
            var keys = valueHandlers.Keys;
            var list = new string[keys.Count];
            keys.CopyTo(list, 0);
            return list;
        }

        /// <summary>
        /// Registers a value handler for a given session value.
        /// </summary>
        /// <param name="name">The name of the session value to register the handler with.</param>
        /// <param name="handler">The handler delegate that will handle session value changes.</param>
        public void RegisterValueHandler(string name, SessionsValueHandler handler)
        {
            if (string.IsNullOrEmpty(name)) throw new System.ArgumentNullException("name");
            if (handler == null) throw new System.ArgumentNullException("handler");

            // Ensure handler list for current value
            if (!valueHandlers.ContainsKey(name)) valueHandlers.Add(name, new List<SessionsValueHandler>());

            // Add the current handler
            var list = valueHandlers[name];
            if (!list.Contains(handler)) list.Add(handler);
        }

        /// <summary>
        /// Unregisters a value handler from a given session value.
        /// </summary>
        /// <param name="name">The name of the session value to unregister the handler from.</param>
        /// <param name="handler">The handler delegate to remove.</param>
        public void UnregisterValueHandler(string name, SessionsValueHandler handler)
        {
            if (string.IsNullOrEmpty(name)) throw new System.ArgumentNullException("name");
            if (handler == null) throw new System.ArgumentNullException("handler");

            // Ensure handler list for current value
            if (!valueHandlers.ContainsKey(name)) return;

            // Add the current handler
            var list = valueHandlers[name];
            if (list.Contains(handler)) list.Remove(handler);
        }

        #endregion Values

        #region Recording & Playback

        #region Recording

        /// <summary>
        /// Starts recording the session.
        /// </summary>
        public void StartRecording()
        {
            if (IsRecording) return;
            StopPlayback();
            recordedNamesById.Clear();
            recordedIdsByName.Clear();
            recordedValues.Clear();
            recordStartTime = Time.time;
            IsRecording = true;
            CyLog.LogInfo("[REC] Session recording started");
        }

        /// <summary>
        /// Stops recording the session.
        /// </summary>
        public void StopRecording()
        {
            if (!IsRecording) return;
            recordEndTime = Time.time;
            IsRecording = false;

            // Go through and convert frame times from absolute to relative
            for (var i = 0; i < recordedValues.Count; i++)
            {
                recordedValues[i].Time -= recordStartTime;
            }

            CyLog.LogInfoFormat("[REC] Session recording stopped with {0} samples.", recordedValues.Count);
        }

        /// <summary>
        /// Clears the current recording.
        /// </summary>
        public void ClearRecording()
        {
            recordedValues.Clear();
        }

        #endregion Recording

        #region Playback

        /// <summary>
        /// Starts playback of the current buffer of recorded session values.
        /// </summary>
        /// <param name="time">The time at which to start playback.</param>
        public void StartPlayback(float? time = null)
        {
            StopRecording();

            if (time != null)
            {
                playTime = time.Value;
                // TODO seek to play index?
            }
            else
            {
                playTime = recordedValues.Count > 0 ? recordedValues[0].Time : 0;
                playIndex = 0;
            }

            if (IsPlaying) return;

            playIndex = 0;
            IsPlaying = true;

            CyLog.LogInfo("[PLAY] Session playback started");
        }

        /// <summary>
        /// Stops recorded session value playback.
        /// </summary>
        public void StopPlayback()
        {
            if (!IsPlaying) return;
            IsPlaying = false;
            CyLog.LogInfo("[PLAY] Session playback stopped");
        }

        #endregion Playback

        #region Save

        /// <summary>
        /// Saves the current in-memory recording to file.
        /// </summary>
        /// <param name="path">The file path to save the file to.</param>
        public void SaveRecording(string path = null)
        {
            try
            {
                // Generate the recording file name and path
                var filename = DateTime.Now.Ticks.ToString() + ".srf";
                if (string.IsNullOrEmpty(path)) path = Path.Combine(Directory.GetCurrentDirectory(), filename);

                using (FileStream fs = new FileStream(path, FileMode.Create))
                {
                    SaveRecording(fs);
                }

                CyLog.LogInfoFormat("Session recording saved to {0}", path);
            }
            catch (Exception ex)
            {
                CyLog.LogError(ex);
            }
        }

        /// <summary>
        /// Saves the current in-memory recording to stream.
        /// </summary>
        /// <param name="stream">The stream to save to.</param>
        public void SaveRecording(Stream stream)
        {
            try
            {
                var duration = recordEndTime - recordStartTime;

                using (BinaryWriter bw = new BinaryWriter(stream))
                {
                    // Write the version (2 bytes)
                    ushort version = 1;
                    bw.Write(version);

                    // Reserved
                    bw.Write((byte)0); // reserved (1 byte)

                    // Clip duration in seconds as float (4 bytes)
                    bw.Write(duration);

                    // Get the length of the session name to write
                    var sessionName = "Sessions";
                    var nameBuff = System.Text.Encoding.UTF8.GetBytes(sessionName);

                    // Session name length (1 byte)
                    bw.Write((byte)nameBuff.Length); // 255 max characters for name

                    // Write the name
                    bw.Write(nameBuff);

                    // Next write the value-to-ID table length (4 bytes)
                    bw.Write(recordedNamesById.Count);

                    foreach (var pair in recordedNamesById)
                    {
                        // Write the integer ID
                        bw.Write(pair.Key);

                        // Now write the name length
                        nameBuff = System.Text.Encoding.UTF8.GetBytes(pair.Value);
                        bw.Write((byte)nameBuff.Length); // name length (1 byte) 255 characters max
                        bw.Write(nameBuff);
                    }

                    // Write the samples length
                    int totalFrames = recordedValues.Count;
                    bw.Write(totalFrames);

                    // Now write each sample
                    foreach (var frame in recordedValues)
                    {
                        // Write the frame name ID
                        bw.Write(frame.Id);

                        // Write the frame value
                        bw.Write(frame.Value);

                        // Write the frame time
                        bw.Write(frame.Time);
                    }
                }


                CyLog.LogInfo("Session recording saved");
            }
            catch (Exception ex)
            {
                CyLog.LogError(ex);
            }
        }

        #endregion Save

        #region Load

        /// <summary>
        /// Loads a recorded session from file.
        /// </summary>
        /// <param name="path">The file path to the recorded session file to load.</param>
        public void LoadRecording(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");

            if (!File.Exists(path))
            {
                CyLog.LogErrorFormat("Cannot load session recording from missing file {0}", path);
                return;
            }

            try
            {
                CyLog.LogInfoFormat("Opening recorded session file {0}...", path);

                using (FileStream fs = new FileStream(path, FileMode.Open))
                {
                    LoadRecording(fs);
                }
            }
            catch (Exception ex)
            {
                CyLog.LogError(ex);
            }
        }

        /// <summary>
        /// Loads a recorded session from file.
        /// </summary>
        /// <param name="stream">The file stream to load from.</param>
        public void LoadRecording(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException("stream");

            try
            {
                float duration = -1f;
                CyLog.LogInfo("Loading recorded session file...");

                recordedValues.Clear();
                recordedIdsByName.Clear();
                recordedNamesById.Clear();

                // The total number of loaded frames
                var totalFrames = 0;
                var totalValues = 0;

                using (BinaryReader br = new BinaryReader(stream))
                {
                    // Read version (2 bytes)
                    ushort version = br.ReadUInt16();
                    CyLog.LogInfoFormat("File Version: {0}", version);

                    // Read reserved (1 byte)
                    //var reserved = br.ReadByte();
                    br.ReadByte();

                    // Read clip duration (4 bytes)
                    duration = br.ReadSingle();

                    // Read name length (1 byte)
                    var nameLength = br.ReadByte();

                    // Read the name
                    var nameBuff = br.ReadBytes(nameLength);
                    var sessionName = System.Text.Encoding.UTF8.GetString(nameBuff, 0, nameLength);

                    CyLog.LogInfoFormat("Session: {0}", sessionName);

                    // Next read the number of normalized value names
                    var idCount = br.ReadInt32();

                    // Read in the name to ID conversions
                    for (var i = 0; i < idCount; i++)
                    {
                        var id = br.ReadInt32();
                        nameLength = br.ReadByte();
                        nameBuff = br.ReadBytes(nameLength);
                        var valueName = System.Text.Encoding.UTF8.GetString(nameBuff, 0, nameLength);

                        // Load the ID/value name look ups into memory
                        if (!recordedIdsByName.ContainsKey(valueName))
                        {
                            recordedIdsByName.Add(valueName, id);
                            recordedNamesById.Add(id, valueName);
                        }

                        CyLog.LogVerboseFormat("[VALUE] {0} = {1}", id, valueName);
                        totalValues++;
                    }

                    // Read in the number of frames
                    totalFrames = br.ReadInt32();

                    for (var i = 0; i < totalFrames; i++)
                    {
                        var id = br.ReadInt32();
                        var value = br.ReadSingle();
                        var time = br.ReadSingle();
                        recordedValues.Add(new RecordedSessionValue(id, value, time));
                    }
                }


                CyLog.LogInfoFormat("Total unique values in recording: {0}", totalValues);
                CyLog.LogInfoFormat("Total frames loaded: {0}", totalFrames);
                CyLog.LogInfoFormat("Recording duration: {0:0.000} sec", duration);
                CyLog.LogInfoFormat("Recorded session file loaded");
            }
            catch (Exception ex)
            {
                CyLog.LogError(ex);
            }
        }

        #endregion Load

        #endregion Recording & Playback

        #endregion Sessions

        #region Scenes

        /// <summary>
        /// Sets the current scene given its index in the current scenes configuration.
        /// </summary>
        /// <param name="index">The index of the scene to set.</param>
        /// <returns>The scene if it was found, otherwise NULL.</returns>
        public void SetCurrentScene(int index)
        {
            if (index < 0 || ScenesConfiguration == null || ScenesConfiguration.Scenes == null || index >= ScenesConfiguration.Scenes.Length) return;
            CurrentSceneInfo = ScenesConfiguration.Scenes[index];
            CyLog.LogInfoFormat("[SCENE] current scene set to '{0}'", CurrentSceneInfo != null ? CurrentSceneInfo.Name : "");
        }

        /// <summary>
        /// Begins additively loading a scene by name.
        /// </summary>
        /// <param name="name">The name of the scene to load.</param>
        /// <param name="callback">The optional callback to call when the scene is done loading.</param>
        public void LoadScene(string name, Action<string> callback = null)
        {
            StartCoroutine(DoLoadScene(name, callback));
        }

        /// <summary>
        /// Begins additively loading a scene by name.
        /// </summary>
        /// <param name="name">The name of the scene to load.</param>
        public void LoadScene(string name)
        {
            StartCoroutine(DoLoadScene(name, null));
        }

        // Loads a scene in the background
        private IEnumerator DoLoadScene(string name, Action<string> callback)
        {
            var ogName = name;
            CyLog.LogInfoFormat("[SCENE] loading scene: {0}", ogName);

            // Is this a scene contained within the application?
            var isAppScene = false;

            // If this is an in-app scene...
            if (name.StartsWith("app://"))
            {
                isAppScene = true;

                // Get its name and load it
                name = name.Replace("app://", "");

                // Show scene load progress
                if (SessionsProgressMenu.Current != null)
                {
                    SessionsProgressMenu.Current.SetProgress(0);
                    SessionsProgressMenu.Current.ShowMenu();
                }
            }
            else if (name.Contains("://"))
            {
                CyLog.LogWarnFormat("Sessions scene URL type not supported: {0}", name);
            }

            // If this is a scene contained within the application, load it additively
            if (isAppScene)
            {
                // If this is the default lobby scene...
                if (name == "SessionsLobby" && SceneManager.GetActiveScene().name == "SessionsLobby")
                {
                    // Hide scene load progress
                    if (SessionsProgressMenu.Current != null) SessionsProgressMenu.Current.HideMenu();

                    // There's nothing to do, it should already be loaded
                    if (callback != null) callback(ogName);
                    yield break;
                }

                if (SessionsUdpNetworking.IsXR)
                {
                    var sceneFader = FindObjectOfType<OVRScreenFade>();

                    if (sceneFader != null)
                    {
                        var duration = sceneFader.fadeTime;
                        sceneFader.FadeOut();
                        yield return new WaitForSeconds(duration);
                        if (callback != null) callback(ogName);
                        SessionsUdpNetworking.Current.EndSession();
                        yield return new WaitForSeconds(2); // wait a bit
                        SceneManager.LoadScene(name, LoadSceneMode.Single);
                    }
                }
                else
                {
                    if (callback != null) callback(ogName);
                    SessionsUdpNetworking.Current.EndSession(); // cleanly disconnect
                    yield return new WaitForSeconds(2); // wait a bit
                    SceneManager.LoadScene(name, LoadSceneMode.Single);
                }
            }
        }

        #endregion Scenes

        #region Application

        /// <summary>
        /// Shuts sessions down and shuts the application.
        /// </summary>
        public void QuitApp()
        {
            StartCoroutine(DoQuitApp());
        }

        // Shuts sessions down and shuts the application.
        IEnumerator DoQuitApp()
        {
            if (SessionsUdpNetworking.Current != null) SessionsUdpNetworking.Current.CleanUp();
            SessionsSound.PlayEfx("Quit");
            yield return new WaitForSeconds(2);
            Application.Quit();
        }

        #endregion Application

        #region Configuration

        /// <summary>
        /// Loads the scenes configuration
        /// </summary>
        public void LoadConfiguration()
        {
            // Set the current scene to the first one in the configuration
            CurrentSceneInfo = null;
            
            if (Configuration == null)
            {
                CyLog.LogWarn("Scene Manager has no configuration file applied and no configuration will be loaded.");
                return;
            }

            if (scenesList == null) scenesList = new List<SessionsSceneInfo>();
            scenesList.Clear();

            // If a settings file was supplied, use that
            if (Configuration != null)
            {
                var config = JsonConvert.DeserializeObject<DataSessionsScenesConfiguration>(Configuration.text);
                ScenesConfiguration = SessionsScenesConfiguration.ToScriptable(config);
                SetCurrentScene(0); // set the current scene to the first index in the list
            }

            // Notify
            OnConfigurationLoaded.Invoke();
        }

        #endregion Configuration

        #region Utility

        /// <summary>
        /// Gets the current platform that the sessions client is running on.
        /// </summary>
        /// <returns>The detected platform.</returns>
        public static SessionsPlatforms GetPlatform()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                    if (UnityEngine.XR.XRSettings.enabled) return SessionsPlatforms.Android_VR;
                    // TODO figure out AR vs VR?
                    return SessionsPlatforms.Android;

                case RuntimePlatform.IPhonePlayer:
                    if (UnityEngine.XR.XRSettings.enabled) return SessionsPlatforms.iOS_VR;
                    // TODO figure out AR vs VR?
                    return SessionsPlatforms.iOS;

                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.LinuxPlayer:
                    if (UnityEngine.XR.XRSettings.enabled) return SessionsPlatforms.Linux_VR;
                    // TODO figure out AR vs VR?
                    return SessionsPlatforms.Linux;

                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    if (UnityEngine.XR.XRSettings.enabled) return SessionsPlatforms.Mac_VR;
                    // TODO figure out AR vs VR?
                    return SessionsPlatforms.Mac;

                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    if (UnityEngine.XR.XRSettings.enabled) return SessionsPlatforms.Win_VR;
                    // TODO figure out AR vs VR?
                    return SessionsPlatforms.Win;

                default:
                    return SessionsPlatforms.Unsuported;
            }
        }

        #endregion Utility

        #endregion Methods
    }

    /// <summary>
    /// Represents a session value recorded in time.
    /// </summary>
    public class RecordedSessionValue
    {
        /// <summary>
        /// The name of the session value.
        /// </summary>
        public int Id;

        /// <summary>
        /// The value that was recorded.
        /// </summary>
        public float Value;

        /// <summary>
        /// The time stamp at which the value was recorded.
        /// </summary>
        public float Time;

        /// <summary>
        /// The optional raw object associated with the value change.
        /// </summary>
        public object Raw;

        /// <summary>
        /// Creates a new recorded session value.
        /// </summary>
        /// <param name="id">The id of the value.</param>
        /// <param name="value">The value.</param>
        /// <param name="time">The time of the recording.</param>
        /// <param name="raw">The optional raw object associated with the value change.</param>
        public RecordedSessionValue(int id, float value, float time, object raw = null)
        {
            Id = id;
            Value = value;
            Time = time;
            Raw = raw;
        }
    }

    /// <summary>
    /// Handler delegate used to handle events or updates to session values.
    /// </summary>
    /// <param name="name">The name of the session value.</param>
    /// <param name="value">The new/current value.</param>
    public delegate void SessionsValueHandler(string name, float value);

    /// <summary>
    /// Unity event related to <see cref="SessionScene"/>.
    /// </summary>
    [Serializable]
    public class SessionsSceneEvent : UnityEvent<string> { };

    /// <summary>
    /// Unity event related to the loading progress of a <see cref="SessionScene"/>.
    /// </summary>
    [Serializable]
    public class SessionsSceneProgressEvent : UnityEvent<float> { };
}
