using UnityEngine;

/// <summary>
/// Control direccional táctil para la pelota.
///
/// El dedo define continuamente hacia dónde apunta la cara de la bola:
/// el vector (posición bola en pantalla → posición dedo en pantalla)
/// se convierte en una dirección mundo y se aplica instantáneamente a la cara.
/// Esto sustituye al modelo de swipe discreto: la dirección es siempre el
/// estado actual del dedo, no un gesto puntual.
///
/// El movimiento del dedo (delta entre frames) da o quita velocidad:
///   Dedo alejándose de la bola (alineado con la cara)  → impulso en la dirección de la cara.
///   Dedo acercándose a la bola (opuesto a la cara)     → freno proporcional.
///   Movimiento perpendicular → solo cambia dirección, sin efecto en velocidad.
///
/// La propiedad <see cref="IsSwipeActive"/> es leída por <see cref="CameraFollowController"/>
/// para decidir si la cámara puede alinearse con la cara de la bola:
/// la cámara solo rota mientras el dedo se mueve activamente.
/// </summary>
public sealed class DirectionalTouchController : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]
    [SerializeField]
    [Tooltip("Input táctil crudo de la escena.")]
    private UnifiedBallInput unifiedInput;

    [SerializeField]
    [Tooltip("Transform de la pelota. Se usa para calcular su posición en pantalla.")]
    private Transform ballTransform;

    [SerializeField]
    [Tooltip("Cámara principal. Se usa para proyectar la posición de pantalla a dirección mundo.")]
    private Camera mainCamera;

    [SerializeField]
    [Tooltip("Motor de movimiento de la pelota.")]
    private BallMovementMotor movementMotor;

    [SerializeField]
    [Tooltip("Controlador de rotación lógica de la esfera.")]
    private SphereRotationController rotationController;

    [Header("Dirección de la Cara")]
    [SerializeField]
    [Tooltip("Si activo, la cara snappea instantáneamente al dedo (SetForward directo).\n" +
             "Si inactivo, usa SetTargetForward con la velocidad configurada en SphereRotationController.\n" +
             "El usuario pidió 'súper rápido': dejar activo.")]
    private bool instantFaceTracking = true;

    [SerializeField]
    [Tooltip("Distancia mínima en píxeles entre la posición del dedo y la posición de la bola\n" +
             "para que se actualice la dirección de la cara.\n" +
             "Evita rotaciones erráticas cuando el dedo está casi encima de la bola.")]
    [Min(1f)]
    private float minDirectionDistancePx = 15f;

    [Header("Velocidad — Impulso y Freno")]
    [SerializeField]
    [Tooltip("Impulso en m/s añadido por pixel de desplazamiento del dedo en dirección forward.\n" +
             "Ejemplo: 0.05 m/s/px → swipe de 60px = 3 m/s de impulso.")]
    private float impulsePerPixel = 0.05f;

    [SerializeField]
    [Tooltip("Reducción de velocidad en m/s por pixel de desplazamiento del dedo en dirección backward.\n" +
             "Ejemplo: 0.04 m/s/px → swipe atrás de 60px = 2.4 m/s de freno.")]
    private float brakePerPixel = 0.04f;

    [SerializeField]
    [Tooltip("Dot product mínimo para aplicar impulso o freno.\n" +
             "0.3 = el movimiento debe estar al menos a 72° del eje cara para tener efecto.\n" +
             "Evita que movimientos puramente laterales (cambio de dirección) alteren la velocidad.")]
    [Range(0f, 1f)]
    private float velocityDotThreshold = 0.3f;

    [SerializeField]
    [Tooltip("Desplazamiento mínimo en píxeles entre frames para que el movimiento del dedo\n" +
             "produzca un cambio de velocidad. Filtra ruido de hardware.")]
    [Min(0f)]
    private float minSwipeDeltaPx = 3f;

    [Header("Swipe Activo (para cámara)")]
    [SerializeField]
    [Tooltip("Tiempo en segundos que IsSwipeActive permanece en true después del último\n" +
             "movimiento del dedo. Evita que la cámara se congele en cuanto el dedo se ralentiza.")]
    [Range(0f, 0.5f)]
    private float swipeActiveLingerSeconds = 0.08f;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("Muestra dirección mundo calculada, dot product y cambio de velocidad.")]
    private bool debugController;

    #endregion

    #region Runtime

    private bool    isFingerDown;
    private Vector2 currentFingerPos;
    private float   swipeActiveCooldown;

    #endregion

    #region Properties

    /// <summary>
    /// True mientras el dedo está presionado Y se está moviendo activamente.
    /// Leído por <see cref="CameraFollowController"/> para controlar el alineamiento de cámara.
    /// </summary>
    public bool IsSwipeActive => isFingerDown && swipeActiveCooldown > 0f;

    /// <summary>True mientras el dedo está presionado (aunque no se mueva).</summary>
    public bool IsFingerDown => isFingerDown;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        unifiedInput   = FindFirstObjectByType<UnifiedBallInput>();
        movementMotor  = GetComponent<BallMovementMotor>();
        rotationController = GetComponent<SphereRotationController>();
        ballTransform  = transform;
        mainCamera     = Camera.main;
    }

    private void Awake()
    {
        if (unifiedInput       == null) unifiedInput       = FindFirstObjectByType<UnifiedBallInput>();
        if (movementMotor      == null) movementMotor      = GetComponent<BallMovementMotor>();
        if (rotationController == null) rotationController = GetComponent<SphereRotationController>();
        if (ballTransform      == null) ballTransform      = transform;
        if (mainCamera         == null) mainCamera         = Camera.main;
    }

    private void OnEnable()
    {
        if (unifiedInput == null) return;
        unifiedInput.OnTouchBegan  += HandleTouchBegan;
        unifiedInput.OnTouchMoved  += HandleTouchMoved;
        unifiedInput.OnTouchEnded  += HandleTouchEnded;
    }

    private void OnDisable()
    {
        if (unifiedInput == null) return;
        unifiedInput.OnTouchBegan  -= HandleTouchBegan;
        unifiedInput.OnTouchMoved  -= HandleTouchMoved;
        unifiedInput.OnTouchEnded  -= HandleTouchEnded;

        isFingerDown      = false;
        swipeActiveCooldown = 0f;
    }

    private void Update()
    {
        if (!isFingerDown) return;

        // Actualizar cara continuamente hacia el dedo (cada frame mientras está presionado).
        UpdateFaceDirection();

        // Decrementar cooldown de swipe activo.
        if (swipeActiveCooldown > 0f)
            swipeActiveCooldown = Mathf.Max(0f, swipeActiveCooldown - Time.deltaTime);
    }

    private void OnValidate()
    {
        impulsePerPixel         = Mathf.Max(0f, impulsePerPixel);
        brakePerPixel           = Mathf.Max(0f, brakePerPixel);
        minDirectionDistancePx  = Mathf.Max(1f, minDirectionDistancePx);
        minSwipeDeltaPx         = Mathf.Max(0f, minSwipeDeltaPx);
        velocityDotThreshold    = Mathf.Clamp01(velocityDotThreshold);
        swipeActiveLingerSeconds = Mathf.Clamp(swipeActiveLingerSeconds, 0f, 0.5f);
    }

    #endregion

    #region Touch Handlers

    private void HandleTouchBegan(Vector2 screenPos)
    {
        isFingerDown      = true;
        currentFingerPos  = screenPos;
        swipeActiveCooldown = 0f;

        // Actualizar cara inmediatamente al poner el dedo.
        UpdateFaceDirection();
    }

    private void HandleTouchMoved(Vector2 screenPos, Vector2 screenDelta)
    {
        if (!isFingerDown) return;

        currentFingerPos = screenPos;

        // Velocidad: solo si el movimiento supera el umbral mínimo.
        if (screenDelta.magnitude >= minSwipeDeltaPx)
        {
            ProcessVelocity(screenDelta);
            swipeActiveCooldown = swipeActiveLingerSeconds;
        }
    }

    private void HandleTouchEnded(Vector2 screenPos)
    {
        isFingerDown      = false;
        currentFingerPos  = screenPos;
        swipeActiveCooldown = 0f;
    }

    #endregion

    #region Face Direction

    /// <summary>
    /// Calcula la dirección mundo desde la posición de la bola en pantalla
    /// hasta la posición del dedo, y la aplica a la cara de la bola.
    /// </summary>
    private void UpdateFaceDirection()
    {
        Vector3 worldDir = ComputeFingerWorldDirection();
        if (worldDir == Vector3.zero) return;

        if (instantFaceTracking)
            rotationController.SetForward(worldDir);
        else
            rotationController.SetTargetForward(worldDir);

        if (debugController)
            Debug.Log($"[DirTouch] FaceDir: {worldDir:F2}");
    }

    /// <summary>
    /// Convierte el vector (posición bola en pantalla → posición dedo en pantalla)
    /// a una dirección mundo planar usando los ejes de la cámara proyectados al plano XZ.
    ///
    /// El eje derecho de la cámara ya es horizontal. El forward de la cámara se proyecta
    /// sobre el plano XZ para obtener la dirección mundo correspondiente a "arriba en pantalla".
    /// </summary>
    private Vector3 ComputeFingerWorldDirection()
    {
        if (mainCamera == null || ballTransform == null) return Vector3.zero;

        Vector3 ballScreenPos3 = mainCamera.WorldToScreenPoint(ballTransform.position);

        // Si la bola está detrás de la cámara, ignorar.
        if (ballScreenPos3.z <= 0f) return Vector3.zero;

        Vector2 ballScreenPos2 = new Vector2(ballScreenPos3.x, ballScreenPos3.y);
        Vector2 screenDelta    = currentFingerPos - ballScreenPos2;

        if (screenDelta.sqrMagnitude < minDirectionDistancePx * minDirectionDistancePx)
            return Vector3.zero; // Dedo demasiado cerca del centro: mantener dirección actual.

        Vector2 screenDirNorm = screenDelta.normalized;

        // Ejes de cámara proyectados al plano XZ.
        Vector3 camRight   = mainCamera.transform.right;
        camRight.y = 0f;
        if (camRight.sqrMagnitude < 0.0001f) return Vector3.zero;
        camRight.Normalize();

        Vector3 camForward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up);
        if (camForward.sqrMagnitude < 0.0001f) return Vector3.zero;
        camForward.Normalize();

        Vector3 worldDir = camRight * screenDirNorm.x + camForward * screenDirNorm.y;
        worldDir.y = 0f;

        return worldDir.sqrMagnitude > 0.0001f ? worldDir.normalized : Vector3.zero;
    }

    #endregion

    #region Velocity

    /// <summary>
    /// Aplica impulso o freno según si el movimiento del dedo está alineado
    /// con la cara actual de la bola o va en dirección opuesta.
    ///
    /// La cara ya apunta hacia el dedo, por lo que el eje forward en pantalla
    /// es el vector (bola → dedo). Moverse hacia afuera = acelerar;
    /// moverse hacia dentro = frenar.
    ///
    /// Se usa el dot product entre el delta del dedo y el eje forward-en-pantalla
    /// para determinar si es impulso, freno o movimiento neutro (cambio de dirección).
    /// </summary>
    private void ProcessVelocity(Vector2 screenDelta)
    {
        if (mainCamera == null || ballTransform == null) return;

        // Eje forward de la cara proyectado a pantalla.
        Vector3 faceWorld      = rotationController.CurrentForward;
        Vector3 ballScreenPos3 = mainCamera.WorldToScreenPoint(ballTransform.position);
        Vector3 faceScreenPos3 = mainCamera.WorldToScreenPoint(ballTransform.position + faceWorld);

        if (ballScreenPos3.z <= 0f || faceScreenPos3.z <= 0f) return;

        Vector2 faceScreenDir = new Vector2(
            faceScreenPos3.x - ballScreenPos3.x,
            faceScreenPos3.y - ballScreenPos3.y).normalized;

        float dot = Vector2.Dot(screenDelta.normalized, faceScreenDir);

        if (debugController)
            Debug.Log($"[DirTouch] SwipeDelta:{screenDelta.magnitude:F1}px dot:{dot:F2}");

        if (dot >= velocityDotThreshold)
        {
            // Movimiento hacia adelante → impulso en la dirección de la cara.
            float impulse = screenDelta.magnitude * impulsePerPixel * dot;
            movementMotor.ApplyImpulseInDirection(rotationController.CurrentForward, impulse);

            if (debugController) Debug.Log($"[DirTouch] IMPULSO: {impulse:F3} m/s");
        }
        else if (dot <= -velocityDotThreshold)
        {
            // Movimiento hacia atrás → freno proporcional.
            float brake = screenDelta.magnitude * brakePerPixel * Mathf.Abs(dot);
            movementMotor.ApplyBrakePulse(brake);

            if (debugController) Debug.Log($"[DirTouch] FRENO: {brake:F3} m/s");
        }
        // Movimiento perpendicular: solo cambia dirección (ya aplicado en UpdateFaceDirection).
    }

    #endregion
}