using System.Collections;
using UnityEngine;
using CymaticLabs.Sessions.Core;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Utility behaviour that activates/deactivates game objects based on detected platform.
    /// </summary>
    public class PlatformActivate : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// If assigned, the owner's platform will be used to trigger activations.
        /// </summary>
        [Tooltip("If assigned, the owner's platform will be used to trigger activations.")]
        public SessionsNetworkEntity NetworkEntity;

        /// <summary>
        /// A list of platforms that will trigger activations.
        /// </summary>
        [Header("Activations")]
        [Tooltip("A list of platforms that will trigger activations.")]
        public SessionsPlatforms[] ActivatePlatforms;

        /// <summary>
        /// A list of game objects to activate if one of any of the activation platforms are detected.
        /// </summary>
        [Tooltip("A list of game objects to activate if one of any of the activation platforms are detected.")]
        public GameObject[] ActivateObjects;

        /// <summary>
        /// A list of platforms that will trigger deactivations.
        /// </summary>
        [Header("Deactivations")]
        [Tooltip("A list of platforms that will trigger deactivations.")]
        public SessionsPlatforms[] DeactivatePlatforms;

        /// <summary>
        /// A list of game objects to deactivate if one of any of the deactivation platforms are detected.
        /// </summary>
        [Tooltip("A list of game objects to deactivate if one of any of the deactivation platforms are detected.")]
        public GameObject[] DeactivateObjects;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        private void Start()
        {
            // If a network entity was supplied, wait for its owner to exist before apply the platform check...
            if (NetworkEntity != null)
            {
                StartCoroutine(DoCheckOwnerPlatform());
            }
            // Otherwise just use the current platofrm...
            else
            {
                UpdateActivations(SessionsSceneManager.GetPlatform());
            }
        }

        // Checks a network entity's owner's platform before running activatins
        IEnumerator DoCheckOwnerPlatform()
        {
            if (NetworkEntity == null) yield break;
            while (NetworkEntity.Owner == null) yield return 0;
            UpdateActivations(NetworkEntity.Owner.Platform);
        }

        /// <summary>
        /// Updates activations based on the given platform.
        /// </summary>
        /// <param name="platform">The platform to use to update activations against.</param>
        public void UpdateActivations(SessionsPlatforms platform)
        {
            // Run activation checks...
            if (ActivatePlatforms != null && ActivatePlatforms.Length > 0)
            {
                var runActivations = false;

                // If at least one of the platforms matches, run the activations...
                foreach (var ap in ActivatePlatforms)
                {
                    if (ap == platform)
                    {
                        runActivations = true;
                        break;
                    }
                }

                if (runActivations && ActivateObjects != null && ActivateObjects.Length > 0)
                {
                    foreach (var go in ActivateObjects)
                    {
                        if (go == null) return;
                        go.SetActive(true);
                    }
                }
            }

            // Run deactivation checks...
            if (DeactivatePlatforms != null && DeactivatePlatforms.Length > 0)
            {
                var runDectivations = false;

                // If at least one of the platforms matches, run the activations...
                foreach (var ap in DeactivatePlatforms)
                {
                    if (ap == platform)
                    {
                        runDectivations = true;
                        break;
                    }
                }

                if (runDectivations && DeactivateObjects != null && DeactivateObjects.Length > 0)
                {
                    foreach (var go in DeactivateObjects)
                    {
                        if (go == null) return;
                        go.SetActive(false);
                    }
                }
            }
        }

        #endregion Init

        #endregion Methods
    }
}
