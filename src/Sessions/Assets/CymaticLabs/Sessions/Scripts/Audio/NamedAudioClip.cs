using System;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    [Serializable]
    public class NamedAudioClip
    {
        /// <summary>
        /// The alias/name of the audio clip.
        /// </summary>
        [Tooltip("The alias/name of the audio clip.")]
        public string Name;

        /// <summary>
        /// The audio clip to register with the name.
        /// </summary>
        [Tooltip("The audio clip to register with the name.")]
        public AudioClip Clip;

        /// <summary>
        /// The volume to play the clip at.
        /// </summary>
        [Tooltip("The volume to play the clip at.")]
        public float Volume;

        /// <summary>
        /// Whether or not to loop the clip.
        /// </summary>
        [Tooltip("Whether or not to loop the clip.")]
        public bool Loop = false;

        public NamedAudioClip()
        {
            Volume = 1.0f;
        }
    }
}
