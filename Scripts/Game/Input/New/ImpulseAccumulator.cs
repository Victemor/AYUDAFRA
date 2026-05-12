using System;
using UnityEngine;

/// <summary>
/// Acumula impulsos de avance a partir de swipes hacia adelante.
/// Los swipes consecutivos incrementan progresivamente la fuerza aplicada.
/// </summary>
public sealed class ImpulseAccumulator : MonoBehaviour
{
    #region Events

    /// <summary>
    /// Se emite cuando hay un impulso listo para ser consumido por el motor.
    /// </summary>
    public event Action<float> OnImpulseReady;

    #endregion

    #region Inspector

    [Header("Referencias")]

    [SerializeField]
    [Tooltip("Input unificado de la escena.")]
    private UnifiedBallInput unifiedInput;

    [Header("Impulso base")]

    [SerializeField]
    [Tooltip("Fuerza base de impulso en m/s aplicada con intensidad 1.0.")]
    private float baseImpulseForce = 6f;

    [SerializeField]
    [Tooltip("Fuerza mínima de impulso independientemente de la intensidad del swipe.")]
    private float minimumImpulseForce = 1.5f;

    [SerializeField]
    [Tooltip("Fuerza máxima de impulso por swipe individual.")]
    private float maximumImpulseForce = 18f;

    [Header("Acumulación consecutiva")]

    [SerializeField]
    [Tooltip("Tiempo máximo en segundos entre swipes para considerarlos consecutivos.")]
    private float consecutiveTimeWindow = 0.65f;

    [SerializeField]
    [Tooltip("Multiplicador adicional por cada swipe consecutivo.")]
    [Range(0f, 1f)]
    private float consecutiveScaleFactor = 0.22f;

    [SerializeField]
    [Tooltip("Cantidad máxima de swipes consecutivos acumulables.")]
    [Range(1, 20)]
    private int maxConsecutiveCount = 8;

    [Header("Velocidad actual como modificador")]

    [SerializeField]
    [Tooltip("Influencia de la velocidad actual sobre el impulso. " +
             "0 = sin influencia. 1 = duplica el impulso al máximo.")]
    [Range(0f, 1f)]
    private float speedInfluenceFactor = 0.35f;

    [Header("Debug")]

    [SerializeField]
    [Tooltip("Muestra logs del acumulador.")]
    private bool debugAccumulator;

    #endregion

    #region Runtime

    private int consecutiveCount;
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
        {
            unifiedInput = FindFirstObjectByType<UnifiedBallInput>();
        }
    }

    private void OnEnable()
    {
        if (unifiedInput != null)
        {
            unifiedInput.OnSwipeDetected += HandleSwipe;
        }
    }

    private void OnDisable()
    {
        if (unifiedInput != null)
        {
            unifiedInput.OnSwipeDetected -= HandleSwipe;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Actualiza la velocidad planar normalizada actual.
    /// Llamado por el motor cada FixedUpdate.
    /// </summary>
    public void NotifyCurrentSpeed(float currentSpeed, float maxSpeed)
    {
        currentSpeedNormalized = maxSpeed > 0f
            ? Mathf.Clamp01(currentSpeed / maxSpeed)
            : 0f;
    }

    /// <summary>
    /// Resetea el contador de swipes consecutivos.
    /// Llamado cuando el jugador frena o cambia de dirección.
    /// </summary>
    public void ResetConsecutive()
    {
        consecutiveCount = 0;
    }

    #endregion

    #region Private

    private void HandleSwipe(SwipeData swipe)
    {
        if (swipe.Intent == SwipeIntent.Forward)
        {
            ProcessForwardSwipe(swipe.Intensity);
            return;
        }

        // Cualquier swipe no-adelante rompe la cadena consecutiva
        consecutiveCount = 0;
    }

    private void ProcessForwardSwipe(float intensity)
    {
        UpdateConsecutiveCount();

        float consecutiveBonus = 1f + consecutiveCount * consecutiveScaleFactor;
        float speedBonus       = 1f + currentSpeedNormalized * speedInfluenceFactor;

        float rawImpulse = baseImpulseForce * intensity * consecutiveBonus * speedBonus;

        float finalImpulse = Mathf.Clamp(rawImpulse, minimumImpulseForce, maximumImpulseForce);

        lastForwardSwipeTime = Time.unscaledTime;

        if (debugAccumulator)
        {
            Debug.Log(
                $"[ImpulseAccumulator] Consecutivos: {consecutiveCount} | " +
                $"Bonus acum: {consecutiveBonus:F2}× | " +
                $"Bonus vel: {speedBonus:F2}× | " +
                $"Impulso final: {finalImpulse:F2}");
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