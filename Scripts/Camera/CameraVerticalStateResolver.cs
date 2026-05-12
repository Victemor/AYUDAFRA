using UnityEngine;

namespace Game.CameraSystem
{
    /// <summary>
    /// Resuelve el estado vertical del encuadre de cámara.
    /// 
    /// Responsabilidades:
    /// - Detectar si el target está subiendo.
    /// - Detectar si el target está bajando.
    /// - Mantener histéresis básica para evitar parpadeo entre estados.
    /// </summary>
    public sealed class CameraVerticalStateResolver
    {
        #region Runtime

        private CameraVerticalState currentState = CameraVerticalState.Normal;

        #endregion

        #region Public API

        /// <summary>
        /// Evalúa y devuelve el estado vertical actual.
        /// </summary>
        public CameraVerticalState Resolve(
            bool isRespawnTransitionActive,
            bool ignoreVerticalStates,
            Rigidbody targetRigidbody,
            BallMovementMotor movementMotor,
            CameraFollowConfig config)
        {
            if (isRespawnTransitionActive)
            {
                currentState = CameraVerticalState.RespawnTransition;
                return currentState;
            }

            if (config == null || targetRigidbody == null)
            {
                currentState = CameraVerticalState.Normal;
                return currentState;
            }

            if (ignoreVerticalStates)
            {
                currentState = CameraVerticalState.Normal;
                return currentState;
            }

            float logicalSpeed = movementMotor != null ? movementMotor.CurrentSpeed : 0f;
            if (logicalSpeed < config.MinimumLogicalSpeedForVerticalState)
            {
                currentState = CameraVerticalState.Normal;
                return currentState;
            }

            float verticalVelocity = targetRigidbody.linearVelocity.y;

            switch (currentState)
            {
                case CameraVerticalState.Ascending:
                    if (verticalVelocity <= config.AscendingExitVelocityThreshold)
                    {
                        currentState = ResolveEntryState(verticalVelocity, config);
                    }

                    break;

                case CameraVerticalState.Descending:
                    if (verticalVelocity >= config.DescendingExitVelocityThreshold)
                    {
                        currentState = ResolveEntryState(verticalVelocity, config);
                    }

                    break;

                default:
                    currentState = ResolveEntryState(verticalVelocity, config);
                    break;
            }

            return currentState;
        }

        /// <summary>
        /// Reinicia el estado interno.
        /// </summary>
        public void Reset()
        {
            currentState = CameraVerticalState.Normal;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Determina el estado de entrada desde un contexto neutral.
        /// </summary>
        private static CameraVerticalState ResolveEntryState(
            float verticalVelocity,
            CameraFollowConfig config)
        {
            if (config.UseAscendingState
                && verticalVelocity >= config.AscendingEnterVelocityThreshold)
            {
                return CameraVerticalState.Ascending;
            }

            if (config.UseDescendingState
                && verticalVelocity <= config.DescendingEnterVelocityThreshold)
            {
                return CameraVerticalState.Descending;
            }

            return CameraVerticalState.Normal;
        }

        #endregion
    }
}