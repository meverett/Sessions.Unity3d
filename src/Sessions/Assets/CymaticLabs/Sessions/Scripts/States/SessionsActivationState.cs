using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using CymaticLabs.Sessions.Core;
using CymaticLabs.Logging;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Behavior that is a state machine used to activate and deactivate game objects
    /// while entering and existing a customized state name.
    /// </summary>
    public class SessionsActivationState : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The name of the state used to activate/deactive on enter/exit.
        /// </summary>
        public string StateName = "ActivationState";

        /// <summary>
        /// The network state machine to register with.
        /// </summary>
        public SessionsNetworkStateMachine NetworkStates;

        /// <summary>
        /// A list of game objects to activate on enter.
        /// </summary>
        public GameObject[] ActivateOnEnter;

        /// <summary>
        /// A list of game objects to deactivate on exit.
        /// </summary>
        public GameObject[] DeactivateOnExit;

        #endregion Inspector

        #region Fields

        // The underlying state used by the state machine
        private CustomSessionState activationState;

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Methods

        #region Init

        private void Start()
        {
            if (NetworkStates == null) NetworkStates = GetComponentInChildren<SessionsNetworkStateMachine>();

            if (NetworkStates == null)
            {
                CyLog.LogWarn("No Network State Machine reference could be found and network activation states disabled.");
                return;
            }

            // Create the activation state
            activationState = new CustomSessionState(StateName, false, false);

            // Activate handler sets locally references game object list active on enter
            activationState.EnterHandler = (time, forced) =>
            {
                if (ActivateOnEnter != null && ActivateOnEnter.Length > 0)
                {
                    foreach (var gameObj in ActivateOnEnter)
                    {
                        if (gameObj == null) continue;
                        gameObj.SetActive(false);
                        gameObj.SetActive(true);
                    }
                }
                return true;
            };

            // Activate handler sets locally referenced game object list inactive on exit
            activationState.ExitHandler = (time, force) =>
            {
                if (DeactivateOnExit != null && DeactivateOnExit.Length > 0)
                {
                    foreach (var gameObj in DeactivateOnExit)
                    {
                        if (gameObj == null) continue;
                        gameObj.SetActive(false);
                    }
                }
                return true;
            };

            // Register with the network state machine
            NetworkStates.States.Add(activationState);
        }

        #endregion Init

        #region Update

        #endregion Update

        #endregion Methods
    }
}
