using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CymaticLabs.Logging;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Allows for interaction events to drive material changes.
    /// </summary>
    public class SessionsMaterialInteraction : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The interactable reference to use.
        /// </summary>
        [Tooltip("The interactable reference to use.")]
        public SessionsInteractable Interactable;

        /// <summary>
        /// The type of interaction event to bind to.
        /// </summary>
        [Tooltip("The type of interaction event to bind to.")]
        public string Interaction = "Touch";

        /// <summary>
        /// When true, the interaction will be inverted and the audio will trigger on the stop event.
        /// </summary>
        [Tooltip("When true, the interaction will be inverted and the audio will trigger on the stop event.")]
        public bool Invert = false;

        /// <summary>
        /// Whether or not material swapping is currently enabled.
        /// </summary>
        [Header("Swapping")]
        [Tooltip("Whether or not material swapping is currently enabled.")]
        public bool SwapMaterials = true;

        /// <summary>
        /// A list of materials to swap during interaction events.
        /// </summary>
        [Tooltip("A list of materials to swap during the interaction events.")]
        public MaterialSwap[] MaterialSwaps;

        /// <summary>
        /// Whether or not property updating is currently enabled.
        /// </summary>
        [Header("Material Properties")]
        [Tooltip("Whether or not property updating is currently enabled.")]
        public bool UpdatedProperties = true;

        /// <summary>
        /// A list of material property updates to apply during interaction events.
        /// </summary>
        [Tooltip("A list of material property updates to apply during interaction events.")]
        public MaterialPropertyUpdater[] PropertyUpdates;

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        private void Start()
        {
            if (Interactable == null) GetComponentInChildren<SessionsInteractable>();

            if (Interactable != null)
            {
                Interactable.OnInteractionStarted.AddListener((interaction, pointer) =>
                {
                    if (MaterialSwaps == null || interaction != Interaction) return;

                    if (SwapMaterials)
                    {
                        if (!Invert) ApplyActiveMaterials();
                        else ApplyInactiveMaterials();
                    }

                    if (UpdatedProperties)
                    {
                        if (!Invert) ApplyActiveProperties();
                        else ApplyInactiveProperties();
                    }
                });

                Interactable.OnInteractionStopped.AddListener((interaction, pointer) =>
                {
                    if (MaterialSwaps == null || interaction != Interaction) return;

                    if (SwapMaterials)
                    {
                        if (!Invert) ApplyInactiveMaterials();
                        else ApplyActiveMaterials();
                    }

                    if (UpdatedProperties)
                    {
                        if (!Invert) ApplyInactiveProperties();
                        else ApplyActiveProperties();
                    }
                });
            }
        }

        #endregion Init

        #region Operation

        #region Swap

        // Applies the active material swap
        private void ApplyActiveMaterials()
        {
            foreach (var ms in MaterialSwaps)
            {
                if (ms == null || ms.Renderer == null || ms.ActiveMaterial == null) continue;

                if (ms.MaterialIndex < 0 || ms.MaterialIndex >= ms.Renderer.materials.Length)
                {
                    CyLog.LogWarnFormat("Material index is out of range: {0}[{1}]", ms.Renderer.name, ms.MaterialIndex);
                    continue;
                }

                // Get current materials list
                var materials = new Material[ms.Renderer.materials.Length];

                // Copy to new list
                for (var i = 0; i < materials.Length; i++) materials[i] = ms.Renderer.materials[i];

                // Update the specified material
                materials[ms.MaterialIndex] = ms.ActiveMaterial;

                // Reassign new material list
                ms.Renderer.materials = materials;
            }
        }

        // Applies the inactive material swap
        private void ApplyInactiveMaterials()
        {
            foreach (var ms in MaterialSwaps)
            {
                if (ms == null || ms.Renderer == null || ms.InactiveMaterial == null) continue;

                if (ms.MaterialIndex < 0 || ms.MaterialIndex >= ms.Renderer.materials.Length)
                {
                    CyLog.LogWarnFormat("Material index is out of range: {0}[{1}]", ms.Renderer.name, ms.MaterialIndex);
                    continue;
                }

                // Get current materials list
                var materials = new Material[ms.Renderer.materials.Length];

                // Copy to new list
                for (var i = 0; i < materials.Length; i++) materials[i] = ms.Renderer.materials[i];

                // Update the specified material
                materials[ms.MaterialIndex] = ms.InactiveMaterial;

                // Reassign new material list
                ms.Renderer.materials = materials;
            }
        }

        #endregion Swap

        #region Properties

        // Applies the active material property updates
        private void ApplyActiveProperties()
        {
            foreach (var pu in PropertyUpdates)
            {
                if (pu == null || pu.Renderer == null) continue;

                if (pu.MaterialIndex < 0 || pu.MaterialIndex >= pu.Renderer.materials.Length)
                {
                    CyLog.LogWarnFormat("Material index is out of range: {0}[{1}]", pu.Renderer.name, pu.MaterialIndex);
                    continue;
                }

                ApplyActiveProperty(pu);
            }
        }

        // Applies the active material property updates
        private void ApplyInactiveProperties()
        {
            foreach (var pu in PropertyUpdates)
            {
                if (pu == null || pu.Renderer == null) continue;

                if (pu.MaterialIndex < 0 || pu.MaterialIndex >= pu.Renderer.materials.Length)
                {
                    CyLog.LogWarnFormat("Material index is out of range: {0}[{1}]", pu.Renderer.name, pu.MaterialIndex);
                    continue;
                }

                ApplyInactiveProperty(pu);
            }
        }

        // Applies the inactive material property updates
        private void ApplyActiveProperty(MaterialPropertyUpdater pu)
        {
            if (pu.ActiveCoroutine != null) StopCoroutine(pu.ActiveCoroutine);
            if (pu.InactiveCoroutine != null) StopCoroutine(pu.InactiveCoroutine);
            pu.ActiveCoroutine = DoApplyActiveProperty(pu);
            StartCoroutine(pu.ActiveCoroutine);
        }

        // Applies active material properties
        private IEnumerator DoApplyActiveProperty(MaterialPropertyUpdater pu)
        {
            if (pu.Renderer == null) yield break;
            var material = pu.Renderer.materials[pu.MaterialIndex];
            float timer = 0;
            var current = GetMaterialProperty(pu, material);

            while (timer < pu.ApplyDuration)
            {
                timer += Time.deltaTime;
                var percent = timer / pu.ApplyDuration;
                var x = Mathf.Lerp(current.x, pu.ActiveValues.x, percent);
                var y = Mathf.Lerp(current.y, pu.ActiveValues.y, percent);
                var z = Mathf.Lerp(current.z, pu.ActiveValues.z, percent);
                var w = Mathf.Lerp(current.w, pu.ActiveValues.w, percent);
                UpdateMaterialProperty(pu, material, new Vector4(x, y, z, w));
                yield return 0;
            }

            UpdateMaterialProperty(pu, material, pu.ActiveValues);
            yield break;
        }

        // Applies the inactive material property updates
        private void ApplyInactiveProperty(MaterialPropertyUpdater pu)
        {
            if (pu.ActiveCoroutine != null) StopCoroutine(pu.ActiveCoroutine);
            if (pu.InactiveCoroutine != null) StopCoroutine(pu.InactiveCoroutine);
            pu.InactiveCoroutine = DoApplyInactiveProperty(pu);
            StartCoroutine(pu.InactiveCoroutine);
        }

        // Applies inactive material properties
        private IEnumerator DoApplyInactiveProperty(MaterialPropertyUpdater pu)
        {
            if (pu.Renderer == null) yield break;
            var material = pu.Renderer.materials[pu.MaterialIndex];
            float timer = 0;
            var current = GetMaterialProperty(pu, material);

            while (timer < pu.RemoveDuration)
            {
                timer += Time.deltaTime;
                var percent = timer / pu.RemoveDuration;
                var x = Mathf.Lerp(current.x, pu.InactiveValues.x, percent);
                var y = Mathf.Lerp(current.y, pu.InactiveValues.y, percent);
                var z = Mathf.Lerp(current.z, pu.InactiveValues.z, percent);
                var w = Mathf.Lerp(current.w, pu.InactiveValues.w, percent);
                UpdateMaterialProperty(pu, material, new Vector4(x, y, z, w));
                yield return 0;
            }

            UpdateMaterialProperty(pu, material, pu.InactiveValues);
            yield break;
        }

        // Gets a material property value
        private Vector4 GetMaterialProperty(MaterialPropertyUpdater pu, Material material)
        {
            var values = Vector4.zero;

            switch (pu.Type)
            {
                case UpdatableMaterialProperties.Float:
                    values.x = material.GetFloat(pu.PropertyName);
                    break;

                case UpdatableMaterialProperties.Integer:
                    values.x = material.GetInt(pu.PropertyName);
                    break;

                case UpdatableMaterialProperties.Color:
                    values = material.GetColor(pu.PropertyName);
                    break;

                case UpdatableMaterialProperties.Vector:
                    values = material.GetVector(pu.PropertyName);
                    break;

                case UpdatableMaterialProperties.TextureOffset:
                    var v = material.GetTextureOffset(pu.PropertyName);
                    values = new Vector4(v.x, v.y, 0, 0);
                    break;
            }

            return values;
        }

        // Updates a material property
        private void UpdateMaterialProperty(MaterialPropertyUpdater pu, Material material, Vector4 values)
        {
            switch (pu.Type)
            {
                case UpdatableMaterialProperties.Float:
                    material.SetFloat(pu.PropertyName, values.x);
                    break;

                case UpdatableMaterialProperties.Integer:
                    material.SetInt(pu.PropertyName, (int)values.x);
                    break;

                case UpdatableMaterialProperties.Color:
                    material.SetColor(pu.PropertyName, values);
                    break;

                case UpdatableMaterialProperties.Vector:
                    material.SetVector(pu.PropertyName, values);
                    break;

                case UpdatableMaterialProperties.TextureOffset:
                    material.SetTextureOffset(pu.PropertyName, new Vector2(values.x, values.y));
                    break;
            }
        }

        #endregion Properties

        #endregion Operation

        #endregion Methods
    }

    /// <summary>
    /// Utility class used to capture a material swap on a target renderer.
    /// </summary>
    [Serializable]
    public class MaterialSwap
    {
        /// <summary>
        /// The target renderer to swap materials for.
        /// </summary>
        [Header("Target")]
        [Tooltip("The target renderer to swap materials for.")]
        public Renderer Renderer;

        /// <summary>
        /// The index in the target renderer's material list to target.
        /// </summary>
        [Tooltip("The index in the target renderer's material list to target.")]
        public int MaterialIndex = 0;

        /// <summary>
        /// The material to use when the event exits/is inactive.
        /// </summary>
        [Header("Materials")]
        [Tooltip("The material to use when the event exits/is inactive.")]
        public Material InactiveMaterial;

        /// <summary>
        /// The material to use when the event enters/is active.
        /// </summary>
        [Tooltip("The material to use when the event enters/is active.")]
        public Material ActiveMaterial;
    }

    /// <summary>
    /// Different types of updatable material properties.
    /// </summary>
    public enum UpdatableMaterialProperties
    {
         /// <summary>
        /// The material property to change is a floating point.
        /// </summary>
        Float,

        /// <summary>
        /// The material property to change is an integer.
        /// </summary>
        Integer,

        /// <summary>
        /// The material property to change is a color.
        /// </summary>
        Color,

        /// <summary>
        /// The material property to change is a vector.
        /// </summary>
        Vector,

        /// <summary>
        /// The material property to change is a texture offset.
        /// </summary>
        TextureOffset,
    }

    /// <summary>
    /// Utility class used to capture a material property change interaction effect.
    /// </summary>
    [Serializable]
    public class MaterialPropertyUpdater
    {
        /// <summary>
        /// The target renderer to swap materials for.
        /// </summary>
        [Header("Target")]
        [Tooltip("The target renderer to swap materials for.")]
        public Renderer Renderer;

        /// <summary>
        /// The index in the target renderer's material list to target.
        /// </summary>
        [Tooltip("The index in the target renderer's material list to target.")]
        public int MaterialIndex = 0;

        /// <summary>
        /// The name of the material property to update.
        /// </summary>
        [Tooltip("The name of the material property to update.")]
        public string PropertyName;

        /// <summary>
        /// The type of property to update.
        /// </summary>
        [Tooltip("The type of property to update.")]
        public UpdatableMaterialProperties Type = UpdatableMaterialProperties.Float;

        /// <summary>
        /// The 'inactive' values to apply to the property.
        /// </summary>
        [Tooltip("The 'inactive' values to apply to the property.")]
        public Vector4 InactiveValues;

        /// <summary>
        /// The 'active' values to apply to the property.
        /// </summary>
        [Tooltip("The 'active' values to apply to the property.")]
        public Vector4 ActiveValues;

        /// <summary>
        /// The time in seconds of the application of the effect.
        /// </summary>
        [Tooltip("The time in seconds of the application of the effect.")]
        public float ApplyDuration = 1;

        /// <summary>
        /// The time in seconds of the removal of the effect.
        /// </summary>
        [Tooltip("The time in seconds of the removal of the effect.")]
        public float RemoveDuration = 1;

        /// <summary>
        /// The apply active coroutine, if any.
        /// </summary>
        internal IEnumerator ActiveCoroutine;

        /// <summary>
        /// The apply active coroutine, if any.
        /// </summary>
        internal IEnumerator InactiveCoroutine;
    }
}
