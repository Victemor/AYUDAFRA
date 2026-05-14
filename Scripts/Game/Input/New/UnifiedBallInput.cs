using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Input unificado para control de la bola con un solo dedo.
/// Lee touch real, touch simulado del Device Simulator y mouse en editor como fallback.
/// Aplica sensibilidad y curva de respuesta independiente para el eje horizontal.
/// </summary>
public sealed class UnifiedBallInput : MonoBehaviour
{
    #region Types

    /// <summary>
    /// Define cómo se leerá el input en runtime/editor.
    /// </summary>
    private enum InputReadMode
    {
        /// <summary>Primero intenta touch y si no hay touch activo usa mouse.</summary>
        Auto = 0,

        /// <summary>Solo lee Touchscreen.</summary>
        TouchOnly = 1,

        /// <summary>Solo lee Mouse.</summary>
        MouseOnly = 2
    }

    #endregion

    #region Events

    /// <summary>
    /// Dirección horizontal continua mientras el dedo está abajo.
    /// Rango: -1 izquierda, 1 derecha.
    /// </summary>
    public event Action<float> OnDirectionInput;

    /// <summary>
    /// Eje vertical continuo mientras el dedo está abajo.
    /// Rango: -1 abajo, 1 arriba.
    /// </summary>
    public event Action<float> OnJoystickY;

    /// <summary>
    /// Swipe detectado al soltar el dedo o mouse.
    /// </summary>
    public event Action<SwipeData> OnSwipeDetected;

    #endregion

    #region Inspector

    [Header("Input Source")]

    [SerializeField]
    [Tooltip("Modo de lectura de input. Auto es recomendado para móvil, editor y Device Simulator.")]
    private InputReadMode inputReadMode = InputReadMode.Auto;

    [Header("Joystick Base")]

    [SerializeField]
    [Tooltip("Radio en píxeles desde el toque inicial para alcanzar input máximo. Más alto = menos sensible.")]
    private float joystickRadiusPx = 180f;

    [SerializeField]
    [Tooltip("Zona muerta central en píxeles. Más alto evita micro giros accidentales.")]
    private float joystickDeadzonePx = 28f;

    [Header("Horizontal Rotation Feel")]

    [SerializeField]
    [Tooltip("Multiplicador final del eje horizontal. Menor a 1 reduce sensibilidad de giro.")]
    [Range(0.1f, 2f)]
    private float horizontalSensitivity = 0.62f;

    [SerializeField]
    [Tooltip("Curva de respuesta horizontal. 1 = lineal. 1.5-2.2 suaviza movimientos pequeños y conserva fuerza al extremo.")]
    [Range(1f, 3f)]
    private float horizontalResponseExponent = 1.85f;

    [SerializeField]
    [Tooltip("Suavizado del eje horizontal en segundos. 0 = respuesta inmediata. Recomendado móvil: 0.035 a 0.07.")]
    [Range(0f, 0.2f)]
    private float horizontalSmoothTime = 0.045f;

    [SerializeField]
    [Tooltip("Cambio mínimo requerido para volver a emitir el eje horizontal.")]
    private float horizontalEmitThreshold = 0.001f;

    [Header("Vertical Movement Feel")]

    [SerializeField]
    [Tooltip("Multiplicador final del eje vertical. Normalmente se deja en 1.")]
    [Range(0.1f, 2f)]
    private float verticalSensitivity = 1f;

    [SerializeField]
    [Tooltip("Curva de respuesta vertical. 1 = lineal. Valores mayores suavizan freno/avance cerca del centro.")]
    [Range(1f, 3f)]
    private float verticalResponseExponent = 1.2f;

    [SerializeField]
    [Tooltip("Suavizado del eje vertical en segundos.")]
    [Range(0f, 0.2f)]
    private float verticalSmoothTime = 0.025f;

    [SerializeField]
    [Tooltip("Cambio mínimo requerido para volver a emitir el eje vertical.")]
    private float verticalEmitThreshold = 0.001f;

    [Header("Swipe")]

    [SerializeField]
    [Tooltip("Longitud mínima del gesto en píxeles para considerar swipe.")]
    private float minimumSwipeLengthPx = 55f;

    [SerializeField]
    [Tooltip("Duración máxima en segundos para que el gesto se considere swipe.")]
    private float maximumSwipeDuration = 0.5f;

    [SerializeField]
    [Tooltip("Ángulo en grados desde cada eje cardinal para clasificar el swipe.")]
    [Range(15f, 45f)]
    private float swipeCardinalAngleThreshold = 35f;

    [SerializeField]
    [Tooltip("Longitud en píxeles que equivale a intensidad máxima.")]
    private float maxIntensityLengthPx = 340f;

    [SerializeField]
    [Tooltip("Velocidad en píxeles por segundo que equivale a intensidad máxima.")]
    private float maxIntensitySpeedPxPerSecond = 1400f;

    [SerializeField]
    [Tooltip("Peso de longitud contra velocidad al calcular intensidad.")]
    [Range(0f, 1f)]
    private float intensityLengthWeight = 0.6f;

    [Header("Debug")]

    [SerializeField]
    [Tooltip("Muestra logs de lectura y clasificación de input.")]
    private bool debugInput;

    #endregion

    #region Runtime

    private bool isTracking;
    private Vector2 touchStart;
    private float touchStartTime;

    private float targetHorizontal;
    private float targetVertical;
    private float smoothedHorizontal;
    private float smoothedVertical;
    private float horizontalVelocity;
    private float verticalVelocity;
    private float lastEmittedHorizontal;
    private float lastEmittedVertical;

    #endregion

    #region Properties

    /// <summary>
    /// Indica si hay un gesto activo.
    /// </summary>
    public bool IsTracking => isTracking;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        switch (inputReadMode)
        {
            case InputReadMode.TouchOnly:
                UpdateTouch();
                break;

            case InputReadMode.MouseOnly:
                UpdateMouse();
                break;

            default:
                UpdateAuto();
                break;
        }

        UpdateSmoothedAxes();
    }

    private void OnDisable()
    {
        if (isTracking)
        {
            ForceEnd();
        }
    }

    private void OnValidate()
    {
        joystickRadiusPx = Mathf.Max(1f, joystickRadiusPx);
        joystickDeadzonePx = Mathf.Clamp(joystickDeadzonePx, 0f, joystickRadiusPx - 1f);

        horizontalSensitivity = Mathf.Clamp(horizontalSensitivity, 0.1f, 2f);
        horizontalResponseExponent = Mathf.Clamp(horizontalResponseExponent, 1f, 3f);
        horizontalSmoothTime = Mathf.Clamp(horizontalSmoothTime, 0f, 0.2f);
        horizontalEmitThreshold = Mathf.Max(0f, horizontalEmitThreshold);

        verticalSensitivity = Mathf.Clamp(verticalSensitivity, 0.1f, 2f);
        verticalResponseExponent = Mathf.Clamp(verticalResponseExponent, 1f, 3f);
        verticalSmoothTime = Mathf.Clamp(verticalSmoothTime, 0f, 0.2f);
        verticalEmitThreshold = Mathf.Max(0f, verticalEmitThreshold);

        minimumSwipeLengthPx = Mathf.Max(1f, minimumSwipeLengthPx);
        maximumSwipeDuration = Mathf.Max(0.01f, maximumSwipeDuration);

        maxIntensityLengthPx = Mathf.Max(1f, maxIntensityLengthPx);
        maxIntensitySpeedPxPerSecond = Mathf.Max(1f, maxIntensitySpeedPxPerSecond);
        intensityLengthWeight = Mathf.Clamp01(intensityLengthWeight);
    }

    #endregion

    #region Input Reading

    /// <summary>
    /// Lee touch primero y mouse como respaldo.
    /// </summary>
    private void UpdateAuto()
    {
        if (TryUpdateTouch())
        {
            return;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        UpdateMouse();
#else
        if (isTracking)
        {
            ForceEnd();
        }
#endif
    }

    private void UpdateTouch()
    {
        if (!TryUpdateTouch() && isTracking)
        {
            ForceEnd();
        }
    }

    private bool TryUpdateTouch()
    {
        if (Touchscreen.current == null)
        {
            return false;
        }

        var touch = Touchscreen.current.primaryTouch;

        if (touch.press.wasPressedThisFrame)
        {
            BeginTracking(touch.position.ReadValue(), "Touch");
            return true;
        }

        if (touch.press.isPressed && isTracking)
        {
            UpdateDirectionTargets(touch.position.ReadValue(), "Touch");
            return true;
        }

        if (touch.press.wasReleasedThisFrame && isTracking)
        {
            EndTracking(touch.position.ReadValue(), "Touch");
            return true;
        }

        return touch.press.isPressed;
    }

    private void UpdateMouse()
    {
        if (Mouse.current == null)
        {
            if (isTracking)
            {
                ForceEnd();
            }

            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            BeginTracking(Mouse.current.position.ReadValue(), "Mouse");
            return;
        }

        if (Mouse.current.leftButton.isPressed && isTracking)
        {
            UpdateDirectionTargets(Mouse.current.position.ReadValue(), "Mouse");
            return;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && isTracking)
        {
            EndTracking(Mouse.current.position.ReadValue(), "Mouse");
        }
    }

    #endregion

    #region Tracking

    private void BeginTracking(Vector2 position, string source)
    {
        isTracking = true;
        touchStart = position;
        touchStartTime = Time.unscaledTime;

        targetHorizontal = 0f;
        targetVertical = 0f;
        smoothedHorizontal = 0f;
        smoothedVertical = 0f;
        horizontalVelocity = 0f;
        verticalVelocity = 0f;
        lastEmittedHorizontal = 0f;
        lastEmittedVertical = 0f;

        OnDirectionInput?.Invoke(0f);
        OnJoystickY?.Invoke(0f);

        if (debugInput)
        {
            Debug.Log($"[UnifiedBallInput] Begin {source}: {position}");
        }
    }

    /// <summary>
    /// Calcula los objetivos de eje X/Y mientras el input está presionado.
    /// </summary>
    private void UpdateDirectionTargets(Vector2 position, string source)
    {
        float usableRadius = Mathf.Max(1f, joystickRadiusPx - joystickDeadzonePx);
        Vector2 delta = position - touchStart;

        targetHorizontal = ResolveAxis(
            delta.x,
            usableRadius,
            horizontalSensitivity,
            horizontalResponseExponent);

        targetVertical = ResolveAxis(
            delta.y,
            usableRadius,
            verticalSensitivity,
            verticalResponseExponent);

        if (debugInput)
        {
            Debug.Log(
                $"[UnifiedBallInput] {source} Target X:{targetHorizontal:F2} Y:{targetVertical:F2}");
        }
    }

    private void EndTracking(Vector2 position, string source)
    {
        isTracking = false;

        ResetAxesAndEmit();

        if (debugInput)
        {
            Debug.Log($"[UnifiedBallInput] End {source}: {position}");
        }

        EvaluateSwipe(position);
    }

    private void ForceEnd()
    {
        isTracking = false;
        ResetAxesAndEmit();
    }

    private void ResetAxesAndEmit()
    {
        targetHorizontal = 0f;
        targetVertical = 0f;
        smoothedHorizontal = 0f;
        smoothedVertical = 0f;
        horizontalVelocity = 0f;
        verticalVelocity = 0f;
        lastEmittedHorizontal = 0f;
        lastEmittedVertical = 0f;

        OnDirectionInput?.Invoke(0f);
        OnJoystickY?.Invoke(0f);
    }

    #endregion

    #region Axis Processing

    /// <summary>
    /// Suaviza y emite los ejes continuos.
    /// </summary>
    private void UpdateSmoothedAxes()
    {
        if (!isTracking)
        {
            return;
        }

        smoothedHorizontal = SmoothAxis(
            smoothedHorizontal,
            targetHorizontal,
            ref horizontalVelocity,
            horizontalSmoothTime);

        smoothedVertical = SmoothAxis(
            smoothedVertical,
            targetVertical,
            ref verticalVelocity,
            verticalSmoothTime);

        EmitAxesIfNeeded();
    }

    private void EmitAxesIfNeeded()
    {
        if (Mathf.Abs(smoothedHorizontal - lastEmittedHorizontal) >= horizontalEmitThreshold)
        {
            lastEmittedHorizontal = smoothedHorizontal;
            OnDirectionInput?.Invoke(smoothedHorizontal);
        }

        if (Mathf.Abs(smoothedVertical - lastEmittedVertical) >= verticalEmitThreshold)
        {
            lastEmittedVertical = smoothedVertical;
            OnJoystickY?.Invoke(smoothedVertical);
        }
    }

    private static float SmoothAxis(
        float current,
        float target,
        ref float velocity,
        float smoothTime)
    {
        if (smoothTime <= 0f)
        {
            velocity = 0f;
            return target;
        }

        return Mathf.SmoothDamp(
            current,
            target,
            ref velocity,
            smoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);
    }

    private float ResolveAxis(
        float delta,
        float usableRadius,
        float sensitivity,
        float exponent)
    {
        if (Mathf.Abs(delta) <= joystickDeadzonePx)
        {
            return 0f;
        }

        float signedOffset = delta - joystickDeadzonePx * Mathf.Sign(delta);
        float normalized = Mathf.Clamp(signedOffset / usableRadius, -1f, 1f);

        float curved = Mathf.Pow(Mathf.Abs(normalized), exponent) * Mathf.Sign(normalized);
        float resolved = curved * sensitivity;

        return Mathf.Clamp(resolved, -1f, 1f);
    }

    #endregion

    #region Swipe Evaluation

    /// <summary>
    /// Evalúa si el gesto completo califica como swipe y lo clasifica en 8 direcciones.
    /// </summary>
    private void EvaluateSwipe(Vector2 endPosition)
    {
        float duration = Time.unscaledTime - touchStartTime;

        if (duration <= 0f || duration > maximumSwipeDuration)
        {
            return;
        }

        Vector2 delta = endPosition - touchStart;
        float length = delta.magnitude;

        if (length < minimumSwipeLengthPx)
        {
            return;
        }

        float speed = length / duration;
        float intensity = CalculateIntensity(length, speed);
        Vector2 direction = delta.normalized;
        SwipeIntent intent = ClassifyIntent(direction);

        SwipeData swipe = new SwipeData(
            direction,
            length,
            speed,
            intent,
            intensity);

        if (debugInput)
        {
            Debug.Log(
                $"[UnifiedBallInput] Swipe: {intent} | " +
                $"Length: {length:F0}px | " +
                $"Speed: {speed:F0}px/s | " +
                $"Intensity: {intensity:F2}");
        }

        OnSwipeDetected?.Invoke(swipe);
    }

    private SwipeIntent ClassifyIntent(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;

        if (Mathf.Abs(angle) <= swipeCardinalAngleThreshold)
        {
            return SwipeIntent.Forward;
        }

        if (Mathf.Abs(angle) >= 180f - swipeCardinalAngleThreshold)
        {
            return SwipeIntent.Backward;
        }

        if (angle > 90f - swipeCardinalAngleThreshold &&
            angle < 90f + swipeCardinalAngleThreshold)
        {
            return SwipeIntent.Right;
        }

        if (angle < -(90f - swipeCardinalAngleThreshold) &&
            angle > -(90f + swipeCardinalAngleThreshold))
        {
            return SwipeIntent.Left;
        }

        if (direction.y >= 0f)
        {
            return direction.x > 0f
                ? SwipeIntent.DiagonalForwardRight
                : SwipeIntent.DiagonalForwardLeft;
        }

        return direction.x > 0f
            ? SwipeIntent.DiagonalBackwardRight
            : SwipeIntent.DiagonalBackwardLeft;
    }

    private float CalculateIntensity(float length, float speed)
    {
        float lengthNorm = Mathf.Clamp01(length / maxIntensityLengthPx);
        float speedNorm = Mathf.Clamp01(speed / maxIntensitySpeedPxPerSecond);

        return Mathf.Clamp01(
            lengthNorm * intensityLengthWeight +
            speedNorm * (1f - intensityLengthWeight));
    }

    #endregion
}