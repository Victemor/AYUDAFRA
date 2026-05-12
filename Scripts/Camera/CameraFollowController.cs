using UnityEngine;
using Game.CameraSystem;

/// <summary>
/// Orquestador del sistema de cámara para móvil.
/// Usa la velocidad real de la bola como referencia de dirección cuando está en movimiento,
/// garantizando que la cámara siempre quede detrás sin depender de la rotación inicial.
/// </summary>
public sealed class CameraFollowController : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]

    [Tooltip("Transform del objetivo a seguir (NewPlayer).")]
    [SerializeField] private Transform target;

    [Tooltip("Rigidbody del objetivo (NewPlayer).")]
    [SerializeField] private Rigidbody targetRigidbody;

    [Tooltip("Motor de movimiento del jugador (NewPlayer).")]
    [SerializeField] private BallMovementMotor movementMotor;

    [Tooltip("Controlador de rotación de la esfera (NewPlayer).")]
    [SerializeField] private SphereRotationController rotationController;

    [Tooltip("Configuración de la cámara.")]
    [SerializeField] private CameraFollowConfig config;

    [Header("Forward Reference")]

    [Tooltip("Velocidad mínima en m/s a partir de la cual se usa la dirección " +
             "de velocidad real en lugar del forward del controlador de rotación. " +
             "Evita que la cámara se invierta si el forward inicial es incorrecto.")]
    [SerializeField] private float velocityForwardThreshold = 0.5f;

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

    public bool IsRespawnTransitionActive => isRespawnTransitionActive;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        verticalStateResolver  = new CameraVerticalStateResolver();
        forwardReferenceSolver = new CameraForwardReferenceSolver();
        rigComposer            = new CameraRigComposer();

        cameraComponent = GetComponent<Camera>();
        if (cameraComponent == null)
            cameraComponent = GetComponentInChildren<Camera>();

        if (target != null)
            currentLookAheadTarget = target.position;

        Vector3 initialForward = ResolveSphereForward();
        forwardReferenceSolver.Initialize(initialForward);

        if (cameraComponent != null && config != null && config.DynamicFovEnabled)
        {
            currentFov = config.BaseFov;
            cameraComponent.fieldOfView = currentFov;
        }
    }

    private void LateUpdate()
    {
        if (target == null || config == null)
            return;

        UpdateTimers();

        CameraVerticalState verticalState = verticalStateResolver.Resolve(
            isRespawnTransitionActive,
            verticalStateIgnoreTimer > 0f,
            targetRigidbody,
            movementMotor,
            config);

        // Usa velocidad real cuando la bola se mueve para evitar inversión de cámara.
        Vector3 sphereForward = ResolveSphereForward();

        cachedReferenceForward = forwardReferenceSolver.UpdateReferenceForward(
            sphereForward,
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

    #endregion

    #region Public API

    public void BeginRespawnTransition()
    {
        if (target == null || config == null)
            return;

        posVelocityX      = 0f;
        posVelocityY      = 0f;
        posVelocityZ      = 0f;
        lookAheadVelocity = Vector3.zero;

        verticalStateIgnoreTimer  = config.VerticalStateIgnoreDurationAfterRespawn;
        isRespawnTransitionActive = true;
        respawnTransitionTimer    = 0f;

        Vector3 currentForward = ResolveSphereForward();
        forwardReferenceSolver.SnapToForward(currentForward);
        verticalStateResolver.Reset();

        if (target != null)
            currentLookAheadTarget = target.position;
    }

    #endregion

    #region Forward Resolution

    /// <summary>
    /// Resuelve la dirección de referencia de la esfera.
    /// Prioriza la dirección de velocidad real cuando la bola se mueve,
    /// garantizando que la cámara siempre quede detrás independientemente
    /// de cómo esté inicializado el controlador de rotación.
    /// </summary>
    private Vector3 ResolveSphereForward()
    {
        if (targetRigidbody != null)
        {
            Vector3 velocity = targetRigidbody.linearVelocity;
            velocity.y = 0f;

            if (velocity.magnitude >= velocityForwardThreshold)
                return velocity.normalized;
        }

        if (rotationController != null)
            return rotationController.CurrentForward;

        return Vector3.forward;
    }

    #endregion

    #region Dynamic Distance

    private CameraRigPose ApplyDynamicDistance(CameraRigPose pose)
    {
        if (!config.DynamicDistanceEnabled || movementMotor == null)
            return pose;

        float speedNorm = movementMotor.MaxSpeed > 0f
            ? Mathf.Clamp01(movementMotor.CurrentPlanarVelocity / movementMotor.MaxSpeed)
            : 0f;

        currentExtraDistance = Mathf.SmoothDamp(
            currentExtraDistance,
            config.ExtraDistanceAtMaxSpeed * speedNorm,
            ref extraDistanceVelocity,
            config.DistanceSmoothTime);

        if (currentExtraDistance < 0.01f)
            return pose;

        Vector3 flatForward = cachedReferenceForward;
        flatForward.y = 0f;

        if (flatForward.sqrMagnitude <= 0.0001f)
            return pose;

        return new CameraRigPose(
            pose.Position - flatForward.normalized * currentExtraDistance,
            pose.Rotation);
    }

    #endregion

    #region Look-Ahead

    private CameraRigPose ApplyLookAhead(CameraRigPose pose)
    {
        if (config.LookAheadDistance <= 0.001f || movementMotor == null)
            return pose;

        float speedNorm = movementMotor.MaxSpeed > 0f
            ? Mathf.Clamp01(movementMotor.CurrentPlanarVelocity / movementMotor.MaxSpeed)
            : 0f;

        Vector3 rawTarget = target.position
            + config.NormalLookAtOffset
            + cachedReferenceForward * (config.LookAheadDistance * speedNorm);

        currentLookAheadTarget = Vector3.SmoothDamp(
            currentLookAheadTarget, rawTarget,
            ref lookAheadVelocity, config.LookAheadSmoothTime);

        Vector3 lookDir = currentLookAheadTarget - transform.position;

        if (lookDir.sqrMagnitude < 0.001f)
            return pose;

        return new CameraRigPose(
            pose.Position,
            Quaternion.LookRotation(lookDir.normalized, Vector3.up));
    }

    #endregion

    #region Position

    private void ApplyPosition(Vector3 desiredPosition, bool useRespawnSmoothing)
    {
        float xzSmooth = useRespawnSmoothing
            ? config.RespawnHorizontalSmoothTime
            : config.HorizontalSmoothTime;

        float ySmooth = useRespawnSmoothing
            ? config.RespawnVerticalSmoothTime
            : config.VerticalSmoothTime;

        Vector3 current = transform.position;

        transform.position = new Vector3(
            Mathf.SmoothDamp(current.x, desiredPosition.x, ref posVelocityX, xzSmooth),
            Mathf.SmoothDamp(current.y, desiredPosition.y, ref posVelocityY, ySmooth),
            Mathf.SmoothDamp(current.z, desiredPosition.z, ref posVelocityZ, xzSmooth));
    }

    #endregion

    #region Rotation

    private void ApplyRotation(Quaternion desiredRotation, bool useRespawnSmoothing)
    {
        float speed = useRespawnSmoothing
            ? config.RespawnRotationLerpSpeed
            : config.RotationLerpSpeed;

        transform.rotation = Quaternion.Slerp(
            transform.rotation, desiredRotation,
            speed * Time.deltaTime);
    }

    #endregion

    #region Dynamic FOV

    private void UpdateDynamicFov()
    {
        if (cameraComponent == null || !config.DynamicFovEnabled || movementMotor == null)
            return;

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

    #region Timers y Respawn

    private void UpdateTimers()
    {
        if (verticalStateIgnoreTimer > 0f)
            verticalStateIgnoreTimer = Mathf.Max(0f, verticalStateIgnoreTimer - Time.deltaTime);

        if (isRespawnTransitionActive)
            respawnTransitionTimer += Time.deltaTime;
    }

    private void UpdateRespawnTransitionState(CameraRigPose desiredPose)
    {
        if (!isRespawnTransitionActive || config == null)
            return;

        if (respawnTransitionTimer < config.MinimumRespawnTransitionDuration)
            return;

        if (Vector3.Distance(transform.position, desiredPose.Position) <= config.RespawnPositionTolerance
            && Quaternion.Angle(transform.rotation, desiredPose.Rotation) <= config.RespawnRotationTolerance)
        {
            isRespawnTransitionActive = false;
            posVelocityX = 0f;
            posVelocityY = 0f;
            posVelocityZ = 0f;
        }
    }

    #endregion
}