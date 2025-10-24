using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Scales the parent object's transforms to a sine wave.
    /// </summary>
    public class SineScaler : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// Whether or not to use the minumum and maximum scales relative to current scale or as absolute values.
        /// </summary>
        public bool RelativeScale = false;

        /// <summary>
        /// The speed of the sine wave.
        /// </summary>
        public float Speed = 1.0f;

        /// <summary>
        /// The minimum scale.
        /// </summary>
        public Vector3 MinScale = Vector3.one;

        /// <summary>
        /// THe maximum scale.
        /// </summary>
        public Vector3 MaxScale = Vector3.one;

        #endregion Inspector

        #region Fields

        // The starting scale of the parent transform
        private Vector3 startScale;

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        private void Start()
        {
            startScale = transform.localScale;
        }

        #endregion Init

        #region Update

        private void Update()
        {
            var value = 0.5f + (Mathf.Sin(Time.time * Speed) / 2); // normalize the sine wave
            var scale = Vector3.Lerp(MinScale, MaxScale, value);

            if (RelativeScale)
            {
                transform.localScale = startScale + scale;
            }
            else
            {
                transform.localScale = scale;
            }
        }

        #endregion Update

        #endregion Methods
    }
}


