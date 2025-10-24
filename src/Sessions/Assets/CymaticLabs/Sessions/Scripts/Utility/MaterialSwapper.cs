using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Used to swap out mesh materials on start.
    /// </summary>
    public class MaterialSwapper : MonoBehaviour
    {
        #region Inspector

        ///// <summary>
        ///// The list of materials to use.
        ///// </summary>
        [Tooltip("The list of materials to use.")]
        public Material[] MaterialList;

        #endregion Inspector

        #region Enumerations

        /// <summary>
        /// Different material swap modes.
        /// </summary>
        public enum MaterialSwapModes
        {
            /// <summary>
            /// Randomly select a material from the material list.
            /// </summary>
            Random,

            /// <summary>
            /// Cycle through the list of materials one at a time, looping at the end.
            /// </summary>
            RoundRobin,
        }

        #endregion Enumerations

        #region Fields

        // The internal material index used for selecting the appropriate material
        private int matIndex = -1;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static MaterialSwapper Current { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            Current = this;
            matIndex = -1;
        }

        private void Start()
        {
        }

        #endregion Init

        #region Materials

        /// <summary>
        /// Swaps the materials on the target renderers.
        /// </summary>
        /// <param name="mode">The swamp mode to use.</param>
        /// <param name="renderers">The list of renderers to swamp materials on.</param>
        public void SwapMaterials(MaterialSwapModes mode, Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0 || MaterialList == null || MaterialList.Length == 0)
                return;

            var newIndex = matIndex;

            // Select the new material based on mode
            switch (mode)
            {
                case MaterialSwapModes.Random:
                    newIndex = Mathf.RoundToInt(UnityEngine.Random.value * (MaterialList.Length - 1));
                    break;

                case MaterialSwapModes.RoundRobin:
                    if (matIndex + 1 >= MaterialList.Length) matIndex = 0;
                    else matIndex++;
                    newIndex = matIndex;
                    break;
            }

            // Select the new material from the list
            var newMat = MaterialList[newIndex];

            // Apply to each renderer
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.material = newMat;
            }
        }

        #endregion Materials

        #endregion Methods
    }
}
