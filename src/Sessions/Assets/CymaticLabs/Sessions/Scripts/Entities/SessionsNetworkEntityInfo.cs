using System;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Utility class used to register and name network prefabs in the inspector.
    /// </summary>
    [System.Serializable]
    public class SessionsNetworkEntityInfo : ScriptableObject
    {
        /// <summary>
        /// Whether or not the prefab is currently disabled and will not appear in the registered prefab list at runtime.
        /// </summary>
        public bool Enabled = true;

        /// <summary>
        /// The network name of the prefab.
        /// </summary>
        public string Name;

        /// <summary>
        /// The maximum allowed number of instances of the object during a single session.
        /// </summary>
        public int MaxInstances = 10;

        /// <summary>
        /// The main object prefab.
        /// </summary>
        public GameObject Prefab;

        /// <summary>
        /// The optional non-XR/VR prefab to substitute when executing outside of XR/VR;
        /// </summary>
        public GameObject NonXrPrefab;

        /// <summary>
        /// Gets the current position of the mapping in the editor's list.
        /// </summary>
        [HideInInspector]
        public int EditIndex;

        /// <summary>
        /// Used as a work around to allow edits compatibility with Unity Editor's Undo/Redo stack.
        /// </summary>
        static internal Action<SessionsNetworkEntityInfo> OnCreated;

        public SessionsNetworkEntityInfo()
        {
            // Notify we were created
            EditIndex = -1;
        }

        private void Awake()
        {
            if (OnCreated != null) OnCreated(this);
        }
    }
}
