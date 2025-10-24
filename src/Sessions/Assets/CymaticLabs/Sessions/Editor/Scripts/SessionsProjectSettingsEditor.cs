using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CymaticLabs.Logging;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Used to update project settings to Sessions recommended settings.
    /// </summary>
    [InitializeOnLoad]
    public class SessionsProjectSettingsEditor : EditorWindow
    {
        #region Constants

        // The size of the window
        private static Vector2 windowSize = new Vector2(320, 180);

        #endregion Constants

        #region Fields

        private static bool autoShow = false;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static SessionsProjectSettingsEditor Current { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        // Static constructor
        static SessionsProjectSettingsEditor()
        {
            // Check to see if this project has been initialized before
            var filePath = GetProjectSettingsFilePath();

            // If the initialization file does not exist, this project has not been initialized yet, show the update project window
            if (!File.Exists(filePath)) autoShow = true;
           
            // Hook up an update listener to automatically show this window if the project is uninitialized
            EditorApplication.update += () =>
            {
                if (autoShow)
                {
                    autoShow = false;
                    ShowWindow();
                }
            };
        }

        #endregion Init

        #region Window

        /// <summary>
        /// Shows the editor window.
        /// </summary>
        [MenuItem("Window/Sessions/Project Settings")]
        public static void ShowWindow()
        {
            var title = "Sessions Project Settings";
            Current = GetWindow<SessionsProjectSettingsEditor>(true, title, true);
            var w = Current;
            w.titleContent = new GUIContent(title);
            w.minSize = windowSize;
            w.maxSize = windowSize;
            var res = Screen.currentResolution;
            w.position = new Rect((res.width / 2) - windowSize.x, (res.height / 2) - windowSize.y, windowSize.x, windowSize.y);
            w.Repaint();
        }

        #endregion Window

        #region OnGUI

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Finish Sessions Configuration", new GUIStyle(GUI.skin.label) { fontSize = 20, padding = new RectOffset(8, 0, 0, 0) }, GUILayout.Height(30));
            EditorGUILayout.BeginHorizontal();
            var message = "Sessions requires specific project settings to run correctly. Press the 'Update Project Settings' button below to automatically update project settings to the correct values.";
            var msgStyle = new GUIStyle(GUI.skin.label) { padding = new RectOffset(8, 0, 0, 0), wordWrap = true };
            EditorGUILayout.LabelField(message, msgStyle);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("If you need to make a project backup you should do so before pressing the update button.", msgStyle);
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            // Update project settings
            if (GUILayout.Button("Update Project Settings", GUILayout.Width(192)))
            {
                UpdateProjectSettings();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        #endregion OnGUI

        #region Settings

        // Updates the project settings to the correct values
        private void UpdateProjectSettings()
        {
            if (!EditorUtility.DisplayDialog("Confirm Project Update", "Are you sure you want to update your project settings?", "Update", "Cancel"))
                return;

            #region Project Settings

            string projectSettingsAssetPath = "ProjectSettings/ProjectSettings.asset";
            var projectSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(projectSettingsAssetPath)[0]);

            #region Use 32-bit Display buffer

            var use32BitDisplayBuffer = projectSettings.FindProperty("use32BitDisplayBuffer");
            use32BitDisplayBuffer.boolValue = true;

            #endregion Use 32-bit Display buffer

            #region Disable Depth and Stencil

            var disableDepthAndStencilBuffers = projectSettings.FindProperty("disableDepthAndStencilBuffers");
            disableDepthAndStencilBuffers.boolValue = false;

            #endregion Disable Depth and Stencil

            #region Color Space: Linear

            var m_ActiveColorSpace = projectSettings.FindProperty("m_ActiveColorSpace");
            m_ActiveColorSpace.intValue = 1;

            #endregion Color Space: Linear

            #region Autographics APIM

            var m_BuildTargetGraphicsAPIs = projectSettings.FindProperty("m_BuildTargetGraphicsAPIs");
            var iterator = m_BuildTargetGraphicsAPIs.Copy();

            // Look through value children until we find the Android Player entry
            var isAndroidPlayer = false;
            var isAPISet = false;
            var isAutomaticSet = false;

            while (iterator.Next(true) && (!isAndroidPlayer || !isAPISet || !isAutomaticSet))
            {
                // Check to see if this is the Android player nested value
                if (iterator.name == "m_BuildTarget" && iterator.stringValue == "AndroidPlayer")
                {
                    isAndroidPlayer = true;
                }
                // Otherwise check to see if this is the API value
                else if (isAndroidPlayer && !isAPISet && iterator.name == "m_APIs")
                {
                    // Clear the current graphics API list
                    iterator.ClearArray();

                    // Add APIs for OpenGL ES3 and Vulkan
                    iterator.arraySize++;
                    iterator.GetArrayElementAtIndex(iterator.arraySize - 1).intValue = 11;
                    iterator.arraySize++;
                    iterator.GetArrayElementAtIndex(iterator.arraySize - 1).intValue = 21;

                    isAPISet = true;
                }
                // Otherwise check to see if this is the automatic value
                else if (isAndroidPlayer && !isAutomaticSet && iterator.name == "m_Automatic")
                {
                    iterator.boolValue = false;
                    isAutomaticSet = true;
                }
            }

            #endregion Autographics APIM

            #region Multithreaded Rendering

            var mobileMTRendering = projectSettings.FindProperty("mobileMTRendering");
            iterator = mobileMTRendering.Copy();
            var isAndroid = false;

            while (iterator.Next(true))
            {
                if (iterator.name == "m_") break;

                if (iterator.isArray && iterator.arraySize > 0)
                {
                    if (iterator.name != "first" && iterator.name != "Array" && iterator.name != "data" && iterator.name != "size" && iterator.name != "second") continue;

                    if (iterator.name == "first")
                    {
                        isAndroid = iterator.stringValue == "Android";
                    }
                }
                // If this is the "second" of the value pair in the array and this is Android, this is the setting
                else if (isAndroid && iterator.name == "second")
                {
                    iterator.boolValue = true; // enable
                    break;
                }
            }

            #endregion Multithreaded Rendering

            #region Static & Dynamic Batching

            // Static Batching
            // Dynamic Batching
            var m_BuildTargetBatching = projectSettings.FindProperty("m_BuildTargetBatching");
            iterator = m_BuildTargetBatching.Copy();
            isAndroid = false;
            var isStaticBatchingSet = false;
            var isDynamicBatchingSet = false;

            while (iterator.Next(true))
            {
                // If this is a build target, update the Android flag
                if (iterator.name == "m_BuildTarget")
                {
                    isAndroid = iterator.stringValue == "Android";
                }
                else if (isAndroid && iterator.name == "m_StaticBatching")
                {
                    iterator.boolValue = true;
                    isStaticBatchingSet = true;
                }
                else if (isAndroid && iterator.name == "m_DynamicBatching")
                {
                    iterator.boolValue = true;
                    isDynamicBatchingSet = true;
                }

                if (isStaticBatchingSet && isDynamicBatchingSet) break;
            }

            #endregion Static & Dynamic Batching

            #region GPU Skinning

            var gpuSkinning = projectSettings.FindProperty("gpuSkinning");
            gpuSkinning.boolValue = true;

            #endregion GPU Skinning

            #region Graphics Jobs

            var graphicsJobs = projectSettings.FindProperty("graphicsJobs");
            graphicsJobs.boolValue = false;

            #endregion Graphics Jobs

            #region Minimum API Level

            var minSdkVersion = projectSettings.FindProperty("AndroidMinSdkVersion");
            minSdkVersion.intValue = 21;

            #endregion Minimum API Level

            #region Script Runtime Version

            // Update to Mono 4.x
            var scriptingRuntimeVersion = projectSettings.FindProperty("scriptingRuntimeVersion");
            scriptingRuntimeVersion.intValue = 1;

            #endregion Script Runtime Version

            #region Scripting Backend

            var scriptingBackend = projectSettings.FindProperty("scriptingBackend");
            iterator = scriptingBackend.Copy();
            isAndroid = false;

            while (iterator.Next(true))
            {
                if (iterator.name == "m_") break;

                if (iterator.isArray && iterator.arraySize > 0)
                {
                    if (iterator.name != "first" && iterator.name != "Array" && iterator.name != "data" && iterator.name != "size" && iterator.name != "second") continue;

                    if (iterator.name == "first")
                    {
                        isAndroid = iterator.stringValue == "Android";
                    }
                }
                // If this is the "second" of the value pair in the array and this is Android, this is the setting
                else if (isAndroid && iterator.name == "second")
                {
                    iterator.intValue = 0; // Mono
                    break;
                }
            }

            #endregion Scripting Backend

            #region API Compatibility

            var apiCompatibilityLevelPerPlatform = projectSettings.FindProperty("apiCompatibilityLevelPerPlatform");
            
            iterator = apiCompatibilityLevelPerPlatform.Copy();
            isAndroid = false;
            var isStandAlone = false;

            // If there are no values currently, create the first entry
            if (iterator.arraySize == 0)
            {
                iterator.arraySize++;
                var pair = iterator.GetArrayElementAtIndex(iterator.arraySize - 1);
                var first = pair.FindPropertyRelative("first");
                first.stringValue = "Android";
                var second = pair.FindPropertyRelative("second");
                second.intValue = 3; // .NET 4.x

                iterator.arraySize++;
                pair = iterator.GetArrayElementAtIndex(iterator.arraySize - 1);
                first = pair.FindPropertyRelative("first");
                first.stringValue = "Standalone";
                second = pair.FindPropertyRelative("second");
                second.intValue = 3; // .NET 4.x
            }
            // Otherwise scan current entries
            else
            {
                while (iterator.Next(true))
                {
                    if (iterator.isArray && iterator.arraySize > 0)
                    {
                        if (iterator.name != "first" && iterator.name != "Array" && iterator.name != "data" && iterator.name != "size" && iterator.name != "second") continue;

                        if (iterator.name == "first")
                        {
                            isAndroid = iterator.stringValue == "Android";
                            isStandAlone = iterator.stringValue == "Standalone";
                        }
                    }
                    // If this is the "second" of the value pair in the array and this is Android, this is the setting
                    else if ((isAndroid || isStandAlone) && iterator.name == "second")
                    {
                        iterator.intValue = 3; // .NET 4.x
                    }
                }
            }

            #endregion API Compatibility

            #region Prebake Collision Meshes

            var bakeCollisionMeshes = projectSettings.FindProperty("bakeCollisionMeshes");
            bakeCollisionMeshes.boolValue = true;

            #endregion Prebake Collision Meshes

            #region Keep Loaded Shaders Alive

            var keepLoadedShadersAlive = projectSettings.FindProperty("keepLoadedShadersAlive");
            keepLoadedShadersAlive.boolValue = true;

            #endregion Keep Loaded Shaders Alive

            #region Optimize Mesh Data

            var StripUnusedMeshComponents = projectSettings.FindProperty("StripUnusedMeshComponents");
            StripUnusedMeshComponents.boolValue = true;

            #endregion Optimize Mesh Data

            #region Virtual Reality SDKs

            var m_BuildTargetVRSettings = projectSettings.FindProperty("m_BuildTargetVRSettings");
            iterator = m_BuildTargetBatching.Copy();
            isAndroid = false;
            var isEnabledSet = false;
            var isDevices = false;
            
            while (iterator.Next(true))
            {
                // If this is a build target, update the Android flag
                if (iterator.name == "m_BuildTarget")
                {
                    isAndroid = iterator.stringValue == "Android";
                }
                else if (isAndroid && iterator.name == "m_Enabled")
                {
                    iterator.boolValue = true;
                    isEnabledSet = true;
                }
                else if (isAndroid && iterator.name == "m_Devices")
                {
                    // Clear current list
                    iterator.ClearArray();

                    // Add items
                    iterator.arraySize++;
                    iterator.GetArrayElementAtIndex(0).stringValue = "Oculus";
                    iterator.arraySize++;
                    iterator.GetArrayElementAtIndex(1).stringValue = "None";
                    isDevices = true;
                }

                if (isEnabledSet && isDevices) break;
            }

            // If there was no enabled entry found for Android, create one
            if (!isEnabledSet)
            {
                m_BuildTargetVRSettings.arraySize++;
                var buildTarget = m_BuildTargetVRSettings.GetArrayElementAtIndex(m_BuildTargetVRSettings.arraySize - 1);

                buildTarget.FindPropertyRelative("m_BuildTarget").stringValue = "Android";

                var m_Enabled = buildTarget.FindPropertyRelative("m_Enabled");
                m_Enabled.boolValue = true;

                var m_Devices = buildTarget.FindPropertyRelative("m_Devices");
                m_Devices.ClearArray();

                // Add items
                m_Devices.arraySize++;
                m_Devices.GetArrayElementAtIndex(0).stringValue = "Oculus";
                m_Devices.arraySize++;
                m_Devices.GetArrayElementAtIndex(1).stringValue = "None";
                isEnabledSet = true;
                isDevices = true;
            }

            #endregion Virtual Reality SDKs

            #region Stereo Render Method

            var m_StereoRenderingPath = projectSettings.FindProperty("m_StereoRenderingPath");
            m_StereoRenderingPath.intValue = 1; // Single pass

            #endregion Stereo Render Method

            // Save settings
            projectSettings.ApplyModifiedProperties();

            #endregion Project Settings

            #region Audio Settings

            string audioSettingsAssetPath = "ProjectSettings/AudioManager.asset";
            var audioSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(audioSettingsAssetPath)[0]);

            #region DSP Buffer Size

            var m_DSPBufferSize = audioSettings.FindProperty("m_DSPBufferSize");
            m_DSPBufferSize.intValue = 512; // Good latency

            #endregion DSP Buffer Size

            #region Spatializer Plugin

            var m_SpatializerPlugin = audioSettings.FindProperty("m_SpatializerPlugin");
            m_SpatializerPlugin.stringValue = "OculusSpatializer";

            #endregion Spatializer Plugin

            #region Ambisonic Plugin

            var m_AmbisonicDecoderPlugin = audioSettings.FindProperty("m_AmbisonicDecoderPlugin");
            m_AmbisonicDecoderPlugin.stringValue = "OculusSpatializer";

            #endregion Ambisonic Plugin

            // Save settings
            audioSettings.ApplyModifiedProperties();

            #endregion Audio Settings

            #region Quality Settings

            string qualitySettingsAssetPath = "ProjectSettings/QualitySettings.asset";
            var qualitySettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(qualitySettingsAssetPath)[0]);

            #region Configure Quality Settings

            var m_QualitySettings = qualitySettings.FindProperty("m_QualitySettings");
            iterator = m_QualitySettings.Copy();
            string qualityName = null;

            while (iterator.Next(true))
            {
                var value = GetValueFromProperty(iterator);

                // Set current quality
                if (iterator.name == "name") qualityName = (string)value;

                // If this is anything but "Medium/Simple" quality
                if (qualityName != null && qualityName != "Medium" && qualityName != "Simple")
                {
                    if (iterator.name == "excludedTargetPlatforms")
                    {
                        iterator.ClearArray();
                        iterator.arraySize++;
                        iterator.GetArrayElementAtIndex(0).stringValue = "Android";
                        //Debug.LogFormat("{0} Array? {1}:{2}", qualityName, iterator.arrayElementType, iterator.arraySize);
                    }
                }
                // Configure medium quality
                else if (qualityName == "Medium" || qualityName == "Simple")
                {
                    #region Configure Medium Quality

                    switch (iterator.name)
                    {
                        case "pixelLightCount":
                            iterator.intValue = 1;
                            break;

                        case "shadows":
                            iterator.intValue = 1;
                            break;

                        case "shadowResolution":
                            iterator.intValue = 0;
                            break;

                        case "shadowProjection":
                            iterator.intValue = 1;
                            break;

                        case "shadowCascades":
                            iterator.intValue = 1;
                            break;

                        case "shadowDistance":
                            iterator.floatValue = 20f;
                            break;

                        case "shadowNearPlaneOffset":
                            iterator.floatValue = 3;
                            break;

                        case "shadowCascade2Split":
                            iterator.floatValue = 0.33333334f;
                            break;

                        case "shadowCascade4Split":
                            iterator.vector3Value = new Vector3(0.06666667f, 0.2f, 0.46666667f);
                            break;

                        case "shadowmaskMode":
                            iterator.intValue = 0;
                            break;

                        case "blendWeights":
                            iterator.intValue = 2;
                            break;

                        case "textureQuality":
                            iterator.intValue = 0;
                            break;

                        case "anisotropicTextures":
                            iterator.intValue = 0;
                            break;

                        case "antiAliasing":
                            iterator.intValue = 4;
                            break;

                        case "softParticles":
                            iterator.boolValue = false;
                            break;

                        case "softVegetation":
                            iterator.boolValue = false;
                            break;

                        case "realtimeReflectionProbes":
                            iterator.boolValue = false;
                            break;

                        case "billboardsFaceCameraPosition":
                            iterator.boolValue = false;
                            break;

                        case "vSyncCount":
                            iterator.intValue = 0;
                            break;

                        case "lodBias":
                            iterator.floatValue = 0.7f;
                            break;

                        case "maximumLODLevel":
                            iterator.intValue = 0;
                            break;

                        case "streamingMipmapsActive":
                            iterator.boolValue = false;
                            break;

                        case "particleRaycastBudget":
                            iterator.intValue = 64;
                            break;

                        case "asyncUploadTimeSlice":
                            iterator.intValue = 2;
                            break;

                        case "asyncUploadBufferSize":
                            iterator.intValue = 4;
                            break;

                        case "resolutionScalingFixedDPIFactor":
                            iterator.floatValue = 1;
                            break;
                    }

                    #endregion Configure Medium Quality
                }
            }

            #endregion Configure Quality Settings

            // Save settings
            qualitySettings.ApplyModifiedProperties();

            #endregion Quality Settings

            #region Graphics Settings

            string graphicsSettingsAssetPath = "ProjectSettings/GraphicsSettings.asset";
            var graphicsSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(graphicsSettingsAssetPath)[0]);

            #region Tier Settings

            var m_TierSettings = graphicsSettings.FindProperty("m_TierSettings");
            iterator = m_TierSettings.Copy();
            iterator.ClearArray();

            #region Tier 1

            iterator.arraySize++;
            var tier1 = iterator.GetArrayElementAtIndex(0);

            var m_BuildTarget = tier1.FindPropertyRelative("m_BuildTarget");
            m_BuildTarget.intValue = 7;

            var m_Tier = tier1.FindPropertyRelative("m_Tier");
            m_Tier.intValue = 0;

            var m_Automatic = tier1.FindPropertyRelative("m_Automatic");
            m_Automatic.boolValue = false;

            var m_Settings = tier1.FindPropertyRelative("m_Settings");

            var standardShaderQuality = m_Settings.FindPropertyRelative("standardShaderQuality");
            standardShaderQuality.intValue = 0;

            var renderingPath = m_Settings.FindPropertyRelative("renderingPath");
            renderingPath.intValue = 1;

            var hdrMode = m_Settings.FindPropertyRelative("hdrMode");
            hdrMode.intValue = 2;

            var realtimeGICPUUsage = m_Settings.FindPropertyRelative("realtimeGICPUUsage");
            realtimeGICPUUsage.intValue = 25;

            #endregion Tier 1

            #region Tier 2

            iterator.arraySize++;
            var tier2 = iterator.GetArrayElementAtIndex(1);

            var m_BuildTarget2 = tier2.FindPropertyRelative("m_BuildTarget");
            m_BuildTarget2.intValue = 7;

            var m_Tier2 = tier2.FindPropertyRelative("m_Tier");
            m_Tier2.intValue = 1;

            var m_Automatic2 = tier2.FindPropertyRelative("m_Automatic");
            m_Automatic2.boolValue = false;

            var m_Settings2 = tier2.FindPropertyRelative("m_Settings");

            var standardShaderQuality2 = m_Settings2.FindPropertyRelative("standardShaderQuality");
            standardShaderQuality2.intValue = 0;

            var renderingPath2 = m_Settings2.FindPropertyRelative("renderingPath");
            renderingPath2.intValue = 1;

            var hdrMode2 = m_Settings2.FindPropertyRelative("hdrMode");
            hdrMode2.intValue = 2;

            var realtimeGICPUUsage2 = m_Settings2.FindPropertyRelative("realtimeGICPUUsage");
            realtimeGICPUUsage2.intValue = 25;

            #endregion Tier 2

            #region Tier 3

            iterator.arraySize++;
            var tier3 = iterator.GetArrayElementAtIndex(2);

            var m_BuildTarget3 = tier3.FindPropertyRelative("m_BuildTarget");
            m_BuildTarget3.intValue = 7;

            var m_Tier3 = tier3.FindPropertyRelative("m_Tier");
            m_Tier3.intValue = 2;

            var m_Automatic3 = tier3.FindPropertyRelative("m_Automatic");
            m_Automatic3.boolValue = false;

            var m_Settings3 = tier3.FindPropertyRelative("m_Settings");

            var standardShaderQuality3 = m_Settings3.FindPropertyRelative("standardShaderQuality");
            standardShaderQuality3.intValue = 0;

            var renderingPath3 = m_Settings3.FindPropertyRelative("renderingPath");
            renderingPath3.intValue = 1;

            var hdrMode3 = m_Settings3.FindPropertyRelative("hdrMode");
            hdrMode3.intValue = 2;

            var realtimeGICPUUsage3 = m_Settings3.FindPropertyRelative("realtimeGICPUUsage");
            realtimeGICPUUsage3.intValue = 25;

            #endregion Tier 3

            #endregion Tier Settings

            // Save settings
            graphicsSettings.ApplyModifiedProperties();

            #endregion Graphics Settings

            #region Input Settings

            string inputSettingsAssetPath = "ProjectSettings/InputManager.asset";
            var inputSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(inputSettingsAssetPath)[0]);

            // Check to see if the required axes are configured
            var doesPrimaryExist = false;
            var doesSecondaryExist = false;
            var doesBackExist = false;

            // Get the axes array and iterate through it
            var m_Axes = inputSettings.FindProperty("m_Axes");
            iterator = m_Axes.Copy();

            while (iterator.Next(true))
            {
                // This is the name of the axis, check for our names
                if (iterator.name == "m_Name")
                {
                    if (iterator.stringValue == "Primary") doesPrimaryExist = true;
                    else if (iterator.stringValue == "Secondary") doesSecondaryExist = true;
                    else if (iterator.stringValue == "Back") doesBackExist = true;
                }
            }

            //Debug.LogFormat("Primary: {0}, Secondary: {1}, Back: {2}", doesPrimaryExist, doesSecondaryExist, doesBackExist);

            #region Create "Primary" Axis

            // Build the "Primary" axes entry if it doesn't exist
            if (!doesPrimaryExist)
            {
                m_Axes.arraySize++;
                var axis = m_Axes.GetArrayElementAtIndex(m_Axes.arraySize - 1);

                var m_Name = axis.FindPropertyRelative("m_Name");
                m_Name.stringValue = "Primary";

                var positiveButton = axis.FindPropertyRelative("positiveButton");
                positiveButton.stringValue = "k";

                var altPositiveButton = axis.FindPropertyRelative("altPositiveButton");
                altPositiveButton.stringValue = "mouse 0";

                var gravity = axis.FindPropertyRelative("gravity");
                gravity.floatValue = 1000;

                var dead = axis.FindPropertyRelative("dead");
                dead.floatValue = 0.001f;

                var sensitivity = axis.FindPropertyRelative("sensitivity");
                sensitivity.floatValue = 1000;

                var snap = axis.FindPropertyRelative("snap");
                snap.boolValue = false;

                var invert = axis.FindPropertyRelative("invert");
                invert.boolValue = false;

                var type = axis.FindPropertyRelative("type");
                type.intValue = 0;
            }

            #endregion Create "Primary" Axis

            #region Create "Secondary" Axis

            // Build the "Secondary" axes entry if it doesn't exist
            if (!doesSecondaryExist)
            {
                m_Axes.arraySize++;
                var axis = m_Axes.GetArrayElementAtIndex(m_Axes.arraySize - 1);

                var m_Name = axis.FindPropertyRelative("m_Name");
                m_Name.stringValue = "Secondary";

                var positiveButton = axis.FindPropertyRelative("positiveButton");
                positiveButton.stringValue = "l";

                var altPositiveButton = axis.FindPropertyRelative("altPositiveButton");
                altPositiveButton.stringValue = "mouse 1";

                var gravity = axis.FindPropertyRelative("gravity");
                gravity.floatValue = 1000;

                var dead = axis.FindPropertyRelative("dead");
                dead.floatValue = 0.001f;

                var sensitivity = axis.FindPropertyRelative("sensitivity");
                sensitivity.floatValue = 1000;

                var snap = axis.FindPropertyRelative("snap");
                snap.boolValue = false;

                var invert = axis.FindPropertyRelative("invert");
                invert.boolValue = false;

                var type = axis.FindPropertyRelative("type");
                type.intValue = 0;
            }

            #endregion Create "Secondary" Axis

            #region Create "Back" Axis

            // Build the "Back/Menu" axes entry if it doesn't exist
            if (!doesBackExist)
            {
                m_Axes.arraySize++;
                var axis = m_Axes.GetArrayElementAtIndex(m_Axes.arraySize - 1);

                var m_Name = axis.FindPropertyRelative("m_Name");
                m_Name.stringValue = "Back";

                var positiveButton = axis.FindPropertyRelative("positiveButton");
                positiveButton.stringValue = "f1";

                var altPositiveButton = axis.FindPropertyRelative("altPositiveButton");
                altPositiveButton.stringValue = null;

                var gravity = axis.FindPropertyRelative("gravity");
                gravity.floatValue = 1000;

                var dead = axis.FindPropertyRelative("dead");
                dead.floatValue = 0.001f;

                var sensitivity = axis.FindPropertyRelative("sensitivity");
                sensitivity.floatValue = 1000;

                var snap = axis.FindPropertyRelative("snap");
                snap.boolValue = false;

                var invert = axis.FindPropertyRelative("invert");
                invert.boolValue = false;

                var type = axis.FindPropertyRelative("type");
                type.intValue = 0;
            }

            #endregion Create "Back" Axis

            // Save settings
            inputSettings.ApplyModifiedProperties();

            #endregion Input Settings

            EditorUtility.DisplayDialog("Project Update Complete", "Project settings have been updated. Make sure you save to commit the new project settings. Reopen any inspector UI so that it will reflect the latest changes if you have any settings windows open.\n\nRemember to set the Android build 'Texture Compression' settings to 'ASTC' in Build Settings; this final step must be done manually. Finally set player settings 'Script Runtime Version' and 'Api Compatibility Level' to .NET 4.x.", "OK");
            CyLog.LogInfo("Sessions finsihed updating project settings.");

            // Write initialization file as needed
            var filePath = GetProjectSettingsFilePath();

            if (!File.Exists(filePath))
            {
                // Write the file
                File.WriteAllText(filePath, string.Format("{{\"isInitialized\":true}}")); 
            }

            // Close the window
            Close();
        }

        // Converts most major values from a property into a generic object raw value
        private object GetValueFromProperty(SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.Integer || property.propertyType == SerializedPropertyType.ArraySize) return property.intValue;
            else if (property.propertyType == SerializedPropertyType.Boolean) return property.boolValue;
            else if (property.propertyType == SerializedPropertyType.Color) return property.colorValue;
            else if (property.propertyType == SerializedPropertyType.Float) return property.floatValue;
            else if (property.propertyType == SerializedPropertyType.String) return property.stringValue;
            else if (property.propertyType == SerializedPropertyType.Vector2) return property.vector2Value;
            else if (property.propertyType == SerializedPropertyType.Vector2Int) return property.vector2IntValue;
            else if (property.propertyType == SerializedPropertyType.Vector3) return property.vector3Value;
            else if (property.propertyType == SerializedPropertyType.Vector3Int) return property.vector3IntValue;
            else if (property.propertyType == SerializedPropertyType.Vector4) return property.vector4Value;
            else if (property.propertyType == SerializedPropertyType.Rect) return property.rectValue;
            else if (property.propertyType == SerializedPropertyType.RectInt) return property.rectIntValue;
            else if (property.propertyType == SerializedPropertyType.Quaternion) return property.quaternionValue;
            else if (property.propertyType == SerializedPropertyType.Enum) return property.enumValueIndex;
            return null;
        }

        // Gets the file path to the Sessions project settings file
        private static string GetProjectSettingsFilePath()
        {
            var ps = Path.DirectorySeparatorChar;
            return Path.Combine(Directory.GetCurrentDirectory(), "Assets" + ps + "CymaticLabs" + ps + "Sessions" + ps + "sessions.json");
        }

        #endregion Settings

        #endregion Methods
    }
}
