using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Represents a collection of sessions scenes.
    /// </summary>
    [Serializable]
    public class SessionsScenesConfiguration : ScriptableObject
    {
        #region Fields

        /// <summary>
        /// The name of the configuration.
        /// </summary>
        public string Name;

        /// <summary>
        /// The version of the settings.
        /// </summary>
        [HideInInspector]
        public float Version;

        /// <summary>
        /// The list of available scenes.
        /// </summary>
        //[HideInInspector]
        public SessionsSceneInfo[] Scenes;

        /// <summary>
        /// The path to the configuration file on disk, if available.
        /// </summary>
        internal string FilePath;

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Constructors

        public SessionsScenesConfiguration() : this(1.0f)
        {
        }

        public SessionsScenesConfiguration(float version = 1.0f)
        {
            Version = version;
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Converts between a non-scriptable to scriptable version of the configuration file.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static SessionsScenesConfiguration ToScriptable(DataSessionsScenesConfiguration config)
        {
            var c = CreateInstance<SessionsScenesConfiguration>();
            c.Name = config.Name;
            c.Version = config.Version;

            var valueList = new List<SessionsSceneInfo>(config.Scenes != null ? config.Scenes.Length : 0);

            if (config.Scenes != null)
            {
                foreach (var s in config.Scenes)
                {
                    // Copy over to a scriptable version
                    var scene = CreateInstance<SessionsSceneInfo>();

                    // See if this asset path was in a resources folder, and if so, extract its relative resources path
                    string imageRes = s.ImageRes != null && s.ImageRes.Contains("Resources/") ? s.ImageRes : null;

                    if (imageRes != null)
                    {
                        imageRes = imageRes.Split(new string[] { "Resources/" }, StringSplitOptions.RemoveEmptyEntries)[1];

                        // Now we have the relative resources path, just need to remove the file extension
                        var extIndex = imageRes.LastIndexOf('.');
                        if (extIndex > -1) imageRes = imageRes.Substring(0, extIndex);

                        // If we have a resource path, load it
                        if (!string.IsNullOrEmpty(imageRes)) scene.Image = Resources.Load<Texture2D>(imageRes);
                    }

                    //if (!string.IsNullOrEmpty(s.ImageRes)) scene.Image = AssetDatabase.LoadAssetAtPath<Texture2D>(s.ImageRes);
                    scene.ImageUrl = s.ImageUrl;
                    scene.Info = s.Info;
                    scene.Name = s.Name;
                    scene.Url = s.Url;
                    valueList.Add(scene);
                }
            }

            // Add to the new configuration
            c.Scenes = valueList.ToArray();
            return c;
        }

        #endregion Methods
    }
}
