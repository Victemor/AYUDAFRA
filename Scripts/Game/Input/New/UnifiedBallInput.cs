using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Input unificado para control de la bola con un solo dedo.
///
/// Dos eventos con responsabilidades distintas:
///
/// <see cref="OnSwipeDirection"/>: emitido continuamente mientras el dedo se mueve.
///   Cada vez que el dedo avanza <see cref="minimumSwipeSegmentPx"/> píxeles desde el
///   último segmentStart, se emite un SwipeData y ese punto se convierte en el nuevo origen.
///   Este evento es SOLO de dirección — no da impulso de velocidad.
///   Permite steering continuo sin necesidad de levantar el dedo.
///
/// <see cref="OnSwipeDetected"/>: emitido al levantar el dedo si el segmento restante
///   supera <see cref="minimumReleaseSegmentPx"/> píxeles.
///   Este evento da IMPULSO de velocidad además de dirección.
///   El jugador debe levantar y volver a presionar para acumular velocidad progresivamente.
///
/// Resultado de gameplay:
///   - Dedo deslizando = steering continuo, fricción sigue reduciendo velocidad.
///   - Levantar + volver a presionar = impulso acumulativo (consecutive chain).
///   - Obliga al jugador a hacer swipes repetidos para construir velocidad.
/// </summary>
public sealed class UnifiedBallInput : MonoBehaviour
{
    #region Types

    private enum InputReadMode
    {
        Auto      = 0,
        TouchOnly = 1,
        MouseOnly = 2,
    }

    #endregion

    #region Events

    /// <summary>
    /// Evento de dirección continua. Emitido cada vez que el dedo avanza
    /// <see cref="minimumSwipeSegmentPx"/> píxeles desde el último segmentStart.
    /// NO debe dar impulso — solo actualizar dirección/rotación.
    /// </summary>
    public event Action<SwipeData> OnSwipeDirection;

    /// <summary>Evento de swipe completo. Emitido al levantar el dedo. DA impulso.</summary>
    public event Action<SwipeData> OnSwipeDetected;

    /// <summary>
    /// Emitido cuando el dedo toca la pantalla por primera vez.
    /// Usado por <see cref="SwipeDirectionController"/> para hacer snapshot de la cara
    /// actual y usarla como referencia absoluta para todos los segmentos del gesto,
    /// eliminando la acumulación de deltas por segmentos consecutivos.
    /// </summary>
    public event Action OnTrackingBegan;

    #endregion

    #region Inspector

    [Header("Input Source")]
    [SerializeField]
    [Tooltip("Auto: touch primero, mouse como fallback en editor.\n" +
             "TouchOnly: solo Touchscreen.\nMouseOnly: solo Mouse.")]
    private InputReadMode inputReadMode = InputReadMode.Auto;

    [Header("Detección Continua (OnSwipeDirection)")]
    [SerializeField]
    [Tooltip("Distancia mínima en píxeles para emitir un segmento de dirección continua.\n" +
             "Más pequeño = steering más fino pero más eventos por frame.")]
    private float minimumSwipeSegmentPx = 55f;

    [Header("Swipe con Impulso (OnSwipeDetected)")]
    [SerializeField]
    [Tooltip("Distancia mínima del segmento restante al levantar el dedo\n" +
             "para emitir el swipe de impulso final.")]
    private float minimumReleaseSegmentPx = 30f;

    [Header("Cálculo de Intensidad")]
    [SerializeField]
    [Tooltip("Longitud de segmento en píxeles que produce intensidad máxima (1.0).")]
    private float maxIntensityLengthPx = 200f;

    [SerializeField]
    [Tooltip("Velocidad del segmento en px/s que produce intensidad máxima (1.0).")]
    private float maxIntensitySpeedPxPerSecond = 1200f;

    [SerializeField]
    [Tooltip("Peso de la longitud frente a la velocidad al calcular intensidad.")]
    [Range(0f, 1f)]
    private float intensityLengthWeight = 0.7f;

    [Header("Clasificación de Dirección")]
    [SerializeField]
    [Tooltip("Tolerancia en grados alrededor de cada eje cardinal.")]
    [Range(15f, 45f)]
    private float swipeCardinalAngleThreshold = 35f;

    [Header("Debug")]
    [SerializeField]
    private bool debugInput;

    #endregion

    #region Runtime

    private bool    isTracking;
    private Vector2 segmentStart;
    private float   segmentStartTime;

    #endregion

    #region Properties

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
        isTracking = false;
    }

    private void OnValidate()
    {
        minimumSwipeSegmentPx    = Mathf.Max(1f,    minimumSwipeSegmentPx);
        minimumReleaseSegmentPx  = Mathf.Max(0f,    minimumReleaseSegmentPx);
        maxIntensityLengthPx     = Mathf.Max(1f,    maxIntensityLengthPx);
        maxIntensitySpeedPxPerSecond = Mathf.Max(1f, maxIntensitySpeedPxPerSecond);
        intensityLengthWeight    = Mathf.Clamp01(intensityLengthWeight);
    }

    #endregion

    #region Input Reading

    private void UpdateAuto()
    {
        if (TryUpdateTouch()) return;
#if UNITY_EDITOR || UNITY_STANDALONE
        UpdateMouse();
#else
        if (isTracking) isTracking = false;
#endif
    }

    private void UpdateTouch()
    {
        if (!TryUpdateTouch() && isTracking) isTracking = false;
    }

    private bool TryUpdateTouch()
    {
        if (Touchscreen.current == null) return false;
        var touch = Touchscreen.current.primaryTouch;

        if (touch.press.wasPressedThisFrame)  { BeginTracking(touch.position.ReadValue(), "Touch"); return true; }
        if (touch.press.isPressed && isTracking) { UpdateContinuousDetection(touch.position.ReadValue()); return true; }
        if (touch.press.wasReleasedThisFrame && isTracking) { EndTracking(touch.position.ReadValue(), "Touch"); return true; }

        return false;
    }

    private void UpdateMouse()
    {
        if (Mouse.current == null) { isTracking = false; return; }

        if (Mouse.current.leftButton.wasPressedThisFrame)           { BeginTracking(Mouse.current.position.ReadValue(), "Mouse"); return; }
        if (Mouse.current.leftButton.isPressed && isTracking)        { UpdateContinuousDetection(Mouse.current.position.ReadValue()); return; }
        if (Mouse.current.leftButton.wasReleasedThisFrame && isTracking) { EndTracking(Mouse.current.position.ReadValue(), "Mouse"); }
    }

    #endregion

    #region Tracking

    private void BeginTracking(Vector2 position, string source)
    {
        isTracking       = true;
        segmentStart     = position;
        segmentStartTime = Time.unscaledTime;

        OnTrackingBegan?.Invoke();

        if (debugInput) Debug.Log($"[UnifiedBallInput] Begin {source}: {position}");
    }

    /// <summary>
    /// Llamado cada frame mientras el dedo está presionado.
    /// Si el dedo avanzó lo suficiente, emite <see cref="OnSwipeDirection"/> (solo dirección)
    /// y resetea el segmentStart al punto actual.
    /// </summary>
    private void UpdateContinuousDetection(Vector2 position)
    {
        Vector2 delta = position - segmentStart;
        if (delta.magnitude < minimumSwipeSegmentPx) return;

        EmitSwipeData(position, isImpulse: false);
        segmentStart     = position;
        segmentStartTime = Time.unscaledTime;
    }

    /// <summary>
    /// Al levantar el dedo: emite <see cref="OnSwipeDetected"/> (con impulso)
    /// si el segmento restante es suficientemente largo.
    /// </summary>
    private void EndTracking(Vector2 position, string source)
    {
        isTracking = false;

        Vector2 delta = position - segmentStart;
        if (delta.magnitude >= minimumReleaseSegmentPx)
            EmitSwipeData(position, isImpulse: true);

        if (debugInput) Debug.Log($"[UnifiedBallInput] End {source}: {position}");
    }

    #endregion

    #region Swipe Emission

    private void EmitSwipeData(Vector2 endPosition, bool isImpulse)
    {
        Vector2 delta    = endPosition - segmentStart;
        float   length   = delta.magnitude;
        float   duration = Time.unscaledTime - segmentStartTime;
        float   speed    = duration > 0.001f ? length / duration : maxIntensitySpeedPxPerSecond;

        Vector2     direction = delta.normalized;
        SwipeIntent intent    = ClassifyIntent(direction);
        float       intensity = CalculateIntensity(length, speed);

        SwipeData swipe = new SwipeData(direction, length, speed, intent, intensity);

        if (debugInput)
            Debug.Log($"[UnifiedBallInput] {(isImpulse ? "IMPULSO" : "Dirección")} | {intent} | {length:F0}px");

        if (isImpulse)
            OnSwipeDetected?.Invoke(swipe);
        else
            OnSwipeDirection?.Invoke(swipe);
    }

    private SwipeIntent ClassifyIntent(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;

        if (Mathf.Abs(angle) <= swipeCardinalAngleThreshold)                                              return SwipeIntent.Forward;
        if (Mathf.Abs(angle) >= 180f - swipeCardinalAngleThreshold)                                       return SwipeIntent.Backward;
        if (angle >  90f - swipeCardinalAngleThreshold && angle <  90f + swipeCardinalAngleThreshold)     return SwipeIntent.Right;
        if (angle < -(90f - swipeCardinalAngleThreshold) && angle > -(90f + swipeCardinalAngleThreshold)) return SwipeIntent.Left;
        if (direction.y >= 0f) return direction.x > 0f ? SwipeIntent.DiagonalForwardRight : SwipeIntent.DiagonalForwardLeft;
        return direction.x > 0f ? SwipeIntent.DiagonalBackwardRight : SwipeIntent.DiagonalBackwardLeft;
    }

    private float CalculateIntensity(float length, float speed)
    {
        float l = Mathf.Clamp01(length / maxIntensityLengthPx);
        float s = Mathf.Clamp01(speed  / maxIntensitySpeedPxPerSecond);
        return Mathf.Clamp01(l * intensityLengthWeight + s * (1f - intensityLengthWeight));
    }

    #endregion
}