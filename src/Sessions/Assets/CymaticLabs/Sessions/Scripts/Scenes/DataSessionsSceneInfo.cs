using System;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Captures information about an available Sessions scene.
    /// </summary>
    [Serializable]
    public class DataSessionsSceneInfo
    {
        /// <summary>
        /// The scene's name.
        /// </summary>
        public string Name;

        /// <summary>
        /// The scene's URL.
        /// </summary>
        public string Url;

        /// <summary>
        /// The resource path to the application image to use for the scene.
        /// </summary>
        public string ImageRes;

        /// <summary>
        /// The optional URL to the scene's image.
        /// </summary>
        public string ImageUrl;

        /// <summary>
        /// Optional information about the scene.
        /// </summary>
        [TextArea]
        public string Info;
    }
}
