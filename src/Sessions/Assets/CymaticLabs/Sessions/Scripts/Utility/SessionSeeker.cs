using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CymaticLabs.Sessions.Unity3d
{
    public class SessionSeeker : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The world position that the seeker is currently seeking towards.
        /// </summary>
        [Tooltip("The world position that the seeker is currently seeking towards.")]
        public Vector3 TargetPosition;

        /// <summary>
        /// A speed multiplier to increase or decrease the speed of travel towards the target position.
        /// </summary>
        [Tooltip("A speed multiplier to increase or decrease the speed of travel towards the target position.")]
        public float Speed = 1;

        /// <summary>
        /// The name of the session value to bind to.
        /// </summary>
        public string ValueName = "Global.Value1";

        #endregion Inspector

        #region Fields

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        private void Start()
        {
            if (SessionsSceneManager.Current == null) return;

            // Bind to session value if present
            if (!string.IsNullOrEmpty(ValueName))
                SessionsSceneManager.Current.RegisterValueHandler(ValueName, HandleValueEvent);
        }

        #endregion Init

        #region Update

        private void Update()
        {
            var delta = TargetPosition - transform.position;
            transform.position += delta * Time.deltaTime * Speed;
        }

        #endregion Update

        #region Event Handlers

        void HandleValueEvent(string name, float value)
        {
            var pos = TargetPosition;
            pos.y = 2 + (value * 4);
            TargetPosition = pos;
        }

        #endregion Event Handlers

        #endregion Methods
    }
}
