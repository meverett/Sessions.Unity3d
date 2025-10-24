using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using CymaticLabs.Logging;
using CymaticLabs.Sessions.Core;
using CymaticLabs.xAPI.Unity3d;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// A base class for interactions with sessions objects.
    /// </summary>
    public class SessionsInteractable : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The network entity who the interaction belongs to.
        /// </summary>
        [Tooltip("The network entity who the interaction belongs to.")]
        public SessionsNetworkEntity NetworkEntity;

        /// <summary>
        /// The network state machine to broadcast interaction states to.
        /// </summary>
        [Tooltip("The network state machine to broadcast interaction states to.")]
        public SessionsNetworkStateMachine NetworkStates;

        /// <summary>
        /// Whether or not the interactable object is grabbable.
        /// </summary>
        [Tooltip("Whether or not the interactable object is grabbable.")]
        public bool IsGrabbable = false;

        /// <summary>
        /// Occurs when a new interaction event has started.
        /// </summary>
        public SessionsPointerEvent OnInteractionStarted;

        /// <summary>
        /// Occurs each frame an interaction is occurring.
        /// </summary>
        public SessionsPointerEvent OnInteraction;

        /// <summary>
        /// Occurs when an existing interaction has stopped.
        /// </summary>
        public SessionsPointerEvent OnInteractionStopped;
                
        /// <summary>
        /// When an interaction is entered, this event emits an xAPI event that can be mapped to xAPI statements.
        /// </summary>
        public XapiStatementUnityEvent OnXapiInteractionEntered;

        #endregion Inspector

        #region Fields

        // The current list of pointers interacting with this object
        private List<SessionsPointer> pointers;

        /// <summary>
        /// The internal touch interaction state used.
        /// </summary>
        protected CustomSessionState touchState;

        /// <summary>
        /// The internal primary click interaction state used.
        /// </summary>
        protected CustomSessionState primaryClickState;

        /// <summary>
        /// The internal secondary click interaction state used.
        /// </summary>
        protected CustomSessionState secondaryClickState;

        #endregion Fields

        #region Properties

        /// <summary>
        /// The interaction state of the "Touch" event.
        /// </summary>
        public InteractionStates TouchState { get; private set; }

        /// <summary>
        /// The interaction state of the "Primary" event.
        /// </summary>
        public InteractionStates PrimaryState { get; private set; }

        /// <summary>
        /// The interaction state of the "Secondary" event.
        /// </summary>
        public InteractionStates SecondaryState { get; private set; }

        /// <summary>
        /// The interaction state of the "Primary" grab event.
        /// </summary>
        public InteractionStates PrimaryGrabState { get; private set; }

        /// <summary>
        /// The interaction state of the "Secondary" grab event.
        /// </summary>
        public InteractionStates SecondaryGrabState { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            pointers = new List<SessionsPointer>();
            TouchState = InteractionStates.Stopped;
            PrimaryState = InteractionStates.Stopped;
            SecondaryState = InteractionStates.Stopped;
            PrimaryGrabState = InteractionStates.Stopped;
            SecondaryGrabState = InteractionStates.Stopped;
        }

        protected virtual void Start()
        {
            if (NetworkEntity == null) NetworkEntity = GetComponentInChildren<SessionsNetworkEntity>();
            
            if (NetworkEntity == null)
            {
                CyLog.LogWarnFormat("No NetworkEntity reference was set so SessionsInteractable functionality will be disabled.");
                return;
            }

            // Register RPCs for this interactable entity
            NetworkEntity.RegisterRpcCommand("Interact.Start", RpcInteractionStart);
            NetworkEntity.RegisterRpcCommand("Interact", RpcInteraction);
            NetworkEntity.RegisterRpcCommand("Interact.Stop", RpcInteractionStop);

            // If a network state machine exists, create states for all of the interactions
            if (NetworkStates != null)
            {
                var touchState = new CustomSessionState("Touch", true, false);
                var primaryState = new CustomSessionState("Primary", true, false);
                var secondaryState = new CustomSessionState("Secondary", true, false);
                var primaryGrabState = new CustomSessionState("PrimaryGrab", true, false);
                var secondaryGrabState = new CustomSessionState("SecondaryGrab", true, false);
                NetworkStates.States.Add(touchState);
                NetworkStates.States.Add(primaryState);
                NetworkStates.States.Add(secondaryState);
                NetworkStates.States.Add(primaryGrabState);
                NetworkStates.States.Add(secondaryGrabState);
            }
        }

        #endregion Init

        #region Update

        #endregion Update

        #region RPC

        // RPC that handles a remote interaction start event
        private void RpcInteractionStart(Guid agentId, bool isLocal, string args, float value)
        {
            // Get the pointer for the agent sending the RPC
            var pointer = SessionsPointer.GetPointerByAgentId(agentId);

            if (pointer == null)
            {
                CyLog.LogWarnFormat("No pointer for agent ID: {0}", agentId);
                return;
            }

            // Get the interaction name from the args; if a comma is present, there are more args, juse the first element
            var interaction = args;
            var parsed = interaction.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (interaction.Contains(',')) interaction = parsed[0];

            // Invoke the local vent
            switch (interaction)
            {
                case "Touch":
                    InvokeTouchEnter(pointer);
                    break;

                case "Primary":
                    InvokePrimaryClickDown(pointer);
                    break;

                case "Secondary":
                    InvokeSecondaryClickDown(pointer);
                    break;

                case "PrimaryGrab":
                    InvokePrimaryGrabStart(pointer);
                    // Also extract the object position and rotation and apply them
                    var p1 = new Vector3(float.Parse(parsed[1]), float.Parse(parsed[2]), float.Parse(parsed[3]));
                    var r1 = new Vector3(float.Parse(parsed[4]), float.Parse(parsed[5]), float.Parse(parsed[6]));
                    transform.position = p1;
                    transform.eulerAngles = r1;
                    break;

                case "SecondaryGrab":
                    InvokeSecondaryGrabStart(pointer);
                    // Also extract the object position and rotation and apply them
                    var p2 = new Vector3(float.Parse(parsed[1]), float.Parse(parsed[2]), float.Parse(parsed[3]));
                    var r2 = new Vector3(float.Parse(parsed[4]), float.Parse(parsed[5]), float.Parse(parsed[6]));
                    transform.position = p2;
                    transform.eulerAngles = r2;
                    break;
            }
        }

        // RPC that handles a remote interaction sustain event
        private void RpcInteraction(Guid agentId, bool isLocal, string args, float value)
        {
            // Get the pointer for the agent sending the RPC
            var pointer = SessionsPointer.GetPointerByAgentId(agentId);

            if (pointer == null)
            {
                CyLog.LogWarnFormat("No pointer for agent ID: {0}", agentId);
                return;
            }

            // Get the interaction name from the args; if a comma is present, there are more args, juse the first element
            var interaction = args;
            if (interaction.Contains(',')) interaction = interaction.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)[0];

            // Invoke the local vent
            switch (interaction)
            {
                case "Touch":
                    InvokeTouch(pointer);
                    break;

                case "Primary":
                    InvokePrimaryClick(pointer);
                    break;

                case "Secondary":
                    InvokeSecondaryClick(pointer);
                    break;
            }
        }

        // RPC that handles a remote interaction stop event
        private void RpcInteractionStop(Guid agentId, bool isLocal, string args, float value)
        {
            // Get the pointer for the agent sending the RPC
            var pointer = SessionsPointer.GetPointerByAgentId(agentId);

            if (pointer == null)
            {
                CyLog.LogWarnFormat("No pointer for agent ID: {0}", agentId);
                return;
            }

            // Get the interaction name from the args; if a comma is present, there are more args, juse the first element
            var interaction = args;
            var parsed = interaction.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (interaction.Contains(',')) interaction = parsed[0];

            // Invoke the local vent
            switch (interaction)
            {
                case "Touch":
                    InvokeTouchExit(pointer);
                    break;

                case "Primary":
                    InvokePrimaryClickUp(pointer);
                    break;

                case "Secondary":
                    InvokeSecondaryClickUp(pointer);
                    break;

                case "PrimaryGrab":
                    InvokePrimaryGrabStop(pointer);
                    // Also extract the object position and rotation and apply them
                    var p1 = new Vector3(float.Parse(parsed[1]), float.Parse(parsed[2]), float.Parse(parsed[3]));
                    var r1 = new Vector3(float.Parse(parsed[4]), float.Parse(parsed[5]), float.Parse(parsed[6]));
                    transform.position = p1;
                    transform.eulerAngles = r1;
                    break;

                case "SecondaryGrab":
                    InvokeSecondaryGrabStop(pointer);
                    // Also extract the object position and rotation and apply them
                    var p2 = new Vector3(float.Parse(parsed[1]), float.Parse(parsed[2]), float.Parse(parsed[3]));
                    var r2 = new Vector3(float.Parse(parsed[4]), float.Parse(parsed[5]), float.Parse(parsed[6]));
                    transform.position = p2;
                    transform.eulerAngles = r2;
                    break;
            }
        }

        #endregion RPC

        #region Touch

        #region Enter

        /// <summary>
        /// Handles the beginning of a touch interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        public void HandleTouchEnter(SessionsPointer pointer)
        {
            if (NetworkEntity == null || NetworkEntity.Owner == null) return;
            NetworkEntity.CallRpc(pointer.Owner.Id, "Interact.Start", false, "Touch");
        }

        /// <summary>
        /// Handles the beginning of a touch interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        protected void InvokeTouchEnter(SessionsPointer pointer)
        {
            if (pointer == null) return;
            if (!pointers.Contains(pointer)) pointers.Add(pointer);

            TouchState = InteractionStates.Started;

            OnTouchEnter(pointer);

            // Unity notify
            OnInteractionStarted.Invoke("Touch", pointer);

            if (NetworkStates != null) NetworkStates.EnterState("Touch");

            // xAPI notify
            var actor = pointer.Owner.Name;
            var verb = "started touching";
            var obj = NetworkEntity != null ? NetworkEntity.name : "???";
            OnXapiInteractionEntered.Invoke(actor, verb, obj);
        }

        /// <summary>
        /// Handles the beginning of a touch interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        protected virtual void OnTouchEnter(SessionsPointer pointer)
        {
        }

        #endregion Enter

        #region Update

        /// <summary>
        /// Handles the sustained touch interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        public void HandleTouch(SessionsPointer pointer)
        {
            if (NetworkEntity == null || NetworkEntity.Owner == null) return;
            NetworkEntity.CallRpc(pointer.Owner.Id, "Interact", false, "Touch");
        }

        /// <summary>
        /// Handles the sustained touch interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        protected void InvokeTouch(SessionsPointer pointer)
        {
            if (pointer == null) return;

            OnTouch(pointer);

            // Unity Notify
            OnInteraction.Invoke("Touch", pointer);
        }

        /// <summary>
        /// Handles the sustained touch interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        protected virtual void OnTouch(SessionsPointer pointer)
        {
        }

        #endregion Update

        #region Exit

        /// <summary>
        /// Handles the end of a touch interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction was from.</param>
        public void HandleTouchExit(SessionsPointer pointer)
        {
            if (NetworkEntity == null || NetworkEntity.Owner == null) return;
            NetworkEntity.CallRpc(pointer.Owner.Id, "Interact.Stop", false, "Touch");
        }

        /// <summary>
        /// Handles the end of a touch interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction was from.</param>
        protected void InvokeTouchExit(SessionsPointer pointer)
        {
            if (pointer == null) return;

            // Remove this current pointer from the list of interacting pointers
            if (pointers.Contains(pointer))
            {
                pointers.Remove(pointer);

                // If there are no more pointers interacting with this objcect, consider it an "exit"
                if (pointers.Count == 0)
                {
                    TouchState = InteractionStates.Stopped;

                    OnTouchExit(pointer);

                    // Unity notify
                    OnInteractionStopped.Invoke("Touch", pointer);

                    // xAPI notify
                    var actor = pointer.Owner.Name;
                    var verb = "stopped touching";
                    var obj = NetworkEntity != null ? NetworkEntity.name : "???";
                    OnXapiInteractionEntered.Invoke(actor, verb, obj);

                    if (NetworkStates != null) NetworkStates.ExitState("Touch");
                }
            }
        }

        /// <summary>
        /// Handles the end of a touch interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction was from.</param>
        protected void OnTouchExit(SessionsPointer pointer)
        {
        }

        #endregion Exit

        #endregion Touch

        #region Primary Click

        #region Down

        /// <summary>
        /// Handles the beginning of a primary click interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        public void HandlePrimaryClickDown(SessionsPointer pointer)
        {
            if (NetworkEntity == null || NetworkEntity.Owner == null) return;
            NetworkEntity.CallRpc(pointer.Owner.Id, "Interact.Start", false, "Primary");

            // If this interactable is also being touched, trigger a grab start
            if (IsGrabbable && PrimaryGrabState != InteractionStates.Started && TouchState == InteractionStates.Started)
            {
                var pos = transform.position;
                var rot = transform.eulerAngles;
                var args = string.Format("PrimaryGrab,{0},{1},{2},{3},{4},{5}", pos.x, pos.y, pos.z, rot.x, rot.y, rot.z);
                NetworkEntity.CallRpc(pointer.Owner.Id, "Interact.Start", false, args);
            }
        }

        /// <summary>
        /// Handles the beginning of a primary click interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        protected void InvokePrimaryClickDown(SessionsPointer pointer)
        {
            if (pointer == null) return;
            if (!pointers.Contains(pointer)) pointers.Add(pointer);

            PrimaryState = InteractionStates.Started;

            OnPrimaryClickDown(pointer);

            // Unity Notify
            OnInteractionStarted.Invoke("Primary", pointer);

            if (NetworkStates != null) NetworkStates.EnterState("Primary");

            // xAPI notify
            var actor = pointer.Owner.Name;
            var verb = "primary pressed";
            var obj = NetworkEntity != null ? NetworkEntity.name : "???";
            OnXapiInteractionEntered.Invoke(actor, verb, obj);
        }

        /// <summary>
        /// Handles the beginning of a primary click interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        protected virtual void OnPrimaryClickDown(SessionsPointer pointer)
        {
        }

        #endregion Down

        #region Sustained

        /// <summary>
        /// Handles the sustained primary click interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        public void HandlePrimaryClick(SessionsPointer pointer)
        {
            if (NetworkEntity == null || NetworkEntity.Owner == null) return;
            NetworkEntity.CallRpc(pointer.Owner.Id, "Interact", false, "Primary");
        }

        /// <summary>
        /// Handles the sustained primary click interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        protected void InvokePrimaryClick(SessionsPointer pointer)
        {
            if (pointer == null) return;
            OnPrimaryClick(pointer);

            // Unity Notify
            OnInteraction.Invoke("Primary", pointer);
        }

        /// <summary>
        /// Handles the sustained primary click interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        protected virtual void OnPrimaryClick(SessionsPointer pointer)
        {
        }

        #endregion Sustained

        #region Up

        /// <summary>
        /// Handles the end of a primary click interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction was from.</param>
        public void HandlePrimaryClickUp(SessionsPointer pointer)
        {
            if (NetworkEntity == null || NetworkEntity.Owner == null) return;
            NetworkEntity.CallRpc(pointer.Owner.Id, "Interact.Stop", false, "Primary");

            // If this interactable is also being touched, trigger a grab stop
            if (IsGrabbable && PrimaryGrabState != InteractionStates.Stopped)
            {
                var pos = transform.position;
                var rot = transform.eulerAngles;
                var args = string.Format("PrimaryGrab,{0},{1},{2},{3},{4},{5}", pos.x, pos.y, pos.z, rot.x, rot.y, rot.z);
                NetworkEntity.CallRpc(pointer.Owner.Id, "Interact.Stop", false, args);
            }
        }

        /// <summary>
        /// Handles the end of a primary click interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction was from.</param>
        protected void InvokePrimaryClickUp(SessionsPointer pointer)
        {
            if (pointer == null) return;

            PrimaryState = InteractionStates.Stopped;

            OnPrimaryClickUp(pointer);

            // Unity Notify
            OnInteractionStopped.Invoke("Primary", pointer);

            if (NetworkStates != null) NetworkStates.ExitState("Primary");

            // xAPI notify
            var actor = pointer.Owner.Name;
            var verb = "primary released";
            var obj = NetworkEntity != null ? NetworkEntity.name : "???";
            OnXapiInteractionEntered.Invoke(actor, verb, obj);
        }

        /// <summary>
        /// Handles the end of a primary click interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction was from.</param>
        public void OnPrimaryClickUp(SessionsPointer pointer)
        {
        }

        #endregion Up

        #endregion Primary Click

        #region Secondary Click

        #region Down

        /// <summary>
        /// Handles the beginning of a secondary click interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        public void HandleSecondaryClickDown(SessionsPointer pointer)
        {
            if (NetworkEntity == null || NetworkEntity.Owner == null) return;
            NetworkEntity.CallRpc(pointer.Owner.Id, "Interact.Start", false, "Secondary");

            // If this interactable is also being touched, trigger a grab start
            if (IsGrabbable && SecondaryGrabState != InteractionStates.Started && TouchState == InteractionStates.Started)
            {
                var pos = transform.position;
                var rot = transform.eulerAngles;
                var args = string.Format("SecondaryGrab,{0},{1},{2},{3},{4},{5}", pos.x, pos.y, pos.z, rot.x, rot.y, rot.z);
                NetworkEntity.CallRpc(pointer.Owner.Id, "Interact.Start", false, args);
            }
        }

        /// <summary>
        /// Handles the beginning of a secondary click interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        protected void InvokeSecondaryClickDown(SessionsPointer pointer)
        {
            if (pointer == null) return;
            if (!pointers.Contains(pointer)) pointers.Add(pointer);

            SecondaryState = InteractionStates.Started;

            OnSecondaryClickDown(pointer);

            // Unity Notify
            OnInteractionStarted.Invoke("Secondary", pointer);

            if (NetworkStates != null) NetworkStates.EnterState("Secondary");

            // xAPI notify
            var actor = pointer.Owner.Name;
            var verb = "secondary pressed";
            var obj = NetworkEntity != null ? NetworkEntity.name : "???";
            OnXapiInteractionEntered.Invoke(actor, verb, obj);
        }

        /// <summary>
        /// Handles the beginning of a secondary click interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        protected virtual void OnSecondaryClickDown(SessionsPointer pointer)
        {
        }

        #endregion Down

        #region Sustained

        /// <summary>
        /// Handles the sustained secondary click interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        public void HandleSecondaryClick(SessionsPointer pointer)
        {
            if (NetworkEntity == null || NetworkEntity.Owner == null) return;
            NetworkEntity.CallRpc(pointer.Owner.Id, "Interact", false, "Secondary");
        }

        /// <summary>
        /// Handles the sustained secondary click interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        protected void InvokeSecondaryClick(SessionsPointer pointer)
        {
            if (pointer == null) return;
            OnSecondaryClick(pointer);

            // Unity Notify
            OnInteraction.Invoke("Secondary", pointer);
        }

        /// <summary>
        /// Handles the sustained secondary click interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        protected virtual void OnSecondaryClick(SessionsPointer pointer)
        {
        }

        #endregion Sustained

        #region Up

        /// <summary>
        /// Handles the end of a secondary click interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction was from.</param>
        public void HandleSecondaryClickUp(SessionsPointer pointer)
        {
            if (NetworkEntity == null || NetworkEntity.Owner == null) return;
            NetworkEntity.CallRpc(pointer.Owner.Id, "Interact.Stop", false, "Secondary");

            // If this interactable is also being touched, trigger a grab stop
            if (IsGrabbable && SecondaryGrabState != InteractionStates.Stopped)
            {
                var pos = transform.position;
                var rot = transform.eulerAngles;
                var args = string.Format("SecondaryGrab,{0},{1},{2},{3},{4},{5}", pos.x, pos.y, pos.z, rot.x, rot.y, rot.z);
                NetworkEntity.CallRpc(pointer.Owner.Id, "Interact.Stop", false, args);
            }
        }

        /// <summary>
        /// Handles the end of a secondary click interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction was from.</param>
        protected void InvokeSecondaryClickUp(SessionsPointer pointer)
        {
            if (pointer == null) return;

            SecondaryState = InteractionStates.Stopped;

            OnSecondaryClickUp(pointer);

            // Unity Notify
            OnInteractionStopped.Invoke("Secondary", pointer);

            if (NetworkStates != null) NetworkStates.ExitState("Secondary");

            // xAPI notify
            var actor = pointer.Owner.Name;
            var verb = "secondary released";
            var obj = NetworkEntity != null ? NetworkEntity.name : "???";
            OnXapiInteractionEntered.Invoke(actor, verb, obj);
        }

        /// <summary>
        /// Handles the end of a secondary click interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction was from.</param>
        public void OnSecondaryClickUp(SessionsPointer pointer)
        {
        }

        #endregion Up

        #endregion Secondary Click

        #region Primary Grab

        #region Start

        /// <summary>
        /// Handles the beginning of a primary grab interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        public void HandlePrimaryGrabStart(SessionsPointer pointer)
        {
            if (NetworkEntity == null || NetworkEntity.Owner == null) return;
            NetworkEntity.CallRpc(pointer.Owner.Id, "Interact.Start", false, "PrimaryGrab");
        }

        /// <summary>
        /// Handles the beginning of a primary grab interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        protected void InvokePrimaryGrabStart(SessionsPointer pointer)
        {
            if (pointer == null) return;
            if (!pointers.Contains(pointer)) pointers.Add(pointer);

            PrimaryGrabState = InteractionStates.Started;

            OnPrimaryGrabStart(pointer);

            // Unity Notify
            OnInteractionStarted.Invoke("PrimaryGrab", pointer);

            if (NetworkStates != null) NetworkStates.EnterState("PrimaryGrab");

            // xAPI notify
            var actor = pointer.Owner.Name;
            var verb = "primary grab started";
            var obj = NetworkEntity != null ? NetworkEntity.name : "???";
            OnXapiInteractionEntered.Invoke(actor, verb, obj);
        }

        /// <summary>
        /// Handles the beginning of a primary grab interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        protected virtual void OnPrimaryGrabStart(SessionsPointer pointer)
        {
        }

        #endregion Start

        #region Stop

        /// <summary>
        /// Handles the ending of a primary grab interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        public void HandlePrimaryGrabStop(SessionsPointer pointer)
        {
            if (NetworkEntity == null || NetworkEntity.Owner == null) return;
            NetworkEntity.CallRpc(pointer.Owner.Id, "Interact.Stop", false, "PrimaryGrab");
        }

        /// <summary>
        /// Handles the ending of a primary grab interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        protected void InvokePrimaryGrabStop(SessionsPointer pointer)
        {
            if (pointer == null) return;
            if (!pointers.Contains(pointer)) pointers.Add(pointer);

            PrimaryGrabState = InteractionStates.Stopped;

            OnPrimaryGrabStop(pointer);

            // Unity Notify
            OnInteractionStopped.Invoke("PrimaryGrab", pointer);

            if (NetworkStates != null) NetworkStates.ExitState("PrimaryGrab");

            // xAPI notify
            var actor = pointer.Owner.Name;
            var verb = "primary grab stopped";
            var obj = NetworkEntity != null ? NetworkEntity.name : "???";
            OnXapiInteractionEntered.Invoke(actor, verb, obj);
        }

        /// <summary>
        /// Handles the ending of a primary grab interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        protected virtual void OnPrimaryGrabStop(SessionsPointer pointer)
        {
        }

        #endregion Stop

        #endregion Primary Grab

        #region Secondary Grab

        #region Start

        /// <summary>
        /// Handles the beginning of a secondary grab interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        public void HandleSecondaryGrabStart(SessionsPointer pointer)
        {
            if (NetworkEntity == null || NetworkEntity.Owner == null) return;
            NetworkEntity.CallRpc(pointer.Owner.Id, "Interact.Start", false, "SecondaryGrab");
        }

        /// <summary>
        /// Handles the beginning of a secondary grab interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        protected void InvokeSecondaryGrabStart(SessionsPointer pointer)
        {
            if (pointer == null) return;
            if (!pointers.Contains(pointer)) pointers.Add(pointer);

            SecondaryGrabState = InteractionStates.Started;

            OnSecondaryGrabStart(pointer);

            // Unity Notify
            OnInteractionStarted.Invoke("SecondaryGrab", pointer);

            if (NetworkStates != null) NetworkStates.EnterState("SecondaryGrab");

            // xAPI notify
            var actor = pointer.Owner.Name;
            var verb = "secondary grab started";
            var obj = NetworkEntity != null ? NetworkEntity.name : "???";
            OnXapiInteractionEntered.Invoke(actor, verb, obj);
        }

        /// <summary>
        /// Handles the beginning of a secondary grab interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        protected virtual void OnSecondaryGrabStart(SessionsPointer pointer)
        {
        }

        #endregion Start

        #region Stop

        /// <summary>
        /// Handles the ending of a secondary grab interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        public void HandleSecondaryGrabStop(SessionsPointer pointer)
        {
            if (NetworkEntity == null || NetworkEntity.Owner == null) return;
            NetworkEntity.CallRpc(pointer.Owner.Id, "Interact.Stop", false, "SecondaryGrab");
        }

        /// <summary>
        /// Handles the ending of a secondary grab interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        protected void InvokeSecondaryGrabStop(SessionsPointer pointer)
        {
            if (pointer == null) return;
            if (!pointers.Contains(pointer)) pointers.Add(pointer);

            SecondaryGrabState = InteractionStates.Stopped;

            OnSecondaryGrabStop(pointer);

            // Unity Notify
            OnInteractionStopped.Invoke("SecondaryGrab", pointer);

            if (NetworkStates != null) NetworkStates.ExitState("SecondaryGrab");

            // xAPI notify
            var actor = pointer.Owner.Name;
            var verb = "secondary grab stopped";
            var obj = NetworkEntity != null ? NetworkEntity.name : "???";
            OnXapiInteractionEntered.Invoke(actor, verb, obj);
        }

        /// <summary>
        /// Handles the ending of a secondary grab interaction from a pointer.
        /// </summary>
        /// <param name="pointer">The pointer the interaction is from.</param>
        protected virtual void OnSecondaryGrabStop(SessionsPointer pointer)
        {
        }

        #endregion Stop

        #endregion Secondary Grab

        #region Agents

        /// <summary>
        /// Returns a list of currently interacting session agents.
        /// </summary>
        /// <returns>The list of agents currently interacting. An empty array if there are no current interactions.</returns>
        public SessionAgent[] GetInteractingAgents()
        {
            if (pointers.Count == 0) return new SessionAgent[0];
            return (from p in pointers where p != null && p.Owner != null select p.Owner).ToArray();
        }

        #endregion Agents

        #endregion Methods
    }

    /// <summary>
    /// Different types of interaction states.
    /// </summary>
    public enum InteractionStates
    {
        /// <summary>
        /// The interaction stopped.
        /// </summary>
        Stopped,

        /// <summary>
        /// The interaction has started.
        /// </summary>
        Started,
    }
}
