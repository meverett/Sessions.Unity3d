using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CymaticLabs.Sessions.Unity3d
{
    [RequireComponent(typeof(Camera))]
    public class SessionsNonXrCamera : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The list of UI canvases to adjust for non XR use.
        /// </summary>
        public Canvas[] Canvases;

        /// <summary>
        /// The notifications container transform.
        /// </summary>
        public RectTransform NotificationsContainer;

        /// <summary>
        /// A list of UI targets to rescale.
        /// </summary>
        public RescaleTarget[] RescaleTargets;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        private void Start()
        {
            if (Canvases != null && Canvases.Length > 0)
            {
                foreach (var c in Canvases)
                {
                    // Set render mode to screen overlay
                    c.renderMode = RenderMode.ScreenSpaceOverlay;

                    // Go through immediate children and apply rescale factor
                    foreach (var rt in RescaleTargets)
                    {
                        rt.Target.localScale = new Vector3(rt.Scale.x, rt.Scale.y, rt.Scale.z);
                    }
                }
            }

            if (NotificationsContainer != null)
            {
                // Anchor top
                NotificationsContainer.anchorMin = new Vector2(NotificationsContainer.anchorMin.x, 1);
                NotificationsContainer.anchorMax = new Vector2(NotificationsContainer.anchorMax.x, 1);
                NotificationsContainer.localPosition = new Vector3(NotificationsContainer.localPosition.x, 0, NotificationsContainer.localPosition.z);
            }

            // Get all available canvases and update their event camera to this one
            var canvases = FindObjectsOfType<Canvas>();
            var nonXrCamera = GetComponent<Camera>();
            foreach (var c in canvases) c.worldCamera = nonXrCamera;
        }

        #endregion Init

        #region Update

        #endregion Update

        #endregion Methods
    }

    /// <summary>
    /// Used to apply UI object rescaling.
    /// </summary>
    [System.Serializable]
    public class RescaleTarget
    {
        public RectTransform Target;
        public Vector3 Scale = Vector3.one;
    }
}
