using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Input unificado para control de la bola con un solo dedo.
///
/// Comportamiento:
/// - Arrastrar horizontal mientras el dedo está abajo → dirección continua (joystick).
/// - Arrastrar vertical mientras el dedo está abajo → eje Y del joystick (mantener/frenar/arrancar).
/// - Soltar con movimiento vertical dominante y rápido → swipe (acelerar/frenar).
/// - Un mismo gesto puede girar Y acelerar si el soltar tiene componente vertical.
/// </summary>
public sealed class UnifiedBallInput : MonoBehaviour
{
    #region Events

    /// <summary>
    /// Dirección horizontal continua mientras el dedo está abajo.
    /// Rango: -1 (izquierda) a 1 (derecha). 0 al soltar.
    /// </summary>
    public event Action<float> OnDirectionInput;

    /// <summary>
    /// Eje vertical continuo mientras el dedo está abajo.
    /// Rango: -1 (abajo) a 1 (arriba). 0 al soltar o en zona muerta.
    /// Usado por JoystickMovementController para arranque, mantenimiento y freno.
    /// </summary>
    public event Action<float> OnJoystickY;

    /// <summary>
    /// Swipe vertical detectado al soltar el dedo.
    /// Solo se emite si el gesto fue dominantemente vertical y rápido.
    /// </summary>
    public event Action<SwipeData> OnSwipeDetected;

    #endregion

    #region Inspector

    [Header("Dirección (Joystick)")]

    [SerializeField]
    [Tooltip("Radio en píxeles desde el toque inicial para alcanzar rotación/desplazamiento máximo.")]
    private float joystickRadiusPx = 120f;

    [SerializeField]
    [Tooltip("Zona muerta central en píxeles. Movimientos menores a esto no generan input.")]
    private float joystickDeadzonePx = 15f;

    [Header("Swipe (Acelerar / Frenar)")]

    [SerializeField]
    [Tooltip("Longitud mínima del gesto en píxeles para considerar swipe.")]
    private float minimumSwipeLengthPx = 45f;

    [SerializeField]
    [Tooltip("Duración máxima en segundos para que el gesto se considere swipe.")]
    private float maximumSwipeDuration = 0.5f;

    [SerializeField]
    [Tooltip("Fracción mínima del gesto que debe ser vertical para disparar swipe. " +
             "0.6 = el 60% del desplazamiento debe ser vertical.")]
    [Range(0.4f, 0.9f)]
    private float minimumVerticalDominance = 0.6f;

    [SerializeField]
    [Tooltip("Longitud en píxeles que equivale a intensidad máxima.")]
    private float maxIntensityLengthPx = 300f;

    [SerializeField]
    [Tooltip("Velocidad en px/s que equivale a intensidad máxima.")]
    private float maxIntensitySpeedPxPerSecond = 1200f;

    [SerializeField]
    [Tooltip("Peso de longitud vs velocidad al calcular intensidad.")]
    [Range(0f, 1f)]
    private float intensityLengthWeight = 0.6f;

    [Header("Editor")]

    [SerializeField]
    [Tooltip("Usa mouse en editor para simular touch.")]
    private bool useMouseInEditor = true;

    [SerializeField]
    [Tooltip("Logs de debug.")]
    private bool debugInput;

    #endregion

    #region Runtime

    private bool isTracking;
    private Vector2 touchStart;
    private float touchStartTime;

    #endregion

    #region Properties

    public bool IsTracking => isTracking;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (useMouseInEditor)
        {
            UpdateMouse();
            return;
        }
#endif
        UpdateTouch();
    }

    private void OnDisable()
    {
        if (isTracking)
        {
            ForceEnd();
        }
    }

    #endregion

    #region Input Reading

    private void UpdateTouch()
    {
        if (Touchscreen.current == null)
        {
            if (isTracking) ForceEnd();
            return;
        }

        var touch = Touchscreen.current.primaryTouch;

        if (touch.press.wasPressedThisFrame)
        {
            BeginTracking(touch.position.ReadValue());
            return;
        }

        if (touch.press.isPressed && isTracking)
        {
            UpdateDirection(touch.position.ReadValue());
            return;
        }

        if (!touch.press.isPressed && isTracking)
        {
            EndTracking(touch.position.ReadValue());
        }
    }

    private void UpdateMouse()
    {
        if (Mouse.current == null)
        {
            if (isTracking) ForceEnd();
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            BeginTracking(Mouse.current.position.ReadValue());
            return;
        }

        if (Mouse.current.leftButton.isPressed && isTracking)
        {
            UpdateDirection(Mouse.current.position.ReadValue());
            return;
        }

        if (!Mouse.current.leftButton.isPressed && isTracking)
        {
            EndTracking(Mouse.current.position.ReadValue());
        }
    }

    #endregion

    #region Tracking

    private void BeginTracking(Vector2 position)
    {
        isTracking     = true;
        touchStart     = position;
        touchStartTime = Time.unscaledTime;

        OnDirectionInput?.Invoke(0f);
        OnJoystickY?.Invoke(0f);
    }

    /// <summary>
    /// Calcula y emite el eje horizontal y el eje vertical mientras el dedo está abajo.
    /// El eje X controla la dirección de rotación.
    /// El eje Y es consumido por JoystickMovementController para arranque/mantenimiento/freno.
    /// </summary>
    private void UpdateDirection(Vector2 position)
    {
        float usableRadius = Mathf.Max(1f, joystickRadiusPx - joystickDeadzonePx);

        // — Eje X (dirección horizontal) —
        float deltaX = position.x - touchStart.x;

        if (Mathf.Abs(deltaX) <= joystickDeadzonePx)
        {
            OnDirectionInput?.Invoke(0f);
        }
        else
        {
            float signedOffsetX = deltaX - joystickDeadzonePx * Mathf.Sign(deltaX);
            float horizontal    = Mathf.Clamp(signedOffsetX / usableRadius, -1f, 1f);

            if (debugInput)
                Debug.Log($"[UnifiedBallInput] Dirección X: {horizontal:F2}");

            OnDirectionInput?.Invoke(horizontal);
        }

        // — Eje Y (joystick vertical: adelante/atrás) —
        float deltaY = position.y - touchStart.y;

        if (Mathf.Abs(deltaY) <= joystickDeadzonePx)
        {
            OnJoystickY?.Invoke(0f);
        }
        else
        {
            float signedOffsetY = deltaY - joystickDeadzonePx * Mathf.Sign(deltaY);
            float vertical      = Mathf.Clamp(signedOffsetY / usableRadius, -1f, 1f);

            if (debugInput)
                Debug.Log($"[UnifiedBallInput] Joystick Y: {vertical:F2}");

            OnJoystickY?.Invoke(vertical);
        }
    }

    /// <summary>
    /// Al soltar el dedo, detiene dirección y eje Y, luego evalúa si fue un swipe vertical.
    /// </summary>
    private void EndTracking(Vector2 position)
    {
        isTracking = false;

        OnDirectionInput?.Invoke(0f);
        OnJoystickY?.Invoke(0f);

        EvaluateSwipe(position);
    }

    private void ForceEnd()
    {
        isTracking = false;
        OnDirectionInput?.Invoke(0f);
        OnJoystickY?.Invoke(0f);
    }

    #endregion

    #region Swipe Evaluation

    /// <summary>
    /// Evalúa si el gesto que terminó califica como swipe vertical.
    /// Solo dispara si la componente vertical es dominante y el gesto fue rápido.
    /// </summary>
    private void EvaluateSwipe(Vector2 endPosition)
    {
        float duration = Time.unscaledTime - touchStartTime;

        if (duration <= 0f || duration > maximumSwipeDuration)
            return;

        Vector2 delta  = endPosition - touchStart;
        float length   = delta.magnitude;

        if (length < minimumSwipeLengthPx)
            return;

        float verticalDominance = Mathf.Abs(delta.y) / length;

        if (verticalDominance < minimumVerticalDominance)
            return;

        float speed     = length / duration;
        float intensity = CalculateIntensity(length, speed);
        SwipeIntent intent = delta.y > 0f ? SwipeIntent.Forward : SwipeIntent.Backward;

        SwipeData swipe = new SwipeData(delta.normalized, length, speed, intent, intensity);

        if (debugInput)
        {
            Debug.Log(
                $"[UnifiedBallInput] Swipe: {intent} | " +
                $"Longitud: {length:F0}px | " +
                $"Velocidad: {speed:F0}px/s | " +
                $"Intensidad: {intensity:F2}");
        }

        OnSwipeDetected?.Invoke(swipe);
    }

    private float CalculateIntensity(float length, float speed)
    {
        float lengthNorm = Mathf.Clamp01(length / maxIntensityLengthPx);
        float speedNorm  = Mathf.Clamp01(speed / maxIntensitySpeedPxPerSecond);

        return Mathf.Clamp01(
            lengthNorm * intensityLengthWeight +
            speedNorm  * (1f - intensityLengthWeight));
    }

    #endregion
}