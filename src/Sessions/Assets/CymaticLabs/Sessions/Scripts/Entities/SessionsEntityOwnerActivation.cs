using System;
using System.Collections;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Changes the activation state of one or more game objects based on an ownership check.
    /// </summary>
    public class SessionsEntityOwnerActivation : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The network entity to use for the ownership check.
        /// </summary>
        [Tooltip("The network entity to use for the ownership check.")]
        public SessionsNetworkEntity NetworkEntity;

        /// <summary>
        /// The ownership check that will trigger activation changes.
        /// </summary>
        [Tooltip("The ownership check that will trigger activation changes.")]
        public NetworkEntityOwner Ownership = NetworkEntityOwner.IsMine;

        /// <summary>
        /// The target game objects to activate.
        /// </summary>
        [Tooltip("The target game objects to activate.")]
        public GameObject[] ActivateObjects;

        /// <summary>
        /// The target game objects to deactivate.
        /// </summary>
        [Tooltip("The target game objects to deactivate.")]
        public GameObject[] DeactivateObjects;

        /// <summary>
        /// The target game objects to activate.
        /// </summary>
        [Tooltip("The target behaviours to activate.")]
        public MonoBehaviour[] ActivateBehaviours;

        /// <summary>
        /// The target game objects to deactivate.
        /// </summary>
        [Tooltip("The target behaviours to deactivate.")]
        public MonoBehaviour[] DeactivateBehaviours;

        /// <summary>
        /// The target line renderers to activate.
        /// </summary>
        [Tooltip("The target line renderers to activate.")]
        public LineRenderer[] ActivateLineRenderers;

        /// <summary>
        /// The target line renderers to deactivate.
        /// </summary>
        [Tooltip("The target line renderers to deactivate.")]
        public LineRenderer[] DeactivateLineRenderers;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        private void Start()
        {
            if (NetworkEntity == null) NetworkEntity = GetComponentInChildren<SessionsNetworkEntity>();
            if (NetworkEntity == null) return;
            StartCoroutine(DoCheckOwnership());
        }

        // Checks ownership on the configured network entity and applies activations based on the result
        private IEnumerator DoCheckOwnership()
        {
            // Wait until the entity has an assigned owner
            while (NetworkEntity.Owner == null) yield return 0;

            if (NetworkEntity.IsMine)
            {
                switch (Ownership)
                {
                    case NetworkEntityOwner.Everyone:
                    case NetworkEntityOwner.IsMine:
                        UpdateTargets();
                        break;
                }
            }
            else
            {
                switch (Ownership)
                {
                    case NetworkEntityOwner.Everyone:
                    case NetworkEntityOwner.NotMine:
                        UpdateTargets();
                        break;
                }
            }
        }

        #endregion Init

        #region Opertation

        /// <summary>
        /// Applies the activation changes to the configured targets.
        /// </summary>
        public void UpdateTargets()
        {
            // Set activate game objects active...
            if (ActivateObjects != null && ActivateObjects.Length > 0)
            {
                foreach (var target in ActivateObjects)
                {
                    if (target == null) continue;
                    target.SetActive(true);
                }
            }

            // Set deactive game objects inactive
            if (DeactivateObjects != null && DeactivateObjects.Length > 0)
            {
                foreach (var target in DeactivateObjects)
                {
                    if (target == null) continue;
                    target.SetActive(false);
                }
            }

            // Set activate behaviours active...
            if (ActivateBehaviours != null && ActivateBehaviours.Length > 0)
            {
                foreach (var target in ActivateBehaviours)
                {
                    if (target == null) continue;
                    target.enabled = true;
                }
            }

            // Set deactive behaviours inactive
            if (DeactivateBehaviours != null && DeactivateBehaviours.Length > 0)
            {
                foreach (var target in DeactivateBehaviours)
                {
                    if (target == null) continue;
                    target.enabled = false;
                }
            }

            // Set activate line renderers active...
            if (ActivateLineRenderers != null && ActivateLineRenderers.Length > 0)
            {
                foreach (var target in ActivateLineRenderers)
                {
                    if (target == null) continue;
                    target.enabled = true;
                }
            }

            // Set deactivate line renderers inactive...
            if (DeactivateLineRenderers != null && DeactivateLineRenderers.Length > 0)
            {
                foreach (var target in DeactivateLineRenderers)
                {
                    if (target == null) continue;
                    target.enabled = false;
                }
            }
        }

        #endregion Opertation

        #endregion Methods
    }
}
