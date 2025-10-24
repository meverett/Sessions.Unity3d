using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Resets a character controller's position if it reaches a certain height threshold.
    /// </summary>
    public class ResetPositionOnHeightReached : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The Y units at which a ceiling threshold trigger will occur.
        /// </summary>
        public float CeilingThreshold = 100;

        /// <summary>
        /// The Y units at which a floor threshold trigger will occur.
        /// </summary>
        public float FloorThreshold = -100;

        /// <summary>
        /// The position to place the character at during a reset event.
        /// </summary>
        public Vector3 ResetPosition;

        /// <summary>
        /// The rotation to place the character at during a reset event.
        /// </summary>
        public Vector3 ResetRotation;

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
            if (transform.position.y < FloorThreshold)
            {
                ResetCharacter();
            }
            else if (transform.position.y > CeilingThreshold)
            {
                ResetCharacter();
            }
        }

        #endregion Update

        /// <summary>
        /// Resets the current character.
        /// </summary>
        public void ResetCharacter()
        {
            var character = GetComponentInChildren<CharacterController>();

            if (character != null)
            {
                character.transform.position = ResetPosition;
                character.transform.eulerAngles = ResetRotation;
            }
            else
            {
                transform.position = ResetPosition;
                transform.eulerAngles = ResetRotation;
            }
        }

        #endregion Methods
    }

}