using System;
using UnityEngine;

/// <summary>
/// Gestiona los efectos activos de power-ups coleccionables sobre la bola.
///
/// Responsabilidades:
/// - Recibir power-ups mediante <see cref="Collect"/>.
/// - Mantener un timer independiente por tipo de efecto.
/// - Aplicar y revertir los efectos en <see cref="BallMovementMotor"/> (Reduced Inertia).
/// - Atraer monedas cercanas en <c>FixedUpdate</c> mientras el imán está activo.
/// - Exponer eventos para que la UI muestre el estado activo.
///
/// Recolectar el mismo tipo mientras está activo reinicia el timer (no acumula).
/// Dos tipos distintos pueden estar activos simultáneamente.
/// </summary>
[RequireComponent(typeof(BallMovementMotor))]
public sealed class BallPowerUpController : MonoBehaviour
{
    #region Events

    /// <summary>
    /// Disparado al recolectar un power-up. Parámetros: tipo, duración total.
    /// Subscriptores típicos: UI de power-ups activos.
    /// </summary>
    public event Action<CollectiblePowerUpType, float> OnPowerUpCollected;

    /// <summary>
    /// Disparado cuando el efecto de un power-up expira.
    /// </summary>
    public event Action<CollectiblePowerUpType> OnPowerUpExpired;

    #endregion

    #region Inspector

    [Header("Referencias")]
    [SerializeField]
    [Tooltip("Motor de movimiento de la bola. Auto-resuelto en Reset.")]
    private BallMovementMotor movementMotor;

    [Header("Imán")]
    [SerializeField]
    [Tooltip("Capa de las monedas en la escena. El imán hace OverlapSphere con esta máscara.")]
    private LayerMask coinLayer;

    #endregion

    #region Runtime

    private float reducedInertiaTimer;
    private float magnetTimer;

    private CollectiblePowerUpData activeInertiaData;
    private CollectiblePowerUpData activeMagnetData;

    #endregion

    #region Properties

    /// <summary><c>true</c> mientras el efecto de Reduced Inertia está activo.</summary>
    public bool IsReducedInertiaActive => reducedInertiaTimer > 0f;

    /// <summary><c>true</c> mientras el efecto de imán está activo.</summary>
    public bool IsMagnetActive => magnetTimer > 0f;

    /// <summary>Tiempo restante de Reduced Inertia en segundos.</summary>
    public float ReducedInertiaRemainingTime => reducedInertiaTimer;

    /// <summary>Tiempo restante del imán en segundos.</summary>
    public float MagnetRemainingTime => magnetTimer;

    /// <summary>Indica si el tipo de efecto indicado está actualmente activo.</summary>
    public bool IsActive(CollectiblePowerUpType type)
    {
        return type switch
        {
            CollectiblePowerUpType.ReducedInertia => IsReducedInertiaActive,
            CollectiblePowerUpType.Magnet         => IsMagnetActive,
            _                                     => false,
        };
    }

    /// <summary>Tiempo restante del tipo de efecto indicado en segundos.</summary>
    public float GetRemainingTime(CollectiblePowerUpType type)
    {
        return type switch
        {
            CollectiblePowerUpType.ReducedInertia => reducedInertiaTimer,
            CollectiblePowerUpType.Magnet         => magnetTimer,
            _                                     => 0f,
        };
    }

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        movementMotor = GetComponent<BallMovementMotor>();
    }

    private void Awake()
    {
        if (movementMotor == null)
            movementMotor = GetComponent<BallMovementMotor>();
    }

    private void Update()
    {
        TickReducedInertia();
        TickMagnet();
    }

    private void FixedUpdate()
    {
        if (IsMagnetActive)
            AttractCoins();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Aplica el efecto del power-up indicado.
    /// Si el mismo tipo ya está activo, reinicia su timer.
    /// </summary>
    public void Collect(CollectiblePowerUpData powerUpData)
    {
        if (powerUpData == null) return;

        switch (powerUpData.Type)
        {
            case CollectiblePowerUpType.ReducedInertia:
                ApplyReducedInertia(powerUpData);
                break;

            case CollectiblePowerUpType.Magnet:
                ApplyMagnet(powerUpData);
                break;
        }
    }

    #endregion

    #region Effect Application

    private void ApplyReducedInertia(CollectiblePowerUpData data)
    {
        activeInertiaData   = data;
        reducedInertiaTimer = data.EffectDuration;
        movementMotor?.SetInertiaReduction(data.FrictionMultiplier, data.GroundStickMultiplier);
        OnPowerUpCollected?.Invoke(data.Type, data.EffectDuration);
    }

    private void ApplyMagnet(CollectiblePowerUpData data)
    {
        activeMagnetData = data;
        magnetTimer      = data.EffectDuration;
        OnPowerUpCollected?.Invoke(data.Type, data.EffectDuration);
    }

    #endregion

    #region Tick

    private void TickReducedInertia()
    {
        if (!IsReducedInertiaActive) return;

        reducedInertiaTimer -= Time.deltaTime;

        if (reducedInertiaTimer > 0f) return;

        reducedInertiaTimer = 0f;
        activeInertiaData   = null;
        movementMotor?.ClearInertiaReduction();
        OnPowerUpExpired?.Invoke(CollectiblePowerUpType.ReducedInertia);
    }

    private void TickMagnet()
    {
        if (!IsMagnetActive) return;

        magnetTimer -= Time.deltaTime;

        if (magnetTimer > 0f) return;

        magnetTimer      = 0f;
        activeMagnetData = null;
        OnPowerUpExpired?.Invoke(CollectiblePowerUpType.Magnet);
    }

    #endregion

    #region Magnet

    /// <summary>
    /// Mueve las monedas cercanas hacia la bola en cada FixedUpdate mientras el imán está activo.
    /// La recolección real ocurre cuando la moneda entra en el trigger normal de la bola.
    /// </summary>
    private void AttractCoins()
    {
        if (activeMagnetData == null) return;

        Collider[] nearby = Physics.OverlapSphere(
            transform.position,
            activeMagnetData.MagnetRadius,
            coinLayer,
            QueryTriggerInteraction.Collide);

        float stepDistance = activeMagnetData.MagnetAttractionSpeed * Time.fixedDeltaTime;

        for (int i = 0; i < nearby.Length; i++)
        {
            if (nearby[i] == null) continue;

            Transform coinTransform = nearby[i].transform;
            Vector3 toCenter        = transform.position - coinTransform.position;
            float   distance        = toCenter.magnitude;

            if (distance < 0.05f) continue;

            coinTransform.position += toCenter.normalized * Mathf.Min(stepDistance, distance);
        }
    }

    #endregion
}