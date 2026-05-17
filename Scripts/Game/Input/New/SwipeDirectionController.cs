// using UnityEngine;

// /// <summary>
// /// Procesa swipes emitidos por <see cref="UnifiedBallInput"/> y los traduce en
// /// rotación de cara e impulso de velocidad sobre la pelota.
// ///
// /// ═══ Sistema de coordenadas ═══
// /// El usuario describe ángulos en su sistema: 90°=forward, 0°=derecha, 180°=izquierda, 270°=backward.
// /// Internamente, rawAngle = Atan2(screenDir.x, screenDir.y) mapea así:
// ///   rawAngle  0°  → usuario 90°  (forward, cara principal)
// ///   rawAngle ±90° → usuario 0°/180° (giros laterales puros)
// ///   rawAngle ±180°→ usuario 270° (backward)
// ///
// /// ═══ Cuatro zonas por |rawAngle| ═══
// ///
// ///   ZONA MUERTA     [0°, deadZoneDeg)
// ///     Swipe casi recto hacia el forward. Da impulso en la dirección actual de la cara.
// ///     Sin rotación. ClearTarget() cancela cualquier rotación pendiente.
// ///
// ///   ZONA DE IMPULSO [deadZoneDeg, impulseZoneDeg)
// ///     Swipe ligeramente desviado del forward. Da impulso + rotación pequeña (con rampa suave).
// ///
// ///   ZONA DE GIRO    [impulseZoneDeg, 180°−brakeZoneDeg)
// ///     Swipe claramente direccional. Da SOLO rotación, sin impulso.
// ///     El costo de velocidad escala con el ángulo del swipe y con la velocidad actual.
// ///
// ///   ZONA DE FRENO   [180°−brakeZoneDeg, 180°]
// ///     Swipe hacia atrás. Frena si la bola se mueve; rota la cara si está detenida.
// ///
// /// ═══ Acumulación de impulso ═══
// /// Cada swipe en zona muerta o de impulso suma velocidad a la bola.
// /// El impulso se reduce inversamente a la velocidad actual (<see cref="impulseSpeedReductionFactor"/>)
// /// para que la aceleración sea progresiva y no se llegue al máximo en un solo swipe.
// /// Los swipes consecutivos dentro de <see cref="consecutiveWindowSeconds"/> acumulan un bonus.
// /// </summary>
// public sealed class SwipeDirectionController : MonoBehaviour
// {
//     #region Inspector

//     [Header("Referencias")]
//     [SerializeField]
//     [Tooltip("Input unificado de la escena. Fuente de los eventos de swipe.")]
//     private UnifiedBallInput unifiedInput;

//     [SerializeField]
//     [Tooltip("Motor de movimiento de la pelota.")]
//     private BallMovementMotor movementMotor;

//     [SerializeField]
//     [Tooltip("Controlador de rotación lógica de la esfera.")]
//     private SphereRotationController rotationController;

//     [Header("Zona Muerta")]
//     [SerializeField]
//     [Tooltip("Swipes con |rawAngle| menor a este valor se tratan como forward exacto.\n" +
//              "Resultado: impulso recto sin rotación ni costo de velocidad.\n" +
//              "En el sistema del usuario: protege el rango (90°±deadZoneDeg).")]
//     [Range(0f, 45f)]
//     private float deadZoneDeg = 5f;

//     [Header("Zona de Impulso")]
//     [SerializeField]
//     [Tooltip("Swipes con |rawAngle| entre deadZoneDeg y este valor dan impulso + rotación pequeña.\n" +
//              "Swipes fuera de este rango solo rotan (sin impulso).\n" +
//              "En el sistema del usuario: solo swipes cercanos a 90° aceleran la bola.")]
//     [Range(0f, 90f)]
//     private float impulseZoneDeg = 10f;

//     [Header("Impulso por Swipe")]
//     [SerializeField]
//     [Tooltip("Impulso base en m/s añadido por cada swipe en zona muerta o de impulso.")]
//     private float baseSwipeImpulse = 3f;

//     [SerializeField]
//     [Tooltip("Impulso adicional en m/s acumulado por cada swipe consecutivo.\n" +
//              "Se suma al base hasta maxConsecutiveCount veces.")]
//     private float consecutiveImpulseBonus = 0.5f;

//     [SerializeField]
//     [Tooltip("Número máximo de swipes consecutivos acumulables.")]
//     [Min(0)]
//     private int maxConsecutiveCount = 5;

//     [SerializeField]
//     [Tooltip("Tiempo máximo en segundos entre swipes para que cuenten como consecutivos.")]
//     private float consecutiveWindowSeconds = 0.5f;

//     [SerializeField]
//     [Tooltip("Cuánto se reduce el impulso conforme la bola se acerca a la velocidad máxima.\n" +
//              "0.0 = impulso constante independiente de la velocidad.\n" +
//              "0.75 = a velocidad máxima el impulso queda en el 25% del valor base.\n" +
//              "Impide llegar al máximo en un solo swipe y hace la aceleración progresiva.")]
//     [Range(0f, 1f)]
//     private float impulseSpeedReductionFactor = 0.75f;

//     [Header("Curva de Reducción de Giro")]
//     [SerializeField]
//     [Tooltip("Rango de transición suave en grados desde el borde del dead zone.\n" +
//              "Dentro de este rango la influence sube de 0 a maxRotationInfluence gradualmente.\n" +
//              "Elimina el salto brusco entre zona muerta (0° rotación) y zona de curva.")]
//     [Range(0f, 45f)]
//     private float rotationRampDeg = 20f;

//     [SerializeField]
//     [Tooltip("Techo máximo de influence de rotación, alcanzado cuando el swipe es muy largo\n" +
//              "y la velocidad es cero. 1.0 = giro completo sin reducción global.")]
//     [Range(0f, 1f)]
//     private float maxRotationInfluence = 0.6f;

//     [SerializeField]
//     [Tooltip("Influence mínima de rotación cuando la velocidad es máxima y el desvío del forward es máximo.")]
//     [Range(0f, 1f)]
//     private float minInfluenceAtMaxRestriction = 0.08f;

//     [SerializeField]
//     [Tooltip("Exponente de la curva de velocidad en el factor de restricción de giro.\n" +
//              "0.2 = restricción brusca desde velocidades bajas. 1.0 = lineal.")]
//     [Range(0.05f, 2f)]
//     private float swipeRotationFalloffExponent = 0.2f;

//     [Header("Escalado de Impulso por Longitud")]
//     [SerializeField]
//     [Tooltip("Factor mínimo de impulso para el swipe de longitud mínima detectable.\n" +
//              "0.4 = un swipe corto da el 40% del impulso calculado.\n" +
//              "Rango recomendado: 0.2–0.6.")]
//     [Range(0f, 1f)]
//     private float minLengthImpulseScale = 0.4f;

//     [SerializeField]
//     [Tooltip("Longitud de swipe en píxeles a partir de la cual el impulso se aplica al 100%.\n" +
//              "Swipes más largos no superan el 100%.")]
//     private float fullImpulseLengthPx = 200f;

//     [Header("Escalado por Longitud del Swipe")]
//     [SerializeField]
//     [Tooltip("Factor mínimo de rotación para el swipe de longitud mínima detectable.\n" +
//              "0.35 = un swipe corto aplica el 35% del ángulo calculado.")]
//     [Range(0f, 1f)]
//     private float minLengthRotationScale = 0.35f;

//     [SerializeField]
//     [Tooltip("Longitud de swipe en píxeles a partir de la cual el ángulo se aplica al 100%.")]
//     private float fullRotationLengthPx = 160f;

//     [Header("Costo de Velocidad al Girar")]
//     [SerializeField]
//     [Tooltip("Si activo, cada swipe en zona de giro reduce la velocidad de la bola.\n" +
//              "El costo escala con el ángulo del swipe (no el aplicado) y con la velocidad actual,\n" +
//              "de modo que girar rápido tiene un costo mayor que girar despacio.")]
//     private bool applyTurnSpeedCost = true;

//     [SerializeField]
//     [Tooltip("Multiplicador de velocidad cuando el rawAngle es el máximo antes de la zona de freno.\n" +
//              "0.4 = un giro lateral puro a velocidad máxima reduce la velocidad al 40%.")]
//     [Range(0f, 1f)]
//     private float turnSpeedMultiplierAtMaxAngle = 0.4f;

//     [Header("Zona de Freno")]
//     [SerializeField]
//     [Tooltip("Grados a cada lado del backward puro (rawAngle ±180°) que activan el freno.\n" +
//              "Ejemplo: 10° → frena cuando |rawAngle| ≥ 170°.\n" +
//              "Swipes entre la zona de giro y la de freno siguen siendo giros, no frenos.")]
//     [Range(0f, 90f)]
//     private float brakeZoneDeg = 10f;

//     [SerializeField]
//     [Tooltip("Velocidad en m/s restada por cada swipe que cae en la zona de freno.")]
//     private float backSwipeBrakeImpulse = 4f;

//     [SerializeField]
//     [Tooltip("Velocidad planar mínima en m/s para considerar la bola en movimiento.\n" +
//              "Swipes de freno con velocidad inferior a este valor rotan la cara en lugar de frenar.")]
//     private float stopThreshold = 0.12f;

//     [Header("Debug")]
//     [SerializeField]
//     [Tooltip("Imprime en consola la zona detectada, rawAngle, ángulo aplicado e impulso de cada swipe.")]
//     private bool debugController;

//     #endregion

//     #region Runtime

//     private int   consecutiveCount = 0;
//     private float lastSwipeTime    = float.NegativeInfinity;

//     #endregion

//     #region Unity Lifecycle

//     private void Reset()
//     {
//         unifiedInput       = FindFirstObjectByType<UnifiedBallInput>();
//         movementMotor      = GetComponent<BallMovementMotor>();
//         rotationController = GetComponent<SphereRotationController>();
//     }

//     private void Awake()
//     {
//         if (unifiedInput       == null) unifiedInput       = FindFirstObjectByType<UnifiedBallInput>();
//         if (movementMotor      == null) movementMotor      = GetComponent<BallMovementMotor>();
//         if (rotationController == null) rotationController = GetComponent<SphereRotationController>();
//     }

//     private void OnEnable()
//     {
//         if (unifiedInput != null) unifiedInput.OnSwipeDetected += HandleSwipe;
//     }

//     private void OnDisable()
//     {
//         if (unifiedInput != null) unifiedInput.OnSwipeDetected -= HandleSwipe;
//     }

//     private void OnValidate()
//     {
//         deadZoneDeg                  = Mathf.Clamp(deadZoneDeg,      0f, 45f);
//         impulseZoneDeg               = Mathf.Max(impulseZoneDeg,     deadZoneDeg);
//         baseSwipeImpulse             = Mathf.Max(0f,                 baseSwipeImpulse);
//         consecutiveImpulseBonus      = Mathf.Max(0f,                 consecutiveImpulseBonus);
//         maxConsecutiveCount          = Mathf.Max(0,                  maxConsecutiveCount);
//         consecutiveWindowSeconds     = Mathf.Max(0.05f,              consecutiveWindowSeconds);
//         impulseSpeedReductionFactor  = Mathf.Clamp01(               impulseSpeedReductionFactor);
//         rotationRampDeg              = Mathf.Clamp(rotationRampDeg,  0f, 45f);
//         minInfluenceAtMaxRestriction = Mathf.Clamp01(               minInfluenceAtMaxRestriction);
//         maxRotationInfluence         = Mathf.Clamp(maxRotationInfluence, minInfluenceAtMaxRestriction, 1f);
//         swipeRotationFalloffExponent = Mathf.Max(0.05f,             swipeRotationFalloffExponent);
//         minLengthImpulseScale        = Mathf.Clamp01(               minLengthImpulseScale);
//         fullImpulseLengthPx          = Mathf.Max(1f,                fullImpulseLengthPx);
//         minLengthRotationScale       = Mathf.Clamp01(               minLengthRotationScale);
//         fullRotationLengthPx         = Mathf.Max(1f,                fullRotationLengthPx);
//         turnSpeedMultiplierAtMaxAngle = Mathf.Clamp01(              turnSpeedMultiplierAtMaxAngle);
//         brakeZoneDeg                 = Mathf.Clamp(brakeZoneDeg,    0f, 90f);
//         backSwipeBrakeImpulse        = Mathf.Max(0f,                backSwipeBrakeImpulse);
//         stopThreshold                = Mathf.Max(0f,                stopThreshold);
//     }

//     #endregion

//     #region Public API

//     /// <summary>Resetea el contador de swipes consecutivos.</summary>
//     public void ResetConsecutive() => consecutiveCount = 0;

//     #endregion

//     #region Swipe Handler

//     private void HandleSwipe(SwipeData swipe)
//     {
//         // rawAngle 0° = forward (cara principal). ±90° = giro lateral. ±180° = backward.
//         float rawAngle    = Mathf.Atan2(swipe.ScreenDirection.x, swipe.ScreenDirection.y)
//                             * Mathf.Rad2Deg;
//         float absRaw      = Mathf.Abs(rawAngle);
//         float brakeThresh = 180f - brakeZoneDeg;

//         // ── ZONA DE FRENO ────────────────────────────────────────────────────────────
//         if (absRaw >= brakeThresh)
//         {
//             consecutiveCount = 0;

//             if (movementMotor.CurrentSpeed > stopThreshold)
//             {
//                 movementMotor.ApplyBrakePulse(backSwipeBrakeImpulse);
//             }
//             else
//             {
//                 // Bola detenida: girar la cara hacia esa dirección sin impulso.
//                 float a = CalculateAppliedAngle(absRaw, rawAngle, brakeThresh, swipe.Length);
//                 ApplyRotation(a);
//             }

//             if (debugController) Debug.Log($"[SwipeDir] FRENO | raw:{rawAngle:F1}°");
//             return;
//         }

//         // ── ZONA MUERTA ──────────────────────────────────────────────────────────────
//         if (absRaw < deadZoneDeg)
//         {
//             rotationController.ClearTarget();
//             float impulse = CalculateImpulse(swipe.Length);
//             movementMotor.ApplyImpulseInDirection(rotationController.CurrentForward, impulse);

//             if (debugController) Debug.Log($"[SwipeDir] MUERTA | raw:{rawAngle:F1}° imp:{impulse:F2}");
//             return;
//         }

//         // ── ZONA DE IMPULSO ──────────────────────────────────────────────────────────
//         if (absRaw < impulseZoneDeg)
//         {
//             float a       = CalculateAppliedAngle(absRaw, rawAngle, brakeThresh, swipe.Length);
//             float impulse = CalculateImpulse(swipe.Length);
//             ApplyRotationAndImpulse(a, impulse);
//             ApplyTurnSpeedCost(absRaw, brakeThresh);

//             if (debugController) Debug.Log($"[SwipeDir] IMPULSO | raw:{rawAngle:F1}° applied:{a:F1}° imp:{impulse:F2}");
//             return;
//         }

//         // ── ZONA DE GIRO ─────────────────────────────────────────────────────────────
//         {
//             float a = CalculateAppliedAngle(absRaw, rawAngle, brakeThresh, swipe.Length);
//             ApplyRotation(a);
//             ApplyTurnSpeedCost(absRaw, brakeThresh);

//             if (debugController) Debug.Log($"[SwipeDir] GIRO | raw:{rawAngle:F1}° applied:{a:F1}°");
//         }
//     }

//     #endregion

//     #region Calculations

//     /// <summary>
//     /// Calcula el ángulo de giro aplicado combinando tres factores:
//     ///   1. Curva de velocidad: a mayor velocidad, menor giro.
//     ///   2. Rampa desde dead zone: transición suave al salir de la zona muerta.
//     ///   3. Escala por longitud: swipes cortos giran menos que swipes largos.
//     /// </summary>
//     private float CalculateAppliedAngle(float absRaw, float rawAngle, float brakeThresh, float length)
//     {
//         // Factor angular: 0 = forward, 1 = justo antes del umbral de freno.
//         float angleT = brakeThresh > 0f ? Mathf.Clamp01(absRaw / brakeThresh) : 1f;

//         // Factor de velocidad con curva de potencia (caída brusca al arrancar si exponent < 1).
//         float normalizedSpeed = movementMotor.MaxSpeed > 0f
//             ? Mathf.Clamp01(movementMotor.CurrentSpeed / movementMotor.MaxSpeed)
//             : 0f;
//         float speedT = Mathf.Pow(normalizedSpeed, swipeRotationFalloffExponent);

//         float influence = Mathf.Lerp(maxRotationInfluence, minInfluenceAtMaxRestriction, angleT * speedT);

//         // Rampa suave desde el borde del dead zone: elimina el salto brusco.
//         float rampT = rotationRampDeg > 0f
//             ? Mathf.Clamp01((absRaw - deadZoneDeg) / rotationRampDeg)
//             : 1f;
//         influence *= rampT;

//         // Escala por longitud del swipe.
//         float lengthScale = Mathf.Lerp(
//             minLengthRotationScale, 1f,
//             Mathf.Clamp01(length / fullRotationLengthPx));

//         return Mathf.Sign(rawAngle) * absRaw * influence * lengthScale;
//     }

//     /// <summary>
//     /// Calcula el impulso del swipe escalado por tres factores:
//     ///   1. Velocidad actual: a mayor velocidad, menor impulso (aceleración progresiva).
//     ///   2. Swipes consecutivos: cada swipe seguido suma un bonus hasta el máximo.
//     ///   3. Longitud del swipe: swipes más largos dan más impulso.
//     /// </summary>
//     private float CalculateImpulse(float swipeLength)
//     {
//         float now = Time.unscaledTime;
//         if (now - lastSwipeTime <= consecutiveWindowSeconds)
//             consecutiveCount = Mathf.Min(consecutiveCount + 1, maxConsecutiveCount);
//         else
//             consecutiveCount = 0;
//         lastSwipeTime = now;

//         float rawImpulse = baseSwipeImpulse + consecutiveCount * consecutiveImpulseBonus;

//         float normalizedSpeed = movementMotor.MaxSpeed > 0f
//             ? Mathf.Clamp01(movementMotor.CurrentSpeed / movementMotor.MaxSpeed)
//             : 0f;
//         float speedScale = 1f - normalizedSpeed * impulseSpeedReductionFactor;

//         float lengthScale = Mathf.Lerp(
//             minLengthImpulseScale, 1f,
//             Mathf.Clamp01(swipeLength / fullImpulseLengthPx));

//         return rawImpulse * speedScale * lengthScale;
//     }

//     /// <summary>
//     /// Penalización de velocidad proporcional al rawAngle del swipe (intención del giro)
//     /// multiplicada por la velocidad actual. Girar rápido tiene mayor costo que girar despacio.
//     /// Se usa el rawAngle, no el applied, para que la penalización refleje la intensidad
//     /// del giro intentado independientemente de cuánto lo haya restringido la curva.
//     /// </summary>
//     private void ApplyTurnSpeedCost(float absRaw, float brakeThresh)
//     {
//         if (!applyTurnSpeedCost || absRaw < 0.5f) return;

//         float normalizedSpeed = movementMotor.MaxSpeed > 0f
//             ? Mathf.Clamp01(movementMotor.CurrentSpeed / movementMotor.MaxSpeed)
//             : 0f;

//         float turnT      = brakeThresh > 0f ? Mathf.Clamp01(absRaw / brakeThresh) : 1f;
//         float multiplier = Mathf.Lerp(1f, turnSpeedMultiplierAtMaxAngle, turnT * normalizedSpeed);
//         movementMotor.MultiplySpeed(multiplier);
//     }

//     #endregion

//     #region Rotation Helpers

//     /// <summary>
//     /// Rota la cara hacia el ángulo indicado (relativo a la cara actual) y aplica el impulso
//     /// en esa misma dirección.
//     /// </summary>
//     private void ApplyRotationAndImpulse(float angleDeg, float impulse)
//     {
//         Vector3 dir = ComputeTargetDirection(angleDeg);
//         rotationController.SetTargetForward(dir);
//         if (impulse > 0f)
//             movementMotor.ApplyImpulseInDirection(dir, impulse);
//     }

//     /// <summary>
//     /// Rota la cara hacia el ángulo indicado (relativo a la cara actual) sin impulso.
//     /// </summary>
//     private void ApplyRotation(float angleDeg)
//     {
//         Vector3 dir = ComputeTargetDirection(angleDeg);
//         rotationController.SetTargetForward(dir);
//     }

//     /// <summary>
//     /// Calcula la dirección mundo destino rotando la cara actual por <paramref name="angleDeg"/>
//     /// grados alrededor del eje Y. Usar la cara actual como base es correcto para swipe puro
//     /// (press→release) ya que cada swipe es un gesto independiente sin acumulación entre gestos.
//     /// </summary>
//     private Vector3 ComputeTargetDirection(float angleDeg)
//     {
//         Vector3 dir = Quaternion.AngleAxis(angleDeg, Vector3.up) * rotationController.CurrentForward;
//         dir.y = 0f;
//         if (dir.sqrMagnitude < 0.0001f) dir = rotationController.CurrentForward;
//         return dir.normalized;
//     }

//     #endregion
// }