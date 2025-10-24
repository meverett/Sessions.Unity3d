using UnityEngine;

namespace CymaticLabs.Language.Unity3d
{
    /// <summary>
    /// Registers an audio source for use with translated speech playback.
    /// </summary>
    public class TranslationAudioSource : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The target audio source to use for translation playback.
        /// </summary>
        [Tooltip("The target audio source to use for translation playback.")]
        public AudioSource Audio;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            if (Audio == null) Audio = GetComponentInChildren<AudioSource>();
        }

        #endregion Init

        #region Update

        #endregion Update

        #endregion Methods
    }
}
