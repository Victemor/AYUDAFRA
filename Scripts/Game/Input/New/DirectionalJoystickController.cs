using UnityEngine;

/// <summary>
/// Controlador de movimiento basado en la dirección polar del joystick.
/// Reemplaza a <c>BallDirectionInputRouter</c> y <c>JoystickMovementController</c>.
///
/// Paradigma del plano de referencia (cara-relativo):
/// El plano de joystick usa la cara de la bola como referencia, no la cámara.
/// - Arriba en pantalla (Y+) = dirección actual de la cara = avanzar.
/// - Izquierda en pantalla (X-) = 90° a la izquierda de la cara actual.
/// - Abajo en pantalla (Y-) = opuesto de la cara = hemisferio trasero.
///
/// A medida que la cara rota hacia un objetivo, el plano de referencia rota con ella,
/// de modo que el jugador siempre "mira hacia donde apunta su dedo" sin necesidad
/// de ajustar la posición del dedo.
///
/// Bloqueo de objetivo:
/// El objetivo se calcula UNA VEZ cuando el joystick se mueve a una nueva dirección
/// y se bloquea hasta que el joystick se mueva significativamente de nuevo.
/// Esto evita que el ball espiralize si la cara y el dedo siguen moviéndose juntos.
///
/// Origen del joystick:
/// El (0,0) es siempre donde el jugador pone el dedo. Cada nuevo toque crea un
/// nuevo origen. El joystick es invisible y dinámico.
/// </summary>
public sealed class DirectionalJoystickController : MonoBehaviour
{
    #region Types

    private enum JoystickState
    {
        Idle,
        FrontHemisphere,
        BackHemisphere_Braking,
        BackHemisphere_WaitingReTrigger,
        BackHemisphere_MovingToTarget,
        BackHemisphere_Moving,
    }

    #endregion

    #region Inspector

    [Header("Referencias")]
    [SerializeField]
    [Tooltip("Input unificado de la escena.")]
    private UnifiedBallInput unifiedInput;

    [SerializeField]
    [Tooltip("Motor de movimiento de la pelota.")]
    private BallMovementMotor movementMotor;

    [SerializeField]
    [Tooltip("Controlador de rotación lógica de la esfera.\n" +
             "Su CurrentForward es el eje de referencia del plano de joystick.")]
    private SphereRotationController rotationController;

    [Header("Movimiento")]
    [SerializeField]
    [Tooltip("Impulso en m/s dado a la bola al arrancar desde cero con joystick.")]
    private float kickstartImpulse = 3f;

    [SerializeField]
    [Tooltip("Desaceleración en m/s² mientras el joystick apunta al hemisferio trasero y la bola se mueve.")]
    private float brakeDeceleration = 18f;

    [SerializeField]
    [Tooltip("Velocidad planar en m/s por debajo de la cual la bola se considera detenida.")]
    private float stopThreshold = 0.12f;

    [Header("Bloqueo de Objetivo")]
    [SerializeField]
    [Tooltip("Ángulo mínimo en grados que debe moverse el joystick para actualizar el objetivo.\n" +
             "Evita que micro-movimientos del dedo o la rotación de la cara restablezcan el target.\n" +
             "Rango recomendado: 10°–20°.")]
    [Range(5f, 40f)]
    private float targetUpdateAngleThreshold = 15f;

    [Header("Re-trigger Trasero")]
    [SerializeField]
    [Tooltip("Incremento mínimo de magnitud del joystick para re-activar el movimiento\n" +
             "cuando la bola está detenida en hemisferio trasero ('empujar más').")]
    [Range(0.01f, 0.5f)]
    private float reTriggerMagnitudeDelta = 0.08f;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("Muestra el estado interno y la dirección objetivo.")]
    private bool debugController;

    #endregion

    #region Runtime

    private JoystickState state             = JoystickState.Idle;
    private float         previousMagnitude = 0f;

    /// <summary>
    /// Dirección de pantalla que estableció el objetivo actual.
    /// Se usa para detectar si el joystick se movió lo suficiente para actualizar el target.
    /// </summary>
    private Vector2 lockedScreenDir    = Vector2.zero;
    private bool    hasLockedTarget    = false;

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
        if (unifiedInput != null)
            unifiedInput.OnJoystickScreenDirection += HandleJoystickDirection;
    }

    private void OnDisable()
    {
        if (unifiedInput != null)
            unifiedInput.OnJoystickScreenDirection -= HandleJoystickDirection;

        TransitionToIdle();
    }

    private void OnValidate()
    {
        kickstartImpulse        = Mathf.Max(0f,   kickstartImpulse);
        brakeDeceleration       = Mathf.Max(0f,   brakeDeceleration);
        stopThreshold           = Mathf.Max(0f,   stopThreshold);
        targetUpdateAngleThreshold = Mathf.Clamp(targetUpdateAngleThreshold, 5f, 40f);
        reTriggerMagnitudeDelta = Mathf.Clamp(reTriggerMagnitudeDelta, 0.01f, 0.5f);
    }

    #endregion

    #region Event Handler

    private void HandleJoystickDirection(Vector2 screenDir, float magnitude)
    {
        if (magnitude <= 0f)
        {
            TransitionToIdle();
            previousMagnitude = 0f;
            return;
        }

        // Dirección mundo cara-relativa: Y de pantalla = cara actual, X de pantalla = derecha de cara.
        // El marco de referencia es la cara, no la cámara.
        Vector3 worldDir = InputDirectionProjector.FaceRelative(screenDir, GetCurrentFace());
        bool    isFront  = screenDir.y >= 0f;

        if (debugController)
        {
            Debug.Log(
                $"[DirectionalJoystick] State:{state} Front:{isFront} " +
                $"Mag:{magnitude:F2} Face:{GetCurrentFace():F2} WorldDir:{worldDir:F2}");
        }

        if (isFront)
            HandleFrontHemisphere(screenDir, worldDir);
        else
            HandleBackHemisphere(screenDir, worldDir, magnitude);

        previousMagnitude = magnitude;
    }

    #endregion

    #region Hemisphere Handlers

    private void HandleFrontHemisphere(Vector2 screenDir, Vector3 worldDir)
    {
        movementMotor.ClearJoystickBrake();
        state = JoystickState.FrontHemisphere;

        // ── Bloqueo de objetivo ─────────────────────────────────────────────────────
        // El objetivo se establece UNA VEZ y se bloquea. Solo se actualiza cuando el
        // joystick se mueve significativamente (> targetUpdateAngleThreshold).
        // Esto evita que pequeñas variaciones de la cara durante la rotación
        // causen que el objetivo se desplace (efecto espiral).
        bool joystickMoved = !hasLockedTarget ||
            Vector2.Angle(screenDir, lockedScreenDir) > targetUpdateAngleThreshold;

        if (joystickMoved)
        {
            rotationController.SetTargetForward(worldDir);
            lockedScreenDir = screenDir;
            hasLockedTarget = true;
        }

        // ── Movimiento ──────────────────────────────────────────────────────────────
        bool isStopped = movementMotor.CurrentSpeed <= stopThreshold;

        if (isStopped)
        {
            // Bola detenida: esperar a que la cara llegue, luego kickstart.
            movementMotor.SetSpeedMaintenance(false);

            if (rotationController.IsAlignedWithTarget)
            {
                movementMotor.ApplyJoystickKickstart(kickstartImpulse);
                movementMotor.SetSpeedMaintenance(true);
            }
        }
        else
        {
            // Bola en movimiento: mantener velocidad actual.
            // La cara rota, el steering redirige el vector de velocidad sin frenar.
            movementMotor.SetSpeedMaintenance(true);
        }
    }

    private void HandleBackHemisphere(Vector2 screenDir, Vector3 worldDir, float magnitude)
    {
        bool isStopped = movementMotor.CurrentSpeed <= stopThreshold;

        // ── Bola en movimiento → solo frena ─────────────────────────────────────────
        if (!isStopped)
        {
            movementMotor.SetJoystickBrake(brakeDeceleration);
            movementMotor.SetSpeedMaintenance(false);
            state = JoystickState.BackHemisphere_Braking;
            return;
        }

        // ── Bola detenida ────────────────────────────────────────────────────────────
        movementMotor.ClearJoystickBrake();

        if (state == JoystickState.BackHemisphere_Moving)
        {
            bool joystickMoved = Vector2.Angle(screenDir, lockedScreenDir) > targetUpdateAngleThreshold;
            if (joystickMoved)
            {
                rotationController.SetTargetForward(worldDir);
                lockedScreenDir = screenDir;
            }
            movementMotor.SetSpeedMaintenance(true);
            return;
        }

        if (state == JoystickState.BackHemisphere_MovingToTarget)
        {
            if (rotationController.IsAlignedWithTarget)
            {
                movementMotor.ApplyJoystickKickstart(kickstartImpulse);
                movementMotor.SetSpeedMaintenance(true);
                state = JoystickState.BackHemisphere_Moving;
            }
            return;
        }

        bool shouldTrigger = false;

        if (state == JoystickState.Idle || state == JoystickState.FrontHemisphere)
        {
            shouldTrigger = true;
        }
        else if (state == JoystickState.BackHemisphere_Braking)
        {
            state = JoystickState.BackHemisphere_WaitingReTrigger;
        }
        else if (state == JoystickState.BackHemisphere_WaitingReTrigger)
        {
            shouldTrigger = magnitude > previousMagnitude + reTriggerMagnitudeDelta;
        }

        if (shouldTrigger)
        {
            rotationController.SetTargetForward(worldDir);
            lockedScreenDir = screenDir;
            hasLockedTarget = true;
            movementMotor.SetSpeedMaintenance(false);
            state = JoystickState.BackHemisphere_MovingToTarget;
        }
        else
        {
            movementMotor.SetSpeedMaintenance(false);
        }
    }

    #endregion

    #region Helpers

    private void TransitionToIdle()
    {
        movementMotor?.SetSpeedMaintenance(false);
        movementMotor?.ClearJoystickBrake();
        rotationController?.ClearTarget();
        state             = JoystickState.Idle;
        previousMagnitude = 0f;
        hasLockedTarget   = false;
        lockedScreenDir   = Vector2.zero;
    }

    private Vector3 GetCurrentFace()
    {
        return rotationController != null ? rotationController.CurrentForward : Vector3.forward;
    }

    #endregion
}