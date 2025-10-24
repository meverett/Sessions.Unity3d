using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Used to swap out mesh materials on start.
    /// </summary>
    public class MaterialSwapTarget : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The material swapper to use.
        /// </summary>
        [Tooltip("The material swapper to use.")]
        public MaterialSwapper Swapper;

        /// <summary>
        /// Whether or not to perform the material swap on start.
        /// </summary>
        [Tooltip("Whether or not to perform the material swap on start.")]
        public bool ApplyOnStart = true;

        /// <summary>
        /// The material swamp mode to use when swapping materials.
        /// </summary>
        [Tooltip("The material swamp mode to use when swapping materials.")]
        public MaterialSwapper.MaterialSwapModes SwapMode = MaterialSwapper.MaterialSwapModes.Random;

        /// <summary>
        /// The target renderers to swap materials on.
        /// </summary>
        [Tooltip("The target renderers to swap materials on.")]
        public Renderer[] Renderers;

        #endregion Inspector

        #region Fields

        // The internal material index used for selecting the appropriate material
        private int matIndex = -1;

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        private void Start()
        {
            if (Swapper == null) Swapper = MaterialSwapper.Current;
            if (ApplyOnStart && Swapper != null) Swapper.SwapMaterials(SwapMode, Renderers);
        }

        #endregion Init

        #endregion Methods
    }
}
