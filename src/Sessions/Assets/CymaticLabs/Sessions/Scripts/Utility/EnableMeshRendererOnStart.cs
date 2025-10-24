using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Enables a disabled mesh renderer on start.
    /// </summary>
    public class EnableMeshRendererOnStart : MonoBehaviour
    {
        /// <summary>
        /// The mesh renderer to activate.
        /// </summary>
        public MeshRenderer MeshRenderer;

        private void Start()
        {
            if (this.MeshRenderer == null) this.MeshRenderer = GetComponentInChildren<MeshRenderer>();
            if (this.MeshRenderer != null) this.MeshRenderer.enabled = true;
        }
    }
}
