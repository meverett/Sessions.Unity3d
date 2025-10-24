using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using CymaticLabs.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using MP3Sharp;

namespace CymaticLabs.Language.Unity3d
{
    /// <summary>
    /// A behaviour that provides spoken and written language transcription and translation.
    /// </summary>
    public class LanguageServices : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The audio source to use when recording speech.
        /// </summary>
        [Header("Speech")]
        [Tooltip("The audio source to use when recording speech.")]
        public AudioSource AudioInput;

        /// <summary>
        /// The audio source to play translated audio through.
        /// </summary>
        [Tooltip("The audio source to play translated audio through.")]
        public AudioSource AudioOutput;

        /// <summary>
        /// The number of seconds of the vocal recording audio clip used for auto-translation.
        /// </summary>
        [Tooltip("The number of seconds of the vocal recording audio clip used for auto-translation.")]
        [Range(3, 30)]
        public int VocalClipLength = 30;

        /// <summary>
        /// The gain of translated speech (db).
        /// </summary>
        [Tooltip("The gain of translated speech (db).")]
        [Range(-96, 16)]
        public float TranslatedSpeechGain = 0f;

        /// <summary>
        /// A post-translation speech gain multiplier.
        /// </summary>
        [Tooltip("A post-translation speech gain multiplier.")]
        [Range(0, 3)]
        public float PostSpeechGain = 1.0f;

        /// <summary>
        /// The list of supported languages.
        /// </summary>
        [Tooltip("The list of supported languages.")]
        [Header("Language")]
        public TextAsset LanguageList;

        /// <summary>
        /// The speech-to-text URL to use.
        /// </summary>
        [Header("Speech-to-Text API")]
        [Tooltip("The speech-to-text URL to use.")]
        public string SpeechToTextURL = "https://speech.googleapis.com/v1/speech:recognize";

        /// <summary>
        /// The Google Speech API key to include in requests.
        /// </summary>
        [Tooltip("The Google Speech-to-Text API key to include in requests.")]
        public string SpeechToTextApiKey = "";

        /// <summary>
        /// The translate URL to use.
        /// </summary>
        [Header("Translation API")]
        [Tooltip("The translate URL to use.")]
        public string TranslateURL = "https://translation.googleapis.com/language/translate/v2";

        /// <summary>
        /// The Google Translate API key to include in requests.
        /// </summary>
        [Tooltip("The Google Translate API key to include in requests.")]
        public string TranslateApiKey = "";

        /// <summary>
        /// The text-to-speech URL to use.
        /// </summary>
        [Header("Text-to-Speech API")]
        [Tooltip("The text-to-speech URL to use.")]
        public string TextToSpeechURL = "https://texttospeech.googleapis.com/v1/text:synthesize";

        /// <summary>
        /// The Google Translate API key to include in requests.
        /// </summary>
        [Tooltip("The Google Text-to-Speech API key to include in requests.")]
        public string TextToSpeechApiKey = "";

        /// <summary>
        /// Occurs when user voice capture has started.
        /// </summary>
        [Space]
        public UnityEvent OnVoiceCaptureStarted;

        /// <summary>
        /// Occurs when user voice capture has stopped.
        /// </summary>
        public UnityEvent OnVoiceCaptureStopped;

        /// <summary>
        /// Occurs when user voice playback has started.
        /// </summary>
        public TranslationPlaybackUnityEvent OnVoicePlaybackStarted;

        /// <summary>
        /// Occurs when user voice playback has stopped.
        /// </summary>
        public TranslationPlaybackUnityEvent OnVoicePlaybackStopped;

        #endregion Inspector

        #region Fields

        // A list of available languages and their language codes
        private SortedDictionary<string, LanguageInfo> languageList;

        // The current language code being used for captured vocal audio
        private string captureLanguage;

        // The current user-supplied callback to trigger when vocal capture and transcription has completed.
        private Action<string, string> captureCallback;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static LanguageServices Current { get; private set; }

        /// <summary>
        /// Whether or not the translator is currently capturing voice audio.
        /// </summary>
        public bool IsCapturingVoice { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            IsCapturingVoice = false;
            Current = this;
            languageList = new SortedDictionary<string, LanguageInfo>();
            ParseLanguageList();
        }

        private void Start()
        {
            CyLog.LogInfo("[SPEECH] Translation Services Started");

            //TranslateAndSay("I don't know!", "en", "fr");
            //StartVoiceCapture(true);
            //var data = File.ReadAllBytes(@"C:\Dev\Sandbox\GCloud\test.raw");

            //SpeechToText("en-US", data, (transcript) =>
            //{
            //    TranslateAndSay(transcript, "en", "fr");
            //});
        }

        #endregion Init

        #region Speech-to-Text

        #region Voice Capture

        /// <summary>
        /// Starts recording a statement with the microphone.
        /// </summary>
        public void StartVoiceCapture(string language, Action<string, string> callback)
        {
            if (IsCapturingVoice || AudioInput == null) return;
            if (string.IsNullOrEmpty(language)) throw new ArgumentNullException("language");

            // Assign the language and callback references for later
            captureLanguage = language;
            captureCallback = callback;

            // Start capturing audio
            CyLog.LogVerbose("[SPEECH] starting capture");
            AudioInput.mute = true;
            AudioInput.timeSamples = 0;
            AudioInput.clip = Microphone.Start(null, false, VocalClipLength + 1, 16000);
            AudioInput.timeSamples = 0;
            AudioInput.Play();
            StartCoroutine(DoStopRecording(VocalClipLength));

            // Notify
            IsCapturingVoice = true;
            OnVoiceCaptureStarted.Invoke();
        }

        // Stops recording after a specified time delay
        private IEnumerator DoStopRecording(float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            if (AudioInput != null && AudioInput.isPlaying) StopVoiceCapture();
        }

        /// <summary>
        /// Stops recording the current statement with the microphone.
        /// </summary>
        public void StopVoiceCapture()
        {
            if (!IsCapturingVoice || AudioInput == null || AudioInput.clip == null) return;

            // Stop any coroutines that are presently waiting to stop recording based on a timeout
            StopAllCoroutines();

            // Convert to raw PCM
            AudioInput.Pause();
            var stopIndex = AudioInput.timeSamples;
            //CyLog.LogInfoFormat("{0} / {1}", AudioInput.timeSamples, AudioInput.clip.samples);
            //var data = new float[AudioInput.clip.samples];
            var data = new float[stopIndex + 1];
            AudioInput.clip.GetData(data, 0);
            var buffer = new byte[data.Length * 2]; // 2 bytes per sample, linear PCM raw audio
            var c = 0;

            for (var i = 0; i < data.Length; i++)
            {
                var sOut = (Int16)(data[i] * Int16.MaxValue);
                buffer[c++] = (byte)(sOut & 0xFF); // LSB
                buffer[c++] = (byte)(sOut >> 8); // MSB
            }

            IsCapturingVoice = false;
            AudioInput.timeSamples = 0;

            // Notify
            OnVoiceCaptureStopped.Invoke();

            CyLog.LogVerbose("[SPEECH] capture stopped");

            // Transcribe captured speech...
            SpeechToText(captureLanguage, buffer, (transcript) =>
            {
                // Notify
                if (captureCallback != null) captureCallback(captureLanguage, transcript);
            });
        }

        #endregion Voice Capture

        #region Convert-to-Text

        /// <summary>
        /// Converts a buffer of PCM audio into a string of text.
        /// </summary>
        /// <param name="language">The language of the speech audio.</param>
        /// <param name="pcm">The raw PCM buffer (it should be 1 channel, 16khz, 16 bit linear).</param>
        /// <param name="completed">A completed handler to be handed the text once it is transcribed.</param>
        public void SpeechToText(string language, byte[] pcm, Action<string> completed)
        {
            StartCoroutine(DoSpeechToText(language, pcm, completed));
        }

        private IEnumerator DoSpeechToText(string language, byte[] pcm, Action<string> completed)
        {
            if (pcm == null || pcm.Length == 0)
            {
                if (completed != null) completed(null);
                yield break;
            }

            if (string.IsNullOrEmpty(language)) language = "en-US";

            // Form the post data
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"config\":{");
            sb.Append("\"encoding\":\"LINEAR16\",");
            sb.Append("\"sampleRateHertz\":16000,");
            sb.AppendFormat("\"languageCode\":\"{0}\",", language);
            sb.Append("\"enableWordTimeOffsets\":false");
            sb.Append("},");
            sb.Append("\"audio\":{");
            sb.AppendFormat("\"content\":\"{0}\"", Convert.ToBase64String(pcm));
            sb.Append("}");
            sb.Append("}");

            // Form the URL with API key
            var url = SpeechToTextURL;
            url += (url.Contains("?") ? "&" : "?") + "key=" + SpeechToTextApiKey;

            var headers = new Dictionary<string, string>();
            //headers.Add("Authorization", "Bearer " + Authorization);
            headers.Add("Content-Type", "application/json");
            var data = Encoding.UTF8.GetBytes(sb.ToString());
            var www = new WWW(url, data, headers);
            yield return www;

            if (www.error != null)
            {
                CyLog.LogError(www.error);
            }
            else
            {
                var response = JsonConvert.DeserializeObject<JObject>(www.text);
                var results = response["results"];
                if (results == null) yield break;

                foreach (var result in results.Value<JArray>())
                {
                    foreach (var alt in result["alternatives"].Value<JArray>())
                    {
                        var transcript = alt["transcript"].Value<string>();
                        if (transcript != null)
                        {
                            if (completed != null) completed(transcript);
                            yield break;
                        }
                    }
                }
            }
        }

        #endregion Convert-to-Text

        #endregion Speech-to-Text

        #region Translate

        /// <summary>
        /// Translates text from one language to another.
        /// </summary>
        /// <param name="text">The text to translate.</param>
        /// <param name="fromLanguage">The language to translate from.</param>
        /// <param name="toLanguage">The language to translate to.</param>
        /// <param name="completed">A translation complete handler.</param>
        public void Translate(string text, string fromLanguage, string toLanguage, Action<string> completed)
        {
            if (string.IsNullOrEmpty(text)) throw new System.ArgumentException("text");
            if (string.IsNullOrEmpty(fromLanguage)) throw new System.ArgumentException("fromLanguage");
            if (string.IsNullOrEmpty(toLanguage)) throw new System.ArgumentException("toLanguage");
            fromLanguage = GetTranslateLanguageCode(fromLanguage);
            toLanguage = GetTranslateLanguageCode(toLanguage);

            // If both languages are the same, just return the initial text
            if (fromLanguage == toLanguage && completed != null)
            {
                completed(text);
                return;
            }

            // Otherwise start the translation
            StartCoroutine(DoTranslate(text, fromLanguage, toLanguage, completed));
        }

        private IEnumerator DoTranslate(string text, string fromLanguage, string toLanguage, Action<string> completed)
        {
            // Form the post data
            var sb = new StringBuilder();
            sb.Append("{");
            sb.AppendFormat("\"q\":\"{0}\",", text);
            sb.AppendFormat("\"source\":\"{0}\",", fromLanguage);
            sb.AppendFormat("\"target\":\"{0}\",", toLanguage);
            sb.Append("\"format\":\"text\"");
            sb.Append("}");

            // Form the URL with API key
            var url = TranslateURL;
            url += (url.Contains("?") ? "&" : "?") + "key=" + TranslateApiKey;

            Debug.Log(url);
            Debug.Log(sb.ToString());

            var headers = new Dictionary<string, string>();
            //headers.Add("Authorization", "Bearer " + Authorization);
            headers.Add("Content-Type", "application/json");
            var data = Encoding.UTF8.GetBytes(sb.ToString());
            var www = new WWW(url, data, headers);
            yield return www;

            if (www.error != null)
            {
                CyLog.LogError(www.error);
            }
            else
            {
                var response = JsonConvert.DeserializeObject<JObject>(www.text);
                var tData = response["data"];
                if (tData == null) yield break;
                var tTrans = tData["translations"];
                if (tTrans == null) yield break;
                var translations = tTrans.Value<JArray>();
                var translation = (from t in translations where t["translatedText"].Value<string>() != null select t["translatedText"].Value<string>()).FirstOrDefault();
                if (translation != null && completed != null) completed(translation);
            }
        }

        /// <summary>
        /// Gets the Google Translate API supported form of a language code.
        /// </summary>
        /// <param name="language">The language code value.</param>
        /// <returns>The Translate-API-compatible version of the language code.</returns>
        public string GetTranslateLanguageCode(string language)
        {
            if (string.IsNullOrEmpty(language)) throw new ArgumentNullException("language");

            language = language.Trim().ToLower();

            // Chinese special cases
            if (language.StartsWith("cmn-hant")) return "zh-TW";
            else if (language.StartsWith("cmn-hans")) return "zh-CN";

            // For everything else, just use what's on the first half of the dash
            if (language.Contains("-")) return language.Split('-')[0];

            // Otherwise just pass as-is
            return language;
        }

        #endregion Translate

        #region Text-to-Speech

        /// <summary>
        /// Plays text as speech.
        /// </summary>
        /// <param name="text">The text to convert to speech.</param>
        /// <param name="language">The language of the speaker and text.</param>
        /// <param name="voice">The voice to use when speaking.</param>
        /// <param name="userId">Optional user identifier of the user who is speaking.</param>
        /// <param name="audioSource">Optional audio source to use to play the speech clip.</param>
        public void PlayTextToSpeech(string text, string language = "en", TranslationVoices voice = TranslationVoices.Male, string userId = null, AudioSource audioSource = null)
        {
            StartCoroutine(DoPlayTextToSpeech(text, language, voice, userId, audioSource));
        }

        // Converts the text to speech and plays it
        private IEnumerator DoPlayTextToSpeech(string text, string language = "en", TranslationVoices voice = TranslationVoices.Male, string userId = null, AudioSource audioSource = null)
        {
            if (string.IsNullOrEmpty(text) || AudioOutput == null) yield break;

            // Form the post data
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"input\":{");
            sb.AppendFormat("\"text\":\"{0}\"", text);
            sb.Append("},");
            sb.Append("\"voice\":{");
            sb.AppendFormat("\"languageCode\":\"{0}\",", language);
            //sb.Append("\"languageCode\":\"en-gb\",");
            //sb.Append("\"name\":\"en-GB-Standard-A\",");
            sb.AppendFormat("\"ssmlGender\":\"{0}\"", voice.ToString().ToUpper());
            sb.Append("},");
            sb.Append("\"audioConfig\": {");
            sb.Append("\"audioEncoding\":\"MP3\",");
            sb.AppendFormat("\"volumeGainDb\":{0}", TranslatedSpeechGain);
            sb.Append("}");
            sb.Append("}");

            // Form the URL with API key
            var url = TextToSpeechURL;
            url += (url.Contains("?") ? "&" : "?") + "key=" + TextToSpeechApiKey;

            var headers = new Dictionary<string, string>();
            //headers.Add("Authorization", "Bearer " + Authorization);
            headers.Add("Content-Type", "application/json");
            var data = Encoding.UTF8.GetBytes(sb.ToString());
            var www = new WWW(url, data, headers);
            yield return www;

            if (www.error != null)
            {
                CyLog.LogError(www.error);
            }
            else
            {
                var target = audioSource != null ? audioSource : AudioOutput;

                target.clip = GetAudioDataFromJson(www.text);
                target.volume = 0;
                target.Play();
                var fadeInDuration = 0.25f;
                var fadeOutDuration = 0.1f;
                StartCoroutine(DoFadeSpeechIn(target, fadeInDuration));
                StartCoroutine(DoFadeSpeechOut(target, target.clip.length - (fadeOutDuration * 2) - (target.clip.length/2), fadeOutDuration, userId));

                // Notify
                OnVoicePlaybackStarted.Invoke(userId);
            }
        }

        // Fades in translated speech over a given duration to avoid pops.
        private IEnumerator DoFadeSpeechIn(AudioSource audioSource, float duration)
        {
            if (duration <= 0 || AudioOutput == null) yield break;

            float timer = 0;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(0, 1, timer / duration);
                yield return 0;
            }

            audioSource.volume = 1;
        }

        // Fades out translated speech over a given duration to avoid pops.
        private IEnumerator DoFadeSpeechOut(AudioSource audioSource, float delay, float duration, string userId = null)
        {
            if (duration <= 0 || AudioOutput == null) yield break;

            if (delay > 0) yield return new WaitForSeconds(delay);

            float timer = 0;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(1, 0, timer / duration);
                yield return 0;
            }

            audioSource.volume = 0;

            // Notify
            OnVoicePlaybackStopped.Invoke(userId);
        }

        // Gets an audio clip from a raw JSON response
        private AudioClip GetAudioDataFromJson(string json)
        {
            var buffer = GetRawAudioBufferFromJson(json);
            if (buffer == null || buffer.Length == 0) return null;

            // Now that the raw MP3 audio buffer is extracted from JSON, decode the mp3
            using (var ms = new MemoryStream(buffer))
            {
                using (var mp3 = new MP3Stream(ms))
                {
                    var channels = mp3.ChannelCount;
                    var sampleRate = mp3.Frequency;
                    var format = mp3.Format;
                    var decoded = new List<byte>();
                    var pcmBuffer = new byte[4096];

                    // read the entire mp3 file.
                    int bytesReturned = 1;
                    int totalBytesRead = 0;
                    while (bytesReturned > 0)
                    {
                        bytesReturned = mp3.Read(pcmBuffer, 0, pcmBuffer.Length);
                        totalBytesRead += bytesReturned;
                        for (var i = 0; i < bytesReturned; i++) decoded.Add(pcmBuffer[i]);
                    }

                    // Debug.LogFormat("ch:{0}, sr:{1}, f:{2}, el:{3}, dl:{4}", channels, sampleRate, format, mp3.Length, totalBytesRead);

                    // Convert the linear 16 PCM to 32 bit float
                    var rawPcm = decoded.ToArray();
                    var samples = new float[rawPcm.Length / 2]; // 2 bytes per sample in raw form

                    // Go through the raw 2 byte stream and convert to floats
                    for (var i = 0; i < samples.Length; i++)
                    {
                        var sIn = BitConverter.ToInt16(rawPcm, i * 2);
                        float sOut = (float)sIn / (float)Int16.MaxValue; // normalize audio
                        sOut *= PostSpeechGain;
                        if (sOut > 1) sOut = 1;
                        else if (sOut < -1) sOut = -1;
                        samples[i] = sOut;
                    }

                    // Create a new audio clip
                    var clip = AudioClip.Create("Translation", totalBytesRead, channels, 48000, false);
                    clip.SetData(samples, 0);
                    return clip;
                }
            }
        }

        // Gets the raw audio buffer from a JSOn response
        private byte[] GetRawAudioBufferFromJson(string json)
        {
            if (json == null || string.IsNullOrEmpty(json)) return new byte[0];
            var response = JsonConvert.DeserializeObject<JObject>(json);
            var audioContent = response["audioContent"];
            if (audioContent == null) return new byte[0];
            var rawBase64 = audioContent.Value<string>();
            if (string.IsNullOrEmpty(rawBase64)) return new byte[0];
            return Convert.FromBase64String(rawBase64);
        }

        #endregion Text-to-Speech

        #region Languages

        /// <summary>
        /// Gets the best matching language information based on the detected platform language.
        /// </summary>
        /// <returns>The best matching language information if found, otherwise NULL.</returns>
        public LanguageInfo GetPlatformLanguage()
        {
            return (from li in GetLanguages() where li.SystemLanguage == Application.systemLanguage select li).LastOrDefault();
        }

        /// <summary>
        /// Gets a list of all available languages.
        /// </summary>
        public LanguageInfo[] GetLanguages()
        {
            return languageList.Values.ToArray();
        }

        // Parse the available languages
        private void ParseLanguageList()
        {
            if (LanguageList == null)
            {
                CyLog.LogWarn("[SPEECH] language list is missing");
                return;
            }

            // Parse languages into lines
            var raw = LanguageList.text;
            if (raw != null) raw = raw.Replace("\r\n", "\n");
            var lines = raw.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 1)
            {
                CyLog.LogWarn("[SPEECH] no languages available in language list");
                return;
            }

            // Register all of the languages
            foreach (var line in lines)
            {
                var parsed = line.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parsed.Length < 3) continue;
                var label = parsed[0];
                var code = parsed[1];
                var system = parsed[2];
                SystemLanguage language = (SystemLanguage)Enum.Parse(typeof(SystemLanguage), system, true);
                if (!languageList.ContainsKey(code)) languageList.Add(code, new LanguageInfo(code, label, language));
            }

            CyLog.LogInfoFormat("[SPEECH] {0} languages available", languageList.Count);
        }

        #endregion Languages

        #region Utility

        /// <summary>
        /// Translates text from one language and speaks it in another.
        /// </summary>
        /// <param name="text">The text to translate and speak.</param>
        /// <param name="fromLanguage">The language the text is from.</param>
        /// <param name="toLanguage">The language to translate and speak in.</param>
        /// <param name="voice">The voice type to use when speaking.</param>
        /// <param name="userId">Optional user identifier of the user who is speaking.</param>
        /// <param name="audioSource">Optional audio source to use to play the speech clip.</param>
        public void TranslateAndSay(string text, string fromLanguage, string toLanguage, TranslationVoices voice = TranslationVoices.Male, 
            string userId = null, AudioSource audioSource = null)
        {
            Translate(text, fromLanguage, toLanguage, (translation) =>
            {
                PlayTextToSpeech(translation, toLanguage, voice, userId, audioSource);
            });
        }

        #endregion Utility

        #endregion Methods
    }

    /// <summary>
    /// Unity event related to translation playback.
    /// </summary>
    [Serializable]
    public class TranslationPlaybackUnityEvent : UnityEvent<string> { }
}
