using UnityEngine;

/// <summary>
/// Conecta el eje horizontal del UnifiedBallInput con el SphereRotationController.
/// Bloquea la rotación durante input hacia atrás si la bola todavía se mueve,
/// permitiendo que ese gesto se use exclusivamente para frenar.
/// </summary>
public sealed class BallDirectionInputRouter : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]

    [SerializeField]
    [Tooltip("Input unificado de la escena.")]
    private UnifiedBallInput unifiedInput;

    [SerializeField]
    [Tooltip("Controlador de rotación de la esfera.")]
    private SphereRotationController rotationController;

    [SerializeField]
    [Tooltip("Motor de movimiento de la pelota. Se usa para saber si la bola está detenida.")]
    private BallMovementMotor movementMotor;

    [Header("Reglas de rotación")]

    [SerializeField]
    [Tooltip("Velocidad máxima para considerar que la bola está detenida y permitir rotación hacia atrás.")]
    private float stoppedSpeedThreshold = 0.08f;

    [SerializeField]
    [Tooltip("Valor mínimo del eje Y negativo para considerar que el jugador está intentando frenar.")]
    [Range(0f, 1f)]
    private float backwardAxisThreshold = 0.15f;

    #endregion

    #region Runtime

    private float currentHorizontalInput;
    private float currentVerticalInput;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        unifiedInput = FindFirstObjectByType<UnifiedBallInput>();
        rotationController = GetComponent<SphereRotationController>();
        movementMotor = GetComponent<BallMovementMotor>();
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
    }

    private void OnEnable()
    {
        if (unifiedInput == null)
        {
            return;
        }

        unifiedInput.OnDirectionInput += HandleDirectionInput;
        unifiedInput.OnJoystickY += HandleJoystickY;
    }

    private void OnDisable()
    {
        if (unifiedInput != null)
        {
            unifiedInput.OnDirectionInput -= HandleDirectionInput;
            unifiedInput.OnJoystickY -= HandleJoystickY;
        }

        currentHorizontalInput = 0f;
        currentVerticalInput = 0f;

        rotationController?.SetRotationInput(0f);
    }

    private void OnValidate()
    {
        stoppedSpeedThreshold = Mathf.Max(0f, stoppedSpeedThreshold);
        backwardAxisThreshold = Mathf.Clamp01(backwardAxisThreshold);
    }

    #endregion

    #region Private

    private void HandleDirectionInput(float horizontal)
    {
        currentHorizontalInput = Mathf.Clamp(horizontal, -1f, 1f);
        ApplyResolvedHorizontalInput();
    }

    private void HandleJoystickY(float vertical)
    {
        currentVerticalInput = Mathf.Clamp(vertical, -1f, 1f);
        ApplyResolvedHorizontalInput();
    }

    private void ApplyResolvedHorizontalInput()
    {
        if (rotationController == null)
        {
            return;
        }

        float resolvedHorizontal = ShouldBlockRotationForBackwardBrake()
            ? 0f
            : currentHorizontalInput;

        rotationController.SetRotationInput(resolvedHorizontal);
    }

    private bool ShouldBlockRotationForBackwardBrake()
    {
        if (currentVerticalInput >= -backwardAxisThreshold)
        {
            return false;
        }

        if (movementMotor == null)
        {
            return false;
        }

        return movementMotor.CurrentSpeed > stoppedSpeedThreshold;
    }

    #endregion
}