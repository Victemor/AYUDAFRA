using UnityEngine;

/// <summary>
/// Motor de locomoción de la esfera basado en impulsos discretos.
/// Mantiene la velocidad planar, aplica steering, frenado, fricción y respuestas físicas controladas.
/// Soporta dos sistemas de input: swipe (impulsos discretos) y joystick (mantenimiento continuo).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public sealed class BallMovementMotor : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]

    [SerializeField]
    [Tooltip("Rigidbody de la pelota.")]
    private Rigidbody rb;

    [SerializeField]
    [Tooltip("Controlador de rotación. Provee el forward actual de movimiento.")]
    private SphereRotationController rotationController;

    [SerializeField]
    [Tooltip("Acumulador de impulsos de avance.")]
    private ImpulseAccumulator impulseAccumulator;

    [SerializeField]
    [Tooltip("Acumulador de frenado.")]
    private BrakeAccumulator brakeAccumulator;

    [SerializeField]
    [Tooltip("Sensor de suelo.")]
    private BallGroundSensor groundSensor;

    [SerializeField]
    [Tooltip("Sistema de respuesta a colisiones.")]
    private BallCollisionResponder collisionResponder;

    [Header("Velocidad")]

    [SerializeField]
    [Tooltip("Velocidad máxima en m/s.")]
    private float maxSpeed = 12f;

    [SerializeField]
    [Tooltip("Factor de control horizontal en el aire.")]
    [Range(0f, 1f)]
    private float airControlFactor = 0.4f;

    [Header("Fricción pasiva")]

    [SerializeField]
    [Tooltip("Desaceleración en m/s² aplicada de forma pasiva cuando no hay input.")]
    private float passiveFriction = 0.8f;

    [SerializeField]
    [Tooltip("Velocidad mínima por debajo de la cual la pelota se detiene completamente.")]
    private float stopThreshold = 0.08f;

    [Header("Slope Handling")]

    [SerializeField]
    [Tooltip("Multiplicador de velocidad al subir pendiente.")]
    [Range(0.1f, 1f)]
    private float uphillSpeedFactor = 0.72f;

    [SerializeField]
    [Tooltip("Multiplicador de velocidad al bajar pendiente.")]
    [Range(1f, 3f)]
    private float downhillSpeedFactor = 1.2f;

    [SerializeField]
    [Tooltip("Fuerza de adhesión al suelo en pendientes.")]
    private float groundStickForce = 28f;

    [SerializeField]
    [Tooltip("Velocidad vertical máxima positiva estando en el suelo.")]
    private float maxGroundedUpwardVelocity = 2.5f;

    [SerializeField]
    [Tooltip("Velocidad vertical mínima al bajar en el suelo.")]
    private float minimumGroundedDownwardVelocity = -12f;

    [Header("Impacto")]

    [SerializeField]
    [Tooltip("Duración del estado de retroceso por colisión con obstáculos normales.")]
    private float postImpactRecoveryDuration = 0.1f;

    [SerializeField]
    [Tooltip("Velocidad de disipación del retroceso.")]
    private float impactVelocityDecay = 16f;

    [Header("Barreras")]

    [SerializeField]
    [Tooltip("Tiempo durante el cual se ignoran nuevos rebotes de barrera para evitar doble contacto.")]
    private float barrierBounceCooldownDuration = 0.08f;

    [SerializeField]
    [Tooltip("Tiempo durante el cual se bloquea el steering después de rebotar contra una barrera.")]
    private float barrierSteeringLockDuration = 0.06f;

    [SerializeField]
    [Tooltip("Velocidad mínima conservada al rebotar contra una barrera.")]
    private float minimumBarrierBounceSpeed = 0.25f;

    [SerializeField]
    [Tooltip("Velocidad máxima de separación de barrera suprimida durante deslizamiento sostenido.")]
    private float barrierSeparationDampSpeed = 2f;

    [Header("Deslizamiento lateral")]

    [SerializeField]
    [Tooltip("Factor de deslizamiento lateral al estar bloqueado por una pared.")]
    [Range(0f, 1f)]
    private float blockedSlideFactor = 0.9f;

    [Header("Steering")]

    [SerializeField]
    [Tooltip("Velocidad en grados/s a la que la velocidad planar sigue al forward actual.")]
    private float steeringDegreesPerSecond = 240f;

    #endregion

    #region Runtime

    private float pendingImpulse;
    private float pendingBrake;
    private bool hasNewImpulse;
    private bool hasNewBrake;

    private float impactRecoveryTimer;
    private Vector3 impactVelocity;

    private bool isForceStopping;
    private float forcedStopDeceleration;

    private float speedBoostMultiplier = 1f;
    private float jumpBypassTimer;

    private float barrierBounceCooldownTimer;
    private float barrierSteeringLockTimer;

    // Joystick
    private bool maintainCurrentSpeed;
    private float joystickBrakeDeceleration;
    private float maintainedSpeedTarget;

    #endregion

    #region Properties

    /// <summary>Velocidad planar actual de la pelota.</summary>
    public float CurrentSpeed => GetPlanarVelocity(rb.linearVelocity).magnitude;

    /// <summary>Velocidad planar actual. Alias de compatibilidad.</summary>
    public float CurrentPlanarVelocity => CurrentSpeed;

    /// <summary>Velocidad completa del Rigidbody.</summary>
    public Vector3 CurrentVelocity => rb.linearVelocity;

    /// <summary>Velocidad máxima configurada.</summary>
    public float MaxSpeed => maxSpeed;

    /// <summary>Indica si el motor está procesando un impacto de obstáculo normal.</summary>
    public bool IsRecoveringFromImpact => impactRecoveryTimer > 0f;

    /// <summary>Última dirección válida de movimiento.</summary>
    public Vector3 LastValidMoveDirection =>
        rotationController != null ? rotationController.CurrentForward : Vector3.forward;

    /// <summary>Indica si puede procesar otro rebote contra barrera.</summary>
    public bool CanProcessBarrierBounce => barrierBounceCooldownTimer <= 0f;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        rb                 = GetComponent<Rigidbody>();
        rotationController = GetComponent<SphereRotationController>();
        impulseAccumulator = GetComponent<ImpulseAccumulator>();
        brakeAccumulator   = GetComponent<BrakeAccumulator>();
        groundSensor       = GetComponent<BallGroundSensor>();
        collisionResponder = GetComponent<BallCollisionResponder>();
    }

    private void Awake()
    {
        if (rb == null)                 rb                 = GetComponent<Rigidbody>();
        if (rotationController == null) rotationController = GetComponent<SphereRotationController>();
        if (impulseAccumulator == null) impulseAccumulator = GetComponent<ImpulseAccumulator>();
        if (brakeAccumulator == null)   brakeAccumulator   = GetComponent<BrakeAccumulator>();
        if (groundSensor == null)       groundSensor       = GetComponent<BallGroundSensor>();
        if (collisionResponder == null) collisionResponder = GetComponent<BallCollisionResponder>();

        rb.interpolation          = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints |= RigidbodyConstraints.FreezeRotationX;
        rb.constraints |= RigidbodyConstraints.FreezeRotationY;
        rb.constraints |= RigidbodyConstraints.FreezeRotationZ;
    }

    private void OnEnable()
    {
        if (impulseAccumulator != null)
            impulseAccumulator.OnImpulseReady += HandleImpulseReady;

        if (brakeAccumulator != null)
            brakeAccumulator.OnBrakeReady += HandleBrakeReady;
    }

    private void OnDisable()
    {
        if (impulseAccumulator != null)
            impulseAccumulator.OnImpulseReady -= HandleImpulseReady;

        if (brakeAccumulator != null)
            brakeAccumulator.OnBrakeReady -= HandleBrakeReady;
    }

    private void FixedUpdate()
    {
        TickTimers();

        groundSensor?.RefreshGroundState();

        UpdateImpactRecovery();
        NotifyAccumulators();

        if (IsRecoveringFromImpact)
        {
            ApplyImpactVelocity();
            ApplyGroundAdhesion();
            ClampGroundedVerticalVelocity();
            return;
        }

        if (isForceStopping)
        {
            ApplyForcedStop();
            ApplyGroundAdhesion();
            ClampGroundedVerticalVelocity();
            return;
        }

        ApplyPendingImpulse();
        ApplySteering();
        ApplyPendingBrake();
        ApplyJoystickBrakeForce();
        ApplyPassiveFriction();
        ApplyGroundAdhesion();
        ClampGroundedVerticalVelocity();
        SuppressBarrierSeparationDrift();
        EnforceSpeedMaintenance();
    }

    #endregion

    #region Event Handlers

    private void HandleImpulseReady(float impulse)
    {
        pendingImpulse += impulse;
        hasNewImpulse   = true;
        brakeAccumulator?.ResetConsecutive();
    }

    private void HandleBrakeReady(float brakeForce)
    {
        pendingBrake += brakeForce;
        hasNewBrake   = true;
        impulseAccumulator?.ResetConsecutive();
    }

    #endregion

    #region Public API — Control

    /// <summary>Detiene completamente la pelota y limpia todo el estado.</summary>
    public void Stop()
    {
        maintainedSpeedTarget      = 0f;
        pendingImpulse             = 0f;
        pendingBrake               = 0f;
        hasNewImpulse              = false;
        hasNewBrake                = false;
        impactRecoveryTimer        = 0f;
        impactVelocity             = Vector3.zero;
        isForceStopping            = false;
        forcedStopDeceleration     = 0f;
        barrierBounceCooldownTimer = 0f;
        barrierSteeringLockTimer   = 0f;
        maintainCurrentSpeed       = false;
        joystickBrakeDeceleration  = 0f;

        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        impulseAccumulator?.ResetConsecutive();
        brakeAccumulator?.ResetConsecutive();
    }

    /// <summary>Teleporta la pelota y resetea el estado de movimiento.</summary>
    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        Stop();

        rb.position = position;
        rb.rotation = rotation.normalized;

        rotationController?.SnapToRotation(rotation);
        groundSensor?.RefreshGroundStateImmediate();

        Physics.SyncTransforms();
    }

    /// <summary>Escala la velocidad planar actual por un factor.</summary>
    public void MultiplySpeed(float multiplier)
    {
        float clamped     = Mathf.Clamp01(multiplier);
        Vector3 velocity  = rb.linearVelocity;
        velocity.x       *= clamped;
        velocity.z       *= clamped;
        rb.linearVelocity = velocity;
    }

    /// <summary>Aplica retroceso temporal por colisión con obstáculo normal.</summary>
    public void ApplyImpactRecoil(Vector3 recoilVelocity)
    {
        recoilVelocity.y = 0f;

        if (recoilVelocity.magnitude > maxSpeed)
            recoilVelocity = recoilVelocity.normalized * maxSpeed;

        impactVelocity      = recoilVelocity;
        impactRecoveryTimer = postImpactRecoveryDuration;
        pendingImpulse      = 0f;
        pendingBrake        = 0f;
        hasNewImpulse       = false;
        hasNewBrake         = false;

        Vector3 current   = rb.linearVelocity;
        rb.linearVelocity = new Vector3(recoilVelocity.x, current.y, recoilVelocity.z);
    }

    /// <summary>Aplica rebote contra barrera conservando la velocidad planar.</summary>
    public void ApplyBarrierBounce(Vector3 bounceDirection, float preservedPlanarSpeed)
    {
        if (!CanProcessBarrierBounce)
            return;

        bounceDirection.y = 0f;

        if (bounceDirection.sqrMagnitude < 0.0001f)
            return;

        bounceDirection.Normalize();

        float resolvedSpeed = Mathf.Max(minimumBarrierBounceSpeed, preservedPlanarSpeed);
        Vector3 current     = rb.linearVelocity;
        Vector3 newPlanar   = bounceDirection * resolvedSpeed;

        rb.linearVelocity  = new Vector3(newPlanar.x, current.y, newPlanar.z);
        rb.angularVelocity = Vector3.zero;

        barrierBounceCooldownTimer = barrierBounceCooldownDuration;
        barrierSteeringLockTimer   = barrierSteeringLockDuration;

        pendingImpulse = 0f;
        pendingBrake   = 0f;
        hasNewImpulse  = false;
        hasNewBrake    = false;
    }

    /// <summary>Compatibilidad con llamadas anteriores de deflexión.</summary>
    public void ApplyBarrierDeflect(Vector3 deflectedVelocity)
    {
        Vector3 direction = deflectedVelocity;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        ApplyBarrierBounce(direction.normalized, direction.magnitude);
    }

    /// <summary>Suspende la tracción durante un tiempo sin retroceso.</summary>
    public void SuppressDrive(float duration)
    {
        impactRecoveryTimer = Mathf.Max(impactRecoveryTimer, duration);
        pendingImpulse      = 0f;
        pendingBrake        = 0f;
        hasNewImpulse       = false;
        hasNewBrake         = false;
    }

    /// <summary>Activa el freno forzado.</summary>
    public void BeginForcedStop(float deceleration)
    {
        isForceStopping        = true;
        forcedStopDeceleration = Mathf.Max(0f, deceleration);
        impactRecoveryTimer    = 0f;
        impactVelocity         = Vector3.zero;
        pendingImpulse         = 0f;
        pendingBrake           = 0f;
    }

    /// <summary>Desactiva el freno forzado.</summary>
    public void EndForcedStop()
    {
        isForceStopping        = false;
        forcedStopDeceleration = 0f;
    }

    #endregion

    #region Public API — Joystick

    /// <summary>
    /// Activa o desactiva el mantenimiento de velocidad por joystick.
    /// Cuando está activo, la fricción pasiva no se aplica.
    /// </summary>
    public void SetSpeedMaintenance(bool active)
    {
        if (active && !maintainCurrentSpeed)
        {
            // Al activar: capturar la velocidad actual como línea base
            maintainedSpeedTarget = CurrentSpeed;
        }
        else if (!active)
        {
            maintainedSpeedTarget = 0f;
        }

        maintainCurrentSpeed = active;
    }

    /// <summary>
    /// Configura la desaceleración continua del freno por joystick.
    /// 0 = sin freno. Llamar con 0 para liberar el freno.
    /// </summary>
    public void SetJoystickBrake(float deceleration)
    {
        joystickBrakeDeceleration = Mathf.Max(0f, deceleration);
    }

    /// <summary>
    /// Aplica un impulso de arranque desde el joystick.
    /// Usado cuando la pelota está detenida y el joystick se empuja hacia adelante.
    /// </summary>
    public void ApplyJoystickKickstart(float impulse)
    {
        if (impulse <= 0f)
            return;

        Vector3 forward         = GetMovementForward();
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 planarVelocity  = GetPlanarVelocity(currentVelocity);
        Vector3 newPlanar       = planarVelocity + forward * impulse;

        float effectiveMax = maxSpeed * speedBoostMultiplier;

        if (newPlanar.magnitude > effectiveMax)
            newPlanar = newPlanar.normalized * effectiveMax;

        rb.linearVelocity = new Vector3(newPlanar.x, currentVelocity.y, newPlanar.z);
    }

    #endregion

    #region Public API — Power-Ups

    /// <summary>Aplica impulso vertical instantáneo.</summary>
    public void ApplyJump(float jumpForce)
    {
        Vector3 velocity  = rb.linearVelocity;
        velocity.y        = Mathf.Max(velocity.y, jumpForce);
        rb.linearVelocity = velocity;
        jumpBypassTimer   = 0.4f;
    }

    /// <summary>Activa el multiplicador de velocidad máxima.</summary>
    public void SetSpeedBoostMultiplier(float multiplier)
    {
        speedBoostMultiplier = Mathf.Max(1f, multiplier);
    }

    /// <summary>Elimina el multiplicador de velocidad.</summary>
    public void ClearSpeedBoost()
    {
        speedBoostMultiplier = 1f;
    }

    #endregion

    #region Internal Physics

    /// <summary>
    /// Cuando el joystick mantiene velocidad, restaura la velocidad planar si la física
    /// (fricción de material, contacto con suelo) la redujo por debajo del objetivo.
    /// El objetivo solo sube (swipe puede dar más velocidad), nunca baja mientras
    /// el dedo está presionado.
    /// </summary>
    private void EnforceSpeedMaintenance()
    {
        if (!maintainCurrentSpeed)
            return;

        Vector3 velocity      = rb.linearVelocity;
        Vector3 planar        = GetPlanarVelocity(velocity);
        float   currentSpeed  = planar.magnitude;

        // Si la velocidad subió (por swipe u otro impulso), actualizar el objetivo hacia arriba
        if (currentSpeed > maintainedSpeedTarget)
        {
            maintainedSpeedTarget = currentSpeed;
            return;
        }

        // Si la física redujo la velocidad por debajo del objetivo, restaurarla
        if (maintainedSpeedTarget > stopThreshold && planar.sqrMagnitude > 0.0001f)
        {
            planar            = planar.normalized * maintainedSpeedTarget;
            rb.linearVelocity = new Vector3(planar.x, velocity.y, planar.z);
        }
    }

    private void TickTimers()
    {
        if (jumpBypassTimer > 0f)
            jumpBypassTimer = Mathf.Max(0f, jumpBypassTimer - Time.fixedDeltaTime);

        if (barrierBounceCooldownTimer > 0f)
            barrierBounceCooldownTimer = Mathf.Max(0f, barrierBounceCooldownTimer - Time.fixedDeltaTime);

        if (barrierSteeringLockTimer > 0f)
            barrierSteeringLockTimer = Mathf.Max(0f, barrierSteeringLockTimer - Time.fixedDeltaTime);
    }

    private void NotifyAccumulators()
    {
        float currentSpeed = CurrentSpeed;
        float currentMax   = maxSpeed * speedBoostMultiplier;

        impulseAccumulator?.NotifyCurrentSpeed(currentSpeed, currentMax);
        brakeAccumulator?.NotifyCurrentSpeed(currentSpeed);
    }

    private void UpdateImpactRecovery()
    {
        if (impactRecoveryTimer <= 0f)
        {
            impactVelocity = Vector3.zero;
            return;
        }

        impactRecoveryTimer = Mathf.Max(0f, impactRecoveryTimer - Time.fixedDeltaTime);

        if (impactVelocity.sqrMagnitude <= 0.0001f)
        {
            impactVelocity = Vector3.zero;
            return;
        }

        float nextMagnitude = Mathf.MoveTowards(
            impactVelocity.magnitude, 0f,
            impactVelocityDecay * Time.fixedDeltaTime);

        impactVelocity = impactVelocity.normalized * nextMagnitude;
    }

    private void ApplyImpactVelocity()
    {
        Vector3 current   = rb.linearVelocity;
        rb.linearVelocity = new Vector3(impactVelocity.x, current.y, impactVelocity.z);
    }

    private void ApplyPendingImpulse()
    {
        if (!hasNewImpulse || pendingImpulse <= 0f)
            return;

        hasNewImpulse = false;

        Vector3 forward         = GetMovementForward();
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 planarVelocity  = GetPlanarVelocity(currentVelocity);
        Vector3 newPlanar       = planarVelocity + forward * pendingImpulse;

        float effectiveMax = maxSpeed * speedBoostMultiplier;
        float slopeMax     = ResolveSlopeAdjustedMax(forward, effectiveMax);

        if (newPlanar.magnitude > slopeMax)
            newPlanar = newPlanar.normalized * slopeMax;

        newPlanar = ResolveBlockedVelocity(newPlanar);

        rb.linearVelocity = new Vector3(newPlanar.x, currentVelocity.y, newPlanar.z);
        pendingImpulse    = 0f;
    }

    private void ApplyPendingBrake()
    {
        if (!hasNewBrake || pendingBrake <= 0f)
            return;

        hasNewBrake = false;

        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 planarVelocity  = GetPlanarVelocity(currentVelocity);
        float currentSpeed      = planarVelocity.magnitude;

        if (currentSpeed <= stopThreshold)
        {
            pendingBrake = 0f;
            return;
        }

        float newSpeed    = Mathf.Max(stopThreshold, currentSpeed - pendingBrake);
        Vector3 newPlanar = planarVelocity.normalized * newSpeed;
        rb.linearVelocity = new Vector3(newPlanar.x, currentVelocity.y, newPlanar.z);
        pendingBrake      = 0f;
    }

    /// <summary>
    /// Aplica el freno continuo del joystick, proporcional al eje Y negativo.
    /// Frena hasta llegar a 0 si se mantiene.
    /// </summary>
    private void ApplyJoystickBrakeForce()
    {
        if (joystickBrakeDeceleration <= 0f)
            return;

        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 planarVelocity  = GetPlanarVelocity(currentVelocity);
        float currentSpeed      = planarVelocity.magnitude;

        if (currentSpeed <= stopThreshold)
        {
            rb.linearVelocity = new Vector3(0f, currentVelocity.y, 0f);
            return;
        }

        float newSpeed    = Mathf.MoveTowards(currentSpeed, 0f, joystickBrakeDeceleration * Time.fixedDeltaTime);
        Vector3 newPlanar = planarVelocity.normalized * newSpeed;
        rb.linearVelocity = new Vector3(newPlanar.x, currentVelocity.y, newPlanar.z);
    }

    /// <summary>
    /// Aplica fricción pasiva cuando no hay input activo.
    /// Se omite cuando el joystick mantiene la velocidad.
    /// </summary>
    private void ApplyPassiveFriction()
    {
        // El joystick puede mantener la velocidad actual: en ese caso
        // no aplicar fricción pasiva para que la pelota no desacelere.
        if (maintainCurrentSpeed)
            return;

        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 planarVelocity  = GetPlanarVelocity(currentVelocity);
        float currentSpeed      = planarVelocity.magnitude;

        if (currentSpeed <= stopThreshold)
        {
            rb.linearVelocity = new Vector3(0f, currentVelocity.y, 0f);
            return;
        }

        float newSpeed    = Mathf.MoveTowards(currentSpeed, 0f, passiveFriction * Time.fixedDeltaTime);
        Vector3 newPlanar = planarVelocity.normalized * newSpeed;
        rb.linearVelocity = new Vector3(newPlanar.x, currentVelocity.y, newPlanar.z);
    }

    private void ApplyForcedStop()
    {
        Vector3 velocity       = rb.linearVelocity;
        Vector3 planarVelocity = GetPlanarVelocity(velocity);

        Vector3 nextPlanar = Vector3.MoveTowards(
            planarVelocity, Vector3.zero,
            forcedStopDeceleration * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(nextPlanar.x, velocity.y, nextPlanar.z);
    }

    private void ApplyGroundAdhesion()
    {
        if (groundSensor == null || !groundSensor.IsGrounded) return;
        if (IsRecoveringFromImpact || isForceStopping) return;

        rb.AddForce(-groundSensor.GroundNormal * groundStickForce, ForceMode.Acceleration);
    }

    private void ClampGroundedVerticalVelocity()
    {
        if (jumpBypassTimer > 0f) return;
        if (groundSensor == null || !groundSensor.IsGrounded) return;
        if (IsRecoveringFromImpact) return;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = Mathf.Clamp(
            velocity.y,
            minimumGroundedDownwardVelocity,
            maxGroundedUpwardVelocity);

        rb.linearVelocity = velocity;
    }

    private void ApplySteering()
    {
        if (barrierSteeringLockTimer > 0f)
            return;

        // Durante contacto sostenido con barrera, no aplicar steering.
        // Evita el loop de oscilación entre steering y física de separación.
        if (barrierBounceCooldownTimer <= 0f
            && collisionResponder != null
            && collisionResponder.HasAnyContact)
            return;

        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 planarVelocity  = GetPlanarVelocity(currentVelocity);
        float   speed           = planarVelocity.magnitude;

        if (speed <= stopThreshold)
            return;

        Vector3 targetForward = rotationController != null
            ? rotationController.CurrentForward
            : Vector3.forward;

        targetForward.y = 0f;

        if (targetForward.sqrMagnitude < 0.0001f)
            return;

        targetForward.Normalize();

        Vector3 currentDirection = planarVelocity / speed;

        Vector3 steeredDirection = Vector3.RotateTowards(
            currentDirection,
            targetForward,
            steeringDegreesPerSecond * Mathf.Deg2Rad * Time.fixedDeltaTime,
            0f);

        Vector3 newPlanar = steeredDirection * speed;
        rb.linearVelocity = new Vector3(newPlanar.x, currentVelocity.y, newPlanar.z);
    }

    /// <summary>
    /// Suprime micro-velocidades de separación de barrera durante deslizamiento sostenido.
    /// Evita el wobble de cámara causado por CapsuleColliders superpuestos.
    /// </summary>
    private void SuppressBarrierSeparationDrift()
    {
        if (barrierBounceCooldownTimer > 0f)
            return;

        if (collisionResponder == null || !collisionResponder.HasAnyContact)
            return;

        Vector3 contactNormal = collisionResponder.AnyContactNormal;
        contactNormal.y = 0f;

        if (contactNormal.sqrMagnitude < 0.0001f)
            return;

        contactNormal.Normalize();

        Vector3 velocity = rb.linearVelocity;
        Vector3 planar   = GetPlanarVelocity(velocity);

        float awayComponent = Vector3.Dot(planar, contactNormal);

        if (awayComponent > 0f && awayComponent <= barrierSeparationDampSpeed)
        {
            planar           -= contactNormal * awayComponent;
            rb.linearVelocity = new Vector3(planar.x, velocity.y, planar.z);
        }
    }

    #endregion

    #region Helpers

    private Vector3 GetMovementForward()
    {
        Vector3 forward = rotationController != null
            ? rotationController.CurrentForward
            : transform.forward;

        if (groundSensor != null && groundSensor.IsGrounded)
        {
            Vector3 slopedForward = groundSensor.GetProjectedForward(forward);

            if (slopedForward.sqrMagnitude > 0.0001f)
                return slopedForward.normalized;
        }

        forward.y = 0f;

        return forward.sqrMagnitude < 0.0001f
            ? Vector3.forward
            : forward.normalized;
    }

    private float ResolveSlopeAdjustedMax(Vector3 movementDirection, float effectiveMax)
    {
        float verticalComponent = movementDirection.y;

        if (verticalComponent > 0f)
        {
            return effectiveMax * Mathf.Lerp(
                1f, uphillSpeedFactor,
                Mathf.Clamp01(verticalComponent));
        }

        if (verticalComponent < 0f)
        {
            float downhill = effectiveMax * Mathf.Lerp(
                1f, downhillSpeedFactor,
                Mathf.Clamp01(-verticalComponent));
            return Mathf.Min(downhill, effectiveMax * downhillSpeedFactor);
        }

        return effectiveMax;
    }

    private Vector3 ResolveBlockedVelocity(Vector3 desiredVelocity)
    {
        if (collisionResponder == null || !collisionResponder.HasBlockingContact)
            return desiredVelocity;

        Vector3 normal = collisionResponder.BlockingNormal;
        normal.y = 0f;

        if (normal.sqrMagnitude < 0.0001f)
            return desiredVelocity;

        normal.Normalize();

        Vector3 desiredPlanar = GetPlanarVelocity(desiredVelocity);
        float intoWall        = Vector3.Dot(desiredPlanar, -normal);

        if (intoWall <= 0f)
            return desiredVelocity;

        Vector3 sliding = (desiredPlanar - (-normal * intoWall)) * blockedSlideFactor;

        return sliding.sqrMagnitude < 0.0001f
            ? new Vector3(0f, desiredVelocity.y, 0f)
            : new Vector3(sliding.x, desiredVelocity.y, sliding.z);
    }

    private static Vector3 GetPlanarVelocity(Vector3 velocity)
    {
        velocity.y = 0f;
        return velocity;
    }

    #endregion
}