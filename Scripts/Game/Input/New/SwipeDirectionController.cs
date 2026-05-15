using UnityEngine;

/// <summary>
/// Controlador de swipes.
///
/// ═══ Sistema de coordenadas (siempre tener presente) ═══
/// El usuario describe ángulos en su sistema: 90°=forward, 0°=derecha, 180°=izquierda, 270°=backward.
/// Internamente, rawAngle = Atan2(screenDir.x, screenDir.y) mapea así:
///   rawAngle  0° = user 90° (forward — cara principal)
///   rawAngle ±90° = user 0°/180° (giros laterales puros — izquierda/derecha)
///   rawAngle ±180° = user 270° (backward)
///
/// ═══ Cuatro zonas por |rawAngle| ═══
///
///   ZONA MUERTA     [0°, deadZoneDeg)
///     Solo impulso hacia adelante. Sin rotación. Sin costo de velocidad.
///     ClearTarget() cancela rotaciones pendientes de swipes anteriores.
///
///   ZONA DE IMPULSO [deadZoneDeg, impulseZoneDeg)
///     Impulso + rotación pequeña (con rampa suave desde el borde del dead zone).
///     Costo de velocidad mínimo (basado en ángulo aplicado pequeño).
///
///   ZONA DE GIRO    [impulseZoneDeg, 180°-brakeZone)
///     Solo rotación. SIN impulso. Costo de velocidad basado en rawAngle
///     (intención del giro), no en el ángulo aplicado. Más velocidad = mayor costo.
///
///   ZONA DE FRENO   [180°-brakeZone, 180°]
///     Si se mueve → frena. Si está detenida → gira sin costo de velocidad.
/// </summary>
public sealed class SwipeDirectionController : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]
    [SerializeField] [Tooltip("Input unificado de la escena.")]
    private UnifiedBallInput unifiedInput;

    [SerializeField] [Tooltip("Motor de movimiento de la pelota.")]
    private BallMovementMotor movementMotor;

    [SerializeField] [Tooltip("Controlador de rotación lógica de la esfera.")]
    private SphereRotationController rotationController;

    [Header("Zona Muerta — Sin rotación")]
    [SerializeField]
    [Tooltip("Swipes dentro de este ángulo del forward (rawAngle < deadZone) dan\n" +
             "solo impulso adelante. Sin rotación. Sin costo de velocidad.\n" +
             "Usuario: dentro de (90°±deadZone) = solo acelera recto.")]
    [Range(0f, 45f)]
    private float deadZoneDeg = 5f;

    [Header("Zona de Impulso — Cerca del forward")]
    [SerializeField]
    [Tooltip("Swipes dentro de este ángulo del forward dan IMPULSO + rotación pequeña.\n" +
             "Fuera de este rango: SOLO rotación, sin impulso.\n\n" +
             "Ejemplo: impulseZone=10° → swipes entre user 80°–100° aceleran la bola.\n" +
             "Usuario: solo swipes cercanos a su 90° (forward) aceleran.")]
    [Range(0f, 90f)]
    private float impulseZoneDeg = 10f;

    [Header("Impulso por Swipe")]
    [SerializeField]
    [Tooltip("Impulso base en m/s por swipe en zona de impulso.")]
    private float baseSwipeImpulse = 1.5f;

    [SerializeField]
    [Tooltip("Impulso adicional en m/s por cada swipe consecutivo dentro de la ventana.")]
    private float consecutiveImpulseBonus = 0.3f;

    [SerializeField]
    [Tooltip("Número máximo de swipes consecutivos acumulables.")]
    [Min(0)]
    private int maxConsecutiveCount = 8;

    [SerializeField]
    [Tooltip("Ventana de tiempo en segundos para considerar swipes como consecutivos.\n" +
             "Solo se cuentan swipes en la zona de impulso.")]
    private float consecutiveWindowSeconds = 0.6f;

    [SerializeField]
    [Tooltip("Cuánto se reduce el impulso conforme la bola se acerca a la velocidad máxima.\n" +
             "0.75 = a velocidad máxima el impulso queda al 25%. Aceleración progresiva.")]
    [Range(0f, 1f)]
    private float impulseSpeedReductionFactor = 0.75f;

    [Header("Curva de Reducción de Giro")]
    [SerializeField]
    [Tooltip("Rango de transición suave desde el borde del dead zone hasta maxRotationInfluence.\n" +
             "Elimina el salto brusco entre zona muerta y zona de rotación.")]
    [Range(0f, 45f)]
    private float rotationRampDeg = 20f;

    [SerializeField]
    [Tooltip("Techo máximo de influence de rotación (a velocidad cero y ángulo pequeño).")]
    [Range(0f, 1f)]
    private float maxRotationInfluence = 0.6f;

    [SerializeField]
    [Tooltip("Influencia mínima a velocidad máxima y máximo desvío del forward.")]
    [Range(0f, 1f)]
    private float minInfluenceAtMaxRestriction = 0.08f;

    [SerializeField]
    [Tooltip("Exponente de la curva de velocidad. 0.2 = restricción brusca al arrancar.")]
    [Range(0.05f, 2f)]
    private float swipeRotationFalloffExponent = 0.2f;

    [Header("Escalado por Longitud del Segmento")]
    [SerializeField]
    [Tooltip("Factor mínimo de rotación para el segmento más corto posible.")]
    [Range(0f, 1f)]
    private float minLengthRotationScale = 0.35f;

    [SerializeField]
    [Tooltip("Longitud de segmento en píxeles a partir de la cual el ángulo se aplica al 100%.")]
    private float fullRotationLengthPx = 160f;

    [Header("Costo de Velocidad al Girar")]
    [SerializeField]
    [Tooltip("Activa la penalización de velocidad al girar.")]
    private bool applyTurnSpeedCost = true;

    [SerializeField]
    [Tooltip("Multiplicador de velocidad aplicado cuando el rawAngle del swipe es el máximo\n" +
             "posible antes del freno (justo en el umbral de la zona de freno).\n" +
             "0.4 = un giro lateral puro (user 0°/180°) a máxima velocidad reduce la velocidad al 40%.\n" +
             "El costo escala: ángulo mayor = mayor costo, independientemente del ángulo aplicado.\n" +
             "El costo aumenta además con la velocidad actual.")]
    [Range(0f, 1f)]
    private float turnSpeedMultiplierAtMaxAngle = 0.4f;

    [Header("Zona de Freno")]
    [SerializeField]
    [Tooltip("Grados a cada lado del backward (rawAngle ±180°) que activan el freno.")]
    [Range(0f, 90f)]
    private float brakeZoneDeg = 10f;

    [SerializeField]
    [Tooltip("Velocidad en m/s eliminada por cada swipe en zona de freno.")]
    private float backSwipeBrakeImpulse = 4f;

    [SerializeField]
    [Tooltip("Velocidad mínima para considerar la bola en movimiento.")]
    private float stopThreshold = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool debugController;

    #endregion

    #region Runtime

    private int   consecutiveCount = 0;
    private float lastSwipeTime    = float.NegativeInfinity;

    /// <summary>
    /// Dirección de la cara en el momento en que el dedo tocó la pantalla.
    /// Se usa como referencia ABSOLUTA para calcular el target de todos los segmentos
    /// del gesto. Elimina la acumulación: múltiples segmentos en la misma dirección
    /// convergen al mismo destino en lugar de sumar deltas.
    /// </summary>
    private Vector3 gestureReferenceForward = Vector3.forward;

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
        if (unifiedInput == null) return;
        unifiedInput.OnTrackingBegan  += OnGestureBegan;
        unifiedInput.OnSwipeDirection += HandleDirectionSwipe;
        unifiedInput.OnSwipeDetected  += HandleImpulseSwipe;
    }

    private void OnDisable()
    {
        if (unifiedInput == null) return;
        unifiedInput.OnTrackingBegan  -= OnGestureBegan;
        unifiedInput.OnSwipeDirection -= HandleDirectionSwipe;
        unifiedInput.OnSwipeDetected  -= HandleImpulseSwipe;
    }

    private void OnValidate()
    {
        deadZoneDeg                  = Mathf.Clamp(deadZoneDeg,    0f, 45f);
        impulseZoneDeg               = Mathf.Max(deadZoneDeg,      impulseZoneDeg);
        baseSwipeImpulse             = Mathf.Max(0f,               baseSwipeImpulse);
        consecutiveImpulseBonus      = Mathf.Max(0f,               consecutiveImpulseBonus);
        maxConsecutiveCount          = Mathf.Max(0,                maxConsecutiveCount);
        consecutiveWindowSeconds     = Mathf.Max(0.05f,            consecutiveWindowSeconds);
        impulseSpeedReductionFactor  = Mathf.Clamp01(              impulseSpeedReductionFactor);
        rotationRampDeg              = Mathf.Clamp(rotationRampDeg, 0f, 45f);
        minInfluenceAtMaxRestriction = Mathf.Clamp01(              minInfluenceAtMaxRestriction);
        maxRotationInfluence         = Mathf.Clamp(maxRotationInfluence, minInfluenceAtMaxRestriction, 1f);
        swipeRotationFalloffExponent = Mathf.Max(0.05f,            swipeRotationFalloffExponent);
        minLengthRotationScale       = Mathf.Clamp01(              minLengthRotationScale);
        fullRotationLengthPx         = Mathf.Max(1f,               fullRotationLengthPx);
        turnSpeedMultiplierAtMaxAngle = Mathf.Clamp01(             turnSpeedMultiplierAtMaxAngle);
        brakeZoneDeg                 = Mathf.Clamp(brakeZoneDeg,   0f, 90f);
        backSwipeBrakeImpulse        = Mathf.Max(0f,               backSwipeBrakeImpulse);
        stopThreshold                = Mathf.Max(0f,               stopThreshold);
    }

    #endregion

    #region Public API

    public void ResetConsecutive() => consecutiveCount = 0;

    #endregion

    #region Event Handlers

    /// <summary>Snapshot de la cara al inicio del gesto. Base absoluta para todos los segmentos.</summary>
    private void OnGestureBegan()
    {
        gestureReferenceForward = GetCurrentFace();
    }

    /// <summary>Segmento continuo (OnSwipeDirection): rotación + impulso base (sin bonus consecutivo).</summary>
    private void HandleDirectionSwipe(SwipeData swipe)
    {
        ProcessSwipe(swipe, giveImpulse: true, incrementConsecutive: false);
    }

    /// <summary>Swipe al soltar (OnSwipeDetected): rotación + impulso con bonus consecutivo.</summary>
    private void HandleImpulseSwipe(SwipeData swipe)
    {
        ProcessSwipe(swipe, giveImpulse: true, incrementConsecutive: true);
    }

    private void ProcessSwipe(SwipeData swipe, bool giveImpulse, bool incrementConsecutive)
    {
        float rawAngle    = Mathf.Atan2(swipe.ScreenDirection.x, swipe.ScreenDirection.y)
                            * Mathf.Rad2Deg;
        float absRaw      = Mathf.Abs(rawAngle);
        float brakeThresh = 180f - brakeZoneDeg;

        // ── ZONA DE FRENO ────────────────────────────────────────────────────────────
        if (absRaw >= brakeThresh)
        {
            if (!giveImpulse) return;
            consecutiveCount = 0;
            if (movementMotor.CurrentSpeed > stopThreshold)
            {
                movementMotor.ApplyBrakePulse(backSwipeBrakeImpulse);
                if (debugController) Debug.Log($"[SwipeDir] FRENO | raw:{rawAngle:F1}°");
            }
            else
            {
                float a = CalculateAppliedAngle(absRaw, rawAngle, brakeThresh, swipe.Length);
                RotateFace(a, giveImpulse: false, impulse: 0f);
            }
            return;
        }

        // ── ZONA MUERTA ──────────────────────────────────────────────────────────────
        if (absRaw < deadZoneDeg)
        {
            rotationController.ClearTarget();
            if (giveImpulse)
            {
                float impulse = CalculateImpulse(incrementConsecutive);
                movementMotor.ApplyImpulseInDirection(GetCurrentFace(), impulse);
                if (debugController) Debug.Log($"[SwipeDir] MUERTA+IMPULSO | raw:{rawAngle:F1}° imp:{impulse:F2}");
            }
            return;
        }

        // ── ZONA DE IMPULSO ──────────────────────────────────────────────────────────
        if (absRaw < impulseZoneDeg)
        {
            float a   = CalculateAppliedAngle(absRaw, rawAngle, brakeThresh, swipe.Length);
            float imp = giveImpulse ? CalculateImpulse(incrementConsecutive) : 0f;
            RotateFace(a, giveImpulse, imp);
            ApplyTurnSpeedCost(absRaw, brakeThresh);
            if (debugController) Debug.Log($"[SwipeDir] IMPULSO | raw:{rawAngle:F1}° applied:{a:F1}° imp:{imp:F2}");
            return;
        }

        // ── ZONA DE GIRO ─────────────────────────────────────────────────────────────
        {
            float a = CalculateAppliedAngle(absRaw, rawAngle, brakeThresh, swipe.Length);
            RotateFace(a, giveImpulse: false, impulse: 0f);
            ApplyTurnSpeedCost(absRaw, brakeThresh);
            if (debugController) Debug.Log($"[SwipeDir] GIRO | raw:{rawAngle:F1}° applied:{a:F1}°");
        }
    }

    #endregion

    #region Calculations

    private float CalculateAppliedAngle(float absRaw, float rawAngle, float brakeThresh, float length)
    {
        float angleT = brakeThresh > 0f ? Mathf.Clamp01(absRaw / brakeThresh) : 1f;

        float normalizedSpeed = movementMotor.MaxSpeed > 0f
            ? Mathf.Clamp01(movementMotor.CurrentSpeed / movementMotor.MaxSpeed)
            : 0f;

        float speedT      = Mathf.Pow(normalizedSpeed, swipeRotationFalloffExponent);
        float restriction = angleT * speedT;
        float influence   = Mathf.Lerp(maxRotationInfluence, minInfluenceAtMaxRestriction, restriction);

        // Rampa suave desde el borde del dead zone: elimina el salto brusco.
        float angleAboveDeadZone = absRaw - deadZoneDeg;
        float rampT = rotationRampDeg > 0f
            ? Mathf.Clamp01(angleAboveDeadZone / rotationRampDeg)
            : 1f;
        influence *= rampT;

        float lengthScale = Mathf.Lerp(
            minLengthRotationScale, 1f,
            Mathf.Clamp01(length / fullRotationLengthPx));

        return Mathf.Sign(rawAngle) * absRaw * influence * lengthScale;
    }

    /// <summary>
    /// Calcula el impulso escalado por velocidad actual.
    /// <paramref name="incrementConsecutive"/> = true solo al levantar el dedo (bonus por cadena).
    /// Los segmentos continuos usan false: dan impulso base sin acumular cadena.
    /// </summary>
    private float CalculateImpulse(bool incrementConsecutive = true)
    {
        float now = Time.unscaledTime;
        if (incrementConsecutive)
        {
            if (now - lastSwipeTime <= consecutiveWindowSeconds)
                consecutiveCount = Mathf.Min(consecutiveCount + 1, maxConsecutiveCount);
            else
                consecutiveCount = 0;
            lastSwipeTime = now;
        }

        float rawImpulse      = baseSwipeImpulse + consecutiveCount * consecutiveImpulseBonus;
        float normalizedSpeed = movementMotor.MaxSpeed > 0f
            ? Mathf.Clamp01(movementMotor.CurrentSpeed / movementMotor.MaxSpeed)
            : 0f;

        return rawImpulse * (1f - normalizedSpeed * impulseSpeedReductionFactor);
    }

    /// <summary>
    /// Penalización de velocidad basada en el rawAngle (intención real del giro),
    /// no en el ángulo aplicado. Así un intento de giro lateral siempre cuesta
    /// velocidad, aunque la física haya restringido el giro real.
    /// El costo también escala con la velocidad actual: a más velocidad, más se pierde.
    /// </summary>
    private void ApplyTurnSpeedCost(float absRaw, float brakeThresh)
    {
        if (!applyTurnSpeedCost || absRaw < 0.5f) return;

        float normalizedSpeed = movementMotor.MaxSpeed > 0f
            ? Mathf.Clamp01(movementMotor.CurrentSpeed / movementMotor.MaxSpeed)
            : 0f;

        // turnT: 0 = sin giro, 1 = giro máximo antes del freno.
        float turnT = brakeThresh > 0f ? Mathf.Clamp01(absRaw / brakeThresh) : 1f;

        // El costo escala también con la velocidad: girar rápido cuesta más.
        float effectiveTurnT = turnT * normalizedSpeed;

        float multiplier = Mathf.Lerp(1f, turnSpeedMultiplierAtMaxAngle, effectiveTurnT);
        movementMotor.MultiplySpeed(multiplier);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Aplica rotación usando <see cref="gestureReferenceForward"/> como base absoluta.
    /// Todos los segmentos del mismo gesto convergen al mismo destino mundo,
    /// eliminando la acumulación de deltas que causaba sobrerotación.
    /// </summary>
    private void RotateFace(float angleDeg, bool giveImpulse, float impulse)
    {
        // Rotar desde la referencia del gesto (no desde la cara actual).
        Vector3 dir = Quaternion.AngleAxis(angleDeg, Vector3.up) * gestureReferenceForward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = gestureReferenceForward;
        dir.Normalize();

        rotationController.SetTargetForward(dir);

        if (giveImpulse && impulse > 0f)
            movementMotor.ApplyImpulseInDirection(dir, impulse);
    }

    private Vector3 GetCurrentFace()
        => rotationController != null ? rotationController.CurrentForward : Vector3.forward;

    #endregion
}