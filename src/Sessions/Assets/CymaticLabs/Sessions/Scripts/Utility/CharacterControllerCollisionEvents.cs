using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Exposes character controller collision events to the rest of the collision world for programmable interaction.
    /// </summary>
    public class CharacterControllerCollisionEvents : MonoBehaviour
    {
        #region Inspector

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        #endregion Init

        #region Update

        #endregion Update

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var networkCollision = hit.gameObject.GetComponentInChildren<NetworkStateCollisionTrigger>();
            if (networkCollision == null) return;

            // Simulate a hit on a network state collision trigger
            networkCollision.OnTriggerEnter(hit.collider);

            // Get the rigidbody of the hit
            //var body = hit.collider.attachedRigidbody;
        }

        #endregion Methods
    }
}
