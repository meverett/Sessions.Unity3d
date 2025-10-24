using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Controls the environment during a session.
    /// </summary>
    public class SessionsEnvironment : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The sessions manager instance to use.
        /// </summary>
        [Header("References")]
        [Tooltip("The sessions manager instance to use.")]
        public SessionsSceneManager SessionsManager;

        /// <summary>
        /// The sessions networking instance to use.
        /// </summary>
        [Tooltip("The sessions networking instance to use.")]
        public SessionsUdpNetworking SessionsNetworking;

        /// <summary>
        /// Whether or not environmental lighting control is enabled.
        /// </summary>
        [Header("Lighting")]
        [Tooltip("Whether or not environmental lighting control is enabled.")]
        public bool LightControlEnabled = true;

        /// <summary>
        /// A list of lights in the scene.
        /// </summary>
        [Tooltip("A list of lights in the scene.")]
        public NamedLight[] Lights;

        /// <summary>
        /// Whether or not environmental skybox control is enabled.
        /// </summary>
        [Header("Skybox")]
        [Tooltip("Whether or not environmental skybox control is enabled.")]
        public bool SkyboxControlEnabled = true;

        /// <summary>
        /// A list of skybox materials for the scene.
        /// </summary>
        [Tooltip("A list of skybox materials for the scene.")]
        public Material[] SkyboxMaterials;

        #endregion Inspector

        #region Fields

        // A list of lights given their registered name
        private Dictionary<string, NamedLight> lightsByName;

        // The current skybox intensity
        private float skyboxIntensity = 0.5f;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static SessionsEnvironment Current { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            Current = this;
            lightsByName = new Dictionary<string, NamedLight>();

            // Store all lights by name into look up
            if (Lights != null && Lights.Length > 0)
            {
                foreach (var item in Lights)
                {
                    if (string.IsNullOrEmpty(item.Name)) continue;

                    if (lightsByName.ContainsKey(item.Name))
                    {
                        Debug.LogWarningFormat("Duplicate light registered with name: {0}", item.Name);
                        lightsByName[item.Name] = item; // overwrite
                    }
                    else
                    {
                        lightsByName.Add(item.Name, item);
                    }
                }
            }
        }

        private void Start()
        {
            if (SessionsNetworking == null) SessionsNetworking = SessionsUdpNetworking.Current;
            if (SessionsManager == null) SessionsManager = SessionsSceneManager.Current;

            #region Register Value Handlers

            if (SessionsManager != null)
            {
                #region Lights

                // Create dynamic handlers for all configured lights
                if (Lights != null)
                {
                    for (var i = 0; i < Lights.Length; i++)
                    {
                        var lightId = i;

                        // Intensity
                        var valueName = string.Format("Env/Lights/{0}/Intensity", lightId);
                        SessionsManager.RegisterValueHandler(valueName, (name, value) =>
                        {
                            if (!LightControlEnabled || Lights == null || Lights.Length - 1 < lightId) return;
                            var item = Lights[lightId];
                            var minInput = 0f;
                            var maxInput = 1f;
                            item.Light.intensity = (value - minInput) / (maxInput - minInput) * (item.MaxIntensity - item.MinIntensity) + item.MinIntensity;
                        });

                        // Color - Red
                        valueName = string.Format("Env/Lights/{0}/Color/R", lightId);
                        SessionsManager.RegisterValueHandler(valueName, (name, value) =>
                        {
                            if (!LightControlEnabled || Lights == null || Lights.Length - 1 < lightId) return;
                            var item = Lights[lightId];
                            var c = item.Light.color;
                            c.r = value;
                            item.Light.color = c;
                        });

                        // Color - Green
                        valueName = string.Format("Env/Lights/{0}/Color/G", lightId);
                        SessionsManager.RegisterValueHandler(valueName, (name, value) =>
                        {
                            if (!LightControlEnabled || Lights == null || Lights.Length - 1 < lightId) return;
                            var item = Lights[lightId];
                            var c = item.Light.color;
                            c.g = value;
                            item.Light.color = c;
                        });

                        // Color - Blue
                        valueName = string.Format("Env/Lights/{0}/Color/B", lightId);
                        SessionsManager.RegisterValueHandler(valueName, (name, value) =>
                        {
                            if (!LightControlEnabled || Lights == null || Lights.Length - 1 < lightId) return;
                            var item = Lights[lightId];
                            var c = item.Light.color;
                            c.b = value;
                            item.Light.color = c;
                        });

                        // Color - Hue
                        valueName = string.Format("Env/Lights/{0}/Color/H", lightId);
                        SessionsManager.RegisterValueHandler(valueName, (name, value) =>
                        {
                            if (!LightControlEnabled || Lights == null || Lights.Length - 1 < lightId) return;
                            var item = Lights[lightId];
                            var c = item.Light.color;
                            float h, s, v;
                            Color.RGBToHSV(c, out h, out s, out v);
                            item.Light.color = Color.HSVToRGB(value, s, v);
                        });

                        // Color - Saturation
                        valueName = string.Format("Env/Lights/{0}/Color/S", lightId);
                        SessionsManager.RegisterValueHandler(valueName, (name, value) =>
                        {
                            if (!LightControlEnabled || Lights == null || Lights.Length - 1 < lightId) return;
                            var item = Lights[lightId];
                            var c = item.Light.color;
                            float h, s, v;
                            Color.RGBToHSV(c, out h, out s, out v);
                            item.Light.color = Color.HSVToRGB(h, value, v);
                        });

                        // Color - Value
                        valueName = string.Format("Env/Lights/{0}/Color/V", lightId);
                        SessionsManager.RegisterValueHandler(valueName, (name, value) =>
                        {
                            if (!LightControlEnabled || Lights == null || Lights.Length - 1 < lightId) return;
                            var item = Lights[lightId];
                            var c = item.Light.color;
                            float h, s, v;
                            Color.RGBToHSV(c, out h, out s, out v);
                            item.Light.color = Color.HSVToRGB(h, s, value);
                        });

                        /*==== Spot Lights ======*/

                        // Spot Angle
                        valueName = string.Format("Env/Lights/{0}/SpotAngle", lightId);
                        SessionsManager.RegisterValueHandler(valueName, (name, value) =>
                        {
                            if (!LightControlEnabled || Lights == null || Lights.Length - 1 < lightId) return;
                            var item = Lights[lightId];
                            item.Light.spotAngle = value;
                        });
                    }
                }

                #endregion Lights

                #region Skybox

                // Skybox Intensity
                SessionsManager.RegisterValueHandler("Env/Skybox/Intensity", (name, value) =>
                {
                    if (!SkyboxControlEnabled) return;
                    SetSkyboxIntensity(value);
                });

                // Skybox Material
                SessionsManager.RegisterValueHandler("Env/Skybox/Material", (name, value) =>
                {
                    SetSkyboxMaterial((int)value);
                });

                #endregion Skybox
            }

            #endregion Register Value Handlers
        }

        #endregion Init

        #region Update

        #endregion Update

        #region Lights

        #region Get

        /// <summary>
        /// Gets a registered/name light given its name.
        /// </summary>
        /// <param name="name">The registered name of the light.</param>
        /// <returns>The light if found, otherwise NULL.</returns>
        public NamedLight GetLight(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");
            return lightsByName.ContainsKey(name) ? lightsByName[name] : null;
        }

        #endregion Get

        #endregion Lights

        #region Skybox

        /// <summary>
        /// Sets the intensity of the skybox material if it supports it through color.
        /// </summary>
        /// <param name="intensity">The intensity (whiteness) of the skybox material to set.</param>
        public void SetSkyboxIntensity(float intensity)
        {
            var skybox = RenderSettings.skybox;
            if (skybox == null) return;
            var c = new Color(intensity, intensity, intensity);
            skybox.color = c;
            skybox.SetColor("_Tint", c);
            skyboxIntensity = intensity;
        }

        /// <summary>
        /// Sets the skybox material
        /// </summary>
        /// <param name="index">The index of the material to set.</param>
        public void SetSkyboxMaterial(int index)
        {
            if (!SkyboxControlEnabled || SkyboxMaterials == null || SkyboxMaterials.Length == 0) return;
            if (index < 0 || index > SkyboxMaterials.Length - 1) return;
            RenderSettings.skybox = SkyboxMaterials[index];
            SetSkyboxIntensity(skyboxIntensity);
        }

        #endregion Sybox

        #endregion Methods
    }
}
