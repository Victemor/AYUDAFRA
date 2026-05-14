using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Input unificado para control de la bola con un solo dedo.
/// Lee touch real, touch simulado del Device Simulator y mouse en editor como fallback.
/// Aplica sensibilidad y curva de respuesta independiente para cada eje.
///
/// Cambios respecto a la versión anterior:
/// - <c>horizontalSensitivity</c>: 0.62 → 0.92. Respuesta global más alta.
/// - <c>horizontalResponseExponent</c>: 1.85 → 1.25. Curva más lineal en el centro.
///   Con el exponente anterior, un joystick al 30% producía solo ~6.5% del input real.
///   Con 1.25 produce ~22%, tres veces más reactivo en el rango de uso habitual.
/// </summary>
public sealed class UnifiedBallInput : MonoBehaviour
{
    #region Types

    /// <summary>
    /// Define cómo se leerá el input en runtime/editor.
    /// </summary>
    private enum InputReadMode
    {
        /// <summary>Primero intenta touch; si no hay touch activo, usa mouse como fallback.</summary>
        Auto = 0,

        /// <summary>Solo lee Touchscreen.</summary>
        TouchOnly = 1,

        /// <summary>Solo lee Mouse.</summary>
        MouseOnly = 2,
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
    /// Swipe detectado al soltar el dedo o mouse, clasificado en 8 direcciones.
    /// </summary>
    public event Action<SwipeData> OnSwipeDetected;

    #endregion

    #region Inspector

    [Header("Input Source")]

    [SerializeField]
    [Tooltip("Modo de lectura de input.\n" +
             "Auto: intenta touch primero, usa mouse en editor como fallback.\n" +
             "TouchOnly: solo Touchscreen (ideal para builds de dispositivo).\n" +
             "MouseOnly: solo Mouse (útil para testing rápido en editor).")]
    private InputReadMode inputReadMode = InputReadMode.Auto;

    [Header("Joystick Base")]

    [SerializeField]
    [Tooltip("Radio en píxeles desde el toque inicial para alcanzar input máximo.\n" +
             "Mayor radio = menos sensible al desplazamiento del dedo.")]
    private float joystickRadiusPx = 180f;

    [SerializeField]
    [Tooltip("Zona muerta central en píxeles.\n" +
             "Movimientos dentro de este radio se ignoran para evitar micro giros accidentales.")]
    private float joystickDeadzonePx = 28f;

    [Header("Horizontal Rotation Feel")]

    [SerializeField]
    [Tooltip("Multiplicador final del eje horizontal.\n" +
             "Aumentar para giros más sensibles. Rango recomendado: 0.8–1.1.")]
    [Range(0.1f, 2f)]
    private float horizontalSensitivity = 0.92f;

    [SerializeField]
    [Tooltip("Curva de respuesta horizontal. 1 = completamente lineal.\n" +
             "Valores cercanos a 1 (1.1–1.4) son más reactivos en el centro del joystick.\n" +
             "Evitar valores altos (>1.6): crean una dead zone perceptible donde los giros\n" +
             "pequeños no responden hasta que se supera una cierta presión en el joystick.")]
    [Range(1f, 3f)]
    private float horizontalResponseExponent = 1.25f;

    [SerializeField]
    [Tooltip("Suavizado del eje horizontal en segundos.\n" +
             "0 = respuesta inmediata (puede sentirse nervioso).\n" +
             "Recomendado móvil: 0.035–0.07.")]
    [Range(0f, 0.2f)]
    private float horizontalSmoothTime = 0.045f;

    [SerializeField]
    [Tooltip("Cambio mínimo requerido para volver a emitir el eje horizontal.\n" +
             "Reduce eventos redundantes entre frames sin cambio perceptible.")]
    private float horizontalEmitThreshold = 0.001f;

    [Header("Vertical Movement Feel")]

    [SerializeField]
    [Tooltip("Multiplicador final del eje vertical. Normalmente se deja en 1.")]
    [Range(0.1f, 2f)]
    private float verticalSensitivity = 1f;

    [SerializeField]
    [Tooltip("Curva de respuesta vertical. 1 = lineal.\n" +
             "Valores mayores suavizan el freno/avance cerca del centro del joystick.")]
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
    [Tooltip("Longitud mínima del gesto en píxeles para considerar que es un swipe válido.\n" +
             "Gestos más cortos se ignoran para evitar taps accidentales.")]
    private float minimumSwipeLengthPx = 55f;

    [SerializeField]
    [Tooltip("Duración máxima en segundos para que el gesto se clasifique como swipe.\n" +
             "Gestos más lentos (arrastre prolongado) se descartan como swipe.")]
    private float maximumSwipeDuration = 0.5f;

    [SerializeField]
    [Tooltip("Ángulo de tolerancia en grados alrededor de cada eje cardinal para clasificar el swipe.\n" +
             "35° → ±35° desde arriba = Forward. Más alto = clasificación más permisiva.")]
    [Range(15f, 45f)]
    private float swipeCardinalAngleThreshold = 35f;

    [SerializeField]
    [Tooltip("Longitud en píxeles que produce intensidad máxima (1.0).\n" +
             "Swipes más cortos producen menos impulso.")]
    private float maxIntensityLengthPx = 340f;

    [SerializeField]
    [Tooltip("Velocidad en píxeles por segundo que produce intensidad máxima (1.0).\n" +
             "Swipes más lentos producen menos impulso.")]
    private float maxIntensitySpeedPxPerSecond = 1400f;

    [SerializeField]
    [Tooltip("Peso de la longitud frente a la velocidad al calcular intensidad del swipe.\n" +
             "1 = solo longitud. 0 = solo velocidad. 0.6 = mezcla equilibrada.")]
    [Range(0f, 1f)]
    private float intensityLengthWeight = 0.6f;

    [Header("Debug")]

    [SerializeField]
    [Tooltip("Muestra logs de lectura de input, tracking y clasificación de swipes.")]
    private bool debugInput;

    #endregion

    #region Runtime

    private bool    isTracking;
    private Vector2 touchStart;
    private float   touchStartTime;

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

    /// <summary>Indica si hay un gesto activo en este momento.</summary>
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
        joystickRadiusPx   = Mathf.Max(1f, joystickRadiusPx);
        joystickDeadzonePx = Mathf.Clamp(joystickDeadzonePx, 0f, joystickRadiusPx - 1f);

        horizontalSensitivity      = Mathf.Clamp(horizontalSensitivity,      0.1f, 2f);
        horizontalResponseExponent = Mathf.Clamp(horizontalResponseExponent, 1f,   3f);
        horizontalSmoothTime       = Mathf.Clamp(horizontalSmoothTime,       0f,   0.2f);
        horizontalEmitThreshold    = Mathf.Max(0f, horizontalEmitThreshold);

        verticalSensitivity      = Mathf.Clamp(verticalSensitivity,      0.1f, 2f);
        verticalResponseExponent = Mathf.Clamp(verticalResponseExponent, 1f,   3f);
        verticalSmoothTime       = Mathf.Clamp(verticalSmoothTime,       0f,   0.2f);
        verticalEmitThreshold    = Mathf.Max(0f, verticalEmitThreshold);

        minimumSwipeLengthPx         = Mathf.Max(1f,    minimumSwipeLengthPx);
        maximumSwipeDuration         = Mathf.Max(0.01f, maximumSwipeDuration);
        maxIntensityLengthPx         = Mathf.Max(1f,    maxIntensityLengthPx);
        maxIntensitySpeedPxPerSecond = Mathf.Max(1f,    maxIntensitySpeedPxPerSecond);
        intensityLengthWeight        = Mathf.Clamp01(intensityLengthWeight);
    }

    #endregion

    #region Input Reading

    /// <summary>
    /// Lee touch primero; usa mouse como fallback solo en editor y standalone.
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
            if (isTracking) ForceEnd();
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
        isTracking     = true;
        touchStart     = position;
        touchStartTime = Time.unscaledTime;

        targetHorizontal      = 0f;
        targetVertical        = 0f;
        smoothedHorizontal    = 0f;
        smoothedVertical      = 0f;
        horizontalVelocity    = 0f;
        verticalVelocity      = 0f;
        lastEmittedHorizontal = 0f;
        lastEmittedVertical   = 0f;

        OnDirectionInput?.Invoke(0f);
        OnJoystickY?.Invoke(0f);

        if (debugInput)
        {
            Debug.Log($"[UnifiedBallInput] Begin {source}: {position}");
        }
    }

    /// <summary>
    /// Calcula los objetivos de eje X/Y mientras el input está presionado.
    /// Aplica zona muerta, curva de respuesta y sensibilidad.
    /// </summary>
    private void UpdateDirectionTargets(Vector2 position, string source)
    {
        float   usableRadius = Mathf.Max(1f, joystickRadiusPx - joystickDeadzonePx);
        Vector2 delta        = position - touchStart;

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
        targetHorizontal      = 0f;
        targetVertical        = 0f;
        smoothedHorizontal    = 0f;
        smoothedVertical      = 0f;
        horizontalVelocity    = 0f;
        verticalVelocity      = 0f;
        lastEmittedHorizontal = 0f;
        lastEmittedVertical   = 0f;

        OnDirectionInput?.Invoke(0f);
        OnJoystickY?.Invoke(0f);
    }

    #endregion

    #region Axis Processing

    /// <summary>
    /// Suaviza los ejes hacia sus targets y emite eventos si el cambio supera el umbral.
    /// Solo corre mientras hay tracking activo.
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

    /// <summary>
    /// Convierte el desplazamiento en píxeles a un valor de eje normalizado [-1, 1]
    /// aplicando zona muerta, curva de potencia y multiplicador de sensibilidad.
    /// </summary>
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

        // Desplazamiento efectivo: sustrae la dead zone manteniendo el signo.
        float signedOffset = delta - joystickDeadzonePx * Mathf.Sign(delta);
        float normalized   = Mathf.Clamp(signedOffset / usableRadius, -1f, 1f);

        // Curva de potencia sobre el valor absoluto, preservando el signo.
        float curved   = Mathf.Pow(Mathf.Abs(normalized), exponent) * Mathf.Sign(normalized);
        float resolved = curved * sensitivity;

        return Mathf.Clamp(resolved, -1f, 1f);
    }

    #endregion

    #region Swipe Evaluation

    /// <summary>
    /// Evalúa si el gesto completo califica como swipe y lo clasifica en 8 intents.
    /// Se invoca al soltar el dedo; usa las posiciones de inicio y fin del tracking.
    /// </summary>
    private void EvaluateSwipe(Vector2 endPosition)
    {
        float duration = Time.unscaledTime - touchStartTime;

        if (duration <= 0f || duration > maximumSwipeDuration)
        {
            return;
        }

        Vector2 delta  = endPosition - touchStart;
        float   length = delta.magnitude;

        if (length < minimumSwipeLengthPx)
        {
            return;
        }

        float       speed     = length / duration;
        float       intensity = CalculateIntensity(length, speed);
        Vector2     direction = delta.normalized;
        SwipeIntent intent    = ClassifyIntent(direction);

        SwipeData swipe = new SwipeData(direction, length, speed, intent, intensity);

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

    /// <summary>
    /// Clasifica la dirección del swipe en 8 intents según el ángulo respecto al eje Y positivo.
    /// El eje Y positivo de pantalla corresponde a "adelante" (Forward).
    /// </summary>
    private SwipeIntent ClassifyIntent(Vector2 direction)
    {
        // Ángulo en grados medido desde el eje Y positivo (arriba). Rango: [-180, 180].
        float angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;

        if (Mathf.Abs(angle) <= swipeCardinalAngleThreshold)
            return SwipeIntent.Forward;

        if (Mathf.Abs(angle) >= 180f - swipeCardinalAngleThreshold)
            return SwipeIntent.Backward;

        if (angle > 90f - swipeCardinalAngleThreshold && angle < 90f + swipeCardinalAngleThreshold)
            return SwipeIntent.Right;

        if (angle < -(90f - swipeCardinalAngleThreshold) && angle > -(90f + swipeCardinalAngleThreshold))
            return SwipeIntent.Left;

        // Diagonales: cuadrante determinado por Y (forward/backward) y X (right/left).
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

    /// <summary>
    /// Calcula la intensidad normalizada [0, 1] del swipe combinando longitud y velocidad.
    /// </summary>
    private float CalculateIntensity(float length, float speed)
    {
        float lengthNorm = Mathf.Clamp01(length / maxIntensityLengthPx);
        float speedNorm  = Mathf.Clamp01(speed  / maxIntensitySpeedPxPerSecond);

        return Mathf.Clamp01(
            lengthNorm * intensityLengthWeight +
            speedNorm  * (1f - intensityLengthWeight));
    }

    #endregion
}