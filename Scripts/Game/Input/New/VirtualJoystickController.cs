using UnityEngine;

/// <summary>
/// Control direccional táctil para la pelota.
/// 
/// Este controlador usa un único centro lógico de control y una lectura angular continua del dedo.
/// 
/// Convención angular en pantalla:
/// 0°   = derecha.
/// 90°  = adelante.
/// 180° = izquierda.
/// 270° = atrás.
/// 
/// Reglas principales:
/// - El centro X se captura desde la X de la bola en pantalla al iniciar el toque.
/// - El centro Y nace desde la posición inicial del dedo menos un offset vertical.
/// - La dirección se calcula constantemente desde el centro hacia el dedo.
/// - La rotación lateral no agrega velocidad.
/// - El impulso solo se aplica cuando el ángulo está cerca de 90°.
/// - Si el dedo va hacia atrás y la bola todavía tiene velocidad, primero frena.
/// - Si la bola ya está casi detenida, se permite girar hacia atrás.
/// - A mayor velocidad, menor cantidad de grados aceptados por cada cambio del dedo.
/// </summary>
[DefaultExecutionOrder(10)]
public sealed class VirtualJoystickController : MonoBehaviour
{
    #region Constants

    private const float MinVectorSqrMagnitude = 0.0001f;
    private const float FullCircleDegrees = 360f;
    private const float ForwardAngleDegrees = 90f;

    #endregion

    #region Inspector

    [Header("Referencias")]

    [SerializeField]
    [Tooltip("Input táctil crudo de la escena.")]
    private UnifiedBallInput unifiedInput;

    [SerializeField]
    [Tooltip("Transform de la pelota. Se usa para capturar la X de la bola en pantalla al iniciar el toque.")]
    private Transform ballTransform;

    [SerializeField]
    [Tooltip("Cámara principal. Solo se usa para obtener la X inicial de la bola en pantalla.")]
    private Camera mainCamera;

    [SerializeField]
    [Tooltip("Motor de movimiento de la pelota.")]
    private BallMovementMotor movementMotor;

    [SerializeField]
    [Tooltip("Controlador de rotación lógica de la esfera.")]
    private SphereRotationController rotationController;

    [Header("Centro del Control")]

    [SerializeField]
    [Tooltip("Offset vertical en píxeles aplicado al crear el centro.\n" +
             "El centro queda debajo del dedo inicial para que el primer toque ya tenga intención hacia adelante.")]
    [Min(0f)]
    private float controlCenterYOffsetPx = 85f;

    [SerializeField]
    [Tooltip("Zona muerta en píxeles alrededor del centro direccional.\n" +
             "Dentro de esta distancia no se actualiza dirección, velocidad ni freno.")]
    [Min(0f)]
    private float directionDeadzonePx = 18f;

    [SerializeField]
    [Tooltip("Radio máximo en píxeles usado para normalizar la intensidad del input.\n" +
             "A esta distancia, la intención de impulso o freno llega al 100%.")]
    [Min(1f)]
    private float maxInputRadiusPx = 220f;

    [Header("Impulso Frontal")]

    [SerializeField]
    [Tooltip("Rango angular total alrededor de 90° en el que se permite acelerar.\n" +
             "Ejemplo: 60° significa que se acelera entre 60° y 120°.\n" +
             "Fuera de ese rango, el input solo rota pero no agrega velocidad.")]
    [Range(1f, 180f)]
    private float forwardImpulseAngleRangeDeg = 60f;

    [SerializeField]
    [Tooltip("Curva de intensidad angular del impulso.\n" +
             "X = distancia normalizada al centro del rango frontal.\n" +
             "0 = exactamente 90°. 1 = borde del rango.\n" +
             "Y = intensidad del impulso.")]
    private AnimationCurve forwardImpulseAngleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [SerializeField]
    [Tooltip("Curva de intensidad por distancia.\n" +
             "X = distancia del dedo al centro normalizada por maxInputRadiusPx.\n" +
             "Y = intensidad de velocidad solicitada.")]
    private AnimationCurve inputDistanceCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Restricción Angular por Velocidad")]

    [SerializeField]
    [Tooltip("Activa la restricción de grados aceptados por cambio de input según velocidad.\n" +
             "Esto evita que a velocidad alta un gesto brusco cambie toda la dirección de golpe.")]
    private bool useSpeedBasedAngleRestriction = true;

    [SerializeField]
    [Tooltip("Máximo cambio angular aceptado por movimiento del dedo cuando la bola está detenida.")]
    [Range(1f, 180f)]
    private float acceptedAngleChangeAtMinSpeedDeg = 180f;

    [SerializeField]
    [Tooltip("Máximo cambio angular aceptado por movimiento del dedo cuando la bola está a velocidad máxima.\n" +
             "Ejemplo: 5° hace que pedir 45° desde 90° solo cambie aproximadamente hasta 85°.")]
    [Range(1f, 90f)]
    private float acceptedAngleChangeAtMaxSpeedDeg = 5f;

    [SerializeField]
    [Tooltip("Curva de restricción angular según velocidad.\n" +
             "X = velocidad normalizada [0,1].\n" +
             "Y = libertad angular [0,1].\n" +
             "1 usa acceptedAngleChangeAtMinSpeedDeg. 0 usa acceptedAngleChangeAtMaxSpeedDeg.")]
    private AnimationCurve acceptedAngleChangeBySpeedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [SerializeField]
    [Tooltip("Cambios angulares menores a este valor se ignoran para evitar ruido de dedo.")]
    [Range(0f, 5f)]
    private float rawAngleNoiseDeadzoneDeg = 0.25f;

    [Header("Freno / Giro hacia Atrás")]

    [SerializeField]
    [Tooltip("Velocidad en m/s por debajo de la cual se permite girar hacia atrás.\n" +
             "Si la velocidad es mayor, el input hacia atrás solo frena y no cambia la cara hacia atrás.")]
    [Min(0f)]
    private float rearTurnAllowedSpeedThreshold = 0.18f;

    [SerializeField]
    [Tooltip("Distancia vertical mínima en píxeles bajo el centro para considerar que el jugador quiere ir hacia atrás.\n" +
             "Evita activar freno por micro-movimientos cerca del eje horizontal.")]
    [Min(0f)]
    private float rearInputDeadzoneYPx = 8f;

    [SerializeField]
    [Tooltip("Distancia vertical en píxeles hacia atrás necesaria para alcanzar freno máximo.")]
    [Min(1f)]
    private float maxBrakeDistancePx = 180f;

    [Header("Rotación Física de la Cara")]

    [SerializeField]
    [Tooltip("Velocidad de giro en grados/segundo cuando la bola está detenida o muy lenta.\n" +
             "Esto solo controla la transición visual/lógica hacia el ángulo ya aceptado por la restricción angular.")]
    [Min(1f)]
    private float turnRateAtMinSpeed = 900f;

    [SerializeField]
    [Tooltip("Velocidad de giro en grados/segundo cuando la bola va a velocidad máxima.\n" +
             "Esto solo controla la transición visual/lógica hacia el ángulo ya aceptado por la restricción angular.")]
    [Min(1f)]
    private float turnRateAtMaxSpeed = 260f;

    [SerializeField]
    [Tooltip("Curva de peso de giro según velocidad.\n" +
             "X = velocidad normalizada [0,1].\n" +
             "Y = peso de giro [0,1], donde 1 usa turnRateAtMinSpeed y 0 usa turnRateAtMaxSpeed.")]
    private AnimationCurve turnRateBySpeedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Debug")]

    [SerializeField]
    [Tooltip("Muestra información del control en pantalla y consola.")]
    private bool debugController;

    #endregion

    #region Runtime

    private bool isActive;

    private Vector2 controlCenter;
    private Vector2 currentFingerPosition;
    private Vector2 lastFingerPosition;

    private Vector3 referenceForward = Vector3.forward;
    private Vector3 lastValidWorldDirection = Vector3.forward;

    private float lastRawScreenAngle = ForwardAngleDegrees;
    private float acceptedScreenAngle = ForwardAngleDegrees;
    private float lastEvaluatedRawScreenAngle = ForwardAngleDegrees;

    private bool hasValidDirection;

    #endregion

    #region Properties

    /// <summary>
    /// Indica si el control táctil está activo.
    /// </summary>
    public bool IsActive => isActive;

    /// <summary>
    /// Centro lógico actual del control direccional.
    /// </summary>
    public Vector2 ControlCenter => controlCenter;

    /// <summary>
    /// Último ángulo crudo leído desde el dedo.
    /// </summary>
    public float LastRawScreenAngle => lastRawScreenAngle;

    /// <summary>
    /// Último ángulo aceptado después de aplicar restricción por velocidad.
    /// </summary>
    public float AcceptedScreenAngle => acceptedScreenAngle;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        unifiedInput = FindFirstObjectByType<UnifiedBallInput>();
        movementMotor = GetComponent<BallMovementMotor>();
        rotationController = GetComponent<SphereRotationController>();
        ballTransform = transform;
        mainCamera = Camera.main;
    }

    private void Awake()
    {
        if (unifiedInput == null)
        {
            unifiedInput = FindFirstObjectByType<UnifiedBallInput>();
        }

        if (movementMotor == null)
        {
            movementMotor = GetComponent<BallMovementMotor>();
        }

        if (rotationController == null)
        {
            rotationController = GetComponent<SphereRotationController>();
        }

        if (ballTransform == null)
        {
            ballTransform = transform;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        referenceForward = ResolveCurrentForward();
        lastValidWorldDirection = referenceForward;
    }

    private void OnEnable()
    {
        if (unifiedInput == null)
        {
            return;
        }

        unifiedInput.OnTouchBegan += HandleTouchBegan;
        unifiedInput.OnTouchMoved += HandleTouchMoved;
        unifiedInput.OnTouchEnded += HandleTouchEnded;
    }

    private void OnDisable()
    {
        if (unifiedInput != null)
        {
            unifiedInput.OnTouchBegan -= HandleTouchBegan;
            unifiedInput.OnTouchMoved -= HandleTouchMoved;
            unifiedInput.OnTouchEnded -= HandleTouchEnded;
        }

        StopControl();
    }

    private void OnValidate()
    {
        controlCenterYOffsetPx = Mathf.Max(0f, controlCenterYOffsetPx);
        directionDeadzonePx = Mathf.Max(0f, directionDeadzonePx);
        maxInputRadiusPx = Mathf.Max(1f, maxInputRadiusPx);

        forwardImpulseAngleRangeDeg = Mathf.Clamp(forwardImpulseAngleRangeDeg, 1f, 180f);

        acceptedAngleChangeAtMinSpeedDeg = Mathf.Clamp(acceptedAngleChangeAtMinSpeedDeg, 1f, 180f);
        acceptedAngleChangeAtMaxSpeedDeg = Mathf.Clamp(acceptedAngleChangeAtMaxSpeedDeg, 1f, acceptedAngleChangeAtMinSpeedDeg);
        rawAngleNoiseDeadzoneDeg = Mathf.Clamp(rawAngleNoiseDeadzoneDeg, 0f, 5f);

        rearTurnAllowedSpeedThreshold = Mathf.Max(0f, rearTurnAllowedSpeedThreshold);
        rearInputDeadzoneYPx = Mathf.Max(0f, rearInputDeadzoneYPx);
        maxBrakeDistancePx = Mathf.Max(1f, maxBrakeDistancePx);

        turnRateAtMinSpeed = Mathf.Max(1f, turnRateAtMinSpeed);
        turnRateAtMaxSpeed = Mathf.Max(1f, turnRateAtMaxSpeed);
    }

    /// <summary>
    /// Evalúa el input activo después de que el motor actualizó su velocidad.
    /// </summary>
    private void FixedUpdate()
    {
        if (!isActive || movementMotor == null || rotationController == null)
        {
            return;
        }

        ProcessDirectionalInput();
    }

    #endregion

    #region Input Handlers

    private void HandleTouchBegan(Vector2 screenPosition)
    {
        isActive = true;

        currentFingerPosition = screenPosition;
        lastFingerPosition = screenPosition;

        float centerX = GetBallScreenX();
        float centerY = screenPosition.y - controlCenterYOffsetPx;

        controlCenter = new Vector2(centerX, centerY);

        referenceForward = ResolveCurrentForward();
        lastValidWorldDirection = referenceForward;

        lastRawScreenAngle = ForwardAngleDegrees;
        acceptedScreenAngle = ForwardAngleDegrees;
        lastEvaluatedRawScreenAngle = ForwardAngleDegrees;

        hasValidDirection = false;

        movementMotor?.SetJoystickBrakeStrength(0f);
        movementMotor?.SetJoystickInput(Vector3.zero, 0f);

        if (debugController)
        {
            Debug.Log($"[DirectionalTouch] Begin | Center:{controlCenter:F0} | Reference:{referenceForward:F2}");
        }
    }

    private void HandleTouchMoved(Vector2 screenPosition, Vector2 _)
    {
        if (!isActive)
        {
            return;
        }

        currentFingerPosition = screenPosition;
        lastFingerPosition = screenPosition;
    }

    private void HandleTouchEnded(Vector2 screenPosition)
    {
        if (!isActive)
        {
            return;
        }

        currentFingerPosition = screenPosition;
        lastFingerPosition = screenPosition;

        StopControl();
    }

    #endregion

    #region Processing

    /// <summary>
    /// Procesa la posición actual del dedo y la convierte en rotación, impulso o freno.
    /// </summary>
    private void ProcessDirectionalInput()
    {
        Vector2 inputVector = currentFingerPosition - controlCenter;

        if (inputVector.sqrMagnitude < directionDeadzonePx * directionDeadzonePx)
        {
            movementMotor.SetJoystickInput(Vector3.zero, 0f);
            movementMotor.SetJoystickBrakeStrength(0f);
            return;
        }

        float inputDistance01 = Mathf.Clamp01(inputVector.magnitude / maxInputRadiusPx);
        float rawScreenAngle = ResolveScreenAngle(inputVector);

        lastRawScreenAngle = rawScreenAngle;
        hasValidDirection = true;

        bool isRearInput = inputVector.y < -rearInputDeadzoneYPx;

        if (isRearInput)
        {
            ProcessRearInput(inputVector, inputDistance01, rawScreenAngle);
            return;
        }

        ProcessFrontOrSideInput(inputDistance01, rawScreenAngle);
    }

    /// <summary>
    /// Procesa input frontal o lateral.
    /// La dirección usa el ángulo aceptado por restricción de velocidad.
    /// La velocidad usa el ángulo crudo del dedo para evitar aceleración lateral accidental.
    /// </summary>
    private void ProcessFrontOrSideInput(float inputDistance01, float rawScreenAngle)
    {
        movementMotor.SetJoystickBrakeStrength(0f);

        float restrictedAngle = ResolveSpeedRestrictedScreenAngle(rawScreenAngle);
        Vector2 restrictedDirection = ScreenAngleToDirection(restrictedAngle);

        Vector3 desiredWorldDirection = ProjectScreenDirectionRelativeToForward(
            restrictedDirection,
            referenceForward);

        Vector3 appliedDirection = ApplyWeightedRotation(desiredWorldDirection);

        float targetSpeed = ResolveForwardTargetSpeed(
            inputDistance01,
            rawScreenAngle,
            movementMotor.MaxSpeed);

        movementMotor.SetJoystickInput(
            targetSpeed > 0f ? appliedDirection : Vector3.zero,
            targetSpeed);

        if (debugController)
        {
            Debug.Log(
                $"[DirectionalTouch] FRONT/SIDE | raw:{rawScreenAngle:F1}° | " +
                $"accepted:{restrictedAngle:F1}° | target:{targetSpeed:F2} | dir:{appliedDirection:F2}");
        }
    }

    /// <summary>
    /// Procesa input hacia atrás. Primero frena; solo permite girar atrás cuando la velocidad es casi cero.
    /// </summary>
    private void ProcessRearInput(Vector2 inputVector, float inputDistance01, float rawScreenAngle)
    {
        float currentSpeed = movementMotor.CurrentSpeed;

        if (currentSpeed > rearTurnAllowedSpeedThreshold)
        {
            float brakeStrength = Mathf.Clamp01(Mathf.Abs(inputVector.y) / maxBrakeDistancePx);

            movementMotor.SetJoystickInput(Vector3.zero, 0f);
            movementMotor.SetJoystickBrakeStrength(brakeStrength);

            if (debugController)
            {
                Debug.Log(
                    $"[DirectionalTouch] BRAKE | raw:{rawScreenAngle:F1}° | " +
                    $"strength:{brakeStrength:F2} | speed:{currentSpeed:F2}");
            }

            return;
        }

        movementMotor.SetJoystickBrakeStrength(0f);

        float restrictedAngle = ResolveSpeedRestrictedScreenAngle(rawScreenAngle);
        Vector2 restrictedDirection = ScreenAngleToDirection(restrictedAngle);

        Vector3 desiredWorldDirection = ProjectScreenDirectionRelativeToForward(
            restrictedDirection,
            referenceForward);

        Vector3 appliedDirection = ApplyWeightedRotation(desiredWorldDirection);

        movementMotor.SetJoystickInput(Vector3.zero, 0f);

        if (debugController)
        {
            Debug.Log(
                $"[DirectionalTouch] REAR TURN | raw:{rawScreenAngle:F1}° | " +
                $"accepted:{restrictedAngle:F1}° | speed:{currentSpeed:F2} | dir:{appliedDirection:F2}");
        }
    }

    #endregion

    #region Angle Restriction

    /// <summary>
    /// Resuelve el ángulo aceptado por el sistema según la velocidad actual.
    /// 
    /// La restricción opera sobre el cambio del dedo, no sobre el destino absoluto.
    /// Por eso, si el dedo se queda fijo en 45°, el sistema acepta una porción del cambio
    /// y luego no sigue acumulando giro hasta que el dedo vuelva a moverse o se inicie otro gesto.
    /// </summary>
    private float ResolveSpeedRestrictedScreenAngle(float rawScreenAngle)
    {
        if (!useSpeedBasedAngleRestriction)
        {
            lastEvaluatedRawScreenAngle = rawScreenAngle;
            acceptedScreenAngle = rawScreenAngle;
            return acceptedScreenAngle;
        }

        float rawDelta = Mathf.DeltaAngle(lastEvaluatedRawScreenAngle, rawScreenAngle);
        lastEvaluatedRawScreenAngle = rawScreenAngle;

        if (Mathf.Abs(rawDelta) <= rawAngleNoiseDeadzoneDeg)
        {
            return acceptedScreenAngle;
        }

        float allowedAngleChange = ResolveAcceptedAngleChangeBySpeed();
        float appliedDelta = Mathf.Clamp(rawDelta, -allowedAngleChange, allowedAngleChange);

        acceptedScreenAngle = NormalizeAngle360(acceptedScreenAngle + appliedDelta);
        return acceptedScreenAngle;
    }

    /// <summary>
    /// Calcula cuántos grados puede aceptar el gesto actual según la velocidad de la bola.
    /// </summary>
    private float ResolveAcceptedAngleChangeBySpeed()
    {
        if (movementMotor == null || movementMotor.MaxSpeed <= 0f)
        {
            return acceptedAngleChangeAtMinSpeedDeg;
        }

        float speed01 = Mathf.Clamp01(movementMotor.CurrentSpeed / movementMotor.MaxSpeed);
        float freedom01 = Mathf.Clamp01(acceptedAngleChangeBySpeedCurve.Evaluate(speed01));

        return Mathf.Lerp(
            acceptedAngleChangeAtMaxSpeedDeg,
            acceptedAngleChangeAtMinSpeedDeg,
            freedom01);
    }

    #endregion

    #region Direction

    /// <summary>
    /// Aplica rotación ponderada por velocidad actual.
    /// </summary>
    private Vector3 ApplyWeightedRotation(Vector3 desiredWorldDirection)
    {
        desiredWorldDirection.y = 0f;

        if (desiredWorldDirection.sqrMagnitude < MinVectorSqrMagnitude)
        {
            return lastValidWorldDirection;
        }

        desiredWorldDirection.Normalize();

        Vector3 currentForward = ResolveCurrentForward();

        float turnRate = ResolveTurnRateBySpeed();
        float maxRadians = Mathf.Deg2Rad * turnRate * Time.fixedDeltaTime;

        Vector3 smoothedDirection = Vector3.RotateTowards(
            currentForward,
            desiredWorldDirection,
            maxRadians,
            0f);

        smoothedDirection.y = 0f;

        if (smoothedDirection.sqrMagnitude < MinVectorSqrMagnitude)
        {
            smoothedDirection = desiredWorldDirection;
        }
        else
        {
            smoothedDirection.Normalize();
        }

        rotationController.SetForward(smoothedDirection);
        lastValidWorldDirection = smoothedDirection;

        return smoothedDirection;
    }

    /// <summary>
    /// Convierte un vector de pantalla en una dirección mundo usando un forward estable capturado al inicio del toque.
    /// </summary>
    private Vector3 ProjectScreenDirectionRelativeToForward(Vector2 screenDirection, Vector3 forwardReference)
    {
        forwardReference.y = 0f;

        if (forwardReference.sqrMagnitude < MinVectorSqrMagnitude)
        {
            forwardReference = Vector3.forward;
        }

        forwardReference.Normalize();

        Vector3 rightReference = Vector3.Cross(Vector3.up, forwardReference);

        if (rightReference.sqrMagnitude < MinVectorSqrMagnitude)
        {
            rightReference = Vector3.right;
        }

        rightReference.Normalize();

        Vector3 worldDirection =
            rightReference * screenDirection.x +
            forwardReference * screenDirection.y;

        worldDirection.y = 0f;

        return worldDirection.sqrMagnitude > MinVectorSqrMagnitude
            ? worldDirection.normalized
            : forwardReference;
    }

    /// <summary>
    /// Resuelve el ángulo de pantalla con la convención 0° derecha, 90° arriba, 180° izquierda.
    /// </summary>
    private static float ResolveScreenAngle(Vector2 inputVector)
    {
        float angle = Mathf.Atan2(inputVector.y, inputVector.x) * Mathf.Rad2Deg;

        if (angle < 0f)
        {
            angle += FullCircleDegrees;
        }

        return angle;
    }

    /// <summary>
    /// Convierte un ángulo de pantalla a dirección 2D normalizada.
    /// </summary>
    private static Vector2 ScreenAngleToDirection(float screenAngle)
    {
        float radians = screenAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
    }

    /// <summary>
    /// Normaliza un ángulo al rango [0, 360).
    /// </summary>
    private static float NormalizeAngle360(float angle)
    {
        angle %= FullCircleDegrees;

        if (angle < 0f)
        {
            angle += FullCircleDegrees;
        }

        return angle;
    }

    /// <summary>
    /// Resuelve el forward lógico actual de la bola.
    /// </summary>
    private Vector3 ResolveCurrentForward()
    {
        if (rotationController != null)
        {
            Vector3 forward = rotationController.CurrentForward;
            forward.y = 0f;

            if (forward.sqrMagnitude > MinVectorSqrMagnitude)
            {
                return forward.normalized;
            }
        }

        Vector3 fallback = transform.forward;
        fallback.y = 0f;

        return fallback.sqrMagnitude > MinVectorSqrMagnitude
            ? fallback.normalized
            : Vector3.forward;
    }

    #endregion

    #region Speed And Brake

    /// <summary>
    /// Calcula la velocidad objetivo. Solo devuelve valores positivos cuando el input está cerca del frente.
    /// </summary>
    private float ResolveForwardTargetSpeed(float inputDistance01, float rawScreenAngle, float maxSpeed)
    {
        if (maxSpeed <= 0f)
        {
            return 0f;
        }

        float halfRange = forwardImpulseAngleRangeDeg * 0.5f;
        float angleDistance = Mathf.Abs(Mathf.DeltaAngle(ForwardAngleDegrees, rawScreenAngle));

        if (angleDistance > halfRange)
        {
            return 0f;
        }

        float angleDistance01 = Mathf.Clamp01(angleDistance / halfRange);
        float angleFactor = Mathf.Clamp01(forwardImpulseAngleCurve.Evaluate(angleDistance01));
        float distanceFactor = Mathf.Clamp01(inputDistanceCurve.Evaluate(inputDistance01));

        return maxSpeed * angleFactor * distanceFactor;
    }

    /// <summary>
    /// Resuelve la tasa de giro actual según velocidad.
    /// </summary>
    private float ResolveTurnRateBySpeed()
    {
        if (movementMotor == null || movementMotor.MaxSpeed <= 0f)
        {
            return turnRateAtMinSpeed;
        }

        float speed01 = Mathf.Clamp01(movementMotor.CurrentSpeed / movementMotor.MaxSpeed);
        float curveValue = Mathf.Clamp01(turnRateBySpeedCurve.Evaluate(speed01));

        return Mathf.Lerp(turnRateAtMaxSpeed, turnRateAtMinSpeed, curveValue);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Detiene la lectura activa del control sin alterar la dirección actual de la bola.
    /// </summary>
    private void StopControl()
    {
        isActive = false;

        movementMotor?.SetJoystickInput(Vector3.zero, 0f);
        movementMotor?.SetJoystickBrakeStrength(0f);

        hasValidDirection = false;
    }

    /// <summary>
    /// Devuelve la posición X de la bola en pantalla.
    /// </summary>
    private float GetBallScreenX()
    {
        if (mainCamera == null || ballTransform == null)
        {
            return Screen.width * 0.5f;
        }

        Vector3 screenPosition = mainCamera.WorldToScreenPoint(ballTransform.position);

        return screenPosition.z > 0f
            ? screenPosition.x
            : Screen.width * 0.5f;
    }

    #endregion

    #region Debug

    private void OnGUI()
    {
        if (!debugController || !isActive || movementMotor == null)
        {
            return;
        }

        Vector2 inputVector = currentFingerPosition - controlCenter;
        float inputDistance = inputVector.magnitude;
        float inputDistance01 = Mathf.Clamp01(inputDistance / maxInputRadiusPx);
        bool isRearInput = inputVector.y < -rearInputDeadzoneYPx;

        float targetSpeed = isRearInput
            ? 0f
            : ResolveForwardTargetSpeed(inputDistance01, lastRawScreenAngle, movementMotor.MaxSpeed);

        float brakeStrength = isRearInput && movementMotor.CurrentSpeed > rearTurnAllowedSpeedThreshold
            ? Mathf.Clamp01(Mathf.Abs(inputVector.y) / maxBrakeDistancePx)
            : 0f;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26
        };

        GUI.Label(
            new Rect(10, 10, 1300, 32),
            $"Mode: {(isRearInput ? "REAR/BRAKE" : "FRONT/SIDE")} | Speed: {movementMotor.CurrentSpeed:F2}/{movementMotor.MaxSpeed:F1}",
            style);

        GUI.Label(
            new Rect(10, 45, 1300, 32),
            $"Center: {controlCenter:F0} | Finger: {currentFingerPosition:F0} | Vector: {inputVector:F0}",
            style);

        GUI.Label(
            new Rect(10, 80, 1300, 32),
            $"RawAngle: {lastRawScreenAngle:F1}° | AcceptedAngle: {acceptedScreenAngle:F1}° | AllowedStep: {ResolveAcceptedAngleChangeBySpeed():F1}°",
            style);

        GUI.Label(
            new Rect(10, 115, 1300, 32),
            $"TargetSpeed: {targetSpeed:F2} | Brake: {brakeStrength:F2} | TurnRate: {ResolveTurnRateBySpeed():F0}°/s",
            style);

        GUI.Label(
            new Rect(10, 150, 1300, 32),
            $"ReferenceForward: {referenceForward:F2} | LastDirection: {lastValidWorldDirection:F2} | HasDir:{hasValidDirection}",
            style);
    }

    #endregion
}