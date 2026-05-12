using UnityEngine;

namespace Game.CameraSystem
{
    /// <summary>
    /// Resuelve la dirección horizontal suavizada de referencia para la cámara.
    ///
    /// Lee el forward intencional desde SphereRotationController.CurrentForward,
    /// no la rotación física de la esfera rodando.
    ///
    /// El momentum controla la VELOCIDAD del giro, no si la cámara gira o no.
    /// La cámara siempre sigue al objetivo — solo más lento en giros bruscos.
    /// </summary>
    public sealed class CameraForwardReferenceSolver
    {
        #region Constants

        private const float SharpTurnAngle = 120f;
        private const float SharpTurnSpeedMultiplier = 0.25f;
        private const float MinimumMomentumForTracking = 0.15f;

        #endregion

        #region Runtime

        private Vector3 smoothedForward = Vector3.forward;
        private float alignmentMomentum;
        private Vector3 previousDesiredForward = Vector3.forward;

        #endregion

        #region Public API

        /// <summary>
        /// Inicializa el solver con el forward intencional actual de la esfera.
        /// </summary>
        public void Initialize(Vector3 sphereIntentForward)
        {
            smoothedForward = FlattenAndNormalize(sphereIntentForward);
            previousDesiredForward = smoothedForward;
            alignmentMomentum = 0f;
        }

        /// <summary>
        /// Fuerza la alineación inmediata al forward dado sin suavizado.
        /// Usado en respawn.
        /// </summary>
        public void SnapToForward(Vector3 sphereIntentForward)
        {
            smoothedForward = FlattenAndNormalize(sphereIntentForward);
            previousDesiredForward = smoothedForward;
            alignmentMomentum = 0f;
        }

        /// <summary>
        /// Actualiza y devuelve la dirección horizontal de referencia suavizada.
        /// sphereIntentForward debe ser SphereRotationController.CurrentForward.
        /// </summary>
        public Vector3 UpdateReferenceForward(
            Vector3 sphereIntentForward,
            CameraFollowConfig config,
            float deltaTime,
            bool freezeTracking = false)
        {
            Vector3 desiredForward = FlattenAndNormalize(sphereIntentForward);

            if (freezeTracking)
            {
                alignmentMomentum = 0f;
                previousDesiredForward = desiredForward;
                return smoothedForward;
            }

            if (smoothedForward.sqrMagnitude < 0.0001f)
            {
                smoothedForward = desiredForward;
                previousDesiredForward = desiredForward;
                return smoothedForward;
            }

            float angleToDesired = Vector3.Angle(smoothedForward, desiredForward);

            if (angleToDesired < config.MinimumDirectionAngle)
            {
                previousDesiredForward = desiredForward;
                return smoothedForward;
            }

            // El momentum controla la velocidad del giro, no si gira o no.
            // Se acumula cuando la dirección deseada es consistente frame a frame
            // y decae cuando hay un cambio brusco de dirección.
            float angleFromPrevious = Vector3.Angle(previousDesiredForward, desiredForward);
            bool isConsistent = angleFromPrevious < config.CameraAlignmentConsistencyAngle;

            alignmentMomentum = isConsistent
                ? Mathf.MoveTowards(
                    alignmentMomentum,
                    1f,
                    config.CameraAlignmentMomentumBuildRate * deltaTime)
                : Mathf.MoveTowards(
                    alignmentMomentum,
                    0f,
                    config.CameraAlignmentMomentumDecayRate * deltaTime);

            previousDesiredForward = desiredForward;

            // Mínimo garantizado para que la cámara siempre siga al objetivo.
            // Sin este piso, momentum = 0 bloquea completamente el seguimiento.
            float effectiveMomentum = Mathf.Max(alignmentMomentum, MinimumMomentumForTracking);

            // Los giros bruscos (> SharpTurnAngle) se hacen más lentos intencionalmente
            // para evitar desorientación en móvil.
            float turnSpeedMultiplier = angleToDesired >= SharpTurnAngle
                ? SharpTurnSpeedMultiplier
                : 1f;

            float effectiveSpeed =
                config.ForwardAlignmentSpeed * effectiveMomentum * turnSpeedMultiplier;

            float maxRadians = Mathf.Deg2Rad * effectiveSpeed * Mathf.Max(0f, deltaTime);

            smoothedForward = Vector3.RotateTowards(
                smoothedForward,
                desiredForward,
                maxRadians,
                0f);

            smoothedForward.y = 0f;

            smoothedForward = smoothedForward.sqrMagnitude < 0.0001f
                ? desiredForward
                : smoothedForward.normalized;

            return smoothedForward;
        }

        #endregion

        #region Helpers

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