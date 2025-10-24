using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CymaticLabs.Sessions.Core;

namespace CymaticLabs.Sessions.Unity3d
{
    /// <summary>
    /// Synchronizes a transform over a network session.
    /// </summary>
    public class SessionsNetworkTransform : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// The network entity this transform belongs to.
        /// </summary>
        [Header("Registration")]
        [Tooltip("The network entity this transform belongs to.")]
        public SessionsNetworkEntity NetworkEntity;

        /// <summary>
        /// The network name of the tracked object as part of its parent entity.
        /// </summary>
        [Tooltip("The network name of the tracked object as part of its parent entity.")]
        public string NetworkName = "Object1";

        /// <summary>
        /// Whether or not to sync the object's position.
        /// </summary>
        [Header("Position")]
        [Tooltip("Whether or not to sync the object's position.")]
        public bool SyncPosition = true;

        /// <summary>
        /// The position delta threshold at which updates will be sent out over the network.
        /// </summary>
        [Tooltip("The position delta threshold at which updates will be sent out over the network.")]
        public float PositionThreshold = 0.1f;

        /// <summary>
        /// Whether or not to sync the object's rotation.
        /// </summary>
        [Header("Rotation")]
        [Tooltip("Whether or not to sync the object's rotation.")]
        public bool SyncRotation = true;

        /// <summary>
        /// The rotation delta threshold at which updates will be sent out over the network.
        /// </summary>
        [Tooltip("The rotation delta threshold at which updates will be sent out over the network.")]
        public float RotationThreshold = 0.1f;

        /// <summary>
        /// Whether or not to sync the object's scale.
        /// </summary>
        [Header("Scale")]
        [Tooltip("Whether or not to sync the object's scale.")]
        public bool SyncScale = true;

        /// <summary>
        /// The scale delta threshold at which updates will be sent out over the network.
        /// </summary>
        [Tooltip("The scale delta threshold at which updates will be sent out over the network.")]
        public float ScaleThreshold = 0.1f;

        #endregion Inspector

        #region Fields

        // A game time stamp used to track the timing on the remote host of a sync event
        private float lastSyncTime = 0;

        // Used for delta compression (only send data that has changed)
        //private Vector3 lastPosition;
        //private Vector3 lastRotation;
        //private Vector3 lastScale;
        //private float positionDelta = 0;
        //private float rotationDelta = 0;
        //private float scaleDelta = 0;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Whether or not this instance belongs to the current network peer.
        /// </summary>
        public bool IsMine { get { return NetworkEntity != null && NetworkEntity.IsMine; } }

        #endregion Properties

        #region Methods

        #region Init

        #endregion Init

        #region Update

        private void Update()
        {
            // Check to see how much the transform has changed since last frame
            //var t = transform;
            //var pos = t.position;
            //var rot = t.eulerAngles;
            //var scl = t.localScale;

            // Get the deltas from last frame
            //positionDelta = (pos - lastPosition).magnitude;
            //rotationDelta = (rot - lastRotation).magnitude;
            //scaleDelta =    (scl - lastScale).magnitude;

            // Update to the latest values
            //lastPosition =  pos;
            //lastRotation =  rot;
            //lastScale =     scl;
        }

        #endregion Update

        #region Transform

        /// <summary>
        /// Updates transform information from a network message.
        /// </summary>
        public void BindToTransformMessage(TransformMessage message)
        {
            if (message == null) throw new System.ArgumentException("message");

            if (message.NetworkEntityId != NetworkEntity.Id) throw new System.Exception("Wrong instance ID: " + message.Id.ToString());

            // If this update is older than the most recent we've received, ignore it
            // TODO deal with roll over
            if (message.Value < lastSyncTime) return;

            // Otherwise this is the newest time stamp
            lastSyncTime = message.Value;

            var t = transform;

            if (SyncPosition && message.Position != null)
            {
                var pos = message.Position.Value;
                t.position = new Vector3(pos.X, pos.Y, pos.Z);
            }

            if (SyncRotation && message.Rotation != null)
            {
                var rot = message.Rotation.Value;
                t.eulerAngles = new Vector3(rot.X, rot.Y, rot.Z);
            }

            if (SyncScale && message.Scale != null)
            {
                var scl = message.Scale.Value;
                t.localScale = new Vector3(scl.X, scl.Y, scl.Z);
            }
        }

        /// <summary>
        /// Converts the current transform information into a network message.
        /// </summary>
        /// <returns></returns>
        public TransformMessage AsTransformMessage()
        {
            var msg = new TransformMessage(MessageFlags.None, NetworkName, Time.time); // use the float value of the message as a time stamp

            msg.NetworkEntityId = NetworkEntity.Id;

            var t = transform;

            if (SyncPosition)// && (PositionThreshold <= 0 || positionDelta >= PositionThreshold))
            {
                var pos = t.position;
                msg.Position = new SessionsVector3(pos.x, pos.y, pos.z);
            }

            if (SyncRotation)// && (RotationThreshold <= 0 || rotationDelta >= RotationThreshold))
            {
                var rot = t.eulerAngles;
                msg.Rotation = new SessionsVector3(rot.x, rot.y, rot.z);
            }

            if (SyncScale)// && (ScaleThreshold <= 0 || scaleDelta >= ScaleThreshold))
            {
                var scl = t.localScale;
                msg.Scale = new SessionsVector3(scl.x, scl.y, scl.z);
            }

            return msg;
        }

        #endregion Transform

        #endregion Methods       
    }
}