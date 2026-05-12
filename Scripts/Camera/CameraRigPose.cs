using UnityEngine;

namespace Game.CameraSystem
{
    /// <summary>
    /// Pose objetivo del rig de cámara.
    /// </summary>
    public readonly struct CameraRigPose
    {
        /// <summary>
        /// Posición objetivo de la cámara.
        /// </summary>
        public Vector3 Position { get; }

        /// <summary>
        /// Rotación objetivo de la cámara.
        /// </summary>
        public Quaternion Rotation { get; }

        /// <summary>
        /// Crea una nueva pose de cámara.
        /// </summary>
        public CameraRigPose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }
    }
}