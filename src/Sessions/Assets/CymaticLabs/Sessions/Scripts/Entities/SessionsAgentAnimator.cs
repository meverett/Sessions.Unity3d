using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using CymaticLabs.Logging;
using CymaticLabs.Sessions.Core;
using CymaticLabs.Sessions.Unity3d;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Animates an agent's avatar automatically.
    /// </summary>
    public class SessionsAgentAnimator : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The network entity that represents the agent/user.
        /// </summary>
        public SessionsNetworkEntity NetworkEntity;

        /// <summary>
        /// The agent transform to target.
        /// </summary>
        public Transform TargetTransform;

        /// <summary>
        /// The target animator to manipulate.
        /// </summary>
        public Animator TargetAnimator;
       
        /// <summary>
        /// The distance between which agents will greet.
        /// </summary>
        [Header("Greeting")]
        public float GreetingDistance = 5f;

        /// <summary>
        /// The animator tigger/state name ot use to trigger the greeting.
        /// </summary>
        public string GreetingTriggerName = "Meditate";

        /// <summary>
        /// The agent's left hand/controller.
        /// </summary>
        [Header("Hands")]
        //public RootMotion.FinalIK.CCDIK LeftHandIK;

        /// <summary>
        /// The agent's right hand/controller.
        /// </summary>
        //public RootMotion.FinalIK.CCDIK RightHandIK;

        /// <summary>
        /// The default left hand IK target.
        /// </summary>
        public Transform DefaultLeftHandTarget;

        /// <summary>
        /// The default right hand IK target.
        /// </summary>
        public Transform DefaultRightHandTarget;

        /// <summary>
        /// The primary IK target for the left hand.
        /// </summary>
        public Transform PrimaryLeftHandTarget;

        /// <summary>
        /// The primary IK target for the left hand.
        /// </summary>
        public Transform PrimaryRightHandTarget;

        /// <summary>
        /// Whether or not to automatically control agent color.
        /// </summary>
        [Header("Appearance")]
        public bool AutoColor = true;

        /// <summary>
        /// A list of available agent colors to assign.
        /// </summary>
        public Color[] AgentColors;

        /// <summary>
        /// Whether or not to use a velocity decay to smooth out frames with missed velocity data.
        /// </summary>
        [Header("Movement")]
        [Tooltip("Whether or not to use a velocity decay to smooth out frames with missed velocity data.")]
        public bool UseVelocityDecay = false;

        /// <summary>
        /// The amount of velocity decay to apply.
        /// </summary>
        [Tooltip("The amount of velocity decay to apply.")]
        public float VelocityDecay = 10f;

        /// <summary>
        /// Animation triggers based on dynamic velocity.
        /// </summary>
        public VelocityAnimationTrigger[] VelocityTriggers;

        /// <summary>
        /// A list of game objects of which the agent will activate one from the list based on their agent index.
        /// </summary>
        [Tooltip("A list of game objects of which the agent will activate one from the list based on their agent index.")]
        public GameObject[] OwnedActivation;

        #endregion Inspector

        #region Fields

        // Last recorded agent position
        private Vector3 lastPos;

        // Staticle singleton list of agent animators
        private static List<SessionsAgentAnimator> agents = new List<SessionsAgentAnimator>();

        // The index of the current agent color
        private int colorIndex = -1;

        // The last recorded agent color
        private Color lastColor = Color.black;

        // The agent's left hand pointer
        private SessionsPointer leftPointer;

        // The agent's right hand pointer
        private SessionsPointer rightPointer;

        // The agent's local velocity
        private Vector3 localVel;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets the agent's color index.
        /// </summary>
        public int AgentColorIndex { get { return colorIndex; } }

        #endregion Properties

        #region Methods

        #region Init

        private void Awake()
        {
            //agents.Clear();
        }

        private void Start()
        {
            colorIndex = agents.Count;
            if (OwnedActivation != null && OwnedActivation.Length > colorIndex) OwnedActivation[colorIndex].SetActive(true);
            if (AgentColors.Length > 0 && colorIndex >= AgentColors.Length) colorIndex = 0; // wrap index
            agents.Add(this);
            if (NetworkEntity == null) NetworkEntity = GetComponentInChildren<SessionsNetworkEntity>();
            if (TargetTransform == null) TargetTransform = transform;
            if (TargetAnimator == null) TargetAnimator = GetComponentInChildren<Animator>();
            if (AutoColor) ApplyColors();
        }

        #endregion Init

        #region Clean Up

        private void OnDestroy()
        {
            agents.Remove(this);
        }

        #endregion Clean Up

        #region Update

        private void Update()
        {
            // Update agent color
            var color = GetColor();
            var colorChanged = color != lastColor;
            lastColor = color;

            // Get velocity
            var pos = TargetTransform.position;
            var velocity = pos - lastPos;
            lastPos = pos;

            // Get the localized velocity
            var local = TargetTransform.InverseTransformDirection(velocity);

            // If using velocity decay, apply it
            if (UseVelocityDecay)
            {
                // If there was previously velocity present at the last frame, but there is none this frame, decay...
                if (localVel.magnitude > 0 && local.magnitude == 0)
                {
                    localVel -= (localVel * (Time.deltaTime * VelocityDecay));
                    if (localVel.magnitude < 0.0001f) localVel = Vector3.zero;
                }
                else
                {
                    // Get the localized velocity
                    localVel = TargetTransform.InverseTransformDirection(velocity);
                }
            }
            else
            {
                // Get the localized velocity
                localVel = TargetTransform.InverseTransformDirection(velocity);
            }

            #region Update Movement Animation

            //Debug.LogFormat("{0:0.0000},{1:0.0000},{2:0.0000} => {3}", localVel.x, localVel.y, localVel.z, velocity.magnitude);

            if (TargetAnimator != null)
            {
                if (VelocityTriggers != null && VelocityTriggers.Length > 0)
                {
                    foreach (var vt in VelocityTriggers)
                    {
                        // Skip if we have no name to trigger animation states by...
                        if (string.IsNullOrEmpty(vt.TriggerName)) continue;

                        // Start out assuming a trigger...
                        var isTriggered = false;

                        // Check velocity thresholds on all axis that are non-zero

                        // X axis
                        if (vt.MinVelocity.x > 0 && localVel.x > vt.MinVelocity.x && (vt.MaxVelocity.x == 0 || localVel.x < vt.MaxVelocity.x)) isTriggered = true;
                        else if (vt.MinVelocity.x < 0 && localVel.x < vt.MinVelocity.x && (vt.MaxVelocity.x == 0 || localVel.x > vt.MaxVelocity.x)) isTriggered = true;

                        // Y axis
                        if (vt.MinVelocity.y > 0 && localVel.y > vt.MinVelocity.y && (vt.MaxVelocity.y == 0 || localVel.y < vt.MaxVelocity.y)) isTriggered = true;
                        else if (vt.MinVelocity.y < 0 && localVel.y < vt.MinVelocity.y && (vt.MaxVelocity.y == 0 || localVel.y > vt.MaxVelocity.y)) isTriggered = true;

                        // Z axis
                        if (vt.MinVelocity.z > 0 && localVel.z > vt.MinVelocity.z && (vt.MaxVelocity.z == 0 || localVel.z < vt.MaxVelocity.z)) isTriggered = true;
                        else if (vt.MinVelocity.z < 0 && localVel.z < vt.MinVelocity.z && (vt.MaxVelocity.z == 0 || localVel.z > vt.MaxVelocity.z)) isTriggered = true;

                        // Pick the correct animator...
                        var animator = vt.Animator != null ? vt.Animator : TargetAnimator;

                        // Update Animator accordingly
                        if (vt.Type == AnimationTriggerTypes.Trigger && isTriggered)
                        {
                            animator.SetTrigger(vt.TriggerName);
                        }
                        else if (vt.Type == AnimationTriggerTypes.Bool)
                        {
                            var current = animator.GetBool(vt.TriggerName);
                            if (current != isTriggered) animator.SetBool(vt.TriggerName, isTriggered);
                        }
                    }
                }
            }

            #endregion Update Movement Animation

            #region Update Pointers

            // If no hand targets have been found yet, look for them
            if (leftPointer == null && rightPointer == null && transform.parent != null)
            {
                // Search the parent for the matching pointer(s)
                var pointers = transform.parent.GetComponentsInChildren<SessionsPointer>();
                if (pointers == null) pointers = transform.GetComponentsInChildren<SessionsPointer>();

                if (pointers != null)
                {
                    foreach (var pointer in pointers)
                    {
                        if (pointer.tag == "VR Input Left" && pointer.gameObject.activeInHierarchy)
                        {
                            leftPointer = pointer;
                            UpdatePointerColor();
                        }
                        else if (pointer.tag == "VR Input Right" && pointer.gameObject.activeInHierarchy)
                        {
                            rightPointer = pointer;
                            UpdatePointerColor();
                        }
                    }
                }
            }
            // Otherwise update the pointer's appearance
            else if (colorChanged)
            {
                UpdatePointerColor();
            }

            #endregion Update Pointers

            #region Update Hand Targets

            //// If no hand targets have been found yet, look for them
            //if (PrimaryLeftHandTarget == null && PrimaryRightHandTarget == null)
            //{
            //    // Search the parent for the matching pointer(s)
            //    var pointers = transform.parent.GetComponentsInChildren<SessionsPointer>();

            //    if (pointers != null)
            //    {
            //        foreach (var pointer in pointers)
            //        {
            //            if (pointer.tag == "VR Input Left" && pointer.gameObject.activeInHierarchy)
            //            {
            //                PrimaryLeftHandTarget = pointer.transform.parent;
            //                PrimaryRightHandTarget = pointer.transform.parent;
            //            }
            //            else if (pointer.tag == "VR Input Right" && pointer.gameObject.activeInHierarchy)
            //            {
            //                PrimaryLeftHandTarget = pointer.transform.parent;
            //                PrimaryRightHandTarget = pointer.transform.parent;
            //            }
            //        }
            //    }
            //}

            //if (LeftHandIK != null && LeftHandIK.gameObject.activeInHierarchy)
            //{
            //    if (PrimaryLeftHandTarget == null) LeftHandIK.solver.target = DefaultLeftHandTarget;
            //    else LeftHandIK.solver.target = PrimaryLeftHandTarget;
            //}

            //if (RightHandIK != null && RightHandIK.gameObject.activeInHierarchy)
            //{
            //    if (PrimaryRightHandTarget == null) RightHandIK.solver.target = DefaultRightHandTarget;
            //    else RightHandIK.solver.target = PrimaryRightHandTarget;
            //}

            #endregion Update Hand Targets

            #region Greet Agents

            var entityManager = SessionsNetworkEntityManager.Current;
            if (entityManager != null) // only let the first agent runs this once
            {
                var list = agents.ToArray();

                // Reset all triggers again to start
                foreach (var a in list)
                {
                    if (a == null || a.TargetAnimator == null) continue;
                    a.TargetAnimator.SetBool(GreetingTriggerName, false);
                }

                foreach (var agent in agents)
                {
                    // Ignore self
                    if (agent == this || agent == null || TargetAnimator == null || agent.TargetAnimator == null) continue;

                    // If two agents are within range, trigger them to both greet
                    if (Vector3.Distance(agent.NetworkEntity.transform.position, NetworkEntity.transform.position) <= GreetingDistance)
                    {
                        TargetAnimator.SetBool(GreetingTriggerName, true);
                        agent.TargetAnimator.SetBool(GreetingTriggerName, true);
                    }
                }
            }

            #endregion Greet Agents
        }

        #endregion Update

        #region Colors

        /// <summary>
        /// Gets the current agent color.
        /// </summary>
        /// <returns>The agent's color.</returns>
        public Color GetColor()
        {
            return AgentColors != null && AgentColors.Length > 0 ? AgentColors[colorIndex] : Color.magenta;
        }

        /// <summary>
        /// Swaps the materials on the target renderers.
        /// </summary>
        public void ApplyColors()
        {
            if (AgentColors == null || AgentColors.Length == 0) return;

            if (colorIndex >= AgentColors.Length) colorIndex = 0; // wrap
            var color = AgentColors[colorIndex];

            // Update pointer
            var pointer = GetComponentInChildren<SessionsPointer>();
            if (pointer != null && pointer.LineRenderer != null)
                pointer.LineRenderer.startColor = pointer.LineRenderer.endColor = color;

            // Update particles
            var particles = transform.Find("Particles");
            if (particles != null)
            {
                var ps = particles.GetComponent<ParticleSystem>();
                var main = ps.main;
                main.startColor = color;
            }

            UpdatePointerColor();
            UpdateVoiceIndicatorColor();
        }

        // Updates the pointer colors
        private void UpdatePointerColor()
        {
            if (leftPointer == null && rightPointer == null) return;

            var color = GetColor();

            // If this is the current user's pointer and this is VR/XR, color the system input pointer
            if (leftPointer != null && leftPointer.LineRenderer != null)
            {
                // Pointer line render should be consistent solid color
                var lr = leftPointer.LineRenderer;
                lr.startColor = lr.endColor = color;
                lr.material.SetColor("_Color", color);

                // Change particles
                var ps = leftPointer.GetComponent<ParticleSystem>();
                if (ps == null && leftPointer.transform.parent != null) ps = leftPointer.transform.GetComponentInParent<ParticleSystem>();

                if (ps != null)
                {
                    var main = ps.main;
                    var c = color;
                    var sc = main.startColor;
                    sc.color = color;
                    main.startColor = sc;
                }
            }

            if (rightPointer != null && rightPointer.LineRenderer != null)
            {
                // Pointer line render should be consistent solid color
                var lr = rightPointer.LineRenderer;
                lr.startColor = lr.endColor = color;
                lr.material.SetColor("_Color", color);

                // Change particles
                var ps = rightPointer.GetComponent<ParticleSystem>();
                if (ps == null && rightPointer.transform.parent != null) ps = rightPointer.transform.GetComponentInParent<ParticleSystem>();

                if (ps != null)
                {
                    var main = ps.main;
                    var c = color;
                    var sc = main.startColor;
                    sc.color = color;
                    main.startColor = sc;
                }
            }

            if (NetworkEntity.IsMine)
            {
                var eventSys = UnityEngine.EventSystems.EventSystem.current;
                if (eventSys != null)
                {
                    var lr = eventSys.GetComponentInChildren<LineRenderer>();
                    if (lr != null)
                    {
                        var a = lr.startColor.a;
                        var c = new Color(color.r, color.g, color.b, a);
                        lr.startColor = lr.endColor = c;

                        a = lr.material.GetColor("_Color").a;
                        c = new Color(color.r, color.g, color.b, a);
                        lr.material.SetColor("_Color", c);
                    }
                }
            }
        }

        // Updates the agent's vocal indicator color
        private void UpdateVoiceIndicatorColor()
        {
            // Search the parent
            if (transform.parent == null) return;
            var voice = transform.parent.GetComponentInChildren<SessionsPositionalVoice>();
            if (voice == null || voice.VoiceIndicator == null) return;
            voice.VoiceIndicator.color = GetColor();
        }

        #endregion Colors

        #endregion Methods
    }

    /// <summary>
    /// Used to connect an animation trigger to dynamically detecting object velocity.
    /// </summary>
    [Serializable]
    public class VelocityAnimationTrigger
    {
        /// <summary>
        /// Optional name of the animation trigger.
        /// </summary>
        [Tooltip("Optional name of the animation trigger.")]
        public string Name;

        /// <summary>
        /// The animator to control.
        /// </summary>
        [Tooltip("The animator to control.")]
        public Animator Animator;

        /// <summary>
        /// The name of the <see cref="Animator"/> trigger to use to set the animator state.
        /// </summary>
        [Tooltip("The name of the Animator trigger to use to set the animator state.")]
        public string TriggerName;

        /// <summary>
        /// The type of <see cref="Animator"/> trigger to invoke when speed thresholds are met.
        /// </summary>
        [Tooltip("The type of trigger to invoke when speed thresholds are met.")]
        public AnimationTriggerTypes Type = AnimationTriggerTypes.Trigger;

        /// <summary>
        /// The minimum speed/velocity magnitude that will trigger the animation.
        /// </summary>
        [Tooltip("The minimum local velocity that will trigger the animation.")]
        public Vector3 MinVelocity = Vector3.one;

        /// <summary>
        /// The minimum speed/velocity magnitude that will trigger the animation.
        /// </summary>
        [Tooltip("The maximum local velocity that will trigger the animation.")]
        public Vector3 MaxVelocity = Vector3.zero;
    }

    /// <summary>
    /// Different types of <see cref="Animator"/> animation triggers.
    /// </summary>
    public enum AnimationTriggerTypes
    {
        /// <summary>
        /// An <see cref="Animator"/> trigger.
        /// </summary>
        Trigger,

        /// <summary>
        /// An <see cref="Animator"/> boolean value.
        /// </summary>
        Bool,
    }
}
