using UnityEngine;

/// <summary>
/// Redirecciona parcialmente la cara principal de la pelota usando swipes.
/// Permite rotación hacia atrás solo cuando la bola está detenida,
/// evitando que los gestos de freno cambien la dirección mientras hay velocidad.
/// </summary>
public sealed class SwipeDirectionRouter : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]

    [SerializeField]
    [Tooltip("Input unificado que emite los swipes.")]
    private UnifiedBallInput unifiedInput;

    [SerializeField]
    [Tooltip("Controlador de rotación de la esfera.")]
    private SphereRotationController rotationController;

    [SerializeField]
    [Tooltip("Motor de movimiento usado para consultar velocidad y aplicar micro impulso opcional.")]
    private BallMovementMotor movementMotor;

    [SerializeField]
    [Tooltip("Cámara usada para convertir la dirección del swipe de pantalla a mundo.")]
    private Camera targetCamera;

    [Header("Configuración")]

    [SerializeField]
    [Tooltip("Qué tanto influye el swipe sobre la dirección actual. 0 = no gira. 1 = snap completo.")]
    [Range(0f, 1f)]
    private float swipeRotationInfluence = 0.15f;

    [SerializeField]
    [Tooltip("Influencia específica para swipes hacia atrás cuando la bola está detenida.")]
    [Range(0f, 1f)]
    private float stoppedBackwardSwipeRotationInfluence = 0.65f;

    [SerializeField]
    [Tooltip("Velocidad máxima para considerar que la bola está detenida y permitir rotación hacia atrás.")]
    private float stoppedSpeedThreshold = 0.08f;

    [SerializeField]
    [Tooltip("Si está activo, los swipes forward también pueden redireccionar.")]
    private bool rotateForwardSwipes = true;

    [SerializeField]
    [Tooltip("Si está activo, los swipes hacia atrás pueden rotar únicamente cuando la bola está detenida.")]
    private bool allowBackwardRotationWhenStopped = true;

    [SerializeField]
    [Tooltip("Si está activo, las diagonales hacia atrás pueden rotar únicamente cuando la bola está detenida.")]
    private bool allowBackwardDiagonalRotationWhenStopped = true;

    [Header("Micro impulso")]

    [SerializeField]
    [Tooltip("Aplica un pequeño impulso al cambiar dirección con swipe.")]
    private bool applyMicroKick = true;

    [SerializeField]
    [Tooltip("Pequeño impulso aplicado al redireccionar con swipe. No se aplica en swipes hacia atrás.")]
    private float swipeDirectionKickImpulse = 0.35f;

    [Header("Debug")]

    [SerializeField]
    [Tooltip("Muestra logs de depuración.")]
    private bool debugRouter;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        unifiedInput = FindFirstObjectByType<UnifiedBallInput>();
        rotationController = GetComponent<SphereRotationController>();
        movementMotor = GetComponent<BallMovementMotor>();

        if (Camera.main != null)
        {
            targetCamera = Camera.main;
        }
    }

    private void Awake()
    {
        if (unifiedInput == null)
        {
            unifiedInput = FindFirstObjectByType<UnifiedBallInput>();
        }

        if (rotationController == null)
        {
            rotationController = GetComponent<SphereRotationController>();
        }

        if (movementMotor == null)
        {
            movementMotor = GetComponent<BallMovementMotor>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void OnEnable()
    {
        if (unifiedInput != null)
        {
            unifiedInput.OnSwipeDetected += HandleSwipe;
        }
    }

    private void OnDisable()
    {
        if (unifiedInput != null)
        {
            unifiedInput.OnSwipeDetected -= HandleSwipe;
        }
    }

    private void OnValidate()
    {
        stoppedSpeedThreshold = Mathf.Max(0f, stoppedSpeedThreshold);
        swipeDirectionKickImpulse = Mathf.Max(0f, swipeDirectionKickImpulse);
    }

    #endregion

    #region Private

    /// <summary>
    /// Procesa el swipe y redirecciona parcialmente la dirección frontal actual.
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

        Vector3 blendedDirection = Vector3.Slerp(
            currentForward,
            worldDirection,
            rotationInfluence);

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
                $"[SwipeDirectionRouter] Swipe redirect | " +
                $"Intent: {swipe.Intent} | " +
                $"Influence: {rotationInfluence:F2} | " +
                $"Current: {currentForward} | " +
                $"Target: {worldDirection} | " +
                $"Final: {blendedDirection}");
        }
    }

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
                return true;

            case SwipeIntent.Backward:
                if (!allowBackwardRotationWhenStopped || !IsStopped())
                {
                    return false;
                }

                rotationInfluence = stoppedBackwardSwipeRotationInfluence;
                return true;

            case SwipeIntent.DiagonalBackwardLeft:
            case SwipeIntent.DiagonalBackwardRight:
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

        return intent != SwipeIntent.Backward
               && intent != SwipeIntent.DiagonalBackwardLeft
               && intent != SwipeIntent.DiagonalBackwardRight;
    }

    private bool IsStopped()
    {
        if (movementMotor == null)
        {
            return true;
        }

        return movementMotor.CurrentSpeed <= stoppedSpeedThreshold;
    }

    /// <summary>
    /// Convierte una dirección de pantalla a dirección relativa a cámara en mundo.
    /// </summary>
    private Vector3 ResolveWorldDirection(Vector2 screenDirection)
    {
        Vector3 cameraForward = targetCamera.transform.forward;
        Vector3 cameraRight = targetCamera.transform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        if (cameraForward.sqrMagnitude < 0.0001f)
        {
            cameraForward = Vector3.forward;
        }

        if (cameraRight.sqrMagnitude < 0.0001f)
        {
            cameraRight = Vector3.right;
        }

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 worldDirection =
            cameraRight * screenDirection.x +
            cameraForward * screenDirection.y;

        worldDirection.y = 0f;

        return worldDirection.sqrMagnitude < 0.0001f
            ? Vector3.forward
            : worldDirection.normalized;
    }

    #endregion
}