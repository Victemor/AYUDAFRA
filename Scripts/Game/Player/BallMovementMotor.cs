using UnityEngine;

/// <summary>
/// Motor de locomoción de la esfera basado en impulsos discretos.
///
/// Responsabilidades:
/// - Aplicar impulsos de avance y frenado recibidos desde los acumuladores.
/// - Girar gradualmente la velocidad planar hacia el forward del jugador (steering).
/// - Aplicar fricción pasiva, adhesión al suelo y manejo de pendientes.
/// - Proyectar steering e impulsos sobre el plano de la barrera cuando hay contacto frontal,
///   usando la información de <see cref="BallCollisionResponder.HasBlockingContact"/>.
///
/// Límite de responsabilidad — lo que este motor NO hace:
/// El rebote contra barreras laterales es manejado por PhysicMaterials en Unity.
/// Este motor no sobreescribe rb.linearVelocity al detectar una barrera; solo modula
/// el input del jugador para que el steering "deslice" sobre ella.
/// </summary>
[DefaultExecutionOrder(-15)]
[RequireComponent(typeof(Rigidbody))]
public sealed class BallMovementMotor : MonoBehaviour
{
    // ─── Private State Structs ───────────────────────────────────────────────────

    /// <summary>
    /// Estado del retroceso por colisión con obstáculo.
    /// Agrupa timer y velocidad para que FixedUpdate pueda interrogar el estado
    /// en un solo lugar sin multiplicar flags paralelos.
    /// </summary>
    private struct ImpactState
    {
        public float   RecoveryTimer;
        public Vector3 Velocity;

        /// <summary><c>true</c> mientras el timer de recuperación está activo.</summary>
        public bool IsActive => RecoveryTimer > 0f;

        /// <summary>Valor por defecto: sin impacto activo.</summary>
        public static readonly ImpactState Default = new ImpactState
        {
            RecoveryTimer = 0f,
            Velocity      = Vector3.zero,
        };
    }

    /// <summary>
    /// Estado de la detención forzada al llegar a la meta.
    /// Agrupa el flag de activación y la deceleración para evitar booleans paralelos.
    /// </summary>
    private struct ForcedStopState
    {
        public bool  IsActive;
        public float Deceleration;

        /// <summary>Valor por defecto: sin detención forzada activa.</summary>
        public static readonly ForcedStopState Default = new ForcedStopState
        {
            IsActive     = false,
            Deceleration = 0f,
        };
    }

    // ─── Inspector ───────────────────────────────────────────────────────────────

    #region Inspector

    [Header("Referencias")]
    [SerializeField]
    [Tooltip("Rigidbody de la pelota.")]
    private Rigidbody rb;

    [SerializeField]
    [Tooltip("Controlador de rotación lógica de la pelota.")]
    private SphereRotationController rotationController;

    [SerializeField]
    [Tooltip("Acumulador de impulsos de avance.")]
    private ImpulseAccumulator impulseAccumulator;

    [SerializeField]
    [Tooltip("Acumulador de frenado.")]
    private BrakeAccumulator brakeAccumulator;

    [SerializeField]
    [Tooltip("Sensor que detecta si la bola está en el suelo y la normal del suelo.")]
    private BallGroundSensor groundSensor;

    [SerializeField]
    [Tooltip("Responde a colisiones con obstáculos y expone el estado de bloqueo frontal.")]
    private BallCollisionResponder collisionResponder;

    [Header("Velocidad")]
    [SerializeField]
    [Tooltip("Velocidad máxima planar en m/s.")]
    private float maxSpeed = 12f;

    [SerializeField]
    [Tooltip("Factor de control horizontal cuando la bola está en el aire. " +
             "0 = sin control aéreo. 1 = control completo.")]
    [Range(0f, 1f)]
    private float airControlFactor = 0.4f;

    [Header("Fricción pasiva")]
    [SerializeField]
    [Tooltip("Desaceleración en m/s² aplicada cada frame cuando no hay input activo.")]
    private float passiveFriction = 0.8f;

    [SerializeField]
    [Tooltip("Velocidad planar mínima por debajo de la cual la bola se considera detenida.")]
    private float stopThreshold = 0.08f;

    [Header("Slope Handling")]
    [SerializeField]
    [Tooltip("Multiplicador de velocidad máxima al subir pendiente. " +
             "La velocidad nunca supera maxSpeed * este valor en subida.")]
    [Range(0.1f, 1f)]
    private float uphillSpeedFactor = 0.72f;

    [SerializeField]
    [Tooltip("Multiplicador de velocidad máxima al bajar pendiente.")]
    [Range(1f, 3f)]
    private float downhillSpeedFactor = 1.2f;

    [SerializeField]
    [Tooltip("Fuerza de adhesión al suelo aplicada mientras la bola está en contacto con él. " +
             "Evita que despegue en pendientes y curvas del track.")]
    private float groundStickForce = 28f;

    [SerializeField]
    [Tooltip("Velocidad vertical máxima positiva permitida mientras la bola está en el suelo. " +
             "Limita saltos involuntarios en rampas.")]
    private float maxGroundedUpwardVelocity = 2.5f;

    [SerializeField]
    [Tooltip("Velocidad vertical mínima permitida mientras la bola está en el suelo. " +
             "Evita que la bola flote al bajar pendientes pronunciadas.")]
    private float minimumGroundedDownwardVelocity = -12f;

    [Header("Impacto")]
    [SerializeField]
    [Tooltip("Tiempo en segundos durante el que el retroceso por obstáculo bloquea el input. " +
             "Mantenerlo bajo (≤ 0.15s) para que el jugador no perciba pérdida de control.")]
    private float postImpactRecoveryDuration = 0.1f;

    [SerializeField]
    [Tooltip("Velocidad de disipación del vector de retroceso en m/s². " +
             "Valores altos disipan el retroceso rápidamente.")]
    private float impactVelocityDecay = 16f;

    [Header("Post-Barrera")]
    [SerializeField]
    [Tooltip("Desaceleración en m/s² aplicada tras un choque con barrera. " +
             "Reemplaza la fricción pasiva normal mientras awaitingInputAfterBarrier está activo. " +
             "Valores altos (≥ 30) detienen la bola en 1-2 frames, evitando que la cámara " +
             "siga la dirección del rebote en lugar de la cara principal de la pelota. " +
             "Recomendado: 40–80.")]
    private float barrierBounceDeceleration = 50f;

    [Header("Deslizamiento lateral")]
    [SerializeField]
    [Tooltip("Factor aplicado a la componente de deslizamiento cuando la bola está bloqueada por una pared. " +
             "1.0 = deslizamiento completo (la bola se mueve a velocidad plena a lo largo de la pared). " +
             "0.7 = deslizamiento reducido (algo de velocidad se pierde contra la pared).")]
    [Range(0f, 1f)]
    private float blockedSlideFactor = 0.9f;

    [Header("Steering")]
    [SerializeField]
    [Tooltip("Velocidad máxima en grados/s a la que la dirección de velocidad planar " +
             "sigue al forward lógico del jugador.")]
    private float steeringDegreesPerSecond = 240f;

    [SerializeField]
    [Tooltip("Diferencia angular en grados a partir de la cual se aplica el 100% de steeringDegreesPerSecond. " +
             "Ángulos menores aplican una fracción proporcional (curva suave).")]
    [Range(5f, 90f)]
    private float steeringAngleForFullRate = 15f;

    [SerializeField]
    [Tooltip("Exponente de la curva de respuesta del steering. " +
             "1 = lineal. 1.5 = suave en el centro, más agresivo en los extremos.")]
    [Range(1f, 3f)]
    private float steeringResponseExponent = 1.5f;

    #endregion

    #region Runtime

    private float pendingImpulse;
    private float pendingBrake;
    private bool  hasNewImpulse;
    private bool  hasNewBrake;

    private ImpactState     impactState;
    private ForcedStopState forcedStopState;

    private float speedBoostMultiplier   = 1f;
    private float jumpBypassTimer;

    private bool  maintainCurrentSpeed;
    private float maintainedSpeedTarget;
    private float joystickBrakeDeceleration;

    /// <summary>
    /// Bloquea steering e impulsos tras un choque con barrera.
    /// El rebote del PhysicMaterial decae libremente por fricción pasiva.
    /// Se libera únicamente cuando el jugador da input explícito (swipe o kickstart).
    /// </summary>
    private bool awaitingInputAfterBarrier;

    /// <summary>Multiplicador sobre passiveFriction aplicado por el power-up ReducedInertia.</summary>
    private float inertiaFrictionMultiplier    = 1f;

    /// <summary>Multiplicador sobre groundStickForce aplicado por el power-up ReducedInertia.</summary>
    private float inertiaGroundStickMultiplier = 1f;

    #endregion

    #region Properties

    /// <summary>Velocidad planar actual en m/s.</summary>
    public float   CurrentSpeed          => GetPlanarVelocity(rb.linearVelocity).magnitude;

    /// <summary>Alias de <see cref="CurrentSpeed"/>. Mantenido por compatibilidad.</summary>
    public float   CurrentPlanarVelocity => CurrentSpeed;

    /// <summary>Velocidad 3D completa del Rigidbody.</summary>
    public Vector3 CurrentVelocity       => rb.linearVelocity;

    /// <summary>Velocidad máxima configurada sin boost activo.</summary>
    public float   MaxSpeed              => maxSpeed;

    /// <summary><c>true</c> mientras hay un retroceso por impacto activo.</summary>
    public bool    IsRecoveringFromImpact => impactState.IsActive;

    /// <summary>
    /// Última dirección de movimiento válida del jugador.
    /// Usado por <see cref="BallCollisionResponder"/> como fallback de dirección cuando la velocidad es baja.
    /// </summary>
    public Vector3 LastValidMoveDirection =>
        rotationController != null ? rotationController.CurrentForward : Vector3.forward;

    /// <summary>
    /// Indica si la bola está esperando input explícito después de chocar con una barrera.
    /// La cámara puede usar este estado para ignorar velocidad residual de rebote.
    /// </summary>
    public bool IsAwaitingInputAfterBarrier => awaitingInputAfterBarrier;

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
        rb.constraints           |= RigidbodyConstraints.FreezeRotationX;
        rb.constraints           |= RigidbodyConstraints.FreezeRotationY;
        rb.constraints           |= RigidbodyConstraints.FreezeRotationZ;
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

        if (impactState.IsActive)
        {
            ApplyImpactVelocity();
            ApplyGroundAdhesion();
            ClampGroundedVerticalVelocity();
            return;
        }

        if (forcedStopState.IsActive)
        {
            ApplyForcedStop();
            ApplyGroundAdhesion();
            ClampGroundedVerticalVelocity();
            return;
        }

        // Impulso y steering solo corren con input explícito del jugador.
        // Tras un choque con barrera, awaitingInputAfterBarrier bloquea ambos
        // para que la bola no vuelva a avanzar sola con la inercia post-rebote.
        if (!awaitingInputAfterBarrier)
        {
            ApplyPendingImpulse();
            ApplySteering();
        }

        ApplyPendingBrake();
        ApplyJoystickBrakeForce();
        ApplyPassiveFriction();
        ApplyGroundAdhesion();
        ClampGroundedVerticalVelocity();
        EnforceSpeedMaintenance();
    }

    #endregion

    #region Event Handlers

    private void HandleImpulseReady(float impulse)
    {
        if (awaitingInputAfterBarrier)
            CancelPlanarVelocity();

        awaitingInputAfterBarrier = false;
        pendingImpulse           += impulse;
        hasNewImpulse             = true;
        brakeAccumulator?.ResetConsecutive();
    }

    private void HandleBrakeReady(float brakeForce)
    {
        if (awaitingInputAfterBarrier)
            CancelPlanarVelocity();

        awaitingInputAfterBarrier = false;
        pendingBrake             += brakeForce;
        hasNewBrake               = true;
        impulseAccumulator?.ResetConsecutive();
    }

    #endregion

    #region Public API — Control

    /// <summary>
    /// Detiene la bola inmediatamente y limpia todos los estados pendientes.
    /// Llamado antes de cualquier teleport o respawn.
    /// </summary>
    public void Stop()
    {
        pendingImpulse            = 0f;
        pendingBrake              = 0f;
        hasNewImpulse             = false;
        hasNewBrake               = false;
        impactState               = ImpactState.Default;
        forcedStopState           = ForcedStopState.Default;
        maintainCurrentSpeed      = false;
        maintainedSpeedTarget     = 0f;
        joystickBrakeDeceleration = 0f;
        awaitingInputAfterBarrier = false;

        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        impulseAccumulator?.ResetConsecutive();
        brakeAccumulator?.ResetConsecutive();
    }

    /// <summary>
    /// Notifica al motor que la bola acaba de chocar con una barrera.
    ///
    /// Efectos:
    /// - Limpia impulsos y frenos pendientes.
    /// - Desactiva el mantenimiento de velocidad (evita que el joystick mantenga
    ///   la velocidad hacia la barrera después del rebote).
    /// - Activa <c>awaitingInputAfterBarrier</c>, que bloquea <c>ApplySteering</c>
    ///   y <c>ApplyPendingImpulse</c> hasta que el jugador dé input explícito.
    ///
    /// El rebote del PhysicMaterial sigue vigente — este método no toca
    ///   <c>rb.linearVelocity</c>. La fricción pasiva lo decelerará naturalmente.
    /// Llamado por <see cref="BallCollisionResponder.CancelIntoWallVelocity"/>.
    /// </summary>
    public void NotifyBarrierHit()
    {
        pendingImpulse            = 0f;
        pendingBrake              = 0f;
        hasNewImpulse             = false;
        hasNewBrake               = false;
        maintainCurrentSpeed      = false;
        maintainedSpeedTarget     = 0f;
        awaitingInputAfterBarrier = true;
    }

    /// <summary>
    /// Teleporta la bola a una posición y rotación, reseteando todo el estado de movimiento.
    /// </summary>
    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        Stop();
        rb.position = position;
        rb.rotation = rotation.normalized;
        rotationController?.SnapToRotation(rotation);
        groundSensor?.RefreshGroundStateImmediate();
        Physics.SyncTransforms();
    }

    /// <summary>
    /// Multiplica la velocidad planar actual por el multiplicador dado.
    /// El multiplicador se clampea a [0, 1]: no puede aumentar velocidad, solo reducirla.
    /// </summary>
    public void MultiplySpeed(float multiplier)
    {
        float   c = Mathf.Clamp01(multiplier);
        Vector3 v = rb.linearVelocity;
        v.x              *= c;
        v.z              *= c;
        rb.linearVelocity = v;
    }

    /// <summary>
    /// Aplica un retroceso de velocidad por colisión con obstáculo inamovible.
    /// Activa el estado de impact recovery que bloquea el input temporalmente.
    /// </summary>
    public void ApplyImpactRecoil(Vector3 recoilVelocity)
    {
        recoilVelocity.y = 0f;
        if (recoilVelocity.magnitude > maxSpeed)
            recoilVelocity = recoilVelocity.normalized * maxSpeed;

        impactState = new ImpactState
        {
            RecoveryTimer = postImpactRecoveryDuration,
            Velocity      = recoilVelocity,
        };

        pendingImpulse = 0f;
        pendingBrake   = 0f;
        hasNewImpulse  = false;
        hasNewBrake    = false;

        Vector3 c         = rb.linearVelocity;
        rb.linearVelocity = new Vector3(recoilVelocity.x, c.y, recoilVelocity.z);
    }

    /// <summary>
    /// Suprime temporalmente el drive del jugador sin aplicar retroceso.
    /// Útil en colisiones que solo deben interrumpir el impulso activo.
    /// </summary>
    public void SuppressDrive(float duration)
    {
        impactState = new ImpactState
        {
            RecoveryTimer = Mathf.Max(impactState.RecoveryTimer, duration),
            Velocity      = impactState.Velocity,
        };

        pendingImpulse = 0f;
        pendingBrake   = 0f;
        hasNewImpulse  = false;
        hasNewBrake    = false;
    }

    /// <summary>
    /// Inicia una detención forzada con la deceleración dada.
    /// Usado por <c>BallRespawnController</c> al llegar a la meta.
    /// </summary>
    public void BeginForcedStop(float deceleration)
    {
        forcedStopState = new ForcedStopState
        {
            IsActive     = true,
            Deceleration = Mathf.Max(0f, deceleration),
        };

        impactState    = ImpactState.Default;
        pendingImpulse = 0f;
        pendingBrake   = 0f;
    }

    /// <summary>
    /// Cancela la detención forzada y devuelve el control al jugador.
    /// </summary>
    public void EndForcedStop()
    {
        forcedStopState = ForcedStopState.Default;
    }

    /// <summary>
    /// Aplica multiplicadores de inertia reduction desde un power-up.
    /// Reduce la fricción pasiva y la adhesión al suelo durante el efecto activo.
    /// Llamado por <see cref="BallPowerUpController"/> al recolectar ReducedInertia.
    /// </summary>
    /// <param name="frictionMult">Multiplicador sobre passiveFriction [0–1]. 0.3 = 70% menos fricción.</param>
    /// <param name="groundStickMult">Multiplicador sobre groundStickForce [0–1]. 0.4 = 60% menos adhesión.</param>
    public void SetInertiaReduction(float frictionMult, float groundStickMult)
    {
        inertiaFrictionMultiplier    = Mathf.Clamp01(frictionMult);
        inertiaGroundStickMultiplier = Mathf.Clamp01(groundStickMult);
    }

    /// <summary>
    /// Revierte los multiplicadores de inertia reduction a sus valores neutros.
    /// Llamado por <see cref="BallPowerUpController"/> al expirar el efecto.
    /// </summary>
    public void ClearInertiaReduction()
    {
        inertiaFrictionMultiplier    = 1f;
        inertiaGroundStickMultiplier = 1f;
    }

    #endregion

    #region Public API — Jump

    /// <summary>
    /// Aplica un impulso vertical. Usa <c>Mathf.Max</c> para no cancelar velocidad vertical
    /// positiva preexistente (por ejemplo, al pisar un JumpPad mientras se sube una rampa).
    /// El bypass timer evita que <see cref="ClampGroundedVerticalVelocity"/> anule el salto
    /// en el mismo frame.
    /// </summary>
    public void ApplyJump(float jumpForce)
    {
        Vector3 v         = rb.linearVelocity;
        v.y               = Mathf.Max(v.y, jumpForce);
        rb.linearVelocity = v;
        jumpBypassTimer   = 0.4f;
    }

    #endregion

    #region Public API — Joystick

    /// <summary>
    /// Activa o desactiva el mantenimiento de velocidad.
    /// Cuando está activo, la fricción pasiva no reduce la velocidad por debajo del valor capturado.
    /// </summary>
    public void SetSpeedMaintenance(bool active)
    {
        if (active && !maintainCurrentSpeed)
            maintainedSpeedTarget = CurrentSpeed;
        else if (!active)
            maintainedSpeedTarget = 0f;

        maintainCurrentSpeed = active;
    }

    /// <summary>Aplica una deceleración continua por joystick atrás.</summary>
    public void SetJoystickBrake(float deceleration)
    {
        joystickBrakeDeceleration = Mathf.Max(0f, deceleration);
    }

    /// <summary>Cancela la deceleración continua por joystick.</summary>
    public void ClearJoystickBrake()
    {
        joystickBrakeDeceleration = 0f;
    }

    /// <summary>
    /// Aplica un impulso directo en la dirección del forward actual sin pasar por el acumulador.
    /// Usado por <c>JoystickMovementController</c> para el arranque desde cero y por
    /// <c>SwipeDirectionRouter</c> para el micro-kick de redirección con swipe.
    /// No supera el techo de velocidad efectivo (maxSpeed × speedBoostMultiplier).
    /// </summary>
    public void ApplyJoystickKickstart(float impulse)
    {
        if (awaitingInputAfterBarrier)
            CancelPlanarVelocity();

        awaitingInputAfterBarrier = false;
        if (impulse <= 0f) return;
        Vector3 fwd = GetMovementForward();
        Vector3 v   = rb.linearVelocity;
        Vector3 p   = GetPlanarVelocity(v) + fwd * impulse;
        float   max = maxSpeed * speedBoostMultiplier;
        if (p.magnitude > max) p = p.normalized * max;
        rb.linearVelocity = new Vector3(p.x, v.y, p.z);
    }

    #endregion

    #region Public API — Speed Boost

    /// <summary>
    /// Aumenta el techo de velocidad por el multiplicador dado.
    /// Llamado por <c>SpeedBoostZone</c> al entrar a la zona.
    /// </summary>
    public void SetSpeedBoostMultiplier(float multiplier)
    {
        speedBoostMultiplier = Mathf.Max(1f, multiplier);
    }

    /// <summary>
    /// Restaura el techo de velocidad al valor base.
    /// Llamado por <c>SpeedBoostZone</c> al salir de la zona.
    /// </summary>
    public void ClearSpeedBoost()
    {
        speedBoostMultiplier = 1f;
    }

    #endregion

    #region Private — FixedUpdate Pipeline

    private void TickTimers()
    {
        if (jumpBypassTimer > 0f)
            jumpBypassTimer = Mathf.Max(0f, jumpBypassTimer - Time.fixedDeltaTime);
    }

    private void NotifyAccumulators()
    {
        float speed    = CurrentSpeed;
        float maxEffective = maxSpeed * speedBoostMultiplier;
        impulseAccumulator?.NotifyCurrentSpeed(speed, maxEffective);
        brakeAccumulator?.NotifyCurrentSpeed(speed);
    }

    private void UpdateImpactRecovery()
    {
        if (!impactState.IsActive)
        {
            impactState = ImpactState.Default;
            return;
        }

        float   newTimer = Mathf.Max(0f, impactState.RecoveryTimer - Time.fixedDeltaTime);
        Vector3 newVel   = impactState.Velocity;

        if (newVel.sqrMagnitude > 0.0001f)
        {
            float n = Mathf.MoveTowards(newVel.magnitude, 0f, impactVelocityDecay * Time.fixedDeltaTime);
            newVel  = newVel.normalized * n;
        }
        else
        {
            newVel = Vector3.zero;
        }

        impactState = new ImpactState { RecoveryTimer = newTimer, Velocity = newVel };
    }

    private void ApplyImpactVelocity()
    {
        Vector3 c         = rb.linearVelocity;
        rb.linearVelocity = new Vector3(impactState.Velocity.x, c.y, impactState.Velocity.z);
    }

    /// <summary>
    /// Aplica el impulso pendiente en la dirección del forward actual.
    /// Si la bola está bloqueada por una pared, el forward se proyecta sobre el plano
    /// de la barrera para que el swipe produzca deslizamiento en lugar de presión contra la pared.
    /// </summary>
    private void ApplyPendingImpulse()
    {
        if (!hasNewImpulse || pendingImpulse <= 0f) return;
        hasNewImpulse = false;

        Vector3 fwd = GetMovementForward();

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
        float   eMax = maxSpeed * speedBoostMultiplier;
        float   sMax = ResolveSlopeAdjustedMax(fwd, eMax);

        if (p.magnitude > sMax) p = p.normalized * sMax;

        p                 = ResolveBlockedVelocity(p);
        rb.linearVelocity = new Vector3(p.x, v.y, p.z);
        pendingImpulse    = 0f;
    }

    /// <summary>
    /// Rota gradualmente la dirección de velocidad planar hacia el forward del jugador.
    /// La velocidad se proyecta sobre el plano de la pared si hay bloqueo frontal activo,
    /// lo que convierte el steering en deslizamiento en lugar de presión contra la barrera.
    /// </summary>
    private void ApplySteering()
    {
        Vector3 v    = rb.linearVelocity;
        Vector3 p    = GetPlanarVelocity(v);
        float   speed = p.magnitude;
        if (speed <= stopThreshold) return;

        Vector3 targetFwd = rotationController != null
            ? rotationController.CurrentForward
            : Vector3.forward;
        targetFwd.y = 0f;
        if (targetFwd.sqrMagnitude < 0.0001f) return;
        targetFwd.Normalize();

        Vector3 currentDir    = p / speed;
        float   angleDelta    = Vector3.Angle(currentDir, targetFwd);
        float   normAngle     = Mathf.Clamp01(angleDelta / steeringAngleForFullRate);
        float   effectiveRate = steeringDegreesPerSecond * Mathf.Pow(normAngle, steeringResponseExponent);

        Vector3 steered   = Vector3.RotateTowards(
            currentDir, targetFwd,
            effectiveRate * Mathf.Deg2Rad * Time.fixedDeltaTime,
            0f);

        Vector3 newVel    = ResolveBlockedVelocity(new Vector3(steered.x * speed, v.y, steered.z * speed));
        rb.linearVelocity = newVel;
    }

    private void ApplyPendingBrake()
    {
        if (!hasNewBrake || pendingBrake <= 0f) return;
        hasNewBrake = false;

        Vector3 v     = rb.linearVelocity;
        Vector3 p     = GetPlanarVelocity(v);
        float   speed = p.magnitude;
        if (speed <= stopThreshold) { pendingBrake = 0f; return; }

        float ns = Mathf.Max(stopThreshold, speed - pendingBrake);
        rb.linearVelocity = new Vector3(p.normalized.x * ns, v.y, p.normalized.z * ns);
        pendingBrake = 0f;
    }

    private void ApplyJoystickBrakeForce()
    {
        if (joystickBrakeDeceleration <= 0f) return;

        Vector3 v     = rb.linearVelocity;
        Vector3 p     = GetPlanarVelocity(v);
        float   speed = p.magnitude;

        if (speed <= stopThreshold)
        {
            rb.linearVelocity = new Vector3(0f, v.y, 0f);
            return;
        }

        float ns = Mathf.MoveTowards(speed, 0f, joystickBrakeDeceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(p.normalized.x * ns, v.y, p.normalized.z * ns);
    }

    private void ApplyPassiveFriction()
    {
        if (maintainCurrentSpeed) return;

        Vector3 v     = rb.linearVelocity;
        Vector3 p     = GetPlanarVelocity(v);
        float   speed = p.magnitude;

        if (speed <= stopThreshold)
        {
            rb.linearVelocity = new Vector3(0f, v.y, 0f);
            return;
        }

        // Tras un choque con barrera se aplica una desaceleración mucho mayor que la fricción
        // pasiva normal. Objetivo: detener el rebote en 1-2 frames para que la cámara no
        // abandone la cara principal de la pelota y siga la dirección del rebote.
        float friction = awaitingInputAfterBarrier
            ? barrierBounceDeceleration
            : passiveFriction * inertiaFrictionMultiplier;

        float ns = Mathf.MoveTowards(speed, 0f, friction * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(p.normalized.x * ns, v.y, p.normalized.z * ns);
    }

    private void ApplyForcedStop()
    {
        Vector3 v  = rb.linearVelocity;
        Vector3 p  = GetPlanarVelocity(v);
        Vector3 np = Vector3.MoveTowards(p, Vector3.zero, forcedStopState.Deceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(np.x, v.y, np.z);
    }

    private void ApplyGroundAdhesion()
    {
        if (groundSensor == null || !groundSensor.IsGrounded) return;
        if (impactState.IsActive || forcedStopState.IsActive) return;
        rb.AddForce(
            -groundSensor.GroundNormal * groundStickForce * inertiaGroundStickMultiplier,
            ForceMode.Acceleration);
    }

    private void ClampGroundedVerticalVelocity()
    {
        if (jumpBypassTimer > 0f) return;
        if (groundSensor == null || !groundSensor.IsGrounded) return;
        if (impactState.IsActive) return;

        Vector3 v = rb.linearVelocity;
        v.y = Mathf.Clamp(v.y, minimumGroundedDownwardVelocity, maxGroundedUpwardVelocity);
        rb.linearVelocity = v;
    }

    private void EnforceSpeedMaintenance()
    {
        if (!maintainCurrentSpeed) return;

        Vector3 v     = rb.linearVelocity;
        Vector3 p     = GetPlanarVelocity(v);
        float   speed = p.magnitude;

        // Si la velocidad actual supera el target guardado, actualizar el target.
        // Permite que el jugador acelere por encima del valor de captura inicial.
        if (speed > maintainedSpeedTarget) { maintainedSpeedTarget = speed; return; }

        if (maintainedSpeedTarget > stopThreshold && p.sqrMagnitude > 0.0001f)
        {
            p                 = p.normalized * maintainedSpeedTarget;
            rb.linearVelocity = new Vector3(p.x, v.y, p.z);
        }
    }

    #endregion

    #region Helpers

    private Vector3 GetMovementForward()
    {
        Vector3 fwd = rotationController != null ? rotationController.CurrentForward : transform.forward;

        if (groundSensor != null && groundSensor.IsGrounded)
        {
            Vector3 projected = groundSensor.GetProjectedForward(fwd);
            if (projected.sqrMagnitude > 0.0001f) return projected.normalized;
        }

        fwd.y = 0f;
        return fwd.sqrMagnitude < 0.0001f ? Vector3.forward : fwd.normalized;
    }

    private float ResolveSlopeAdjustedMax(Vector3 dir, float max)
    {
        float v = dir.y;
        if (v > 0f) return max * Mathf.Lerp(1f, uphillSpeedFactor,   Mathf.Clamp01(v));
        if (v < 0f) return Mathf.Min(
            max * Mathf.Lerp(1f, downhillSpeedFactor, Mathf.Clamp01(-v)),
            max * downhillSpeedFactor);
        return max;
    }

    /// <summary>
    /// Proyecta la velocidad deseada sobre el plano de la barrera cuando hay bloqueo frontal.
    /// Esto convierte la componente INTO la pared en deslizamiento lateral.
    /// </summary>
    private Vector3 ResolveBlockedVelocity(Vector3 desired)
    {
        if (collisionResponder == null || !collisionResponder.HasBlockingContact) return desired;

        Vector3 n = collisionResponder.BlockingNormal;
        n.y = 0f;
        if (n.sqrMagnitude < 0.0001f) return desired;
        n.Normalize();

        Vector3 dp   = GetPlanarVelocity(desired);
        float   into = Vector3.Dot(dp, -n);
        if (into <= 0f) return desired;

        Vector3 slide = (dp - (-n * into)) * blockedSlideFactor;
        return slide.sqrMagnitude < 0.0001f
            ? new Vector3(0f, desired.y, 0f)
            : new Vector3(slide.x, desired.y, slide.z);
    }

    private static Vector3 GetPlanarVelocity(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    /// <summary>
    /// Zeroes the planar (XZ) velocity while preserving the vertical component.
    /// Called when the player gives input immediately after a barrier bounce,
    /// so the residual bounce velocity does not fight the new impulse direction.
    /// </summary>
    private void CancelPlanarVelocity()
    {
        Vector3 v         = rb.linearVelocity;
        rb.linearVelocity = new Vector3(0f, v.y, 0f);
    }

    #endregion
}