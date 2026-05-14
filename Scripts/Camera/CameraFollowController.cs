using UnityEngine;
using Game.CameraSystem;

/// <summary>
/// Orquestador del sistema de cámara para móvil.
/// Mantiene la cámara detrás del jugador con respuesta rápida en giros bruscos,
/// ignorando velocidad residual cuando la bola acaba de chocar contra barreras.
/// </summary>
public sealed class CameraFollowController : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]

    [SerializeField]
    [Tooltip("Transform del objetivo a seguir.")]
    private Transform target;

    [SerializeField]
    [Tooltip("Rigidbody del objetivo.")]
    private Rigidbody targetRigidbody;

    [SerializeField]
    [Tooltip("Motor de movimiento del jugador.")]
    private BallMovementMotor movementMotor;

    [SerializeField]
    [Tooltip("Controlador de rotación lógica de la esfera.")]
    private SphereRotationController rotationController;

    [SerializeField]
    [Tooltip("Configuración de la cámara.")]
    private CameraFollowConfig config;

    #endregion

    #region Runtime

    private CameraVerticalStateResolver verticalStateResolver;
    private CameraForwardReferenceSolver forwardReferenceSolver;
    private CameraRigComposer rigComposer;
    private Camera cameraComponent;

    private float posVelocityX;
    private float posVelocityY;
    private float posVelocityZ;

    private float extraDistanceVelocity;
    private float currentExtraDistance;

    private Vector3 currentLookAheadTarget;
    private Vector3 lookAheadVelocity;

    private float currentFov;
    private float fovVelocity;

    private Vector3 cachedReferenceForward = Vector3.forward;

    private float verticalStateIgnoreTimer;
    private bool isRespawnTransitionActive;
    private float respawnTransitionTimer;

    #endregion

    #region Properties

    /// <summary>
    /// Indica si la cámara está ejecutando transición de respawn.
    /// </summary>
    public bool IsRespawnTransitionActive => isRespawnTransitionActive;

    /// <summary>
    /// Dirección horizontal actual usada por la cámara como referencia.
    /// </summary>
    public Vector3 CurrentReferenceForward => cachedReferenceForward;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        cameraComponent = GetComponent<Camera>();

        if (target == null)
        {
            return;
        }

        targetRigidbody = target.GetComponent<Rigidbody>();
        movementMotor = target.GetComponent<BallMovementMotor>();
        rotationController = target.GetComponent<SphereRotationController>();
    }

    private void Awake()
    {
        verticalStateResolver = new CameraVerticalStateResolver();
        forwardReferenceSolver = new CameraForwardReferenceSolver();
        rigComposer = new CameraRigComposer();

        if (cameraComponent == null)
        {
            cameraComponent = GetComponent<Camera>();
        }

        if (cameraComponent == null)
        {
            cameraComponent = GetComponentInChildren<Camera>();
        }

        if (target != null)
        {
            currentLookAheadTarget = target.position;
        }

        Vector3 initialForward = ResolveReferenceForward();

        forwardReferenceSolver.Initialize(initialForward);
        cachedReferenceForward = initialForward;

        if (cameraComponent != null && config != null && config.DynamicFovEnabled)
        {
            currentFov = config.BaseFov;
            cameraComponent.fieldOfView = currentFov;
        }
    }

    private void LateUpdate()
    {
        if (target == null || config == null)
        {
            return;
        }

        UpdateTimers();

        CameraVerticalState verticalState = verticalStateResolver.Resolve(
            isRespawnTransitionActive,
            verticalStateIgnoreTimer > 0f,
            targetRigidbody,
            movementMotor,
            config);

        Vector3 desiredReferenceForward = ResolveReferenceForward();

        cachedReferenceForward = forwardReferenceSolver.UpdateReferenceForward(
            desiredReferenceForward,
            config,
            Time.deltaTime,
            freezeTracking: false);

        CameraRigPose desiredPose = rigComposer.ComposePose(
            target,
            cachedReferenceForward,
            verticalState,
            config,
            transform.position);

        desiredPose = ApplyDynamicDistance(desiredPose);
        desiredPose = ApplyLookAhead(desiredPose);

        ApplyPosition(desiredPose.Position, isRespawnTransitionActive);
        ApplyRotation(desiredPose.Rotation, isRespawnTransitionActive);
        UpdateDynamicFov();
        UpdateRespawnTransitionState(desiredPose);
    }

    private void OnValidate()
    {
    }

    #endregion

    #region Public API

    /// <summary>
    /// Inicia una transición suave de cámara después de respawn.
    /// </summary>
    public void BeginRespawnTransition()
    {
        if (target == null || config == null)
        {
            return;
        }

        posVelocityX = 0f;
        posVelocityY = 0f;
        posVelocityZ = 0f;
        lookAheadVelocity = Vector3.zero;

        verticalStateIgnoreTimer = config.VerticalStateIgnoreDurationAfterRespawn;
        isRespawnTransitionActive = true;
        respawnTransitionTimer = 0f;

        Vector3 currentForward = ResolveReferenceForward();

        forwardReferenceSolver.SnapToForward(currentForward);
        cachedReferenceForward = currentForward;

        verticalStateResolver.Reset();
        currentLookAheadTarget = target.position;
    }

    /// <summary>
    /// Fuerza a la cámara a alinearse inmediatamente con el forward lógico actual.
    /// Útil después de teleports, cambios de carril extremos o correcciones de dirección.
    /// </summary>
    public void SnapToCurrentForward()
    {
        Vector3 currentForward = ResolveReferenceForward();

        forwardReferenceSolver.SnapToForward(currentForward);
        cachedReferenceForward = currentForward;

        if (target != null)
        {
            currentLookAheadTarget = target.position + config.NormalLookAtOffset;
        }

        lookAheadVelocity = Vector3.zero;
    }

    #endregion

    #region Forward Resolution

    /// <summary>
    /// Resuelve la dirección de referencia de la cámara.
    /// Siempre devuelve el forward lógico de la cara de la pelota
    /// (<see cref="SphereRotationController.CurrentForward"/>), independientemente
    /// de la dirección o magnitud de la velocidad física.
    ///
    /// Cambio: se eliminó la rama que usaba velocityForward cuando estaba alineado
    /// con el forward lógico. Eso causaba que después de un rebote la cámara siguiera
    /// brevemente la velocidad antes de corregirse hacia la cara de la bola.
    /// </summary>
    private Vector3 ResolveReferenceForward()
    {
        return ResolveLogicalForward();
    }

    /// <summary>
    /// Resuelve el forward lógico del jugador independientemente de la velocidad física.
    /// </summary>
    private Vector3 ResolveLogicalForward()
    {
        if (rotationController != null)
        {
            Vector3 forward = rotationController.CurrentForward;
            forward.y = 0f;

            if (forward.sqrMagnitude > 0.0001f)
            {
                return forward.normalized;
            }
        }

        Vector3 fallback = target != null
            ? target.forward
            : transform.forward;

        fallback.y = 0f;

        return fallback.sqrMagnitude < 0.0001f
            ? Vector3.forward
            : fallback.normalized;
    }

    #endregion

    #region Dynamic Distance

    /// <summary>
    /// Aplica distancia dinámica según velocidad.
    /// </summary>
    private CameraRigPose ApplyDynamicDistance(CameraRigPose pose)
    {
        if (!config.DynamicDistanceEnabled || movementMotor == null)
        {
            return pose;
        }

        // isPostBarrier eliminado: IsAwaitingInputAfterBarrier siempre devuelve false.
        float speedNorm = movementMotor.MaxSpeed > 0f
            ? Mathf.Clamp01(movementMotor.CurrentPlanarVelocity / movementMotor.MaxSpeed)
            : 0f;

        float targetExtraDistance = config.ExtraDistanceAtMaxSpeed * speedNorm;
        float smoothTime = config.DistanceSmoothTime;

        currentExtraDistance = Mathf.SmoothDamp(
            currentExtraDistance,
            targetExtraDistance,
            ref extraDistanceVelocity,
            smoothTime);

        if (currentExtraDistance < 0.01f)
        {
            return pose;
        }

        Vector3 flatForward = cachedReferenceForward;
        flatForward.y = 0f;

        if (flatForward.sqrMagnitude <= 0.0001f)
        {
            return pose;
        }

        return new CameraRigPose(
            pose.Position - flatForward.normalized * currentExtraDistance,
            pose.Rotation);
    }

    #endregion

    #region Look-Ahead

    /// <summary>
    /// Aplica look-ahead usando la dirección de referencia actual.
    /// </summary>
    private CameraRigPose ApplyLookAhead(CameraRigPose pose)
    {
        if (config.LookAheadDistance <= 0.001f || movementMotor == null)
        {
            return pose;
        }

        // isPostBarrier eliminado: IsAwaitingInputAfterBarrier siempre devuelve false.
        float speedNorm = movementMotor.MaxSpeed > 0f
            ? Mathf.Clamp01(movementMotor.CurrentPlanarVelocity / movementMotor.MaxSpeed)
            : 0f;

        Vector3 rawTarget = target.position
                            + config.NormalLookAtOffset
                            + cachedReferenceForward * (config.LookAheadDistance * speedNorm);

        float smoothTime = config.LookAheadSmoothTime;

        currentLookAheadTarget = Vector3.SmoothDamp(
            currentLookAheadTarget,
            rawTarget,
            ref lookAheadVelocity,
            smoothTime);

        Vector3 lookDirection = currentLookAheadTarget - transform.position;

        if (lookDirection.sqrMagnitude < 0.001f)
        {
            return pose;
        }

        return new CameraRigPose(
            pose.Position,
            Quaternion.LookRotation(lookDirection.normalized, Vector3.up));
    }

    #endregion

    #region Position

    /// <summary>
    /// Aplica movimiento suavizado hacia la posición deseada.
    /// </summary>
    private void ApplyPosition(Vector3 desiredPosition, bool useRespawnSmoothing)
    {
        float horizontalSmooth = useRespawnSmoothing
            ? config.RespawnHorizontalSmoothTime
            : config.HorizontalSmoothTime;

        float verticalSmooth = useRespawnSmoothing
            ? config.RespawnVerticalSmoothTime
            : config.VerticalSmoothTime;

        Vector3 current = transform.position;

        transform.position = new Vector3(
            Mathf.SmoothDamp(current.x, desiredPosition.x, ref posVelocityX, horizontalSmooth),
            Mathf.SmoothDamp(current.y, desiredPosition.y, ref posVelocityY, verticalSmooth),
            Mathf.SmoothDamp(current.z, desiredPosition.z, ref posVelocityZ, horizontalSmooth));
    }

    #endregion

    #region Rotation

    /// <summary>
    /// Aplica rotación suavizada hacia la orientación deseada.
    /// </summary>
    private void ApplyRotation(Quaternion desiredRotation, bool useRespawnSmoothing)
    {
        float speed = useRespawnSmoothing
            ? config.RespawnRotationLerpSpeed
            : config.RotationLerpSpeed;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            speed * Time.deltaTime);
    }

    #endregion

    #region Dynamic FOV

    /// <summary>
    /// Actualiza el FOV dinámico según velocidad real del jugador.
    /// </summary>
    private void UpdateDynamicFov()
    {
        if (cameraComponent == null || !config.DynamicFovEnabled || movementMotor == null)
        {
            return;
        }

        float speedNorm = movementMotor.MaxSpeed > 0f
            ? Mathf.Clamp01(movementMotor.CurrentPlanarVelocity / movementMotor.MaxSpeed)
            : 0f;

        currentFov = Mathf.SmoothDamp(
            currentFov,
            Mathf.Lerp(config.BaseFov, config.MaxFov, speedNorm),
            ref fovVelocity,
            config.FovSmoothTime);

        cameraComponent.fieldOfView = currentFov;
    }

    #endregion

    #region Respawn

    private void UpdateTimers()
    {
        if (verticalStateIgnoreTimer > 0f)
        {
            verticalStateIgnoreTimer = Mathf.Max(0f, verticalStateIgnoreTimer - Time.deltaTime);
        }

        if (isRespawnTransitionActive)
        {
            respawnTransitionTimer += Time.deltaTime;
        }
    }

    private void UpdateRespawnTransitionState(CameraRigPose desiredPose)
    {
        if (!isRespawnTransitionActive || config == null)
        {
            return;
        }

        if (respawnTransitionTimer < config.MinimumRespawnTransitionDuration)
        {
            return;
        }

        bool positionReached =
            Vector3.Distance(transform.position, desiredPose.Position) <= config.RespawnPositionTolerance;

        bool rotationReached =
            Quaternion.Angle(transform.rotation, desiredPose.Rotation) <= config.RespawnRotationTolerance;

        if (!positionReached || !rotationReached)
        {
            return;
        }

        isRespawnTransitionActive = false;
        posVelocityX = 0f;
        posVelocityY = 0f;
        posVelocityZ = 0f;
    }

    #endregion

    #region Helpers

    private bool IsAwaitingInputAfterBarrier()
    {
        return movementMotor != null && movementMotor.IsAwaitingInputAfterBarrier;
    }

    #endregion
}