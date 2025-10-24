using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using CymaticLabs.Logging;
using CymaticLabs.Sessions.Core;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// A pointer that interacts with objects in the session scene.
    /// </summary>
    public class SessionsPointer : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The default length of the pointer.
        /// </summary>
        [Tooltip("The default length of the pointer.")]
        public float PointerLength = 50;

        /// <summary>
        /// The line render used to render the pointer.
        /// </summary>
        [Tooltip("The line render used to render the pointer.")]
        public LineRenderer LineRenderer;

        /// <summary>
        /// The network entity the pointer is attached to (if any).
        /// </summary>
        [Tooltip("The network entity the pointer is attached to (if any).")]
        public SessionsNetworkEntity NetworkEntity;

        #endregion Inspector

        #region Fields

        // Singleton list of pointers
        private static List<SessionsPointer> pointers = new List<SessionsPointer>();

        // The current interaction (if any)
        private SessionsInteractable currentInteraction;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets the owning agent of the pointer.
        /// </summary>
        public SessionAgent Owner
        {
            get { return NetworkEntity == null ? null : NetworkEntity.Owner; }
        }

        /// <summary>
        /// Gets whether or not the primary interaction button is currently held down.
        /// </summary>
        public bool PrimaryButton { get; protected set; }

        /// <summary>
        /// Gets whether or not the primary interaction button has just been pressed down.
        /// </summary>
        public bool PrimaryButtonDown { get; protected set; }

        /// <summary>
        /// Gets whether or not the primary interaction button has just been released.
        /// </summary>
        public bool PrimaryButtonUp { get; protected set; }

        /// <summary>
        /// Gets whether or not the secondary interaction button is currently held down.
        /// </summary>
        public bool SecondaryButton { get; protected set; }

        /// <summary>
        /// Gets whether or not the secondary interaction button has just been pressed down.
        /// </summary>
        public bool SecondaryButtonDown { get; protected set; }

        /// <summary>
        /// Gets whether or not the secondary interaction button has just been released.
        /// </summary>
        public bool SecondaryButtonUp { get; protected set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Start()
        {
            pointers.Add(this);
            if (NetworkEntity == null) NetworkEntity = GetComponentInChildren<SessionsNetworkEntity>();
        }

        #endregion Init

        #region Update

        private void Update()
        {
            // Only worry about updating own pointers state
            if (NetworkEntity != null && !NetworkEntity.IsMine) return;

            #region Read Owner Input States

            // Prepare input states
            PrimaryButton = false;
            PrimaryButtonDown = false;
            PrimaryButtonUp = false;

            SecondaryButton = false;
            SecondaryButtonDown = false;
            SecondaryButtonUp = false;

            // Get input states from user if this is their pointer
            if (NetworkEntity != null && NetworkEntity.IsMine)
            {
                // Get the current user
                var user = SessionsUser.Current;
                
                // Copy input states from user
                if (user != null)
                {
                    user.UpdateButtonInputStates(); // ensure the latest states
                    PrimaryButton = user.PrimaryButton;
                    PrimaryButtonDown = user.PrimaryButtonDown;
                    PrimaryButtonUp = user.PrimaryButtonUp;
                    SecondaryButton = user.SecondaryButton;
                    SecondaryButtonDown = user.SecondaryButtonDown;
                    SecondaryButtonUp = user.SecondaryButtonUp;
                }
            }

            #endregion Read Owner Input States

            #region Do Raycast

            RaycastHit hit;
            if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out hit, PointerLength, 1))
            {
                // If this is self, ignore
                if (hit.transform.tag == "Agent")
                {
                    var entity = hit.transform.GetComponent<SessionsNetworkEntity>();
                    if (entity != null && entity.IsMine) return;
                }
                else if (hit.transform.tag == "Player") return;

                if (LineRenderer != null)
                {
                    LineRenderer.SetPosition(1, new Vector3(0, 0, hit.distance));
                }

                var interaction = hit.collider.GetComponentInChildren<SessionsInteractable>();             

                //Debug.LogFormat("Hit {0}", hit.collider.gameObject.name);

                // If this is a new interaction, mark it
                if (currentInteraction != interaction)
                {
                    // If there is a current interaction, exit it
                    if (currentInteraction != null) currentInteraction.HandleTouchExit(this);

                    // Enter the new interaction
                    currentInteraction = interaction;
                    if (interaction != null) interaction.HandleTouchEnter(this);
                }

                // Handle ongoing interaction
                if (interaction != null)
                {
                    interaction.HandleTouch(this);

                    // Check user input to see if there is a further form of interaction
                    if (PrimaryButtonDown) interaction.HandlePrimaryClickDown(this);
                    if (PrimaryButton) interaction.HandlePrimaryClick(this);
                    if (SecondaryButtonDown) interaction.HandleSecondaryClickDown(this);
                    if (SecondaryButton) interaction.HandleSecondaryClick(this);
                    if (PrimaryButtonUp) interaction.HandlePrimaryClickUp(this);
                    if (SecondaryButtonUp) interaction.HandleSecondaryClickUp(this);
                }
            }
            else
            {
                // If there is a current interaction, it has exited
                if (currentInteraction != null)
                {
                    currentInteraction.HandleTouchExit(this);
                    currentInteraction = null;
                }

                // Set line back to default length
                if (LineRenderer != null) LineRenderer.SetPosition(1, Vector3.forward * PointerLength);
            }

            #endregion Do Raycast
        }

        #endregion Update

        #region Pointers

        /// <summary>
        /// Clears all current pointer references.
        /// </summary>
        public static void ClearAllPointers()
        {
            pointers.Clear();
        }

        /// <summary>
        /// Gets all of the currently available pointers.
        /// </summary>
        /// <returns>All current pointers.</returns>
        public static SessionsPointer[] GetAllPointers()
        {
            return pointers.ToArray();
        }

        /// <summary>
        /// Gets the pointer for an agent given their agent ID.
        /// </summary>
        /// <param name="agentId">The agent ID of the agent to get pointer for.</param>
        /// <returns>The pointer if found, otherwise NULL.</returns>
        public static SessionsPointer GetPointerByAgentId(Guid agentId)
        {
            return pointers.FirstOrDefault(v => v.NetworkEntity != null && v.NetworkEntity.Owner != null && v.NetworkEntity.Owner.Id == agentId);
        }

        #endregion Pointers

        #endregion Methods
    }

    /// <summary>
    /// Events related to session pointers.
    /// </summary>
    [Serializable]
    public class SessionsPointerEvent : UnityEvent<string, SessionsPointer> { }
}
