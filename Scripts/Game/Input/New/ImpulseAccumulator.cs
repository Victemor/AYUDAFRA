using System;
using UnityEngine;

/// <summary>
/// Acumula impulsos de avance a partir de swipes.
/// Los swipes consecutivos hacia adelante incrementan progresivamente la fuerza aplicada.
///
/// Intents que generan impulso (toda la mitad "forward" del gesto):
///   Forward, Left, Right, DiagonalForwardLeft, DiagonalForwardRight.
/// Intents que rompen la cadena sin impulso:
///   Backward, DiagonalBackwardLeft, DiagonalBackwardRight.
///
/// La dirección la gestiona SphereRotationController vía OnDirectionInput continuo.
/// Este acumulador solo decide la magnitud del impulso de avance.
/// </summary>
public sealed class ImpulseAccumulator : MonoBehaviour
{
    #region Events

    /// <summary>Se emite cuando hay un impulso listo para ser consumido por el motor.</summary>
    public event Action<float> OnImpulseReady;

    #endregion

    #region Inspector

    [Header("Referencias")]
    [SerializeField][Tooltip("Input unificado de la escena.")]
    private UnifiedBallInput unifiedInput;

    [Header("Impulso base")]
    [SerializeField][Tooltip("Fuerza base de impulso en m/s aplicada con intensidad 1.0.")]
    private float baseImpulseForce = 6f;

    [SerializeField][Tooltip("Fuerza mínima de impulso independientemente de la intensidad del swipe.")]
    private float minimumImpulseForce = 1.5f;

    [SerializeField][Tooltip("Fuerza máxima de impulso por swipe individual.")]
    private float maximumImpulseForce = 18f;

    [Header("Acumulación consecutiva")]
    [SerializeField][Tooltip("Tiempo máximo en segundos entre swipes para considerarlos consecutivos.")]
    private float consecutiveTimeWindow = 0.65f;

    [SerializeField][Tooltip("Multiplicador adicional por cada swipe consecutivo.")][Range(0f, 1f)]
    private float consecutiveScaleFactor = 0.22f;

    [SerializeField][Tooltip("Cantidad máxima de swipes consecutivos acumulables.")][Range(1, 20)]
    private int maxConsecutiveCount = 8;

    [Header("Velocidad actual como modificador")]
    [SerializeField]
    [Tooltip("Influencia de la velocidad actual sobre el impulso. 0=sin influencia. 1=duplica al máximo.")]
    [Range(0f, 1f)]
    private float speedInfluenceFactor = 0.35f;

    [Header("Swipes laterales")]
    [SerializeField]
    [Tooltip("Multiplicador de impulso aplicado a swipes Left, Right y diagonales forward. " +
             "1.0 = mismo impulso que forward puro. " +
             "0.7 = impulso reducido para laterales (recomendado).")]
    [Range(0f, 1f)]
    private float lateralSwipeImpulseMultiplier = 0.8f;

    [Header("Debug")]
    [SerializeField][Tooltip("Muestra logs del acumulador.")]
    private bool debugAccumulator;

    #endregion

    #region Runtime

    private int   consecutiveCount;
    private float lastForwardSwipeTime;
    private float currentSpeedNormalized;

    #endregion

    #region Properties

    /// <summary>Cantidad actual de swipes consecutivos acumulados.</summary>
    public int ConsecutiveCount => consecutiveCount;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        unifiedInput = FindFirstObjectByType<UnifiedBallInput>();
    }

    private void Awake()
    {
        if (unifiedInput == null)
            unifiedInput = FindFirstObjectByType<UnifiedBallInput>();
    }

    private void OnEnable()
    {
        if (unifiedInput != null)
            unifiedInput.OnSwipeDetected += HandleSwipe;
    }

    private void OnDisable()
    {
        if (unifiedInput != null)
            unifiedInput.OnSwipeDetected -= HandleSwipe;
    }

    #endregion

    #region Public API

    /// <summary>Actualiza la velocidad planar normalizada actual. Llamado por el motor.</summary>
    public void NotifyCurrentSpeed(float currentSpeed, float maxSpeed)
    {
        currentSpeedNormalized = maxSpeed > 0f
            ? Mathf.Clamp01(currentSpeed / maxSpeed)
            : 0f;
    }

    /// <summary>Resetea el contador de swipes consecutivos.</summary>
    public void ResetConsecutive()
    {
        consecutiveCount = 0;
    }

    #endregion

    #region Private

    private void HandleSwipe(SwipeData swipe)
    {
        switch (swipe.Intent)
        {
            case SwipeIntent.Forward:
                // Swipe adelante puro: impulso completo.
                ProcessForwardSwipe(swipe.Intensity, multiplier: 1f);
                break;

            case SwipeIntent.Left:
            case SwipeIntent.Right:
            case SwipeIntent.DiagonalForwardLeft:
            case SwipeIntent.DiagonalForwardRight:
                // Swipes laterales y diagonales hacia adelante: generan impulso con
                // multiplicador reducido. La rotación ya fue aplicada vía OnDirectionInput
                // continuo durante el gesto; este impulso da la tracción de avance.
                ProcessForwardSwipe(swipe.Intensity, multiplier: lateralSwipeImpulseMultiplier);
                break;

            default:
                // Backward y diagonales atrás: rompen la cadena consecutiva sin dar impulso.
                consecutiveCount = 0;
                break;
        }
    }

    private void ProcessForwardSwipe(float intensity, float multiplier)
    {
        UpdateConsecutiveCount();

        float consecutiveBonus = 1f + consecutiveCount * consecutiveScaleFactor;
        float speedBonus       = 1f + currentSpeedNormalized * speedInfluenceFactor;
        float rawImpulse       = baseImpulseForce * intensity * consecutiveBonus * speedBonus * multiplier;
        float finalImpulse     = Mathf.Clamp(rawImpulse, minimumImpulseForce, maximumImpulseForce);

        lastForwardSwipeTime = Time.unscaledTime;

        if (debugAccumulator)
        {
            Debug.Log(
                $"[ImpulseAccumulator] Intent: | Consecutivos: {consecutiveCount} | " +
                $"Multiplier: {multiplier:F2} | Bonus acum: {consecutiveBonus:F2}x | " +
                $"Bonus vel: {speedBonus:F2}x | Impulso final: {finalImpulse:F2}");
        }

        OnImpulseReady?.Invoke(finalImpulse);
    }

    private void UpdateConsecutiveCount()
    {
        float timeSinceLast = Time.unscaledTime - lastForwardSwipeTime;
        bool isConsecutive  = timeSinceLast <= consecutiveTimeWindow && consecutiveCount > 0;

        consecutiveCount = isConsecutive
            ? Mathf.Min(consecutiveCount + 1, maxConsecutiveCount)
            : 1;
    }

    #endregion
}