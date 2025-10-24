using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using CymaticLabs.Logging;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Plays an audio clip when the UI element is highlighted.
    /// </summary>
    public class SoundOnInteractionUI : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        #region Inspector

        /// <summary>
        /// Whether or not to bypass the sound effects.
        /// </summary>
        [Tooltip("Whether or not to bypass the sound effects.")]
        public bool Bypass = false;

        /// <summary>
        /// The name of the audio clip to play during selection of the component.
        /// </summary>
        [Tooltip("The name of the audio clip to play during selection of the component.")]
        public string SelectClipName = "Select";

        /// <summary>
        /// The name of the audio clip to play during selection of the component.
        /// </summary>
        [Tooltip("The name of the audio clip to play during hover over the component.")]
        public string HoverClipName = "Hover";

        #endregion Inspector

        #region Fields

        // The selectable on the game object
        private Selectable selectable;

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        /// <summary>
        /// Handles item click/select.
        /// </summary>
        public void OnPointerClick(PointerEventData ped)
        {
            if (selectable == null) selectable = GetComponent<Selectable>();
            if (selectable != null && !selectable.interactable) return;
            if (Bypass || string.IsNullOrEmpty(SelectClipName)) return;
            SessionsSound.PlayEfx(SelectClipName);
        }

        /// <summary>
        /// Handles item hover enter.
        /// </summary>
        /// <param name="ped"></param>
        public void OnPointerEnter(PointerEventData ped)
        {
            if (selectable == null) selectable = GetComponent<Selectable>();
            if (selectable != null && !selectable.interactable) return;
            if (Bypass || string.IsNullOrEmpty(HoverClipName)) return;
            SessionsSound.PlayEfx(HoverClipName, true);
        }

        #endregion Methods
    }
}
