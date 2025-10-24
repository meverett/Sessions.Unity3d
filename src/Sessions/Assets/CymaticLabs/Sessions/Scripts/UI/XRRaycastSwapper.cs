using UnityEngine;
using UnityEngine.UI;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Autmatically disables/enables the correct UI raycast behavior at runtime based
    /// on detecting whether or not XR/VR is enabled.
    /// </summary>
    public class XRRaycastSwapper : MonoBehaviour
    {
        private void Awake()
        {
            var graphicRaycaster = GetComponentInChildren<GraphicRaycaster>();
            var vrRaycaster = GetComponentInChildren<OVRRaycaster>();

            if (graphicRaycaster != null && vrRaycaster != null)
            {
                var isXR = UnityEngine.XR.XRSettings.enabled;
                graphicRaycaster.enabled = !isXR;
                vrRaycaster.enabled = isXR;
            }
            else
            {
                Debug.LogWarning("XR Raycast Swapper could not find one or both of the Standard/XR raycaster components and will be disabled");
            }
        }
    }
}
