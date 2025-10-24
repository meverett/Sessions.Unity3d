using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using CymaticLabs.Logging;
using CymaticLabs.Sessions.Core;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Teleports an entity on collision.
    /// </summary>
    public class CollisionTeleporter : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The teleporter where the teleportee will be sent.
        /// </summary>
        public CollisionTeleporter To;

        /// <summary>
        /// The position offset to apply to the character after arriving by teleportation.
        /// </summary>
        public Vector3 PositionOffset = Vector3.zero;

        /// <summary>
        /// The rotation offset to apply to the character after ariving by teleportation.
        /// </summary>
        public Vector3 RotationOffset = Vector3.zero;

        #endregion Inspector

        #region Fields

        // A list of recently received/teleported characters
        private List<CharacterController> receivedCharacters;

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region 

        private void Awake()
        {
            receivedCharacters = new List<CharacterController>();
        }

        #endregion Init

        #region Update

        #endregion Update

        #region Trigger/Collision

        public void OnTriggerEnter(Collider other)
        {
            HandleCollisionEnter(other.transform);
        }

        public void OnCollisionEnter(Collision collision)
        {
            HandleCollisionEnter(collision.transform);
        }

        private void HandleCollisionEnter(Transform collider)
        {
            // Attempt to find a character collision controller on the colliding object
            CharacterController character = collider.gameObject.GetComponentInParent<CharacterController>();

            // If that fails, look in children
            if (character == null) character = collider.gameObject.GetComponentInChildren<CharacterController>();

            // This is not a character controller collision...
            if (character == null) return;

            // If we've recently received this character, waive the "enter" event, since we expected them, but remove them from the received list as well
            if (receivedCharacters.Contains(character))
            {
                StartCoroutine(DoRemoveReceivedCharacter(character, 2));
                return;
            }

            // If we have a configured "to" teleporter, teleport the character on to it
            if (To != null) To.ReceiveTeleport(character);
        }

        // Removes a character from the received list after a delay
        IEnumerator DoRemoveReceivedCharacter(CharacterController character, float delay = 0)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            if (receivedCharacters.Contains(character)) receivedCharacters.Remove(character);
            yield break;
        }

        #endregion Trigger/Collision

        #region Teleport

        /// <summary>
        /// Receives the teleport request of a game object.
        /// </summary>
        /// <param name="character">The character that is trying to teleport here.</param>
        /// <param name="positionOffset">The position offset to apply to the teleportee.</param>
        /// /// <param name="positionOffset">The rotation offset to apply to the teleportee.</param>
        public void ReceiveTeleport(CharacterController character)
        {
            if (character == null) throw new ArgumentNullException("character");
            var t = character.transform;
            t.position = transform.position + PositionOffset;
            t.eulerAngles = transform.eulerAngles + RotationOffset;
            receivedCharacters.Add(character);
        }

        #endregion Teleport

        #endregion Methods
    }
}
