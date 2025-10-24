using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    public class UIFollower : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The transform to follow.
        /// </summary>
        [Tooltip("The transform to follow.")]
        public Transform FollowTransform;

        /// <summary>
        /// When enabled, the follower will extend itself offset relative to the targets orientation.
        /// </summary>
        [Tooltip("When enabled, the follower will extend itself offset relative to the targets orientation.")]
        public bool OrientWithTarget = true;

        /// <summary>
        /// Whether or not to constantly update the UI's position every frame.
        /// </summary>
        [Tooltip("Whether or not to constantly update the UI's position every frame.")]
        public bool UpdatePosition = true;

        /// <summary>
        /// Whether or not to constantly update the UI's rotation every frame.
        /// </summary>
        [Tooltip("Whether or not to constantly update the UI's rotation every frame.")]
        public bool UpdateRotation = true;

        /// <summary>
        /// Whether or not to lock the UI's Y position.
        /// </summary>
        [Tooltip("Whether or not to lock the UI's Y position.")]
        public bool LockPositionY = false;

        /// <summary>
        /// Whether or not to lock the UI's X rotation.
        /// </summary>
        [Tooltip("Whether or not to lock the UI's X rotation.")]
        public bool LockRotationX = false;

        /// <summary>
        /// A position offset at which to track the transform.
        /// </summary>
        [Tooltip("A position offset at which to track the transform.")]
        public Vector3 Offset = Vector3.zero;

        /// <summary>
        /// The rotation to set the UI to.
        /// </summary>
        [Tooltip("The rotation to set the UI to.")]
        public Vector3 Rotation = Vector3.zero;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        private void Start()
        {
            UpdateOrientation(true, true);
        }

        #endregion Init

        #region Update

        private void LateUpdate()
        {
            if (UpdatePosition) UpdateOrientation(UpdatePosition, UpdateRotation);
        }

        #endregion Update

        /// <summary>
        /// Updates the orientation of the UI to face the target transform.
        /// </summary>
        /// <param name="updatePosition">Whether or not to update the position.</param>
        /// <param name="updateRotation">Whether or not to update the rotation.</param>
        public void UpdateOrientation(bool updatePosition, bool updateRotation)
        {
            if (FollowTransform == null) return;

            var pos = FollowTransform.position;
            var yPos = transform.position.y;
            var xRot = transform.eulerAngles.x;

            if (updatePosition)
            {
                if (OrientWithTarget)
                {
                    transform.position = pos + FollowTransform.TransformDirection(Offset);
                }
                else if (!OrientWithTarget)
                {
                    transform.position = pos + Offset;
                }
            }

            if (updateRotation)
            {
                if (OrientWithTarget)
                {
                    transform.LookAt(transform.position + FollowTransform.rotation * Vector3.forward,
                    FollowTransform.rotation * Vector3.up);
                    transform.localEulerAngles += Rotation;
                }
                else if (!OrientWithTarget && UpdatePosition)
                {
                    transform.localEulerAngles = Rotation;
                }
            }

            if (updatePosition && LockPositionY) transform.position = new Vector3(transform.position.x, yPos, transform.position.z);
            if (updateRotation && LockRotationX) transform.eulerAngles = new Vector3(xRot, transform.eulerAngles.y, transform.eulerAngles.z);
        }

        #endregion Methods
    }
}