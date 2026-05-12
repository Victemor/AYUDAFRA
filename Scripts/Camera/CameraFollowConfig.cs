using UnityEngine;

namespace Game.CameraSystem
{
    /// <summary>
    /// Configuración del sistema de seguimiento de cámara, rediseñada para móvil.
    ///
    /// Nuevos parámetros respecto a la versión original:
    /// - Distancia dinámica: la cámara se aleja más cuando la pelota va rápido (doc §2.8).
    /// - Look-ahead: la cámara mira un punto delante de la pelota para ver obstáculos
    ///   con anticipación (doc §2.3).
    /// - FOV dinámico moderado: aumenta ligeramente con la velocidad (doc §2.9).
    /// - Smooth times aumentados: seguimiento más suave para evitar mareo en móvil (doc §2.4).
    /// - lateralSmoothTime: lag en el eje lateral para sensación estable.
    /// - Momentum de alineación: la cámara confirma la nueva dirección antes de girar.
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
        [Tooltip("Offset cuando sube.")]
        private Vector3 ascendingOffset = new Vector3(0f, 4.75f, -8.5f);

        [SerializeField]
        [Tooltip("Offset cuando baja.")]
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

        [Header("Dynamic Distance (§2.8)")]

        [SerializeField]
        [Tooltip("Activa la distancia dinámica según velocidad.")]
        private bool dynamicDistanceEnabled = true;

        [SerializeField]
        [Tooltip("Distancia extra (metros) que se añade al offset Z cuando la pelota está a " +
                 "velocidad máxima. Lento → cámara más cerca. Rápido → más lejos.")]
        private float extraDistanceAtMaxSpeed = 4f;

        [SerializeField]
        [Tooltip("Tiempo de suavizado para el cambio de distancia dinámica.")]
        private float distanceSmoothTime = 0.4f;

        #endregion

        #region Look-Ahead

        [Header("Look-Ahead (§2.3)")]

        [SerializeField]
        [Tooltip("Distancia máxima (metros) adelante del jugador que mira la cámara a velocidad máxima. " +
                 "0 = mirar al centro del jugador. Recomendado: 3-6m.")]
        private float lookAheadDistance = 4f;

        [SerializeField]
        [Tooltip("Tiempo de suavizado del punto de mira look-ahead. " +
                 "Valores altos = transición más gradual al cambiar de dirección.")]
        private float lookAheadSmoothTime = 0.25f;

        #endregion

        #region Position Smoothing

        [Header("Position Smoothing (§2.4)")]

        [SerializeField]
        [Tooltip("Suavizado para el eje forward. Más alto que PC para evitar mareo. Recomendado: 0.2-0.3.")]
        private float horizontalSmoothTime = 0.22f;

        [SerializeField]
        [Tooltip("Suavizado para el eje lateral (mayor lag). " +
                 "Genera la sensación de cámara estable cuando la pelota gira.")]
        private float lateralSmoothTime = 0.48f;

        [SerializeField]
        [Tooltip("Suavizado vertical.")]
        private float verticalSmoothTime = 0.22f;

        #endregion

        #region Rotation

        [Header("Rotation")]

        [SerializeField]
        [Tooltip("Velocidad de interpolación de rotación. Más bajo = más suave/lazy (recomendado móvil: 5-8).")]
        private float rotationLerpSpeed = 6f;

        #endregion

        #region Direction Alignment Momentum

        [Header("Direction Alignment (§2.5, §2.6, §2.7)")]

        [SerializeField]
        [Tooltip("Velocidad de alineación de la cámara al forward del jugador (grados/s) " +
                 "cuando el momentum está al 100%. Valores bajos = cámara tarda más en girar.")]
        private float forwardAlignmentSpeed = 65f;

        [SerializeField]
        [Tooltip("Ángulo mínimo para iniciar el proceso de alineación.")]
        private float minimumDirectionAngle = 0.5f;

        [SerializeField]
        [Tooltip("Ángulo frame-a-frame por debajo del cual se considera 'misma dirección' " +
                 "para acumular momentum de alineación.")]
        private float cameraAlignmentConsistencyAngle = 25f;

        [SerializeField]
        [Tooltip("Velocidad de acumulación de momentum (0→1 por segundo). " +
                 "1/buildRate = segundos para llegar a seguimiento completo.")]
        private float cameraAlignmentMomentumBuildRate = 0.7f;

        [SerializeField]
        [Tooltip("Velocidad de pérdida de momentum cuando cambia bruscamente la dirección.")]
        private float cameraAlignmentMomentumDecayRate = 4.0f;

        #endregion

        #region Dynamic FOV

        [Header("Dynamic FOV (§2.9)")]

        [SerializeField]
        [Tooltip("Activa el FOV dinámico según velocidad.")]
        private bool dynamicFovEnabled = true;

        [SerializeField]
        [Tooltip("FOV base (velocidad 0).")]
        [Range(40f, 90f)]
        private float baseFov = 58f;

        [SerializeField]
        [Tooltip("FOV máximo (velocidad máxima). " +
                 "En móvil mantenerlo moderado (±10-15° del base) para no desorientar.")]
        [Range(50f, 100f)]
        private float maxFov = 70f;

        [SerializeField]
        [Tooltip("Suavizado del cambio de FOV.")]
        private float fovSmoothTime = 0.35f;

        #endregion

        #region Vertical States

        [Header("Vertical States")]
        [SerializeField] private bool useAscendingState = true;
        [SerializeField] private bool useDescendingState = true;
        [SerializeField] private float ascendingEnterVelocityThreshold = 1.1f;
        [SerializeField] private float ascendingExitVelocityThreshold = 0.35f;
        [SerializeField] private float descendingEnterVelocityThreshold = -1.1f;
        [SerializeField] private float descendingExitVelocityThreshold = -0.35f;
        [SerializeField] private float minimumLogicalSpeedForVerticalState = 0.35f;

        #endregion

        #region Respawn

        [Header("Respawn")]
        [SerializeField] private float verticalStateIgnoreDurationAfterRespawn = 0.25f;
        [SerializeField] private float respawnHorizontalSmoothTime = 0.4f;
        [SerializeField] private float respawnVerticalSmoothTime = 0.3f;
        [SerializeField] private float respawnRotationLerpSpeed = 4.5f;
        [SerializeField] private float respawnPositionTolerance = 0.1f;
        [SerializeField] private float respawnRotationTolerance = 2f;
        [SerializeField] private float minimumRespawnTransitionDuration = 0.2f;

        #endregion

        #region Properties — Offsets

        public Vector3 NormalOffset => normalOffset;
        public Vector3 AscendingOffset => ascendingOffset;
        public Vector3 DescendingOffset => descendingOffset;
        public Vector3 NormalLookAtOffset => normalLookAtOffset;
        public Vector3 AscendingLookAtOffset => ascendingLookAtOffset;
        public Vector3 DescendingLookAtOffset => descendingLookAtOffset;

        #endregion

        #region Properties — Dynamic Distance

        public bool DynamicDistanceEnabled => dynamicDistanceEnabled;
        public float ExtraDistanceAtMaxSpeed => extraDistanceAtMaxSpeed;
        public float DistanceSmoothTime => distanceSmoothTime;

        #endregion

        #region Properties — Look-Ahead

        public float LookAheadDistance => lookAheadDistance;
        public float LookAheadSmoothTime => lookAheadSmoothTime;

        #endregion

        #region Properties — Smoothing

        public float HorizontalSmoothTime => horizontalSmoothTime;
        public float LateralSmoothTime => lateralSmoothTime;
        public float VerticalSmoothTime => verticalSmoothTime;

        #endregion

        #region Properties — Rotation

        public float RotationLerpSpeed => rotationLerpSpeed;
        public float ForwardAlignmentSpeed => forwardAlignmentSpeed;
        public float MinimumDirectionAngle => minimumDirectionAngle;
        public float CameraAlignmentConsistencyAngle => cameraAlignmentConsistencyAngle;
        public float CameraAlignmentMomentumBuildRate => cameraAlignmentMomentumBuildRate;
        public float CameraAlignmentMomentumDecayRate => cameraAlignmentMomentumDecayRate;

        #endregion

        #region Properties — FOV

        public bool DynamicFovEnabled => dynamicFovEnabled;
        public float BaseFov => baseFov;
        public float MaxFov => maxFov;
        public float FovSmoothTime => fovSmoothTime;

        #endregion

        #region Properties — Vertical States

        public bool UseAscendingState => useAscendingState;
        public bool UseDescendingState => useDescendingState;
        public float AscendingEnterVelocityThreshold => ascendingEnterVelocityThreshold;
        public float AscendingExitVelocityThreshold => ascendingExitVelocityThreshold;
        public float DescendingEnterVelocityThreshold => descendingEnterVelocityThreshold;
        public float DescendingExitVelocityThreshold => descendingExitVelocityThreshold;
        public float MinimumLogicalSpeedForVerticalState => minimumLogicalSpeedForVerticalState;

        #endregion

        #region Properties — Respawn

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
            lateralSmoothTime = Mathf.Max(horizontalSmoothTime, lateralSmoothTime);
            verticalSmoothTime = Mathf.Max(0.01f, verticalSmoothTime);
            rotationLerpSpeed = Mathf.Max(0.01f, rotationLerpSpeed);
            forwardAlignmentSpeed = Mathf.Max(1f, forwardAlignmentSpeed);
            minimumDirectionAngle = Mathf.Clamp(minimumDirectionAngle, 0f, 45f);
            cameraAlignmentConsistencyAngle = Mathf.Clamp(cameraAlignmentConsistencyAngle, 1f, 90f);
            cameraAlignmentMomentumBuildRate = Mathf.Max(0.01f, cameraAlignmentMomentumBuildRate);
            cameraAlignmentMomentumDecayRate = Mathf.Max(0.01f, cameraAlignmentMomentumDecayRate);
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