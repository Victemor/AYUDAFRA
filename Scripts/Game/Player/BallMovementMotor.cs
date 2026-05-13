using UnityEngine;

/// <summary>
/// Motor de locomoción de la esfera basado en impulsos discretos.
/// </summary>
[DefaultExecutionOrder(-15)]
[RequireComponent(typeof(Rigidbody))]
public sealed class BallMovementMotor : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]
    [SerializeField][Tooltip("Rigidbody de la pelota.")]
    private Rigidbody rb;
    [SerializeField][Tooltip("Controlador de rotación. Provee el forward actual de movimiento.")]
    private SphereRotationController rotationController;
    [SerializeField][Tooltip("Acumulador de impulsos de avance.")]
    private ImpulseAccumulator impulseAccumulator;
    [SerializeField][Tooltip("Acumulador de frenado.")]
    private BrakeAccumulator brakeAccumulator;
    [SerializeField][Tooltip("Sensor de suelo.")]
    private BallGroundSensor groundSensor;
    [SerializeField][Tooltip("Sistema de respuesta a colisiones.")]
    private BallCollisionResponder collisionResponder;

    [Header("Velocidad")]
    [SerializeField][Tooltip("Velocidad máxima en m/s.")]
    private float maxSpeed = 12f;
    [SerializeField][Tooltip("Factor de control horizontal en el aire.")][Range(0f, 1f)]
    private float airControlFactor = 0.4f;

    [Header("Fricción pasiva")]
    [SerializeField][Tooltip("Desaceleración en m/s² aplicada de forma pasiva cuando no hay input.")]
    private float passiveFriction = 0.8f;
    [SerializeField][Tooltip("Velocidad mínima por debajo de la cual la pelota se detiene completamente.")]
    private float stopThreshold = 0.08f;

    [Header("Slope Handling")]
    [SerializeField][Tooltip("Multiplicador de velocidad al subir pendiente.")][Range(0.1f, 1f)]
    private float uphillSpeedFactor = 0.72f;
    [SerializeField][Tooltip("Multiplicador de velocidad al bajar pendiente.")][Range(1f, 3f)]
    private float downhillSpeedFactor = 1.2f;
    [SerializeField][Tooltip("Fuerza de adhesión al suelo en pendientes.")]
    private float groundStickForce = 28f;
    [SerializeField][Tooltip("Velocidad vertical máxima positiva estando en el suelo.")]
    private float maxGroundedUpwardVelocity = 2.5f;
    [SerializeField][Tooltip("Velocidad vertical mínima al bajar en el suelo.")]
    private float minimumGroundedDownwardVelocity = -12f;

    [Header("Impacto")]
    [SerializeField][Tooltip("Duración del estado de retroceso por colisión con obstáculos normales.")]
    private float postImpactRecoveryDuration = 0.1f;
    [SerializeField][Tooltip("Velocidad de disipación del retroceso.")]
    private float impactVelocityDecay = 16f;

    [Header("Barreras")]
    [SerializeField]
    [Tooltip("Tiempo en segundos durante el cual se bloquea todo el steering y se ignoran nuevos rebotes " +
             "tras un choque. Permite vuelo libre post-rebote sin que el steering interfiera. " +
             "MÍNIMO RECOMENDADO: 0.08. Cero rompe el sistema.")]
    private float barrierBounceCooldownDuration = 0.1f;

    [SerializeField][Tooltip("Velocidad mínima conservada al rebotar contra una barrera.")]
    private float minimumBarrierBounceSpeed = 0.25f;

    [Header("Deslizamiento lateral")]
    [SerializeField][Tooltip("Factor de deslizamiento lateral al estar bloqueado por una pared.")][Range(0f, 1f)]
    private float blockedSlideFactor = 0.9f;

    [Header("Steering")]
    [SerializeField][Tooltip("Velocidad máxima en grados/s a la que la velocidad planar sigue al forward actual.")]
    private float steeringDegreesPerSecond = 240f;
    [SerializeField]
    [Tooltip("Ángulo de diferencia (grados) a partir del cual se aplica el 100% de steeringDegreesPerSecond. " +
             "15 grados = curva suave sin ser restrictiva para giros intencionales.")]
    [Range(5f, 90f)]
    private float steeringAngleForFullRate = 15f;
    [SerializeField]
    [Tooltip("Exponente de la curva de respuesta del steering. 1=lineal, 1.5=suave (recomendado), 2=cuadrático.")]
    [Range(1f, 3f)]
    private float steeringResponseExponent = 1.5f;

    #endregion

    #region Runtime

    private float pendingImpulse;
    private float pendingBrake;
    private bool  hasNewImpulse;
    private bool  hasNewBrake;

    private float   impactRecoveryTimer;
    private Vector3 impactVelocity;

    private bool  isForceStopping;
    private float forcedStopDeceleration;

    private float speedBoostMultiplier    = 1f;
    private float jumpBypassTimer;
    private float barrierBounceCooldownTimer;

    private bool  maintainCurrentSpeed;
    private float joystickBrakeDeceleration;
    private float maintainedSpeedTarget;

    #endregion

    #region Properties

    public float   CurrentSpeed          => GetPlanarVelocity(rb.linearVelocity).magnitude;
    public float   CurrentPlanarVelocity => CurrentSpeed;
    public Vector3 CurrentVelocity       => rb.linearVelocity;
    public float   MaxSpeed              => maxSpeed;
    public bool    IsRecoveringFromImpact => impactRecoveryTimer > 0f;
    public Vector3 LastValidMoveDirection =>
        rotationController != null ? rotationController.CurrentForward : Vector3.forward;
    public bool CanProcessBarrierBounce  => barrierBounceCooldownTimer <= 0f;

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
        if (impulseAccumulator != null) impulseAccumulator.OnImpulseReady += HandleImpulseReady;
        if (brakeAccumulator   != null) brakeAccumulator.OnBrakeReady    += HandleBrakeReady;
    }

    private void OnDisable()
    {
        if (impulseAccumulator != null) impulseAccumulator.OnImpulseReady -= HandleImpulseReady;
        if (brakeAccumulator   != null) brakeAccumulator.OnBrakeReady    -= HandleBrakeReady;
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
        EnforceSpeedMaintenance();
    }

    private void OnValidate()
    {
        // Garantiza que el cooldown nunca sea 0 accidentalmente en el Inspector.
        // Con 0, el steering se reactiva en el mismo frame del rebote y vuelve a
        // empujar la bola contra la barrera.
        barrierBounceCooldownDuration = Mathf.Max(0.08f, barrierBounceCooldownDuration);
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
        maintainCurrentSpeed       = false;
        joystickBrakeDeceleration  = 0f;

        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        impulseAccumulator?.ResetConsecutive();
        brakeAccumulator?.ResetConsecutive();
    }

    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        Stop();
        rb.position = position;
        rb.rotation = rotation.normalized;
        rotationController?.SnapToRotation(rotation);
        groundSensor?.RefreshGroundStateImmediate();
        Physics.SyncTransforms();
    }

    public void MultiplySpeed(float multiplier)
    {
        float clamped     = Mathf.Clamp01(multiplier);
        Vector3 v         = rb.linearVelocity;
        v.x              *= clamped;
        v.z              *= clamped;
        rb.linearVelocity = v;
    }

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

        Vector3 c         = rb.linearVelocity;
        rb.linearVelocity = new Vector3(recoilVelocity.x, c.y, recoilVelocity.z);
    }

    public void ApplyBarrierBounce(Vector3 bounceDirection, float bounceSpeed)
    {
        if (!CanProcessBarrierBounce) return;

        bounceDirection.y = 0f;
        if (bounceDirection.sqrMagnitude < 0.0001f) return;
        bounceDirection.Normalize();

        float speed       = Mathf.Max(minimumBarrierBounceSpeed, bounceSpeed);
        Vector3 c         = rb.linearVelocity;
        rb.linearVelocity = new Vector3(bounceDirection.x * speed, c.y, bounceDirection.z * speed);
        rb.angularVelocity = Vector3.zero;

        // Resetear el target de velocidad mantenida al valor post-rebote.
        // Sin esto, EnforceSpeedMaintenance restaura la velocidad pre-impacto y
        // la bola vuelve a empujar contra la barrera a alta velocidad.
        maintainedSpeedTarget = speed;

        barrierBounceCooldownTimer = Mathf.Max(0.08f, barrierBounceCooldownDuration);
        pendingImpulse             = 0f;
        pendingBrake               = 0f;
        hasNewImpulse              = false;
        hasNewBrake                = false;
    }

    public void ApplyBarrierDeflect(Vector3 deflectedVelocity)
    {
        Vector3 d = deflectedVelocity;
        d.y = 0f;
        if (d.sqrMagnitude < 0.0001f) return;
        ApplyBarrierBounce(d.normalized, d.magnitude);
    }

    public void SuppressDrive(float duration)
    {
        impactRecoveryTimer = Mathf.Max(impactRecoveryTimer, duration);
        pendingImpulse      = 0f;
        pendingBrake        = 0f;
        hasNewImpulse       = false;
        hasNewBrake         = false;
    }

    public void BeginForcedStop(float deceleration)
    {
        isForceStopping        = true;
        forcedStopDeceleration = Mathf.Max(0f, deceleration);
        impactRecoveryTimer    = 0f;
        impactVelocity         = Vector3.zero;
        pendingImpulse         = 0f;
        pendingBrake           = 0f;
    }

    public void EndForcedStop()
    {
        isForceStopping        = false;
        forcedStopDeceleration = 0f;
    }

    #endregion

    #region Public API — Joystick

    public void SetSpeedMaintenance(bool active)
    {
        if (active && !maintainCurrentSpeed) maintainedSpeedTarget = CurrentSpeed;
        else if (!active)                    maintainedSpeedTarget = 0f;
        maintainCurrentSpeed = active;
    }

    public void SetJoystickBrake(float deceleration)
    {
        joystickBrakeDeceleration = Mathf.Max(0f, deceleration);
    }

    public void ApplyJoystickKickstart(float impulse)
    {
        if (impulse <= 0f) return;
        Vector3 fwd   = GetMovementForward();
        Vector3 v     = rb.linearVelocity;
        Vector3 p     = GetPlanarVelocity(v) + fwd * impulse;
        float max     = maxSpeed * speedBoostMultiplier;
        if (p.magnitude > max) p = p.normalized * max;
        rb.linearVelocity = new Vector3(p.x, v.y, p.z);
    }

    #endregion

    #region Public API — Power-Ups

    public void ApplyJump(float jumpForce)
    {
        Vector3 v     = rb.linearVelocity;
        v.y           = Mathf.Max(v.y, jumpForce);
        rb.linearVelocity = v;
        jumpBypassTimer   = 0.4f;
    }

    public void SetSpeedBoostMultiplier(float multiplier) => speedBoostMultiplier = Mathf.Max(1f, multiplier);
    public void ClearSpeedBoost()                          => speedBoostMultiplier = 1f;

    #endregion

    #region Internal Physics

    private void EnforceSpeedMaintenance()
    {
        if (!maintainCurrentSpeed) return;
        Vector3 v    = rb.linearVelocity;
        Vector3 p    = GetPlanarVelocity(v);
        float speed  = p.magnitude;

        if (speed > maintainedSpeedTarget) { maintainedSpeedTarget = speed; return; }
        if (maintainedSpeedTarget > stopThreshold && p.sqrMagnitude > 0.0001f)
        {
            p                 = p.normalized * maintainedSpeedTarget;
            rb.linearVelocity = new Vector3(p.x, v.y, p.z);
        }
    }

    private void TickTimers()
    {
        if (jumpBypassTimer > 0f)
            jumpBypassTimer = Mathf.Max(0f, jumpBypassTimer - Time.fixedDeltaTime);
        if (barrierBounceCooldownTimer > 0f)
            barrierBounceCooldownTimer = Mathf.Max(0f, barrierBounceCooldownTimer - Time.fixedDeltaTime);
    }

    private void NotifyAccumulators()
    {
        float s = CurrentSpeed;
        float m = maxSpeed * speedBoostMultiplier;
        impulseAccumulator?.NotifyCurrentSpeed(s, m);
        brakeAccumulator?.NotifyCurrentSpeed(s);
    }

    private void UpdateImpactRecovery()
    {
        if (impactRecoveryTimer <= 0f) { impactVelocity = Vector3.zero; return; }
        impactRecoveryTimer = Mathf.Max(0f, impactRecoveryTimer - Time.fixedDeltaTime);
        if (impactVelocity.sqrMagnitude <= 0.0001f) { impactVelocity = Vector3.zero; return; }
        float n = Mathf.MoveTowards(impactVelocity.magnitude, 0f, impactVelocityDecay * Time.fixedDeltaTime);
        impactVelocity = impactVelocity.normalized * n;
    }

    private void ApplyImpactVelocity()
    {
        Vector3 c = rb.linearVelocity;
        rb.linearVelocity = new Vector3(impactVelocity.x, c.y, impactVelocity.z);
    }

    private void ApplyPendingImpulse()
    {
        if (!hasNewImpulse || pendingImpulse <= 0f) return;
        hasNewImpulse = false;

        Vector3 fwd = GetMovementForward();

        // Cuando hay bloqueo de barrera, proyectar el forward sobre el plano de la barrera
        // antes de aplicar el impulso. Sin esto, el swipe añade velocidad INTO la barrera,
        // ResolveBlockedVelocity la cancela y la bola no se mueve.
        // Con la proyección, el swipe acelera paralelo a la barrera permitiendo el escape.
        if (collisionResponder != null && collisionResponder.HasBlockingContact)
        {
            Vector3 blockNormal = collisionResponder.BlockingNormal;
            blockNormal.y = 0f;
            if (blockNormal.sqrMagnitude > 0.0001f)
            {
                blockNormal.Normalize();
                Vector3 projectedFwd = Vector3.ProjectOnPlane(fwd, blockNormal);
                projectedFwd.y = 0f;
                if (projectedFwd.sqrMagnitude > 0.01f)
                    fwd = projectedFwd.normalized;
            }
        }
        Vector3 v    = rb.linearVelocity;
        Vector3 p    = GetPlanarVelocity(v) + fwd * pendingImpulse;
        float eMax   = maxSpeed * speedBoostMultiplier;
        float sMax   = ResolveSlopeAdjustedMax(fwd, eMax);

        if (p.magnitude > sMax) p = p.normalized * sMax;
        p                 = ResolveBlockedVelocity(p);
        rb.linearVelocity = new Vector3(p.x, v.y, p.z);
        pendingImpulse    = 0f;
    }

    private void ApplyPendingBrake()
    {
        if (!hasNewBrake || pendingBrake <= 0f) return;
        hasNewBrake = false;

        Vector3 v   = rb.linearVelocity;
        Vector3 p   = GetPlanarVelocity(v);
        float speed = p.magnitude;
        if (speed <= stopThreshold) { pendingBrake = 0f; return; }

        float ns = Mathf.Max(stopThreshold, speed - pendingBrake);
        rb.linearVelocity = new Vector3(p.normalized.x * ns, v.y, p.normalized.z * ns);
        pendingBrake = 0f;
    }

    private void ApplyJoystickBrakeForce()
    {
        if (joystickBrakeDeceleration <= 0f) return;
        Vector3 v   = rb.linearVelocity;
        Vector3 p   = GetPlanarVelocity(v);
        float speed = p.magnitude;
        if (speed <= stopThreshold) { rb.linearVelocity = new Vector3(0f, v.y, 0f); return; }
        float ns = Mathf.MoveTowards(speed, 0f, joystickBrakeDeceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(p.normalized.x * ns, v.y, p.normalized.z * ns);
    }

    private void ApplyPassiveFriction()
    {
        if (maintainCurrentSpeed) return;
        Vector3 v   = rb.linearVelocity;
        Vector3 p   = GetPlanarVelocity(v);
        float speed = p.magnitude;
        if (speed <= stopThreshold) { rb.linearVelocity = new Vector3(0f, v.y, 0f); return; }
        float ns = Mathf.MoveTowards(speed, 0f, passiveFriction * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(p.normalized.x * ns, v.y, p.normalized.z * ns);
    }

    private void ApplyForcedStop()
    {
        Vector3 v  = rb.linearVelocity;
        Vector3 p  = GetPlanarVelocity(v);
        Vector3 np = Vector3.MoveTowards(p, Vector3.zero, forcedStopDeceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(np.x, v.y, np.z);
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
        Vector3 v = rb.linearVelocity;
        v.y = Mathf.Clamp(v.y, minimumGroundedDownwardVelocity, maxGroundedUpwardVelocity);
        rb.linearVelocity = v;
    }

    /// <summary>
    /// Rota la velocidad planar hacia el forward objetivo del jugador.
    ///
    /// Diseño deliberadamente simple: NO hay lógica especial de contacto con barrera.
    /// Con MeshCollider, el motor de física maneja el deslizamiento naturalmente —
    /// la componente INTO la barrera se cancela por la resolución de contacto de Unity,
    /// y la componente paralela produce el deslizamiento limpio.
    /// Cualquier intento de "proyectar" el forward manualmente generaba atrapamiento.
    ///
    /// El único bloqueo permitido es durante barrierBounceCooldownTimer para dar
    /// vuelo libre post-rebote sin que el steering re-dirija hacia la barrera.
    /// </summary>
    private void ApplySteering()
    {
        if (barrierBounceCooldownTimer > 0f)
            return;

        Vector3 v    = rb.linearVelocity;
        Vector3 p    = GetPlanarVelocity(v);
        float speed  = p.magnitude;

        if (speed <= stopThreshold) return;

        Vector3 targetFwd = rotationController != null
            ? rotationController.CurrentForward
            : Vector3.forward;

        targetFwd.y = 0f;
        if (targetFwd.sqrMagnitude < 0.0001f) return;
        targetFwd.Normalize();

        Vector3 currentDir    = p / speed;
        float angleDelta      = Vector3.Angle(currentDir, targetFwd);
        float normalizedAngle = Mathf.Clamp01(angleDelta / steeringAngleForFullRate);
        float effectiveRate   = steeringDegreesPerSecond * Mathf.Pow(normalizedAngle, steeringResponseExponent);

        Vector3 steered    = Vector3.RotateTowards(currentDir, targetFwd,
            effectiveRate * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);

        // Eliminar la componente INTO la barrera del resultado del steering.
        // ResolveBlockedVelocity ya existe y funciona con impulsos — la aplicamos
        // aquí también para que el steering nunca empuje la bola dentro de la barrera.
        // Cuando HasBlockingContact = false (sin contacto), devuelve el vector sin cambios.
        Vector3 steeredVelocity = ResolveBlockedVelocity(new Vector3(steered.x * speed, v.y, steered.z * speed));
        rb.linearVelocity = steeredVelocity;
    }

    #endregion

    #region Helpers

    private Vector3 GetMovementForward()
    {
        Vector3 fwd = rotationController != null ? rotationController.CurrentForward : transform.forward;
        if (groundSensor != null && groundSensor.IsGrounded)
        {
            Vector3 s = groundSensor.GetProjectedForward(fwd);
            if (s.sqrMagnitude > 0.0001f) return s.normalized;
        }
        fwd.y = 0f;
        return fwd.sqrMagnitude < 0.0001f ? Vector3.forward : fwd.normalized;
    }

    private float ResolveSlopeAdjustedMax(Vector3 dir, float max)
    {
        float v = dir.y;
        if (v > 0f) return max * Mathf.Lerp(1f, uphillSpeedFactor,   Mathf.Clamp01(v));
        if (v < 0f) return Mathf.Min(max * Mathf.Lerp(1f, downhillSpeedFactor, Mathf.Clamp01(-v)),
                                     max * downhillSpeedFactor);
        return max;
    }

    private Vector3 ResolveBlockedVelocity(Vector3 desired)
    {
        if (collisionResponder == null || !collisionResponder.HasBlockingContact) return desired;
        Vector3 n = collisionResponder.BlockingNormal;
        n.y = 0f;
        if (n.sqrMagnitude < 0.0001f) return desired;
        n.Normalize();

        Vector3 dp   = GetPlanarVelocity(desired);
        float into   = Vector3.Dot(dp, -n);
        if (into <= 0f) return desired;

        Vector3 slide = (dp - (-n * into)) * blockedSlideFactor;
        return slide.sqrMagnitude < 0.0001f
            ? new Vector3(0f, desired.y, 0f)
            : new Vector3(slide.x, desired.y, slide.z);
    }

    private static Vector3 GetPlanarVelocity(Vector3 v) { v.y = 0f; return v; }

    #endregion
}