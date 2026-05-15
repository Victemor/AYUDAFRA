using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Input unificado para control de la bola con un solo dedo.
/// Lee touch real, Device Simulator y mouse en editor como fallback.
///
/// Paradigma de eventos:
/// - <see cref="OnJoystickScreenDirection"/>: emitido cada frame mientras el dedo está abajo.
///   Transporta la dirección normalizada del joystick en pantalla y su magnitud [0,1].
///   Cuando el dedo se levanta, se emite una vez con (Vector2.zero, 0f).
/// - <see cref="OnSwipeDetected"/>: emitido al soltar el dedo si el gesto califica como swipe.
///
/// Diferencia respecto a la versión anterior:
/// Los ejes X e Y independientes (OnDirectionInput, OnJoystickY) y el sistema de
/// suavizado por SmoothDamp fueron eliminados. La dirección se emite como un vector 2D
/// directo; el suavizado de la cara proviene de la velocidad de rotación configurada
/// en <see cref="SphereRotationController"/>.
/// </summary>
public sealed class UnifiedBallInput : MonoBehaviour
{
    #region Types

    /// <summary>Define cómo se leerá el input según la plataforma.</summary>
    private enum InputReadMode
    {
        /// <summary>Intenta touch primero; mouse como fallback en editor/standalone.</summary>
        Auto = 0,
        /// <summary>Solo lee Touchscreen. Recomendado para builds de dispositivo.</summary>
        TouchOnly = 1,
        /// <summary>Solo lee Mouse. Útil para pruebas rápidas en editor.</summary>
        MouseOnly = 2,
    }

    #endregion

    #region Events

    /// <summary>
    /// Dirección del joystick en espacio de pantalla, emitida cada frame mientras el dedo está abajo.
    ///
    /// Parámetros:
    /// - <c>Vector2 screenDir</c>: dirección normalizada. Y positivo = arriba = forward en cámara.
    ///   Vector2.zero dentro de la zona muerta o cuando el dedo se levantó.
    /// - <c>float magnitude</c>: distancia normalizada [0,1] entre la deadzone y el radio del joystick.
    ///   0 = en zona muerta o sin input activo. 1 = en el borde exterior del radio.
    ///
    /// Se emite una vez con (Vector2.zero, 0f) al levantar el dedo.
    /// </summary>
    public event Action<Vector2, float> OnJoystickScreenDirection;

    /// <summary>
    /// Swipe detectado al soltar el dedo, clasificado en 8 intents.
    /// Se emite después del (Vector2.zero, 0f) de cierre de <see cref="OnJoystickScreenDirection"/>.
    /// </summary>
    public event Action<SwipeData> OnSwipeDetected;

    #endregion

    #region Inspector

    [Header("Input Source")]
    [SerializeField]
    [Tooltip("Modo de lectura de input.\n" +
             "Auto: touch primero, mouse como fallback en editor.\n" +
             "TouchOnly: solo Touchscreen (builds de dispositivo).\n" +
             "MouseOnly: solo Mouse (testing en editor).")]
    private InputReadMode inputReadMode = InputReadMode.Auto;

    [Header("Joystick")]
    [SerializeField]
    [Tooltip("Radio en píxeles desde el toque inicial para alcanzar magnitud máxima (1.0).\n" +
             "Determina cuánto hay que desplazar el dedo para input completo.")]
    private float joystickRadiusPx = 180f;

    [SerializeField]
    [Tooltip("Zona muerta central en píxeles. Desplazamientos dentro de este radio\n" +
             "producen screenDir = Vector2.zero y magnitude = 0.")]
    private float joystickDeadzonePx = 28f;

    [Header("Swipe")]
    [SerializeField]
    [Tooltip("Longitud mínima del gesto en píxeles para clasificarlo como swipe.")]
    private float minimumSwipeLengthPx = 55f;

    [SerializeField]
    [Tooltip("Duración máxima en segundos del gesto para que sea un swipe.\n" +
             "Gestos más lentos (arrastres lentos) no se clasifican como swipe.")]
    private float maximumSwipeDuration = 0.5f;

    [SerializeField]
    [Tooltip("Tolerancia en grados alrededor de cada eje cardinal para clasificar el swipe.\n" +
             "35° → ±35° de arriba = Forward. Más alto = clasificación más permisiva.")]
    [Range(15f, 45f)]
    private float swipeCardinalAngleThreshold = 35f;

    [SerializeField]
    [Tooltip("Longitud del swipe en píxeles que produce intensidad máxima (1.0).")]
    private float maxIntensityLengthPx = 340f;

    [SerializeField]
    [Tooltip("Velocidad del swipe en px/s que produce intensidad máxima (1.0).")]
    private float maxIntensitySpeedPxPerSecond = 1400f;

    [SerializeField]
    [Tooltip("Peso de la longitud frente a la velocidad al calcular intensidad.\n" +
             "1 = solo longitud. 0 = solo velocidad. 0.6 = mezcla recomendada.")]
    [Range(0f, 1f)]
    private float intensityLengthWeight = 0.6f;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("Muestra logs de tracking, dirección emitida y swipes detectados.")]
    private bool debugInput;

    #endregion

    #region Runtime

    private bool    isTracking;
    private Vector2 touchStart;
    private float   touchStartTime;

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
            case InputReadMode.TouchOnly: UpdateTouch(); break;
            case InputReadMode.MouseOnly: UpdateMouse(); break;
            default:                     UpdateAuto();  break;
        }
    }

    private void OnDisable()
    {
        if (isTracking) ForceEnd();
    }

    private void OnValidate()
    {
        joystickRadiusPx   = Mathf.Max(1f, joystickRadiusPx);
        joystickDeadzonePx = Mathf.Clamp(joystickDeadzonePx, 0f, joystickRadiusPx - 1f);

        minimumSwipeLengthPx         = Mathf.Max(1f,    minimumSwipeLengthPx);
        maximumSwipeDuration         = Mathf.Max(0.01f, maximumSwipeDuration);
        maxIntensityLengthPx         = Mathf.Max(1f,    maxIntensityLengthPx);
        maxIntensitySpeedPxPerSecond = Mathf.Max(1f,    maxIntensitySpeedPxPerSecond);
        intensityLengthWeight        = Mathf.Clamp01(intensityLengthWeight);
    }

    #endregion

    #region Input Reading

    private void UpdateAuto()
    {
        if (TryUpdateTouch()) return;

#if UNITY_EDITOR || UNITY_STANDALONE
        UpdateMouse();
#else
        if (isTracking) ForceEnd();
#endif
    }

    private void UpdateTouch()
    {
        if (!TryUpdateTouch() && isTracking) ForceEnd();
    }

    private bool TryUpdateTouch()
    {
        if (Touchscreen.current == null) return false;

        var touch = Touchscreen.current.primaryTouch;

        if (touch.press.wasPressedThisFrame)
        {
            BeginTracking(touch.position.ReadValue(), "Touch");
            return true;
        }

        if (touch.press.isPressed && isTracking)
        {
            EmitJoystickDirection(touch.position.ReadValue());
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
        if (Mouse.current == null) { if (isTracking) ForceEnd(); return; }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            BeginTracking(Mouse.current.position.ReadValue(), "Mouse");
            return;
        }

        if (Mouse.current.leftButton.isPressed && isTracking)
        {
            EmitJoystickDirection(Mouse.current.position.ReadValue());
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

        OnJoystickScreenDirection?.Invoke(Vector2.zero, 0f);

        if (debugInput) Debug.Log($"[UnifiedBallInput] Begin {source}: {position}");
    }

    /// <summary>
    /// Calcula la dirección y magnitud del joystick desde la posición actual y la emite.
    /// La dirección es el vector normalizado del delta completo (la cara del joystick).
    /// La magnitud se normaliza entre la deadzone y el radio externo.
    /// </summary>
    private void EmitJoystickDirection(Vector2 position)
    {
        Vector2 delta        = position - touchStart;
        float   rawMagnitude = delta.magnitude;

        if (rawMagnitude <= joystickDeadzonePx)
        {
            OnJoystickScreenDirection?.Invoke(Vector2.zero, 0f);
            return;
        }

        Vector2 screenDir = delta.normalized;
        float   magnitude = Mathf.Clamp01(
            (rawMagnitude - joystickDeadzonePx) /
            Mathf.Max(1f, joystickRadiusPx - joystickDeadzonePx));

        OnJoystickScreenDirection?.Invoke(screenDir, magnitude);

        if (debugInput)
            Debug.Log($"[UnifiedBallInput] Dir:{screenDir:F2} Mag:{magnitude:F2}");
    }

    private void EndTracking(Vector2 position, string source)
    {
        isTracking = false;

        // Emitir cierre antes de evaluar el swipe: los controladores procesan
        // primero "sin input" y luego reaccionan al swipe si corresponde.
        OnJoystickScreenDirection?.Invoke(Vector2.zero, 0f);

        if (debugInput) Debug.Log($"[UnifiedBallInput] End {source}: {position}");

        EvaluateSwipe(position);
    }

    private void ForceEnd()
    {
        isTracking = false;
        OnJoystickScreenDirection?.Invoke(Vector2.zero, 0f);
    }

    #endregion

    #region Swipe Evaluation

    private void EvaluateSwipe(Vector2 endPosition)
    {
        float duration = Time.unscaledTime - touchStartTime;
        if (duration <= 0f || duration > maximumSwipeDuration) return;

        Vector2 delta  = endPosition - touchStart;
        float   length = delta.magnitude;
        if (length < minimumSwipeLengthPx) return;

        float       speed     = length / duration;
        float       intensity = CalculateIntensity(length, speed);
        Vector2     direction = delta.normalized;
        SwipeIntent intent    = ClassifyIntent(direction);

        SwipeData swipe = new SwipeData(direction, length, speed, intent, intensity);

        if (debugInput)
        {
            Debug.Log(
                $"[UnifiedBallInput] Swipe: {intent} | " +
                $"Length:{length:F0}px | Speed:{speed:F0}px/s | Intensity:{intensity:F2}");
        }

        OnSwipeDetected?.Invoke(swipe);
    }

    /// <summary>
    /// Clasifica la dirección del swipe en 8 intents según el ángulo desde el eje Y positivo.
    /// Y positivo (arriba en pantalla) = Forward.
    /// </summary>
    private SwipeIntent ClassifyIntent(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;

        if (Mathf.Abs(angle) <= swipeCardinalAngleThreshold)               return SwipeIntent.Forward;
        if (Mathf.Abs(angle) >= 180f - swipeCardinalAngleThreshold)        return SwipeIntent.Backward;
        if (angle >  90f - swipeCardinalAngleThreshold && angle <  90f + swipeCardinalAngleThreshold) return SwipeIntent.Right;
        if (angle < -(90f - swipeCardinalAngleThreshold) && angle > -(90f + swipeCardinalAngleThreshold)) return SwipeIntent.Left;

        if (direction.y >= 0f)
            return direction.x > 0f ? SwipeIntent.DiagonalForwardRight : SwipeIntent.DiagonalForwardLeft;

        return direction.x > 0f ? SwipeIntent.DiagonalBackwardRight : SwipeIntent.DiagonalBackwardLeft;
    }

    private float CalculateIntensity(float length, float speed)
    {
        float lengthNorm = Mathf.Clamp01(length / maxIntensityLengthPx);
        float speedNorm  = Mathf.Clamp01(speed  / maxIntensitySpeedPxPerSecond);
        return Mathf.Clamp01(lengthNorm * intensityLengthWeight + speedNorm * (1f - intensityLengthWeight));
    }

    #endregion
}