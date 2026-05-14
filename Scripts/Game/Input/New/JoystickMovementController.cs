using UnityEngine;

/// <summary>
/// Conecta el eje Y del UnifiedBallInput con los comportamientos de movimiento por joystick.
/// Mantiene velocidad mientras el dedo está presionado, frena al empujar hacia atrás
/// y solo aplica arranque cuando el eje Y apunta explícitamente hacia adelante.
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

    [SerializeField]
    [Tooltip("Si está activo, la bola solo arranca cuando el eje Y supera positivamente el umbral. Evita impulsos accidentales al iniciar swipes hacia atrás.")]
    private bool requireForwardAxisForKickstart = true;

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
        unifiedInput = FindFirstObjectByType<UnifiedBallInput>();
        movementMotor = GetComponent<BallMovementMotor>();
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
    }

    private void OnEnable()
    {
        if (unifiedInput != null)
        {
            unifiedInput.OnJoystickY += HandleJoystickY;
        }
    }

    private void OnDisable()
    {
        if (unifiedInput != null)
        {
            unifiedInput.OnJoystickY -= HandleJoystickY;
        }

        ClearJoystickState();
    }

    private void OnValidate()
    {
        axisThreshold = Mathf.Clamp(axisThreshold, 0f, 0.5f);
        kickstartSpeedThreshold = Mathf.Max(0f, kickstartSpeedThreshold);
        kickstartImpulse = Mathf.Max(0f, kickstartImpulse);
        maxJoystickBrakeDeceleration = Mathf.Max(0f, maxJoystickBrakeDeceleration);
    }

    #endregion

    #region Private

    /// <summary>
    /// Procesa el eje Y emitido por UnifiedBallInput.
    /// </summary>
    private void HandleJoystickY(float yAxis)
    {
        bool fingerIsDown = unifiedInput != null && unifiedInput.IsTracking;

        if (!fingerIsDown)
        {
            ClearJoystickState();
            return;
        }

        if (movementMotor == null)
        {
            return;
        }

        if (yAxis < -axisThreshold)
        {
            HandleBackwardAxis(yAxis);
            return;
        }

        HandleNeutralOrForwardAxis(yAxis);
    }

    private void HandleBackwardAxis(float yAxis)
    {
        kickstartApplied = false;

        movementMotor.SetSpeedMaintenance(false);

        float brakeStrength = Mathf.Clamp01((-yAxis - axisThreshold) / (1f - axisThreshold));
        movementMotor.SetJoystickBrake(brakeStrength * maxJoystickBrakeDeceleration);
    }

    private void HandleNeutralOrForwardAxis(float yAxis)
    {
        movementMotor.SetJoystickBrake(0f);

        if (movementMotor.CurrentSpeed > kickstartSpeedThreshold)
        {
            kickstartApplied = false;
            movementMotor.SetSpeedMaintenance(true);
            return;
        }

        movementMotor.SetSpeedMaintenance(false);

        if (!ShouldApplyKickstart(yAxis))
        {
            return;
        }

        movementMotor.ApplyJoystickKickstart(kickstartImpulse);
        kickstartApplied = true;
    }

    private bool ShouldApplyKickstart(float yAxis)
    {
        if (kickstartApplied)
        {
            return false;
        }

        if (!requireForwardAxisForKickstart)
        {
            return yAxis >= -axisThreshold;
        }

        return yAxis > axisThreshold;
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