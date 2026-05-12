using UnityEngine;

/// <summary>
/// Conecta el eje Y del UnifiedBallInput con los comportamientos de movimiento por joystick.
///
/// Regla principal:
/// - Dedo presionado (cualquier posición) → mantener velocidad actual.
/// - Y adelante + pelota detenida    → impulso de arranque único.
/// - Y atrás                         → freno continuo proporcional hasta 0.
/// - Dedo suelto                     → liberar mantenimiento y freno.
///
/// El eje X sigue siendo responsabilidad de BallDirectionInputRouter.
/// </summary>
public sealed class JoystickMovementController : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]

    [SerializeField]
    [Tooltip("Input unificado de la escena. Provee el eje Y continuo y el estado de tracking.")]
    private UnifiedBallInput unifiedInput;

    [SerializeField]
    [Tooltip("Motor de movimiento de la pelota.")]
    private BallMovementMotor movementMotor;

    [Header("Umbrales")]

    [SerializeField]
    [Tooltip("Valor mínimo del eje Y para salir de la zona muerta y activar arranque o freno.")]
    [Range(0f, 0.5f)]
    private float axisThreshold = 0.15f;

    [SerializeField]
    [Tooltip("Velocidad máxima de la pelota considerada detenida para disparar el arranque.")]
    private float kickstartSpeedThreshold = 0.1f;

    [Header("Arranque")]

    [SerializeField]
    [Tooltip("Impulso en m/s aplicado cuando el joystick arranca la pelota desde cero.")]
    private float kickstartImpulse = 3f;

    [Header("Freno")]

    [SerializeField]
    [Tooltip("Desaceleración máxima en m/s² al mantener el joystick a fondo hacia atrás.")]
    private float maxJoystickBrakeDeceleration = 15f;

    #endregion

    #region Runtime

    private bool kickstartApplied;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        unifiedInput  = FindFirstObjectByType<UnifiedBallInput>();
        movementMotor = GetComponent<BallMovementMotor>();
    }

    private void Awake()
    {
        if (unifiedInput == null)
            unifiedInput = FindFirstObjectByType<UnifiedBallInput>();

        if (movementMotor == null)
            movementMotor = GetComponent<BallMovementMotor>();
    }

    private void OnEnable()
    {
        if (unifiedInput != null)
            unifiedInput.OnJoystickY += HandleJoystickY;
    }

    private void OnDisable()
    {
        if (unifiedInput != null)
            unifiedInput.OnJoystickY -= HandleJoystickY;

        ClearJoystickState();
    }

    #endregion

    #region Private

    /// <summary>
    /// Procesa el eje Y emitido por UnifiedBallInput.
    /// Se llama cada frame mientras el dedo está presionado y con 0 al soltarlo.
    /// </summary>
    private void HandleJoystickY(float yAxis)
{
    bool fingerIsDown = unifiedInput != null && unifiedInput.IsTracking;

    if (!fingerIsDown)
    {
        ClearJoystickState();
        return;
    }

    if (yAxis < -axisThreshold)
    {
        // Atrás: freno continuo
        kickstartApplied = false;
        movementMotor?.SetSpeedMaintenance(false);

        float brakeStrength = Mathf.Clamp01((-yAxis - axisThreshold) / (1f - axisThreshold));
        movementMotor?.SetJoystickBrake(brakeStrength * maxJoystickBrakeDeceleration);
    }
    else
    {
        // Zona muerta O adelante: mantener velocidad siempre
        movementMotor?.SetJoystickBrake(0f);
        movementMotor?.SetSpeedMaintenance(true);

        if (movementMotor != null)
        {
            if (movementMotor.CurrentSpeed > kickstartSpeedThreshold)
            {
                // Pelota en movimiento: resetear para que pueda arrancar de nuevo si se detiene
                kickstartApplied = false;
            }
            else if (!kickstartApplied)
            {
                // Pelota detenida + dedo presionado sin ir hacia atrás → arrancar
                // No requiere Y > threshold: cualquier toque que no sea hacia atrás arranca
                movementMotor.ApplyJoystickKickstart(kickstartImpulse);
                kickstartApplied = true;
            }
        }
    }
}

    /// <summary>
    /// Libera el mantenimiento y el freno al soltar el dedo.
    /// </summary>
    private void ClearJoystickState()
    {
        kickstartApplied = false;
        movementMotor?.SetSpeedMaintenance(false);
        movementMotor?.SetJoystickBrake(0f);
    }

    #endregion
}