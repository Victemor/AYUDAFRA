using System;
using UnityEngine;

/// <summary>
/// Acumula fuerza de frenado a partir de swipes hacia atrás consecutivos.
/// Solo actúa como freno mientras la velocidad supera el umbral mínimo.
/// </summary>
public sealed class BrakeAccumulator : MonoBehaviour
{
    #region Events

    /// <summary>
    /// Se emite cuando hay fuerza de frenado lista para ser consumida por el motor.
    /// </summary>
    public event Action<float> OnBrakeReady;

    /// <summary>
    /// Se emite cuando la velocidad cae al umbral y el swipe atrás
    /// ya no frena sino que podría usarse para otro propósito.
    /// </summary>
    public event Action<SwipeData> OnBrakeToRotateTransition;

    #endregion

    #region Inspector

    [Header("Referencias")]

    [SerializeField]
    [Tooltip("Input unificado de la escena.")]
    private UnifiedBallInput unifiedInput;

    [Header("Frenado")]

    [SerializeField]
    [Tooltip("Fuerza base de frenado en m/s con intensidad 1.0.")]
    private float baseBrakeForce = 5f;

    [SerializeField]
    [Tooltip("Fuerza máxima de frenado acumulada por swipe.")]
    private float maximumBrakeForce = 22f;

    [SerializeField]
    [Tooltip("Velocidad mínima en m/s. Por debajo de este valor el swipe atrás " +
             "deja de frenar y emite OnBrakeToRotateTransition.")]
    private float minimumSpeedThreshold = 1.2f;

    [Header("Acumulación consecutiva")]

    [SerializeField]
    [Tooltip("Tiempo máximo entre swipes atrás para considerarlos consecutivos.")]
    private float consecutiveTimeWindow = 0.7f;

    [SerializeField]
    [Tooltip("Multiplicador adicional por cada swipe atrás consecutivo.")]
    [Range(0f, 1f)]
    private float consecutiveScaleFactor = 0.3f;

    [SerializeField]
    [Tooltip("Máximo de swipes atrás consecutivos acumulables.")]
    [Range(1, 15)]
    private int maxConsecutiveCount = 6;

    [Header("Debug")]

    [SerializeField]
    [Tooltip("Muestra logs del acumulador de frenado.")]
    private bool debugAccumulator;

    #endregion

    #region Runtime

    private int consecutiveCount;
    private float lastBackwardSwipeTime;
    private float currentSpeed;

    #endregion

    #region Properties

    /// <summary>Indica si la velocidad actual permite frenar.</summary>
    public bool CanBrake => currentSpeed > minimumSpeedThreshold;

    /// <summary>Cantidad actual de swipes atrás consecutivos.</summary>
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
    /// Actualiza la velocidad planar actual de la esfera.
    /// Llamado por el motor cada FixedUpdate.
    /// </summary>
    public void NotifyCurrentSpeed(float speed)
    {
        currentSpeed = speed;
    }

    /// <summary>
    /// Resetea el contador de swipes consecutivos.
    /// Llamado cuando el jugador cambia a otro tipo de swipe.
    /// </summary>
    public void ResetConsecutive()
    {
        consecutiveCount = 0;
    }

    #endregion

    #region Private

    private void HandleSwipe(SwipeData swipe)
    {
        if (swipe.Intent == SwipeIntent.Backward)
        {
            ProcessBackwardSwipe(swipe);
            return;
        }

        // Swipe en otra dirección rompe la cadena de frenado
        consecutiveCount = 0;
    }

    private void ProcessBackwardSwipe(SwipeData swipe)
    {
        if (!CanBrake)
        {
            consecutiveCount = 0;

            if (debugAccumulator)
            {
                Debug.Log(
                    "[BrakeAccumulator] Velocidad en umbral — " +
                    "transición a OnBrakeToRotateTransition");
            }

            OnBrakeToRotateTransition?.Invoke(swipe);
            return;
        }

        UpdateConsecutiveCount();

        float consecutiveBonus = 1f + consecutiveCount * consecutiveScaleFactor;
        float rawBrake         = baseBrakeForce * swipe.Intensity * consecutiveBonus;
        float finalBrake       = Mathf.Min(rawBrake, maximumBrakeForce);

        lastBackwardSwipeTime = Time.unscaledTime;

        if (debugAccumulator)
        {
            Debug.Log(
                $"[BrakeAccumulator] Consecutivos: {consecutiveCount} | " +
                $"Bonus acum: {consecutiveBonus:F2}× | " +
                $"Frenado final: {finalBrake:F2}");
        }

        OnBrakeReady?.Invoke(finalBrake);
    }

    private void UpdateConsecutiveCount()
    {
        float timeSinceLast = Time.unscaledTime - lastBackwardSwipeTime;
        bool isConsecutive  = timeSinceLast <= consecutiveTimeWindow && consecutiveCount > 0;

        consecutiveCount = isConsecutive
            ? Mathf.Min(consecutiveCount + 1, maxConsecutiveCount)
            : 1;
    }

    #endregion
}