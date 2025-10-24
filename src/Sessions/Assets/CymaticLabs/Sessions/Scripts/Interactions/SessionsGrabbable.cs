using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Enables an network interactable object to be grabbed.
    /// </summary>
    [RequireComponent(typeof(SessionsInteractable))]
    public class SessionsGrabbable : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The interactable to use.
        /// </summary>
        [Tooltip("The interactable to use.")]
        public SessionsInteractable Interactable;

        /// <summary>
        /// Whether or not physics objects will be kinematic when let go.
        /// </summary>
        [Tooltip("Whether or not physics objects will be kinematic when let go.")]
        public bool IsKinematicWhenDropped = true;

          /// <summary>
        /// The name of the interaction that will drive the grab.
        /// </summary>
        [Tooltip("The name of the interaction that will drive the grab.")]
        public string Interaction = "PrimaryGrab";

        #endregion Inspector

        #region Fields

        // Whether or not the current object is originally kinemetic
        private bool isKinematic = false;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Whether or not the object is currently in a "grabbed" state.
        /// </summary>
        public bool IsGrabbed { get; private set; }

        /// <summary>
        /// Gets the pointer that is grabbing the object (if any).
        /// </summary>
        public SessionsPointer GrabbingPointer { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Start()
        {
            var physics = GetComponent<Rigidbody>();
            if (physics != null) isKinematic = physics.isKinematic;

            if (Interactable == null) Interactable = GetComponent<SessionsInteractable>();

            if (Interactable != null)
            {
                Interactable.OnInteractionStarted.AddListener((interaction, pointer) =>
                {
                    if (Interactable.TouchState == InteractionStates.Started && interaction == Interaction)
                    {
                        StartGrabbing(pointer);
                    }
                });

                Interactable.OnInteractionStopped.AddListener((interaction, pointer) =>
                {
                    if (interaction == Interaction)
                    {
                        StopGrabbing(pointer);
                    }
                });
            }
        }

        #endregion Init

        #region Operation

        // Starts the grabbing process
        private void StartGrabbing(SessionsPointer pointer)
        {
            IsGrabbed = true;
            GrabbingPointer = pointer;
            transform.SetParent(pointer.transform);
            var physics = GetComponent<Rigidbody>();

            if (physics != null)
            {
                physics.isKinematic = true;
            }
        }

        // Stops the grabbing process
        private void StopGrabbing(SessionsPointer pointer)
        {
            IsGrabbed = false;
            GrabbingPointer = null;
            transform.SetParent(null);
            var physics = GetComponent<Rigidbody>();

            if (physics != null)
            {
                physics.isKinematic = IsKinematicWhenDropped;
            }
        }

        #endregion Operation

        #region Update

        #endregion Update

        #endregion Methods
    }
}
