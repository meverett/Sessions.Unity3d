using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Utility class used for working with loaded images.
    /// </summary>
    public class SessionsLoadedImages : MonoBehaviour
    {
        #region Fields

        // An internal list of scene images loaded by URL
        private Dictionary<string, Texture2D> imagesByUrl;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Static singleton instance.
        /// </summary>
        public static SessionsLoadedImages Current { get; private set; }

        #endregion Properties

        #region Init

        private void Awake()
        {
            Current = this;
            imagesByUrl = new Dictionary<string, Texture2D>();
        }

        #endregion Init

        #region Methods

        /// <summary>
        /// Clears all loaded images from memory.
        /// </summary>
        public static void ClearLoaded()
        {
            if (Current == null) return;
            Current.imagesByUrl.Clear();
        }

        /// <summary>
        /// Loads an image from cache or URL and returns it to the provided callback.
        /// </summary>
        /// <param name="url">The URL of the image to load.</param>
        /// <param name="callback">The image load callback.</param>
        public static void LoadImage(string url, Action<Texture2D> callback)
        {
            if (Current == null) return;
            Current.StartCoroutine(Current.DoLoadImage(url, callback));
        }

        // Loads an image from cache or URL and returns it to the provided callback
        private IEnumerator DoLoadImage(string url, Action<Texture2D> callback)
        {
            // Image is cached so just return...
            if (imagesByUrl.ContainsKey(url))
            {
                if (callback != null) callback(imagesByUrl[url]);
                yield break;
            }

            // Otherwise load the image
            var www = new WWW(url);
            yield return www;
            if (callback != null) callback(www.texture);
        }

        #endregion Methods
    }
}
