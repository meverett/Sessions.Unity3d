using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Simple behavior that matches the current transform to a target transform.
    /// </summary>
    public class MatchTransform : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The target transform to match.
        /// </summary>
        public Transform Target;

        /// <summary>
        /// Whether or not to match position.
        /// </summary>
        public bool MatchPosition = true;

        /// <summary>
        /// Whether or not to match rotation.
        /// </summary>
        public bool MatchRotation = true;

        /// <summary>
        /// Whether or not to match scale.
        /// </summary>
        public bool MatchScale = true;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        #endregion Init

        #region Update

        private void Update()
        {
            if (Target == null) return;
            if (MatchPosition) transform.position = Target.position;
            if (MatchRotation) transform.rotation = Target.rotation;
            if (MatchScale) transform.localEulerAngles = Target.localEulerAngles;
        }

        #endregion Update

        #endregion Methods
    }
}
