using System;
using System.Collections;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Adds audio playback to interactions.
    /// </summary>
    public class SessionsAudioInteraction : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The interactiable to use.
        /// </summary>
        [Tooltip("The interactiable to use.")]
        public SessionsInteractable Interactable;

        /// <summary>
        /// The type of interaction event to bind to.
        /// </summary>
        [Tooltip("The type of interaction event to bind to.")]
        public string Interaction = "Touch";

        /// <summary>
        /// When true, the interaction will be inverted and the audio will trigger on the stop event.
        /// </summary>
        [Tooltip("When true, the interaction will be inverted and the audio will trigger on the stop event.")]
        public bool Invert = false;

        /// <summary>
        /// Whether or not to stop playing audio when the event stops.
        /// </summary>
        [Tooltip("Whether or not to stop playing audio when the event stops.")]
        public bool StopWithEvent = true;

        /// <summary>
        /// The audio source to use.
        /// </summary>
        [Tooltip("The audio source to use.")]
        public AudioSource Audio;

        /// <summary>
        /// The audio clip to play.
        /// </summary>
        [Tooltip("The audio clip to play.")]
        public AudioClip Clip;

        /// <summary>
        /// Whether or not to loop the audio.
        /// </summary>
        [Tooltip("Whether or not to loop the audio.")]
        public bool Loop = false;

        /// <summary>
        /// The volume to play the audio clip back at.
        /// </summary>
        [Tooltip("The volume to play the audio clip back at.")]
        public float Volume = 1;

        /// <summary>
        /// The amount of time to fade in the audio in seconds.
        /// </summary>
        [Tooltip("The amount of time to fade in the audio in seconds.")]
        public float FadeInTime = 0;

        /// <summary>
        /// The amount of time to fade in the audio in seconds.
        /// </summary>
        [Tooltip("The amount of time to fade in the audio in seconds.")]
        public float FadeOutTime = 0;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        private void Start()
        {
            if (Audio == null) Audio = GetComponent<AudioSource>();
            if (Interactable == null) Interactable = GetComponent<SessionsInteractable>();

            if (Interactable != null)
            {
                Interactable.OnInteractionStarted.AddListener((interaction, pointer) =>
                {
                    if (Audio == null || Clip == null || interaction != Interaction) return;
                    if (Invert && !StopWithEvent) return;

                    if (!Invert) PlayClip();
                    else StopClip();
                });

                Interactable.OnInteractionStopped.AddListener((interaction, pointer) =>
                {
                    if (Audio == null || Clip == null || interaction != Interaction) return;
                    if (!Invert && !StopWithEvent) return;

                    if (!Invert) StopClip();
                    else PlayClip();
                });
            }
        }

        #endregion Init

        #region Operation

        // Plays the configured audio clip through the configured audio source
        private void PlayClip()
        {
            if (Audio.clip != Clip || !Audio.isPlaying)
            {
                Audio.Stop();
                Audio.clip = Clip;
                Audio.volume = 0; // start at 0 for fade in
                Audio.loop = Loop;
                Audio.Play();
                StartCoroutine(DoFadeInClip());
            }
        }

        // Stops playing the configured clip
        private void StopClip()
        {
            if (Audio != null && Audio.isPlaying) StartCoroutine(DoFadeOutClip());
        }

        // Fades in clip audio
        private IEnumerator DoFadeInClip()
        {
            var timer = 0f;

            while (timer < FadeInTime)
            {
                timer += Time.deltaTime;
                Audio.volume = Mathf.SmoothStep(0, Volume, timer / FadeInTime);
                yield return 0;
            }

            Audio.volume = Volume;
        }

        // Fades out clip audio
        private IEnumerator DoFadeOutClip()
        {
            var timer = 0f;

            while (timer < FadeOutTime)
            {
                timer += Time.deltaTime;
                Audio.volume = Mathf.SmoothStep(Volume, 0, timer / FadeOutTime);
                yield return 0;
            }

            Audio.volume = 0;
            Audio.Stop();
        }

        #endregion Operation

        #endregion Methods
    }
}
