using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Captures information about an available Sessions scene.
    /// </summary>
    [Serializable]
    public class SessionsSceneInfo : ScriptableObject
    {
        #region Fields

        #endregion Fields

        /// <summary>
        /// The scene's name.
        /// </summary>
        public string Name;

        /// <summary>
        /// The scene's URL.
        /// </summary>
        public string Url;

        /// <summary>
        /// The image to use for the scene.
        /// </summary>
        public Texture2D Image;

        /// <summary>
        /// The optional URL to the scene's image.
        /// </summary>
        public string ImageUrl;

        /// <summary>
        /// Optional information about the scene.
        /// </summary>
        [TextArea]
        public string Info;

        /// <summary>
        /// Used as a work around to allow edits compatibility with Unity Editor's Undo/Redo stack.
        /// </summary>
        static internal Action<SessionsSceneInfo> OnCreated;

        /// <summary>
        /// Gets the current position of the mapping in the editor's list.
        /// </summary>
        public int EditIndex;

        #region Properties

        #endregion Properties

        #region Constructors

        public SessionsSceneInfo()
        {
            // Notify we were created
            EditIndex = -1;
            if (OnCreated != null) OnCreated(this);
        }

        #endregion Constructors

        #region Methods

        #endregion Methods
    }

    /// <summary>
    /// Different types of Sessions scenes.
    /// </summary>
    public enum SessionsSceneTypes
    {
        /// <summary>
        /// The default lobby scene.
        /// </summary>
        Lobby = 0,
        
        /// <summary>
        /// A scene that is contained within the running application.
        /// </summary>
        Application = 1,
        
        /// <summary>
        /// A scene that is contained within an asset bundle.
        /// </summary>
        AssetBundle = 2,
    }
}
