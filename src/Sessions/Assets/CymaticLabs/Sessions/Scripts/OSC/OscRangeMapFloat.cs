using System;
using UnityEngine;

namespace CymaticLabs.Protocols.Osc.Unity3d
{
    /// <summary>
    /// Maps an expected input range to a desired output range for OSC messages
    /// with floating point message arguments.
    /// </summary>
    [System.Serializable]
    public class OscRangeMapFloat : ScriptableObject
    {
        /// <summary>
        /// The name of the exposed value.
        /// </summary>
        public string Name;

        /// <summary>
        /// Gets or sets the OSC address used for the mapping.
        /// </summary>
        public string Address;

        /// <summary>
        /// Gets or sets the index of the argument that who's range will be mapped.
        /// </summary>
        public int ArgumentIndex = 0;

        /// <summary>
        /// Whether or not to clamp input values using the minimum and maximum input parameters.
        /// </summary>
        public bool ClampInput = false;

        /// <summary>
        /// The minimum input value.
        /// </summary>
        public float MinInputValue = 0;

        /// <summary>
        /// The maxiumum input value.
        /// </summary>
        public float MaxInputValue = 1;

        /// <summary>
        /// Whether or not to scale output values using the minimum and maximum output parameters.
        /// </summary>
        public bool ScaleOutput = false;

        /// <summary>
        /// The minimum output value.
        /// </summary>
        public float MinOutputValue = 0;

        /// <summary>
        /// The maxiumum output value.
        /// </summary>
        public float MaxOutputValue = 1;

        /// <summary>
        /// Whether or not the mapping should send/forward data reliably or unreliably over UDP.
        /// </summary>
        public bool Reliable = false;

        /// <summary>
        /// Whether or not the the value should be rebroadcast to connected peers.
        /// </summary>
        public bool NoBroadcast = false;

        /// <summary>
        /// Used as a work around to allow edits compatibility with Unity Editor's Undo/Redo stack.
        /// </summary>
        static internal Action<OscRangeMapFloat> OnCreated;

        /// <summary>
        /// Gets the current position of the mapping in the editor's list.
        /// </summary>
        public int EditIndex;

        public OscRangeMapFloat()
        {
            // Notify we were created
            EditIndex = -1;
            if (OnCreated != null) OnCreated(this);
        }
    }
}
