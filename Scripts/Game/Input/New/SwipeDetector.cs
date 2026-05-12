using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Detecta y clasifica gestos de swipe en pantalla.
/// Emite SwipeData con dirección, longitud, velocidad e intent clasificado.
/// Compatible con touch en dispositivo y mouse en editor.
/// </summary>
public sealed class SwipeDetector : MonoBehaviour
{
    #region Events

    /// <summary>
    /// Se emite al completar un swipe válido con sus datos clasificados.
    /// </summary>
    public event Action<SwipeData> OnSwipeDetected;

    #endregion

    #region Inspector

    [Header("Thresholds")]

    [SerializeField]
    [Tooltip("Longitud mínima en píxeles para considerar un gesto como swipe válido.")]
    private float minimumSwipeLengthPx = 40f;

    [SerializeField]
    [Tooltip("Tiempo máximo en segundos para que un gesto se considere swipe. " +
             "Gestos más lentos se descartan.")]
    private float maximumSwipeDuration = 0.5f;

    [Header("Diagonal Detection")]

    [SerializeField]
    [Tooltip("Ángulo en grados desde cada eje cardinal dentro del cual se considera " +
             "movimiento puro (no diagonal). 30° significa que ±30° del eje = cardinal.")]
    [Range(15f, 45f)]
    private float cardinalAngleThreshold = 30f;

    [Header("Intensity")]

    [SerializeField]
    [Tooltip("Longitud de swipe en píxeles que equivale a intensidad máxima (1.0).")]
    private float maxIntensityLengthPx = 300f;

    [SerializeField]
    [Tooltip("Velocidad de swipe en px/s que equivale a intensidad máxima (1.0).")]
    private float maxIntensitySpeedPxPerSecond = 1200f;

    [SerializeField]
    [Tooltip("Peso de la longitud vs la velocidad al calcular intensidad. " +
             "1 = solo longitud. 0 = solo velocidad.")]
    [Range(0f, 1f)]
    private float intensityLengthWeight = 0.6f;

    [Header("Editor")]

    [SerializeField]
    [Tooltip("Usa mouse para simular swipes en el editor.")]
    private bool useMouseInEditor = true;

    [SerializeField]
    [Tooltip("Muestra logs del detector para depuración.")]
    private bool debugSwipes;

    #endregion

    #region Runtime

    private bool isTracking;
    private Vector2 gestureStartPosition;
    private float gestureStartTime;

    #endregion

    #region Properties

    /// <summary>Indica si hay un gesto en progreso.</summary>
    public bool IsTracking => isTracking;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (useMouseInEditor)
        {
            UpdateMouseInput();
            return;
        }
#endif
        UpdateTouchInput();
    }

    #endregion

    #region Input Reading

    private void UpdateTouchInput()
    {
        if (Touchscreen.current == null)
        {
            return;
        }

        var touch = Touchscreen.current.primaryTouch;

        if (touch.press.wasPressedThisFrame)
        {
            BeginTracking(touch.position.ReadValue());
            return;
        }

        if (touch.press.wasReleasedThisFrame && isTracking)
        {
            EndTracking(touch.position.ReadValue());
        }
    }

    private void UpdateMouseInput()
    {
        if (Mouse.current == null)
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            BeginTracking(Mouse.current.position.ReadValue());
            return;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && isTracking)
        {
            EndTracking(Mouse.current.position.ReadValue());
        }
    }

    #endregion

    #region Gesture Lifecycle

    private void BeginTracking(Vector2 screenPosition)
    {
        isTracking = true;
        gestureStartPosition = screenPosition;
        gestureStartTime = Time.unscaledTime;
    }

    private void EndTracking(Vector2 screenPosition)
    {
        isTracking = false;

        float duration = Time.unscaledTime - gestureStartTime;

        if (duration <= 0f || duration > maximumSwipeDuration)
        {
            return;
        }

        Vector2 delta = screenPosition - gestureStartPosition;
        float length = delta.magnitude;

        if (length < minimumSwipeLengthPx)
        {
            return;
        }

        float speed = length / duration;
        Vector2 direction = delta.normalized;
        float intensity = CalculateIntensity(length, speed);
        SwipeIntent intent = ClassifyIntent(direction);

        SwipeData swipeData = new SwipeData(direction, length, speed, intent, intensity);

        if (debugSwipes)
        {
            Debug.Log(
                $"[SwipeDetector] Intent: {intent} | " +
                $"Longitud: {length:F0}px | " +
                $"Velocidad: {speed:F0}px/s | " +
                $"Intensidad: {intensity:F2}");
        }

        OnSwipeDetected?.Invoke(swipeData);
    }

    #endregion

    #region Classification

    /// <summary>
    /// Clasifica el intent del swipe según la dirección del gesto.
    /// </summary>
    private SwipeIntent ClassifyIntent(Vector2 direction)
    {
        // Ángulo en grados desde el eje Y positivo (arriba de pantalla = adelante)
        // Rango: -180 a 180
        float angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;

        bool isForward = Mathf.Abs(angle) <= cardinalAngleThreshold;
        bool isBackward = Mathf.Abs(angle) >= 180f - cardinalAngleThreshold;
        bool isRight = angle > 90f - cardinalAngleThreshold
                       && angle < 90f + cardinalAngleThreshold;
        bool isLeft = angle < -(90f - cardinalAngleThreshold)
                      && angle > -(90f + cardinalAngleThreshold);

        if (isForward) return SwipeIntent.Forward;
        if (isBackward) return SwipeIntent.Backward;
        if (isRight) return SwipeIntent.Right;
        if (isLeft) return SwipeIntent.Left;

        // Diagonales — cuadrante por cuadrante
        if (direction.y > 0f)
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
    /// Calcula la intensidad normalizada [0,1] combinando longitud y velocidad.
    /// </summary>
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