using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

using CymaticLabs.Sessions.Core;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Displays the owner's name on a UI text component for a given network entity.
    /// </summary>
    public class DisplayOwnerName : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The network entity who's owner name will be displayed.
        /// </summary>
        [Tooltip("The network entity who's owner name will be displayed.")]
        public SessionsNetworkEntity NetworkEntity;

        /// <summary>
        /// The canvas that is rendering the name.
        /// </summary>
        [Tooltip("The canvas that is rendering the name.")]
        public Canvas NameCanvas;

        /// <summary>
        /// The UI Text component to display the owner name with.
        /// </summary>
        [Tooltip("The UI Text component to display the owner name with.")]
        public Text NameText;

        /// <summary>
        /// Whether or not to automatically track the main camera as a look at target.
        /// </summary>
        [Tooltip("Whether or not to automatically track the main camera as a look at target.")]
        public bool UseMainCamera = true;

        /// <summary>
        /// The optional target transform to align the name with.
        /// </summary>
        [Tooltip("The optional target transform to align the name with.")]
        public Transform LookAtTarget;

        /// <summary>
        /// An optional rotation offset to apply to the name.
        /// </summary>
        public Vector3 RotationOffset = Vector3.zero;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        private void Start()
        {
            if (NameCanvas == null) NameCanvas = GetComponentInChildren<Canvas>();
            if (NameText == null) NameText = GetComponentInChildren<Text>();

            // If set to automatically track the main camera, setup the reference for tracking
            if (UseMainCamera)
            {
                LookAtTarget = Camera.main.transform;
            }
        }

        #endregion Init

        #region Update

        private void Update()
        {
            if (NetworkEntity == null || NameText == null) return;

            // If this is the current user's name or we don't know who the current user is, hide the name
            if (NetworkEntity.IsMine || NetworkEntity.Owner == null || (SessionsUdpNetworking.Current != null && SessionsUdpNetworking.Current.AgentId == Guid.Empty))
            {
                if (NameCanvas.enabled) NameCanvas.enabled = false;
                return;
            }

            if (NameText.text != NetworkEntity.Owner.Name) NameText.text = NetworkEntity.Owner.Name;
            LookAtTarget = Camera.current != null ? Camera.current.transform : null;
            if (LookAtTarget == null || NameCanvas == null) return;

            //NameCanvas.transform.LookAt(NameCanvas.transform.position + LookAtTarget.rotation * Vector3.forward,
            //LookAtTarget.rotation * Vector3.up);
            //NameCanvas.transform.localEulerAngles += RotationOffset;

            NameCanvas.transform.LookAt(LookAtTarget);
            NameCanvas.transform.eulerAngles += RotationOffset + new Vector3(0, 180, 0);
        }

        #endregion Update

        #endregion Methods
    }
}
