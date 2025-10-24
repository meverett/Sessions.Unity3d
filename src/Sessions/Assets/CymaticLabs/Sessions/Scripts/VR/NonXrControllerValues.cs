using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Adjusts player controller settings for non-XR/VR builds.
    /// </summary>
    public class NonXrControllerValues : MonoBehaviour
    {
        #region Inspector

        [Header("Reference")]
        public OVRPlayerController PlayerController;

        [Header("Values")]
        public float Acceleration = 0.1f;
        public float Damping = 0.1f;
        public float BackAndSideDampen = 0.5f;
        public float JumpForce = 0.3f;
        public float RotationAmount = 1.5f;
        public float RotationRatchet = 45f;
        public bool SnapRotation = true;
        public int FixedSpeedSteps = 0;
        public bool HmdResetsY = true;
        public bool HmdResetsX = true;
        public float GravityModifier = 1;
        public bool UseProfileData = false;
        public bool EnableLinearMovement = true;
        public bool EnableRotation = true;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            // Don't apply if XR is enabled...
            if (UnityEngine.XR.XRSettings.enabled) return;

            // Otherwise look for the player control and update its values to non XR values.
            if (PlayerController == null) PlayerController = GetComponentInChildren<OVRPlayerController>();

            if (PlayerController == null)
            {
                Debug.LogWarning("No player controller reference set.");
                return;
            }

            var p = PlayerController;
            p.Acceleration = Acceleration;
            p.Damping = Damping;
            p.BackAndSideDampen = BackAndSideDampen;
            p.JumpForce = JumpForce;
            p.RotationAmount = RotationAmount;
            p.RotationRatchet = RotationRatchet;
            p.SnapRotation = SnapRotation;
            p.FixedSpeedSteps = FixedSpeedSteps;
            p.HmdResetsY = HmdResetsY;
            p.HmdResetsY = HmdResetsX;
            p.GravityModifier = GravityModifier;
            p.useProfileData = UseProfileData;
            p.EnableLinearMovement = EnableLinearMovement;
            p.EnableRotation = EnableRotation;
        }

        #endregion Init

        #region Update

        #endregion Update

        #endregion Methods
    }
}
