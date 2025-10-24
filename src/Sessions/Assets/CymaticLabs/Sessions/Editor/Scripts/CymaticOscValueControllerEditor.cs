using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;

namespace CymaticLabs.Protocols.Osc.Unity3d
{
    /// <summary>
    /// Custom editor for <see cref="SessionsOscValueController"/>.
    /// </summary>
    [CustomEditor(typeof(SessionsOscValueController))]
    [CanEditMultipleObjects]
    public class CymaticOscValueControllerEditor : CustomEditorBase
    {
        #region Inspector

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        public override void OnInspectorGUI()
        {
            //EditorGUILayout.LabelField("Custom Editor", EditorStyles.centeredGreyMiniLabel);
            Color cachedGuiColor = GUI.color;
            serializedObject.Update();
            var property = serializedObject.GetIterator();
            var next = property.NextVisible(true);
            if (next)
                do
                {
                    GUI.color = cachedGuiColor;

                    if (property.name == "AllowedValues" && property.isArray)
                    {
                        EditorGUILayout.Space();
                        //EditorGUILayout.LabelField("OSC Value Mapping", EditorStyles.boldLabel);

                        EditorGUILayout.BeginHorizontal();

                        // Loads OSC settings from file
                        if (GUILayout.Button("Open"))
                        {
                            var openPath = EditorUtility.OpenFilePanel("Open OSC Mapping File", Directory.GetCurrentDirectory(), "json");

                            // Read in the file JSON
                            var json = File.ReadAllText(openPath);

                            // Deserialize
                            var oscSettings = JsonConvert.DeserializeObject<SessionsOscConfiguration>(json);

                            // Clear all of the current property entries
                            property.ClearArray();

                            // Go through each allowed float value in the settings...
                            foreach (var map in oscSettings.AllowedFloats)
                            {
                                // And create a new entry for it
                                property.arraySize++;

                                // Get the newly created entry
                                var m = property.GetArrayElementAtIndex(property.arraySize - 1);

                                // Bind its data
                                m.FindPropertyRelative("Name").stringValue = map.Name;
                                m.FindPropertyRelative("Address").stringValue = map.Address;
                                m.FindPropertyRelative("ArgumentIndex").intValue = map.ArgumentIndex;
                                m.FindPropertyRelative("ClampInput").boolValue = map.ClampInput;
                                m.FindPropertyRelative("MinInputValue").floatValue = map.MinInputValue;
                                m.FindPropertyRelative("MaxInputValue").floatValue = map.MaxInputValue;
                                m.FindPropertyRelative("ScaleOutput").boolValue = map.ScaleOutput;
                                m.FindPropertyRelative("MinOutputValue").floatValue = map.MinOutputValue;
                                m.FindPropertyRelative("MaxOutputValue").floatValue = map.MaxOutputValue;
                                m.FindPropertyRelative("Reliable").boolValue = map.Reliable;
                                m.FindPropertyRelative("NoBroadcast").boolValue = map.NoBroadcast;
                            }
                        }

                        // Saves OSC settings to file
                        if (GUILayout.Button("Save"))
                        {
                            var savePath = EditorUtility.SaveFilePanel("Save OSC Mapping File", Directory.GetCurrentDirectory(), "sessions-osc-map", "json");

                            if (!string.IsNullOrEmpty(savePath))
                            {
                                // Create new OSC settings
                                var oscSettings = new SessionsOscConfiguration();
                                var allowedFloats = new List<OscRangeMapFloat>();

                                // Go through each allowed float value...
                                for (var i = 0; i < property.arraySize; i++)
                                {
                                    var m = property.GetArrayElementAtIndex(i);

                                    // Get the properties from the current entry
                                    var name = m.FindPropertyRelative("Name");
                                    var address = m.FindPropertyRelative("Address");
                                    var argIndex = m.FindPropertyRelative("ArgumentIndex");
                                    var clampInput = m.FindPropertyRelative("ClampInput");
                                    var minInputValue = m.FindPropertyRelative("MinInputValue");
                                    var maxInputValue = m.FindPropertyRelative("MaxInputValue");
                                    var scaleOutput = m.FindPropertyRelative("ScaleOutput");
                                    var minOutputValue = m.FindPropertyRelative("MinOutputValue");
                                    var maxOutputValue = m.FindPropertyRelative("MaxOutputValue");
                                    var reliable = m.FindPropertyRelative("Reliable");
                                    var noBroadcast = m.FindPropertyRelative("NoBroadcast");

                                    // Create a new entry
                                    var map = new OscRangeMapFloat()
                                    {
                                        Name = name.stringValue,
                                        Address = address.stringValue,
                                        ArgumentIndex = argIndex.intValue,
                                        ClampInput = clampInput.boolValue,
                                        MinInputValue = minInputValue.floatValue,
                                        MaxInputValue = maxInputValue.floatValue,
                                        ScaleOutput = scaleOutput.boolValue,
                                        MinOutputValue = minOutputValue.floatValue,
                                        MaxOutputValue = maxOutputValue.floatValue,
                                        Reliable = reliable.boolValue,
                                        NoBroadcast = noBroadcast.boolValue
                                    };

                                    allowedFloats.Add(map);
                                }

                                // Serialize the OSC settings to JSON and save
                                oscSettings.AllowedFloats = allowedFloats.ToArray();
                                File.WriteAllText(savePath, JsonConvert.SerializeObject(oscSettings, Formatting.Indented));
                                Debug.LogFormat("OSC settings file saved to {0}", savePath);
                            }
                        }

                        // Clears OSC settings
                        if (GUILayout.Button("Clear"))
                        {
                            if (EditorUtility.DisplayDialog("Confirm Clear OSC Settings", "Are you sure you want to clear OSC settings?", "OK"))
                            {
                                // Clear the current allowed values array
                                serializedObject.FindProperty("AllowedValues").arraySize = 0;
                            }
                        }

                        EditorGUILayout.EndHorizontal();
                    }

                    this.HandleProperty(property);
                } while (property.NextVisible(false));
            serializedObject.ApplyModifiedProperties();
        }

        #endregion Methods
    }
}    
