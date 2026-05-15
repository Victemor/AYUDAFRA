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
/// Límites de responsabilidad — lo que este motor NO hace:
/// El rebote contra barreras es manejado 100% por código en <see cref="BallCollisionResponder"/>.
/// Este motor no interviene en el cálculo del rebote; solo proyecta el input del jugador
/// sobre el plano de la pared para que el steering "deslice" a lo largo de ella de forma natural.
///
/// Cambios respecto a la versión anterior:
/// Se eliminó el mecanismo <c>awaitingInputAfterBarrier</c> + <c>barrierBounceDeceleration=50</c>
/// que era el causante del bug "avanza y frena": cada contacto con una barrera activaba una
/// fricción de 50 m/s² que detenía la bola en ~6 frames, bloqueando también el joystick y
/// el mantenimiento de velocidad, creando un ciclo de frenado involuntario.
/// </summary>
[DefaultExecutionOrder(-15)]
[RequireComponent(typeof(Rigidbody))]
public sealed class BallMovementMotor : MonoBehaviour
{
    // ─── Private State Structs ───────────────────────────────────────────────────

    /// <summary>
    /// Estado del retroceso por colisión con obstáculo.
    /// Agrupa timer y velocidad para eliminar booleans paralelos.
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
    [Tooltip("Sensor de suelo. Detecta si la bola está en contacto y la normal del suelo.")]
    private BallGroundSensor groundSensor;

    [SerializeField]
    [Tooltip("Responde a colisiones y expone el estado de bloqueo frontal para el steering.")]
    private BallCollisionResponder collisionResponder;

    [Header("Velocidad")]
    [SerializeField]
    [Tooltip("Velocidad máxima planar en m/s.")]
    private float maxSpeed = 12f;

    [SerializeField]
    [Tooltip("Factor de control de steering cuando la bola está en el aire.\n" +
             "0 = sin control aéreo. 1 = mismo control que en suelo.")]
    [Range(0f, 1f)]
    private float airControlFactor = 0.4f;

    [Header("Fricción Pasiva")]
    [SerializeField]
    [Tooltip("Desaceleración en m/s² aplicada cada frame cuando no hay mantenimiento de velocidad activo.\n" +
             "Mantener bajo (0.6–1.2) para un feel de inercia suave.")]
    private float passiveFriction = 0.8f;

    [SerializeField]
    [Tooltip("Velocidad planar mínima en m/s por debajo de la cual la bola se considera detenida.")]
    private float stopThreshold = 0.08f;

    [Header("Pendientes")]
    [SerializeField]
    [Tooltip("Multiplicador de velocidad máxima al subir pendiente.\n" +
             "La bola nunca supera maxSpeed × este valor en subida.")]
    [Range(0.1f, 1f)]
    private float uphillSpeedFactor = 0.72f;

    [SerializeField]
    [Tooltip("Multiplicador de velocidad máxima al bajar pendiente.")]
    [Range(1f, 3f)]
    private float downhillSpeedFactor = 1.2f;

    [SerializeField]
    [Tooltip("Fuerza de adhesión al suelo (m/s²). Evita que la bola despegue en curvas y rampas.")]
    private float groundStickForce = 28f;

    [SerializeField]
    [Tooltip("Velocidad vertical máxima positiva permitida mientras la bola está en el suelo.\n" +
             "Limita saltos involuntarios en rampas.")]
    private float maxGroundedUpwardVelocity = 2.5f;

    [SerializeField]
    [Tooltip("Velocidad vertical mínima permitida mientras la bola está en el suelo.\n" +
             "Evita que la bola flote al bajar pendientes pronunciadas.")]
    private float minimumGroundedDownwardVelocity = -12f;

    [Header("Impacto con Obstáculos")]
    [SerializeField]
    [Tooltip("Duración en segundos del período de recovery tras un impacto con obstáculo inamovible.\n" +
             "Durante este tiempo el input del jugador está bloqueado. Mantener ≤ 0.15s.")]
    private float postImpactRecoveryDuration = 0.1f;

    [SerializeField]
    [Tooltip("Velocidad de disipación del vector de retroceso en m/s².\n" +
             "Valores altos (≥ 16) disipan el retroceso rápidamente para devolver control al jugador.")]
    private float impactVelocityDecay = 16f;

    [Header("Deslizamiento Lateral en Pared")]
    [SerializeField]
    [Tooltip("Factor aplicado a la componente de deslizamiento cuando la bola está bloqueada por una pared.\n" +
             "1.0 = deslizamiento completo sin pérdida de velocidad lateral.\n" +
             "0.9 = deslizamiento con algo de fricción de pared.\n" +
             "Recomendado: 0.95–1.0 para que el jugador no pierda velocidad al bordear paredes.")]
    [Range(0f, 1f)]
    private float blockedSlideFactor = 0.98f;

    [Header("Steering")]
    [SerializeField]
    [Tooltip("Velocidad máxima en grados/s a la que la dirección de velocidad planar\n" +
             "sigue al forward lógico del jugador. Aumentar para giros más reactivos.")]
    private float steeringDegreesPerSecond = 340f;

    [SerializeField]
    [Tooltip("Diferencia angular en grados a partir de la cual se aplica el 100% de steeringDegreesPerSecond.\n" +
             "Ángulos menores aplican una fracción proporcional (curva de respuesta suave).\n" +
             "Valores más bajos (5–10°) hacen que la velocidad máxima se alcance antes,\n" +
             "resultando en un steering más reactivo en el centro.")]
    [Range(3f, 45f)]
    private float steeringAngleForFullRate = 7f;

    [SerializeField]
    [Tooltip("Exponente de la curva de respuesta del steering.\n" +
             "1 = lineal (respuesta constante).\n" +
             "1.15 = ligeramente suave en el inicio, más agresivo pasado el umbral.\n" +
             "Evitar valores altos (>1.5) que crean dead zones perceptibles.")]
    [Range(1f, 3f)]
    private float steeringResponseExponent = 1.15f;

    #endregion

    #region Runtime

    private ImpactState     impactState;
    private ForcedStopState forcedStopState;

    private float speedBoostMultiplier = 1f;
    private float jumpBypassTimer;

    private bool  maintainCurrentSpeed;
    private float maintainedSpeedTarget;
    private float joystickBrakeDeceleration;

    /// <summary>
    /// Multiplicadores aplicados por el power-up ReducedInertia.
    /// Se aplican sobre passiveFriction y groundStickForce respectivamente.
    /// </summary>
    private float inertiaFrictionMultiplier    = 1f;
    private float inertiaGroundStickMultiplier = 1f;

    #endregion

    #region Properties

    /// <summary>Velocidad planar (XZ) actual en m/s.</summary>
    public float CurrentSpeed => GetPlanarVelocity(rb.linearVelocity).magnitude;

    /// <summary>Alias de <see cref="CurrentSpeed"/>. Expuesto para compatibilidad con zonas de boost.</summary>
    public float CurrentPlanarVelocity => CurrentSpeed;

    /// <summary>
    /// Velocidad máxima configurada en m/s.
    /// Usada por sistemas externos (cámara, UI) para normalizar la velocidad actual.
    /// El techo efectivo en runtime puede ser mayor si hay un <see cref="speedBoostMultiplier"/> activo.
    /// </summary>
    public float MaxSpeed => maxSpeed;

    /// <summary>
    /// Forward lógico más reciente con velocidad suficiente para ser válido.
    /// Usado por <see cref="BallCollisionResponder"/> como fallback de dirección cuando
    /// la velocidad es baja.
    /// </summary>
    public Vector3 LastValidMoveDirection =>
        rotationController != null ? rotationController.CurrentForward : Vector3.forward;

    /// <summary>
    /// Mantenido por compatibilidad con sistemas de cámara que lo referencian.
    /// Siempre devuelve <c>false</c>: el mecanismo awaitingInputAfterBarrier fue eliminado
    /// porque causaba el bug "avanza y frena" al activar una fricción de 50 m/s² en cada
    /// contacto con barrera, deteniendo la bola en ~6 frames repetidamente.
    /// </summary>
    public bool IsAwaitingInputAfterBarrier => false;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        rb                 = GetComponent<Rigidbody>();
        rotationController = GetComponent<SphereRotationController>();
        groundSensor       = GetComponent<BallGroundSensor>();
        collisionResponder = GetComponent<BallCollisionResponder>();
    }

    private void Awake()
    {
        if (rb == null)                 rb                 = GetComponent<Rigidbody>();
        if (rotationController == null) rotationController = GetComponent<SphereRotationController>();
        if (groundSensor == null)       groundSensor       = GetComponent<BallGroundSensor>();
        if (collisionResponder == null) collisionResponder = GetComponent<BallCollisionResponder>();

        rb.interpolation          = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints           |= RigidbodyConstraints.FreezeRotationX;
        rb.constraints           |= RigidbodyConstraints.FreezeRotationY;
        rb.constraints           |= RigidbodyConstraints.FreezeRotationZ;
    }

    private void OnEnable() { }

    private void OnDisable() { }

    private void FixedUpdate()
    {
        TickTimers();
        groundSensor?.RefreshGroundState();
        UpdateImpactRecovery();

        // ImpactState: retroceso por obstáculo — bloquea input durante la recuperación.
        if (impactState.IsActive)
        {
            ApplyImpactVelocity();
            ApplyGroundAdhesion();
            ClampGroundedVerticalVelocity();
            return;
        }

        // ForcedStop: detención al llegar a la meta.
        if (forcedStopState.IsActive)
        {
            ApplyForcedStop();
            ApplyGroundAdhesion();
            ClampGroundedVerticalVelocity();
            return;
        }

        // Pipeline normal: steering, frenos, fricción, adhesión, mantenimiento.
        ApplySteering();
        ApplyJoystickBrakeForce();
        ApplyPassiveFriction();
        ApplyGroundAdhesion();
        ClampGroundedVerticalVelocity();
        EnforceSpeedMaintenance();
    }

    #endregion

    #region Public API — Control

    /// <summary>
    /// Detiene la bola inmediatamente y limpia todos los estados pendientes.
    /// Llamado antes de cualquier teleport o respawn.
    /// </summary>
    public void Stop()
    {
        impactState               = ImpactState.Default;
        forcedStopState           = ForcedStopState.Default;
        maintainCurrentSpeed      = false;
        maintainedSpeedTarget     = 0f;
        joystickBrakeDeceleration = 0f;

        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
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
        v.x               *= c;
        v.z               *= c;
        rb.linearVelocity  = v;
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

        Vector3 c         = rb.linearVelocity;
        rb.linearVelocity = new Vector3(recoilVelocity.x, c.y, recoilVelocity.z);
    }

    /// <summary>
    /// Suprime temporalmente el drive del jugador sin aplicar retroceso.
    /// Útil en colisiones con obstáculos empujables que solo interrumpen el impulso activo.
    /// </summary>
    public void SuppressDrive(float duration)
    {
        impactState = new ImpactState
        {
            RecoveryTimer = Mathf.Max(impactState.RecoveryTimer, duration),
            Velocity      = impactState.Velocity,
        };
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

        impactState = ImpactState.Default;
    }

    /// <summary>Cancela la detención forzada y devuelve el control al jugador.</summary>
    public void EndForcedStop()
    {
        forcedStopState = ForcedStopState.Default;
    }

    /// <summary>
    /// Aplica multiplicadores de inertia reduction desde el power-up ReducedInertia.
    /// Reduce la fricción pasiva y la adhesión al suelo durante el efecto activo.
    /// </summary>
    /// <param name="frictionMult">Multiplicador sobre passiveFriction [0–1]. 0.3 = 70% menos fricción.</param>
    /// <param name="groundStickMult">Multiplicador sobre groundStickForce [0–1]. 0.4 = 60% menos adhesión.</param>
    public void SetInertiaReduction(float frictionMult, float groundStickMult)
    {
        inertiaFrictionMultiplier    = Mathf.Clamp01(frictionMult);
        inertiaGroundStickMultiplier = Mathf.Clamp01(groundStickMult);
    }

    /// <summary>Revierte los multiplicadores de inertia reduction a sus valores neutros.</summary>
    public void ClearInertiaReduction()
    {
        inertiaFrictionMultiplier    = 1f;
        inertiaGroundStickMultiplier = 1f;
    }

    #endregion

    #region Public API — Jump

    /// <summary>
    /// Aplica un impulso vertical.
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
    /// Aplica un impulso de velocidad en una dirección mundo explícita.
    /// Se suma a la velocidad planar actual y se clampea al techo efectivo (maxSpeed × boost).
    ///
    /// Usado por <see cref="SwipeDirectionController"/> para aplicar el impulso del swipe
    /// en la dirección objetivo del swipe, independientemente de hacia dónde apunta
    /// la cara en ese frame (la cara puede aún estar rotando hacia el destino).
    /// </summary>
    /// <param name="worldDirection">Dirección en espacio mundo. La componente Y se ignora.</param>
    /// <param name="speed">Velocidad en m/s a añadir en esa dirección.</param>
    public void ApplyImpulseInDirection(Vector3 worldDirection, float speed)
    {
        if (speed <= 0f) return;

        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude < 0.0001f) return;
        worldDirection.Normalize();

        Vector3 v   = rb.linearVelocity;
        Vector3 p   = GetPlanarVelocity(v) + worldDirection * speed;
        float   max = maxSpeed * speedBoostMultiplier;
        if (p.magnitude > max) p = p.normalized * max;
        rb.linearVelocity = new Vector3(p.x, v.y, p.z);
    }

    /// <summary>
    /// Reduce la velocidad planar de la bola en <paramref name="speedReduction"/> m/s de forma inmediata.
    /// Si la velocidad resultante cae por debajo de <c>stopThreshold</c>, la bola se detiene.
    ///
    /// Usado por <see cref="SwipeDirectionController"/> para swipes hacia el hemisferio trasero
    /// mientras la bola está en movimiento: cada swipe quita una cantidad fija de velocidad.
    /// </summary>
    public void ApplyBrakePulse(float speedReduction)
    {
        if (speedReduction <= 0f) return;

        Vector3 v     = rb.linearVelocity;
        Vector3 p     = GetPlanarVelocity(v);
        float   speed = p.magnitude;

        if (speed <= stopThreshold)
        {
            rb.linearVelocity = new Vector3(0f, v.y, 0f);
            return;
        }

        float newSpeed = Mathf.Max(0f, speed - speedReduction);

        if (newSpeed <= stopThreshold)
        {
            rb.linearVelocity = new Vector3(0f, v.y, 0f);
            return;
        }

        p                 = p.normalized * newSpeed;
        rb.linearVelocity = new Vector3(p.x, v.y, p.z);
    }

    /// <summary>
    /// Aplica un impulso directo en la dirección del forward actual sin pasar por el acumulador.
    /// Usado por <c>JoystickMovementController</c> para el arranque desde cero y por
    /// <c>SwipeDirectionRouter</c> para el micro-kick de redirección con swipe.
    /// No supera el techo de velocidad efectivo (maxSpeed × speedBoostMultiplier).
    /// </summary>
    public void ApplyJoystickKickstart(float impulse)
    {
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
    /// Rota gradualmente la dirección de velocidad planar hacia el forward del jugador.
    /// La velocidad se proyecta sobre el plano de la pared si hay bloqueo frontal activo,
    /// convirtiendo el steering en deslizamiento en lugar de presión contra la barrera.
    /// </summary>
    private void ApplySteering()
    {
        Vector3 v     = rb.linearVelocity;
        Vector3 p     = GetPlanarVelocity(v);
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

        // Reducción de control aéreo cuando la bola no está en el suelo.
        if (groundSensor != null && !groundSensor.IsGrounded)
            effectiveRate *= airControlFactor;

        Vector3 steered = Vector3.RotateTowards(
            currentDir, targetFwd,
            effectiveRate * Mathf.Deg2Rad * Time.fixedDeltaTime,
            0f);

        Vector3 newVel    = ResolveBlockedVelocity(new Vector3(steered.x * speed, v.y, steered.z * speed));
        rb.linearVelocity = newVel;
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

        float friction = passiveFriction * inertiaFrictionMultiplier;
        float ns       = Mathf.MoveTowards(speed, 0f, friction * Time.fixedDeltaTime);
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

        // Si la velocidad actual supera el target, actualizar el target.
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
        Vector3 fwd = rotationController != null
            ? rotationController.CurrentForward
            : transform.forward;

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
    /// Convierte la componente INTO la pared en deslizamiento lateral con pérdida mínima de velocidad.
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

        // Proyectar sobre el plano de la pared y aplicar factor de deslizamiento.
        // blockedSlideFactor = 0.98 → prácticamente sin pérdida lateral al bordear paredes.
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

    private void CancelPlanarVelocity()
    {
        Vector3 v         = rb.linearVelocity;
        rb.linearVelocity = new Vector3(0f, v.y, 0f);
    }

    #endregion
}