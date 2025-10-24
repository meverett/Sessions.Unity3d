using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HideOnVR : MonoBehaviour
{
    #region Inspector

    /// <summary>
    /// Deactivates the game object.
    /// </summary>
    public bool Hide = true;

    /// <summary>
    /// Whether to also hide in the editor when in play mode or not.
    /// </summary>
    public bool HideInEditor = false;

    #endregion Inspector

    #region Fields

    #endregion Fields

    #region Properties

    #endregion Properties

    #region Methods

    #region Init

    private void Awake()
    {
        //if ((UnityEngine.XR.XRSettings.enabled) && Hide)
        if ((UnityEngine.XR.XRSettings.enabled || (HideInEditor && Application.isEditor)) && Hide)
            gameObject.SetActive(false);
    }

    #endregion Init

    #region Update

    #endregion Update

    #endregion Methods
}
