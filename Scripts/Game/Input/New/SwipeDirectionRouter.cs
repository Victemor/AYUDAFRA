using UnityEngine;

/// <summary>
/// Permite que los swipes redireccionen parcialmente
/// la cara principal de la pelota.
///
/// IMPORTANTE:
/// - NO reemplaza ninguna lógica existente.
/// - NO modifica el joystick.
/// - NO altera acumuladores.
/// - NO toca el steering.
/// - SOLO agrega una pequeña redirección por swipe.
///
/// El sistema actual seguirá aplicando impulso,
/// steering y movimiento exactamente igual,
/// pero usando un forward ligeramente ajustado.
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
    [Tooltip("Motor de movimiento opcional para aplicar un micro impulso.")]
    private BallMovementMotor movementMotor;

    [SerializeField]
    [Tooltip("Cámara usada para convertir la dirección del swipe " +
             "de pantalla a mundo.")]
    private Camera targetCamera;

    [Header("Configuración")]

    [SerializeField]
    [Tooltip("Qué tanto influye el swipe sobre la dirección actual. " +
             "0 = no gira. 1 = snap completo.")]
    [Range(0f, 1f)]
    private float swipeRotationInfluence = 0.15f;

    [SerializeField]
    [Tooltip("Si está activo, los swipes forward también pueden redireccionar.")]
    private bool rotateForwardSwipes = true;

    [SerializeField]
    [Tooltip("Si está activo, los swipes backward NO rotan.")]
    private bool ignoreBackwardSwipes = true;

    [SerializeField]
    [Tooltip("Si está activo, los swipes diagonales backward NO rotan.")]
    private bool ignoreBackwardDiagonals = true;

    [Header("Micro impulso")]

    [SerializeField]
    [Tooltip("Aplica un pequeño impulso al cambiar dirección con swipe.")]
    private bool applyMicroKick = true;

    [SerializeField]
    [Tooltip("Pequeño impulso aplicado al redireccionar con swipe.")]
    private float swipeDirectionKickImpulse = 0.35f;

    [Header("Debug")]

    [SerializeField]
    [Tooltip("Muestra logs de depuración.")]
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

    #endregion

    #region Private

    /// <summary>
    /// Procesa el swipe y redirecciona parcialmente
    /// la dirección frontal actual.
    /// </summary>
    private void HandleSwipe(SwipeData swipe)
    {
        if (!ShouldRotateForSwipe(swipe.Intent))
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

        Vector3 blendedDirection =
            Vector3.Slerp(
                currentForward,
                worldDirection,
                swipeRotationInfluence);

        blendedDirection.y = 0f;

        if (blendedDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        blendedDirection.Normalize();

        rotationController.SetForward(blendedDirection);

        if (applyMicroKick && movementMotor != null)
        {
            movementMotor.ApplyJoystickKickstart(
                swipeDirectionKickImpulse);
        }

        if (debugRouter)
        {
            Debug.Log(
                $"[SwipeDirectionRouter] Swipe redirect | " +
                $"Intent: {swipe.Intent} | " +
                $"Current: {currentForward} | " +
                $"Target: {worldDirection} | " +
                $"Final: {blendedDirection}");
        }
    }

    /// <summary>
    /// Determina si el swipe actual debe redireccionar.
    /// </summary>
    private bool ShouldRotateForSwipe(SwipeIntent intent)
    {
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
                return !ignoreBackwardSwipes;

            case SwipeIntent.DiagonalBackwardLeft:
            case SwipeIntent.DiagonalBackwardRight:
                return !ignoreBackwardDiagonals;

            default:
                return false;
        }
    }

    /// <summary>
    /// Convierte una dirección de pantalla
    /// a dirección relativa a cámara en mundo.
    /// </summary>
    private Vector3 ResolveWorldDirection(Vector2 screenDirection)
    {
        Vector3 cameraForward = targetCamera.transform.forward;
        Vector3 cameraRight   = targetCamera.transform.right;

        cameraForward.y = 0f;
        cameraRight.y   = 0f;

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