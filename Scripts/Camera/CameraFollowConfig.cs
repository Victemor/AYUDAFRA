using UnityEngine;

namespace Game.CameraSystem
{
    /// <summary>
    /// Configuración del sistema de seguimiento de cámara para móvil.
    /// Centraliza offsets, suavizados, alineación direccional, look-ahead, FOV dinámico y transición por respawn.
    /// </summary>
    [CreateAssetMenu(fileName = "CameraFollowConfig", menuName = "Game/Camera/Camera Follow Config")]
    public sealed class CameraFollowConfig : ScriptableObject
    {
        #region Offsets

        [Header("Offsets")]

        [SerializeField]
        [Tooltip("Offset base cuando la pelota va a velocidad normal.")]
        private Vector3 normalOffset = new Vector3(0f, 7f, -8f);

        [SerializeField]
        [Tooltip("Offset cuando la pelota sube.")]
        private Vector3 ascendingOffset = new Vector3(0f, 4.75f, -8.5f);

        [SerializeField]
        [Tooltip("Offset cuando la pelota baja.")]
        private Vector3 descendingOffset = new Vector3(0f, 9.5f, -7.25f);

        [SerializeField]
        [Tooltip("Offset del punto de mira en estado normal.")]
        private Vector3 normalLookAtOffset = new Vector3(0f, 1f, 0f);

        [SerializeField]
        [Tooltip("Offset del punto de mira al subir.")]
        private Vector3 ascendingLookAtOffset = new Vector3(0f, 1.35f, 0f);

        [SerializeField]
        [Tooltip("Offset del punto de mira al bajar.")]
        private Vector3 descendingLookAtOffset = new Vector3(0f, 0.8f, 0f);

        #endregion

        #region Dynamic Distance

        [Header("Dynamic Distance")]

        [SerializeField]
        [Tooltip("Activa la distancia dinámica según velocidad.")]
        private bool dynamicDistanceEnabled = true;

        [SerializeField]
        [Tooltip("Distancia extra añadida hacia atrás cuando la pelota está a velocidad máxima.")]
        private float extraDistanceAtMaxSpeed = 4f;

        [SerializeField]
        [Tooltip("Tiempo de suavizado para el cambio de distancia dinámica.")]
        private float distanceSmoothTime = 0.4f;

        #endregion

        #region Look-Ahead

        [Header("Look-Ahead")]

        [SerializeField]
        [Tooltip("Distancia máxima adelante del jugador que mira la cámara a velocidad máxima.")]
        private float lookAheadDistance = 4f;

        [SerializeField]
        [Tooltip("Tiempo de suavizado del punto de mira look-ahead.")]
        private float lookAheadSmoothTime = 0.25f;

        #endregion

        #region Position Smoothing

        [Header("Position Smoothing")]

        [SerializeField]
        [Tooltip("Suavizado de posición horizontal. Valores menores hacen que la cámara se ubique más rápido.")]
        private float horizontalSmoothTime = 0.18f;

        [SerializeField]
        [Tooltip("Suavizado lateral. Valores altos generan cámara más pesada al girar.")]
        private float lateralSmoothTime = 0.28f;

        [SerializeField]
        [Tooltip("Suavizado vertical.")]
        private float verticalSmoothTime = 0.22f;

        #endregion

        #region Rotation

        [Header("Rotation")]

        [SerializeField]
        [Tooltip("Velocidad base de interpolación de rotación. Valores mayores ubican la cámara más rápido.")]
        private float rotationLerpSpeed = 8f;

        #endregion

        #region Direction Alignment

        [Header("Direction Alignment")]

        [SerializeField]
        [Tooltip("Velocidad base de alineación de la cámara al forward del jugador en grados por segundo.")]
        private float forwardAlignmentSpeed = 110f;

        [SerializeField]
        [Tooltip("Ángulo mínimo para iniciar el proceso de alineación.")]
        private float minimumDirectionAngle = 0.5f;

        [SerializeField]
        [Tooltip("Ángulo frame-a-frame por debajo del cual se considera que la dirección deseada sigue siendo consistente.")]
        private float cameraAlignmentConsistencyAngle = 35f;

        [SerializeField]
        [Tooltip("Velocidad de acumulación de momentum cuando la dirección deseada es consistente.")]
        private float cameraAlignmentMomentumBuildRate = 1.8f;

        [SerializeField]
        [Tooltip("Velocidad de pérdida de momentum cuando la dirección cambia bruscamente.")]
        private float cameraAlignmentMomentumDecayRate = 3f;

        [Header("Sharp Turn Catch-Up")]

        [SerializeField]
        [Tooltip("Ángulo desde el cual se considera que el jugador hizo un giro brusco.")]
        [Range(30f, 180f)]
        private float sharpTurnAngle = 75f;

        [SerializeField]
        [Tooltip("Multiplicador extra de velocidad de alineación durante giros bruscos.")]
        [Range(1f, 8f)]
        private float sharpTurnAlignmentMultiplier = 3.5f;

        [SerializeField]
        [Tooltip("Ángulo desde el cual la cámara hace un snap parcial para no quedarse atrasada.")]
        [Range(60f, 180f)]
        private float snapTurnAngle = 135f;

        [SerializeField]
        [Tooltip("Porcentaje de snap aplicado cuando el giro supera Snap Turn Angle. 0 = sin snap. 1 = snap completo.")]
        [Range(0f, 1f)]
        private float snapTurnBlend = 0.35f;

        [SerializeField]
        [Tooltip("Momentum mínimo garantizado para que la cámara nunca deje de seguir la dirección objetivo.")]
        [Range(0f, 1f)]
        private float minimumAlignmentMomentum = 0.45f;

        #endregion

        #region Dynamic FOV

        [Header("Dynamic FOV")]

        [SerializeField]
        [Tooltip("Activa el FOV dinámico según velocidad.")]
        private bool dynamicFovEnabled = true;

        [SerializeField]
        [Tooltip("FOV base.")]
        [Range(40f, 90f)]
        private float baseFov = 58f;

        [SerializeField]
        [Tooltip("FOV máximo a velocidad máxima.")]
        [Range(50f, 100f)]
        private float maxFov = 70f;

        [SerializeField]
        [Tooltip("Suavizado del cambio de FOV.")]
        private float fovSmoothTime = 0.35f;

        #endregion

        #region Vertical States

        [Header("Vertical States")]

        [SerializeField]
        [Tooltip("Activa el estado especial de cámara al subir.")]
        private bool useAscendingState = true;

        [SerializeField]
        [Tooltip("Activa el estado especial de cámara al bajar.")]
        private bool useDescendingState = true;

        [SerializeField]
        [Tooltip("Velocidad vertical mínima para entrar en estado de subida.")]
        private float ascendingEnterVelocityThreshold = 1.1f;

        [SerializeField]
        [Tooltip("Velocidad vertical para salir del estado de subida.")]
        private float ascendingExitVelocityThreshold = 0.35f;

        [SerializeField]
        [Tooltip("Velocidad vertical máxima negativa para entrar en estado de bajada.")]
        private float descendingEnterVelocityThreshold = -1.1f;

        [SerializeField]
        [Tooltip("Velocidad vertical para salir del estado de bajada.")]
        private float descendingExitVelocityThreshold = -0.35f;

        [SerializeField]
        [Tooltip("Velocidad lógica mínima para permitir estados verticales.")]
        private float minimumLogicalSpeedForVerticalState = 0.35f;

        #endregion

        #region Respawn

        [Header("Respawn")]

        [SerializeField]
        [Tooltip("Tiempo durante el cual se ignoran estados verticales después de respawn.")]
        private float verticalStateIgnoreDurationAfterRespawn = 0.25f;

        [SerializeField]
        [Tooltip("Suavizado horizontal durante transición de respawn.")]
        private float respawnHorizontalSmoothTime = 0.4f;

        [SerializeField]
        [Tooltip("Suavizado vertical durante transición de respawn.")]
        private float respawnVerticalSmoothTime = 0.3f;

        [SerializeField]
        [Tooltip("Velocidad de rotación durante transición de respawn.")]
        private float respawnRotationLerpSpeed = 4.5f;

        [SerializeField]
        [Tooltip("Tolerancia de posición para finalizar transición de respawn.")]
        private float respawnPositionTolerance = 0.1f;

        [SerializeField]
        [Tooltip("Tolerancia de rotación para finalizar transición de respawn.")]
        private float respawnRotationTolerance = 2f;

        [SerializeField]
        [Tooltip("Duración mínima de la transición de respawn.")]
        private float minimumRespawnTransitionDuration = 0.2f;

        #endregion

        #region Properties

        public Vector3 NormalOffset => normalOffset;
        public Vector3 AscendingOffset => ascendingOffset;
        public Vector3 DescendingOffset => descendingOffset;
        public Vector3 NormalLookAtOffset => normalLookAtOffset;
        public Vector3 AscendingLookAtOffset => ascendingLookAtOffset;
        public Vector3 DescendingLookAtOffset => descendingLookAtOffset;

        public bool DynamicDistanceEnabled => dynamicDistanceEnabled;
        public float ExtraDistanceAtMaxSpeed => extraDistanceAtMaxSpeed;
        public float DistanceSmoothTime => distanceSmoothTime;

        public float LookAheadDistance => lookAheadDistance;
        public float LookAheadSmoothTime => lookAheadSmoothTime;

        public float HorizontalSmoothTime => horizontalSmoothTime;
        public float LateralSmoothTime => lateralSmoothTime;
        public float VerticalSmoothTime => verticalSmoothTime;

        public float RotationLerpSpeed => rotationLerpSpeed;

        public float ForwardAlignmentSpeed => forwardAlignmentSpeed;
        public float MinimumDirectionAngle => minimumDirectionAngle;
        public float CameraAlignmentConsistencyAngle => cameraAlignmentConsistencyAngle;
        public float CameraAlignmentMomentumBuildRate => cameraAlignmentMomentumBuildRate;
        public float CameraAlignmentMomentumDecayRate => cameraAlignmentMomentumDecayRate;

        public float SharpTurnAngle => sharpTurnAngle;
        public float SharpTurnAlignmentMultiplier => sharpTurnAlignmentMultiplier;
        public float SnapTurnAngle => snapTurnAngle;
        public float SnapTurnBlend => snapTurnBlend;
        public float MinimumAlignmentMomentum => minimumAlignmentMomentum;

        public bool DynamicFovEnabled => dynamicFovEnabled;
        public float BaseFov => baseFov;
        public float MaxFov => maxFov;
        public float FovSmoothTime => fovSmoothTime;

        public bool UseAscendingState => useAscendingState;
        public bool UseDescendingState => useDescendingState;
        public float AscendingEnterVelocityThreshold => ascendingEnterVelocityThreshold;
        public float AscendingExitVelocityThreshold => ascendingExitVelocityThreshold;
        public float DescendingEnterVelocityThreshold => descendingEnterVelocityThreshold;
        public float DescendingExitVelocityThreshold => descendingExitVelocityThreshold;
        public float MinimumLogicalSpeedForVerticalState => minimumLogicalSpeedForVerticalState;

        public float VerticalStateIgnoreDurationAfterRespawn => verticalStateIgnoreDurationAfterRespawn;
        public float RespawnHorizontalSmoothTime => respawnHorizontalSmoothTime;
        public float RespawnVerticalSmoothTime => respawnVerticalSmoothTime;
        public float RespawnRotationLerpSpeed => respawnRotationLerpSpeed;
        public float RespawnPositionTolerance => respawnPositionTolerance;
        public float RespawnRotationTolerance => respawnRotationTolerance;
        public float MinimumRespawnTransitionDuration => minimumRespawnTransitionDuration;

        #endregion

        private void OnValidate()
        {
            horizontalSmoothTime = Mathf.Max(0.01f, horizontalSmoothTime);
            lateralSmoothTime = Mathf.Max(0.01f, lateralSmoothTime);
            verticalSmoothTime = Mathf.Max(0.01f, verticalSmoothTime);

            rotationLerpSpeed = Mathf.Max(0.01f, rotationLerpSpeed);

            forwardAlignmentSpeed = Mathf.Max(1f, forwardAlignmentSpeed);
            minimumDirectionAngle = Mathf.Clamp(minimumDirectionAngle, 0f, 45f);
            cameraAlignmentConsistencyAngle = Mathf.Clamp(cameraAlignmentConsistencyAngle, 1f, 90f);
            cameraAlignmentMomentumBuildRate = Mathf.Max(0.01f, cameraAlignmentMomentumBuildRate);
            cameraAlignmentMomentumDecayRate = Mathf.Max(0.01f, cameraAlignmentMomentumDecayRate);

            sharpTurnAngle = Mathf.Clamp(sharpTurnAngle, 30f, 180f);
            sharpTurnAlignmentMultiplier = Mathf.Max(1f, sharpTurnAlignmentMultiplier);
            snapTurnAngle = Mathf.Clamp(snapTurnAngle, sharpTurnAngle, 180f);
            snapTurnBlend = Mathf.Clamp01(snapTurnBlend);
            minimumAlignmentMomentum = Mathf.Clamp01(minimumAlignmentMomentum);

            extraDistanceAtMaxSpeed = Mathf.Max(0f, extraDistanceAtMaxSpeed);
            distanceSmoothTime = Mathf.Max(0.01f, distanceSmoothTime);

            lookAheadDistance = Mathf.Max(0f, lookAheadDistance);
            lookAheadSmoothTime = Mathf.Max(0.01f, lookAheadSmoothTime);

            baseFov = Mathf.Clamp(baseFov, 20f, 120f);
            maxFov = Mathf.Max(baseFov, maxFov);
            fovSmoothTime = Mathf.Max(0.01f, fovSmoothTime);

            ascendingEnterVelocityThreshold = Mathf.Max(0.01f, ascendingEnterVelocityThreshold);
            ascendingExitVelocityThreshold = Mathf.Clamp(ascendingExitVelocityThreshold, 0f, ascendingEnterVelocityThreshold);

            descendingExitVelocityThreshold = Mathf.Min(descendingExitVelocityThreshold, 0f);
            descendingEnterVelocityThreshold = Mathf.Min(descendingEnterVelocityThreshold, descendingExitVelocityThreshold - 0.01f);

            minimumLogicalSpeedForVerticalState = Mathf.Max(0f, minimumLogicalSpeedForVerticalState);

            respawnHorizontalSmoothTime = Mathf.Max(0.01f, respawnHorizontalSmoothTime);
            respawnVerticalSmoothTime = Mathf.Max(0.01f, respawnVerticalSmoothTime);
            respawnRotationLerpSpeed = Mathf.Max(0.01f, respawnRotationLerpSpeed);
            respawnPositionTolerance = Mathf.Max(0.001f, respawnPositionTolerance);
            respawnRotationTolerance = Mathf.Max(0.1f, respawnRotationTolerance);
            minimumRespawnTransitionDuration = Mathf.Max(0f, minimumRespawnTransitionDuration);
        }
    }
}