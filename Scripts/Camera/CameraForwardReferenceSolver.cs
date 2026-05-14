using UnityEngine;

namespace Game.CameraSystem
{
    /// <summary>
    /// Resuelve la dirección horizontal suavizada de referencia para la cámara.
    /// Usa suavizado en giros normales y catch-up agresivo en giros bruscos para evitar que la cámara quede atrasada.
    /// </summary>
    public sealed class CameraForwardReferenceSolver
    {
        #region Runtime

        private Vector3 smoothedForward = Vector3.forward;
        private Vector3 previousDesiredForward = Vector3.forward;
        private float alignmentMomentum;

        #endregion

        #region Public API

        /// <summary>
        /// Inicializa el solver con una dirección horizontal.
        /// </summary>
        public void Initialize(Vector3 sphereIntentForward)
        {
            smoothedForward = FlattenAndNormalize(sphereIntentForward);
            previousDesiredForward = smoothedForward;
            alignmentMomentum = 1f;
        }

        /// <summary>
        /// Fuerza la alineación inmediata al forward dado sin suavizado.
        /// </summary>
        public void SnapToForward(Vector3 sphereIntentForward)
        {
            smoothedForward = FlattenAndNormalize(sphereIntentForward);
            previousDesiredForward = smoothedForward;
            alignmentMomentum = 1f;
        }

        /// <summary>
        /// Actualiza y devuelve la dirección horizontal de referencia suavizada.
        /// </summary>
        public Vector3 UpdateReferenceForward(
            Vector3 desiredForward,
            CameraFollowConfig config,
            float deltaTime,
            bool freezeTracking = false)
        {
            if (config == null)
            {
                return smoothedForward;
            }

            Vector3 normalizedDesired = FlattenAndNormalize(desiredForward);

            if (freezeTracking)
            {
                previousDesiredForward = normalizedDesired;
                return smoothedForward;
            }

            if (smoothedForward.sqrMagnitude < 0.0001f)
            {
                smoothedForward = normalizedDesired;
                previousDesiredForward = normalizedDesired;
                alignmentMomentum = 1f;
                return smoothedForward;
            }

            float angleToDesired = Vector3.Angle(smoothedForward, normalizedDesired);

            if (angleToDesired <= config.MinimumDirectionAngle)
            {
                smoothedForward = normalizedDesired;
                previousDesiredForward = normalizedDesired;
                alignmentMomentum = Mathf.MoveTowards(
                    alignmentMomentum,
                    1f,
                    config.CameraAlignmentMomentumBuildRate * deltaTime);

                return smoothedForward;
            }

            UpdateMomentum(normalizedDesired, config, deltaTime);

            if (angleToDesired >= config.SnapTurnAngle && config.SnapTurnBlend > 0f)
            {
                smoothedForward = Vector3.Slerp(
                    smoothedForward,
                    normalizedDesired,
                    config.SnapTurnBlend);

                smoothedForward = FlattenAndNormalize(smoothedForward);
            }

            float effectiveMomentum = Mathf.Max(
                alignmentMomentum,
                config.MinimumAlignmentMomentum);

            float turnMultiplier = angleToDesired >= config.SharpTurnAngle
                ? config.SharpTurnAlignmentMultiplier
                : 1f;

            float effectiveSpeed =
                config.ForwardAlignmentSpeed *
                effectiveMomentum *
                turnMultiplier;

            float maxRadians = Mathf.Deg2Rad * effectiveSpeed * Mathf.Max(0f, deltaTime);

            smoothedForward = Vector3.RotateTowards(
                smoothedForward,
                normalizedDesired,
                maxRadians,
                0f);

            smoothedForward = FlattenAndNormalize(smoothedForward);
            previousDesiredForward = normalizedDesired;

            return smoothedForward;
        }

        #endregion

        #region Private

        private void UpdateMomentum(
            Vector3 desiredForward,
            CameraFollowConfig config,
            float deltaTime)
        {
            float angleFromPrevious = Vector3.Angle(previousDesiredForward, desiredForward);
            bool isConsistent = angleFromPrevious <= config.CameraAlignmentConsistencyAngle;

            float targetMomentum = isConsistent ? 1f : config.MinimumAlignmentMomentum;
            float rate = isConsistent
                ? config.CameraAlignmentMomentumBuildRate
                : config.CameraAlignmentMomentumDecayRate;

            alignmentMomentum = Mathf.MoveTowards(
                alignmentMomentum,
                targetMomentum,
                rate * deltaTime);
        }

        private static Vector3 FlattenAndNormalize(Vector3 direction)
        {
            direction.y = 0f;

            return direction.sqrMagnitude < 0.0001f
                ? Vector3.forward
                : direction.normalized;
        }

        #endregion
    }
}