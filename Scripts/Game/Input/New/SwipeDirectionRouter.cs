using UnityEngine;

/// <summary>
/// Redirecciona parcialmente la cara principal de la pelota usando swipes.
///
/// Comportamiento por estado:
/// - Bola en movimiento: los swipes forward, laterales y diagonales forward giran la bola.
///   Los swipes backward y diagonales backward NO giran (solo frenan vía BrakeAccumulator).
/// - Bola detenida (velocidad ≤ stoppedSpeedThreshold): los swipes backward y diagonales
///   backward SÍ giran la bola, permitiendo al jugador cambiar de dirección antes de avanzar.
///
/// Cambios respecto a la versión anterior:
/// - <c>stoppedBackwardSwipeRotationInfluence</c>: 0.65 → 0.88.
///   Con 0.65 un swipe backward de 180° solo completaba ~117° del giro (Slerp incompleto).
///   Con 0.88 completa ~158°, suficiente para una vuelta útil.
/// - <c>stoppedSpeedThreshold</c>: 0.08 → 0.15 m/s.
///   El jugador percibía que tenía que esperar demasiado después de frenar. 0.15 m/s
///   activa el giro mientras la bola todavía se desacelera, sin causar giros accidentales.
/// </summary>
public sealed class SwipeDirectionRouter : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]

    [SerializeField]
    [Tooltip("Input unificado que emite los swipes clasificados.")]
    private UnifiedBallInput unifiedInput;

    [SerializeField]
    [Tooltip("Controlador de rotación lógica de la esfera.")]
    private SphereRotationController rotationController;

    [SerializeField]
    [Tooltip("Motor de movimiento. Se usa para consultar velocidad actual.")]
    private BallMovementMotor movementMotor;

    [SerializeField]
    [Tooltip("Cámara de referencia para convertir la dirección del swipe de espacio de pantalla a mundo.")]
    private Camera targetCamera;

    [Header("Rotación por Swipe")]

    [SerializeField]
    [Tooltip("Influencia del swipe sobre la dirección frontal actual.\n" +
             "0 = no gira nada. 1 = snap completo a la dirección del swipe.\n" +
             "0.15 produce una redirección parcial suave, respetando la inercia visual.")]
    [Range(0f, 1f)]
    private float swipeRotationInfluence = 0.15f;

    [SerializeField]
    [Tooltip("Influencia del swipe backward cuando la bola está detenida.\n" +
             "Controla cuánto se rota hacia atrás al hacer swipe backward con la bola parada.\n" +
             "0.88 produce un giro decisivo (~158° de un swipe de 180°) sin ser un snap brusco.")]
    [Range(0f, 1f)]
    private float stoppedBackwardSwipeRotationInfluence = 0.88f;

    [SerializeField]
    [Tooltip("Velocidad planar máxima en m/s para considerar la bola 'detenida'.\n" +
             "Cuando la velocidad cae por debajo de este valor, los swipes backward\n" +
             "pueden girar la bola. 0.15 permite el giro mientras la bola aún se desacelera.")]
    private float stoppedSpeedThreshold = 0.15f;

    [SerializeField]
    [Tooltip("Si está activo, los swipes Forward también redireccionan la cara de la bola.\n" +
             "Útil para micro-correcciones de dirección durante un swipe hacia adelante.")]
    private bool rotateForwardSwipes = true;

    [SerializeField]
    [Tooltip("Si está activo, los swipes Backward giran la bola cuando está detenida.\n" +
             "Mientras la bola se mueve, los swipes backward SOLO frenan (no giran).")]
    private bool allowBackwardRotationWhenStopped = true;

    [SerializeField]
    [Tooltip("Si está activo, las diagonales Backward giran la bola cuando está detenida.\n" +
             "Mientras la bola se mueve, las diagonales backward SOLO frenan.")]
    private bool allowBackwardDiagonalRotationWhenStopped = true;

    [Header("Micro Impulso")]

    [SerializeField]
    [Tooltip("Si está activo, aplica un pequeño impulso al redireccionar con swipe.\n" +
             "Ayuda a que la bola mantenga momentum al cambiar de dirección.")]
    private bool applyMicroKick = true;

    [SerializeField]
    [Tooltip("Impulso en m/s aplicado al redireccionar con swipe.\n" +
             "No se aplica en swipes backward (que frenan, no aceleran).")]
    private float swipeDirectionKickImpulse = 0.35f;

    [Header("Debug")]

    [SerializeField]
    [Tooltip("Muestra logs de redirección: intent, influence, dirección actual y final.")]
    private bool debugRouter;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        unifiedInput       = FindFirstObjectByType<UnifiedBallInput>();
        rotationController = GetComponent<SphereRotationController>();
        movementMotor      = GetComponent<BallMovementMotor>();

        if (Camera.main != null)
        {
            targetCamera = Camera.main;
        }
    }

    private void Awake()
    {
        if (unifiedInput == null)
            unifiedInput = FindFirstObjectByType<UnifiedBallInput>();

        if (rotationController == null)
            rotationController = GetComponent<SphereRotationController>();

        if (movementMotor == null)
            movementMotor = GetComponent<BallMovementMotor>();

        if (targetCamera == null)
            targetCamera = Camera.main;
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
        stoppedSpeedThreshold     = Mathf.Max(0f, stoppedSpeedThreshold);
        swipeDirectionKickImpulse = Mathf.Max(0f, swipeDirectionKickImpulse);
    }

    #endregion

    #region Private

    /// <summary>
    /// Procesa el swipe y redirecciona parcialmente la cara frontal de la bola.
    /// Si no hay influencia de rotación para este intent, el método retorna sin hacer nada.
    /// </summary>
    private void HandleSwipe(SwipeData swipe)
    {
        if (!TryGetRotationInfluence(swipe.Intent, out float rotationInfluence))
        {
            return;
        }

        if (rotationController == null || targetCamera == null)
        {
            return;
        }

        Vector3 worldDirection = ResolveWorldDirection(swipe.ScreenDirection);

        if (worldDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 currentForward = rotationController.CurrentForward;

        // Slerp desde la dirección actual hacia la dirección del swipe en espacio mundo.
        Vector3 blendedDirection = Vector3.Slerp(currentForward, worldDirection, rotationInfluence);
        blendedDirection.y = 0f;

        if (blendedDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        blendedDirection.Normalize();
        rotationController.SetForward(blendedDirection);

        if (ShouldApplyMicroKick(swipe.Intent))
        {
            movementMotor.ApplyJoystickKickstart(swipeDirectionKickImpulse);
        }

        if (debugRouter)
        {
            Debug.Log(
                $"[SwipeDirectionRouter] Intent: {swipe.Intent} | " +
                $"Influence: {rotationInfluence:F2} | " +
                $"Current: {currentForward} | " +
                $"Target: {worldDirection} | " +
                $"Final: {blendedDirection}");
        }
    }

    /// <summary>
    /// Determina si el intent del swipe produce rotación y con qué influencia.
    /// Devuelve <c>false</c> si este intent no debe rotar la bola en el estado actual.
    /// </summary>
    private bool TryGetRotationInfluence(SwipeIntent intent, out float rotationInfluence)
    {
        rotationInfluence = swipeRotationInfluence;

        switch (intent)
        {
            case SwipeIntent.Forward:
                return rotateForwardSwipes;

            case SwipeIntent.Left:
            case SwipeIntent.Right:
            case SwipeIntent.DiagonalForwardLeft:
            case SwipeIntent.DiagonalForwardRight:
                // Laterales y diagonales forward siempre giran con la influencia estándar.
                return true;

            case SwipeIntent.Backward:
                // Backward solo gira cuando la bola está detenida.
                // Mientras se mueve, este intent es exclusivamente de frenado.
                if (!allowBackwardRotationWhenStopped || !IsStopped())
                {
                    return false;
                }

                rotationInfluence = stoppedBackwardSwipeRotationInfluence;
                return true;

            case SwipeIntent.DiagonalBackwardLeft:
            case SwipeIntent.DiagonalBackwardRight:
                // Mismo criterio que Backward: solo gira cuando está detenida.
                if (!allowBackwardDiagonalRotationWhenStopped || !IsStopped())
                {
                    return false;
                }

                rotationInfluence = stoppedBackwardSwipeRotationInfluence;
                return true;

            default:
                return false;
        }
    }

    private bool ShouldApplyMicroKick(SwipeIntent intent)
    {
        if (!applyMicroKick || movementMotor == null || swipeDirectionKickImpulse <= 0f)
        {
            return false;
        }

        // El micro kick no aplica en swipes que frenan o giran hacia atrás.
        return intent != SwipeIntent.Backward
            && intent != SwipeIntent.DiagonalBackwardLeft
            && intent != SwipeIntent.DiagonalBackwardRight;
    }

    private bool IsStopped()
    {
        if (movementMotor == null) return true;

        return movementMotor.CurrentSpeed <= stoppedSpeedThreshold;
    }

    /// <summary>
    /// Convierte una dirección de pantalla (2D normalizada) a una dirección en espacio mundo
    /// relativa a la cámara, proyectada sobre el plano XZ (sin componente Y).
    /// </summary>
    private Vector3 ResolveWorldDirection(Vector2 screenDirection)
    {
        Vector3 cameraForward = targetCamera.transform.forward;
        Vector3 cameraRight   = targetCamera.transform.right;

        cameraForward.y = 0f;
        cameraRight.y   = 0f;

        if (cameraForward.sqrMagnitude < 0.0001f) cameraForward = Vector3.forward;
        if (cameraRight.sqrMagnitude   < 0.0001f) cameraRight   = Vector3.right;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 worldDirection =
            cameraRight   * screenDirection.x +
            cameraForward * screenDirection.y;

        worldDirection.y = 0f;

        return worldDirection.sqrMagnitude < 0.0001f
            ? Vector3.forward
            : worldDirection.normalized;
    }

    #endregion
}