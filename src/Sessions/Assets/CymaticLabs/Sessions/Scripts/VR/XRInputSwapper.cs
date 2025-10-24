using UnityEngine;
using UnityEngine.EventSystems;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Autmatically disables/enables the correct input system at runtime based
    /// on detecting whether or not XR/VR is enabled.
    /// </summary>
    public class XRInputSwapper : MonoBehaviour
    {
        private void Awake()
        {
            var standardInput = GetComponentInChildren<StandaloneInputModule>();
            var vrInput = GetComponentInChildren<SessionsOVRInputModule>();

            if (standardInput != null && vrInput != null)
            {
                var isXR = UnityEngine.XR.XRSettings.enabled;
                standardInput.enabled = !isXR;
                vrInput.enabled = isXR;
            }
            else
            {
                Debug.LogWarning("XR Input Swapper could not find one or both of the Standard/XR input modules and will be disabled");
            }
        }
    }
}
