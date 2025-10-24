using System;
using System.Collections;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Adds audio playback to interactions.
    /// </summary>
    public class SessionsParticleInteraction : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The interactiable to use.
        /// </summary>
        public SessionsInteractable Interactable;

        /// <summary>
        /// The type of interaction event to bind to.
        /// </summary>
        public string Interaction = "Touch";

        /// <summary>
        /// Whether or not to stop when the event stops.
        /// </summary>
        public bool StopWithEvent = true;

        /// <summary>
        /// The audio source to use.
        /// </summary>
        public ParticleSystem Particles;

        /// <summary>
        /// The constant emission rate for associated particles.
        /// </summary>
        public float EmissionRate = 10;

        /// <summary>
        /// The emission fade in time.
        /// </summary>
        public float FadeInTime = 0;

        /// <summary>
        /// The emission fade out time.
        /// </summary>
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
            if (Particles == null) Particles = GetComponent<ParticleSystem>();
            if (Interactable == null) Interactable = GetComponent<SessionsInteractable>();

            if (Interactable != null)
            {
                Interactable.OnInteractionStarted.AddListener((interaction, pointer) =>
                {
                    if (Particles == null || interaction != Interaction) return;
                    var emission = Particles.emission;
                    emission.rateOverTime = 0;
                    StartCoroutine(DoFadeIn());
                });

                Interactable.OnInteractionStopped.AddListener((interaction, pointer) =>
                {
                    if (!StopWithEvent || Particles == null || interaction != Interaction) return;
                    StartCoroutine(DoFadeOut());
                });
            }
        }

        // Fades in the particle emission
        private IEnumerator DoFadeIn()
        {
            var timer = 0f;
            var emission = Particles.emission;
            emission.enabled = true;

            while (timer < FadeInTime)
            {
                timer += Time.deltaTime;
                emission.rateOverTime = Mathf.SmoothStep(0, EmissionRate, timer / FadeInTime);
                yield return 0;
            }

            emission.rateOverTime = EmissionRate;
        }

        // Fades out the particle emission
        private IEnumerator DoFadeOut()
        {
            var timer = 0f;
            var emission = Particles.emission;

            while (timer < FadeOutTime)
            {
                timer += Time.deltaTime;
                emission.rateOverTime = Mathf.SmoothStep(EmissionRate, 0, timer / FadeOutTime);
                yield return 0;
            }

            emission.rateOverTime = 0;
            emission.enabled = false;
        }

        #endregion Init

        #endregion Methods
    }
}
