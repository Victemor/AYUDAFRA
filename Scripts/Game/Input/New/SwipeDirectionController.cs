using UnityEngine;

/// <summary>
/// Controlador de swipes. Reemplaza a <c>SwipeDirectionRouter</c>, <c>ImpulseAccumulator</c>
/// y <c>BrakeAccumulator</c>.
///
/// Usa proyección cara-relativa (<see cref="InputDirectionProjector"/>):
/// el eje de referencia es la cara actual de la bola, no la cámara.
///
/// Steering resistance basada en velocidad (curva de potencia):
/// La fracción del ángulo del swipe aplicada como giro escala inversamente con la velocidad.
/// La curva cae bruscamente en cuanto la bola empieza a moverse (control por exponente)
/// y llega a un mínimo configurable a velocidad máxima.
///
/// Técnica de rotación:
/// Se usa <c>Quaternion.AngleAxis(ángulo × influencia, Vector3.up)</c> en lugar de Slerp,
/// lo que evita el comportamiento indefinido al girar entre vectores antiparalelos (swipe 180°).
///
/// Comportamiento por tipo de swipe:
///
/// Forward (≤ 35° de desviación): solo impulso en la cara actual. SIN rotar.
/// Lateral / Diagonal-Frontal: rota la cara (influencia por velocidad) + impulso.
/// Hemisferio trasero: frena si hay velocidad, rota si está detenida.
/// </summary>
public sealed class SwipeDirectionController : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]
    [SerializeField]
    [Tooltip("Input unificado de la escena.")]
    private UnifiedBallInput unifiedInput;

    [SerializeField]
    [Tooltip("Motor de movimiento de la pelota.")]
    private BallMovementMotor movementMotor;

    [SerializeField]
    [Tooltip("Controlador de rotación lógica de la esfera.")]
    private SphereRotationController rotationController;

    [Header("Impulso Frontal")]
    [SerializeField]
    [Tooltip("Impulso base en m/s por swipe frontal.")]
    private float baseSwipeImpulse = 3f;

    [SerializeField]
    [Tooltip("Impulso adicional en m/s por cada swipe consecutivo.")]
    private float consecutiveImpulseBonus = 0.5f;

    [SerializeField]
    [Tooltip("Número máximo de swipes consecutivos acumulables.")]
    [Min(0)]
    private int maxConsecutiveCount = 5;

    [SerializeField]
    [Tooltip("Ventana de tiempo en segundos para considerar swipes como consecutivos.")]
    private float consecutiveWindowSeconds = 0.5f;

    [Header("Steering Resistance — Curva de Velocidad")]
    [SerializeField]
    [Tooltip("Fracción del ángulo del swipe que se aplica como giro cuando la bola está detenida.\n" +
             "1.0 = giro completo. 0.95 = 95% del ángulo del swipe.\n" +
             "Ejemplo: swipe de 180° → 171° de giro.")]
    [Range(0f, 1f)]
    private float swipeRotationAtZeroSpeed = 0.95f;

    [SerializeField]
    [Tooltip("Fracción del ángulo del swipe que se aplica como giro a velocidad máxima.\n" +
             "0.12 = solo 12% del ángulo del swipe aunque el gesto sea de 180°.\n" +
             "Ajusta para calibrar cuánto 'peso' siente la bola rápida al intentar desviarla.")]
    [Range(0f, 1f)]
    private float swipeRotationAtMaxSpeed = 0.12f;

    [SerializeField]
    [Tooltip("Exponente de la curva de caída de influencia respecto a la velocidad.\n" +
             "0.2 = caída muy brusca: a velocidad mínima ya cae al ~50% de influencia.\n" +
             "1.0 = caída lineal (más suave).\n" +
             "Valores recomendados: 0.15–0.35.")]
    [Range(0.05f, 2f)]
    private float swipeRotationFalloffExponent = 0.2f;

    [Header("Freno Trasero")]
    [SerializeField]
    [Tooltip("Velocidad en m/s eliminada por cada swipe hacia atrás.")]
    private float backSwipeBrakeImpulse = 4f;

    [SerializeField]
    [Tooltip("Velocidad planar en m/s por debajo de la cual la bola se considera detenida.")]
    private float stopThreshold = 0.12f;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("Muestra logs: intent, velocidad normalizada, influencia calculada, ángulo aplicado.")]
    private bool debugController;

    #endregion

    #region Runtime

    private int   consecutiveCount = 0;
    private float lastSwipeTime    = float.NegativeInfinity;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        unifiedInput       = FindFirstObjectByType<UnifiedBallInput>();
        movementMotor      = GetComponent<BallMovementMotor>();
        rotationController = GetComponent<SphereRotationController>();
    }

    private void Awake()
    {
        if (unifiedInput       == null) unifiedInput       = FindFirstObjectByType<UnifiedBallInput>();
        if (movementMotor      == null) movementMotor      = GetComponent<BallMovementMotor>();
        if (rotationController == null) rotationController = GetComponent<SphereRotationController>();
    }

    private void OnEnable()
    {
        if (unifiedInput != null)
            unifiedInput.OnSwipeDetected += HandleSwipe;
    }

    private void OnDisable()
    {
        if (unifiedInput != null)
            unifiedInput.OnSwipeDetected -= HandleSwipe;
    }

    private void OnValidate()
    {
        baseSwipeImpulse             = Mathf.Max(0f,    baseSwipeImpulse);
        consecutiveImpulseBonus      = Mathf.Max(0f,    consecutiveImpulseBonus);
        maxConsecutiveCount          = Mathf.Max(0,     maxConsecutiveCount);
        consecutiveWindowSeconds     = Mathf.Max(0.05f, consecutiveWindowSeconds);
        backSwipeBrakeImpulse        = Mathf.Max(0f,    backSwipeBrakeImpulse);
        stopThreshold                = Mathf.Max(0f,    stopThreshold);
        swipeRotationFalloffExponent = Mathf.Max(0.05f, swipeRotationFalloffExponent);
        swipeRotationAtMaxSpeed      = Mathf.Min(swipeRotationAtMaxSpeed, swipeRotationAtZeroSpeed);
    }

    #endregion

    #region Public API

    /// <summary>Resetea el contador de swipes consecutivos.</summary>
    public void ResetConsecutive()
    {
        consecutiveCount = 0;
    }

    #endregion

    #region Event Handler

    private void HandleSwipe(SwipeData swipe)
    {
        bool isFront = swipe.ScreenDirection.y >= 0f;

        if (isFront)
            HandleFrontSwipe(swipe.Intent, swipe.ScreenDirection);
        else
            HandleBackSwipe(swipe.ScreenDirection);
    }

    #endregion

    #region Swipe Handlers

    /// <summary>
    /// Swipe frontal.
    ///
    /// Forward intent: solo impulso en la cara actual. Sin rotación.
    ///
    /// Lateral / Diagonal: calcula el ángulo del swipe en espacio cara-relativo,
    /// lo escala por la influencia según velocidad (curva de potencia) y aplica
    /// la rotación mediante <c>Quaternion.AngleAxis</c> alrededor de <c>Vector3.up</c>.
    /// Esto evita el comportamiento indefinido de <c>Slerp</c> en swipes de 180°
    /// (vectores antiparalelos).
    /// </summary>
    private void HandleFrontSwipe(SwipeIntent intent, Vector2 screenDir)
    {
        float impulse = CalculateAndIncrementConsecutive();

        if (intent == SwipeIntent.Forward)
        {
            // Swipe casi recto → impulso sin cambio de dirección.
            movementMotor.ApplyImpulseInDirection(GetCurrentFace(), impulse);

            if (debugController)
                Debug.Log($"[SwipeDirection] Forward boost | impulse:{impulse:F2}");

            return;
        }

        // ── Calcular ángulo cara-relativo del swipe ─────────────────────────────────
        // Atan2(x, y) donde y = componente forward (pantalla arriba) y x = componente right.
        // Resultado: ángulo en grados desde la cara actual, positivo = derecha, negativo = izquierda.
        float rawAngleDeg = Mathf.Atan2(screenDir.x, screenDir.y) * Mathf.Rad2Deg;

        // ── Calcular influencia con curva de potencia ───────────────────────────────
        // La curva cae bruscamente al inicio: a velocidad mínima ya se siente resistencia.
        // exponent < 1 → caída rápida. exponent = 1 → lineal. exponent > 1 → caída lenta.
        float normalizedSpeed = movementMotor.MaxSpeed > 0f
            ? Mathf.Clamp01(movementMotor.CurrentSpeed / movementMotor.MaxSpeed)
            : 0f;

        float curvedSpeed     = Mathf.Pow(normalizedSpeed, swipeRotationFalloffExponent);
        float rotationInfluence = Mathf.Lerp(
            swipeRotationAtZeroSpeed,
            swipeRotationAtMaxSpeed,
            curvedSpeed);

        // ── Aplicar ángulo escalado alrededor del eje Y (arriba del mundo) ──────────
        // Quaternion.AngleAxis maneja correctamente cualquier ángulo incluyendo 180°,
        // a diferencia de Slerp que es indefinido entre vectores antiparalelos.
        float   appliedAngleDeg = rawAngleDeg * rotationInfluence;
        Vector3 blendedDir      = Quaternion.AngleAxis(appliedAngleDeg, Vector3.up) * GetCurrentFace();
        blendedDir.y = 0f;
        if (blendedDir.sqrMagnitude < 0.0001f) blendedDir = GetCurrentFace();
        blendedDir.Normalize();

        rotationController.SetTargetForward(blendedDir);
        movementMotor.ApplyImpulseInDirection(blendedDir, impulse);

        if (debugController)
        {
            Debug.Log(
                $"[SwipeDirection] Intent:{intent} | " +
                $"RawAngle:{rawAngleDeg:F1}° | SpeedNorm:{normalizedSpeed:F2} | " +
                $"Influence:{rotationInfluence:F2} | AppliedAngle:{appliedAngleDeg:F1}°");
        }
    }

    /// <summary>
    /// Swipe trasero: frena si hay velocidad, rota cara si está detenida.
    /// La influencia al girar desde detenida usa el valor máximo (velocidad = 0 → giro libre).
    /// </summary>
    private void HandleBackSwipe(Vector2 screenDir)
    {
        consecutiveCount = 0;

        if (movementMotor.CurrentSpeed > stopThreshold)
        {
            movementMotor.ApplyBrakePulse(backSwipeBrakeImpulse);
            return;
        }

        // Bola detenida → rotar cara en la dirección del swipe con influencia máxima.
        float rawAngleDeg    = Mathf.Atan2(screenDir.x, screenDir.y) * Mathf.Rad2Deg;
        float appliedAngleDeg = rawAngleDeg * swipeRotationAtZeroSpeed;
        Vector3 blendedDir   = Quaternion.AngleAxis(appliedAngleDeg, Vector3.up) * GetCurrentFace();
        blendedDir.y = 0f;
        if (blendedDir.sqrMagnitude < 0.0001f) blendedDir = GetCurrentFace();
        blendedDir.Normalize();

        rotationController.SetTargetForward(blendedDir);
    }

    #endregion

    #region Consecutive

    /// <summary>
    /// Calcula el impulso del swipe actual según los consecutivos y actualiza el contador.
    /// Swipe 1: base. Swipe 2: base+bonus. ... Swipe N: base+(N-1)×bonus (hasta maxConsecutiveCount).
    /// </summary>
    private float CalculateAndIncrementConsecutive()
    {
        float now = Time.unscaledTime;

        if (now - lastSwipeTime <= consecutiveWindowSeconds)
            consecutiveCount = Mathf.Min(consecutiveCount + 1, maxConsecutiveCount);
        else
            consecutiveCount = 0;

        lastSwipeTime = now;

        return baseSwipeImpulse + consecutiveCount * consecutiveImpulseBonus;
    }

    #endregion

    #region Helpers

    private Vector3 GetCurrentFace()
    {
        return rotationController != null ? rotationController.CurrentForward : Vector3.forward;
    }

    #endregion
}