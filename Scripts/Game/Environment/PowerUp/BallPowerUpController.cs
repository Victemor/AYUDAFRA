using System;
using UnityEngine;

/// <summary>
/// Coordinador de efectos de power-up activos sobre la bola.
///
/// Responsabilidades:
/// - Recibir activaciones de efecto desde los pickups (<see cref="CollectiblePowerUpBase"/>).
/// - Gestionar los timers de duración de cada efecto activo.
/// - Delegar los modificadores de movimiento al <see cref="BallMovementMotor"/>.
/// - Ejecutar la lógica de atracción del imán en cada FixedUpdate.
/// - Notificar a sistemas externos (UI, audio) a través de eventos cuando un efecto
///   comienza o expira.
///
/// Diseño de extensibilidad:
/// Para añadir un nuevo power-up:
/// 1. Crear una subclase de <see cref="CollectiblePowerUpBase"/> con sus datos.
/// 2. Añadir un método <c>ActivateXxx</c> público en este controlador.
/// 3. Añadir el timer y la lógica de tick correspondiente.
/// Este controlador no necesita conocer los prefabs ni la lógica de spawn.
/// </summary>
public sealed class BallPowerUpController : MonoBehaviour
{
    #region Constants

    /// <summary>
    /// Tamaño del buffer pre-allocado para OverlapSphereNonAlloc del imán.
    /// Cubre el caso de hasta 32 monedas simultáneas en el radio de atracción.
    /// Aumentar si los niveles generan densidades mayores.
    /// </summary>
    private const int MagnetBufferSize = 32;

    #endregion

    #region Events

    /// <summary>
    /// Disparado cuando un power-up es recolectado y su efecto comienza.
    /// Parámetros: tipo de efecto, duración total en segundos.
    /// Consumidores típicos: HUD (barra de duración), sistema de audio.
    /// </summary>
    public event Action<CollectiblePowerUpType, float> OnPowerUpCollected;

    /// <summary>
    /// Disparado cuando el timer de un power-up llega a cero y el efecto termina.
    /// Parámetro: tipo de efecto que expiró.
    /// Consumidores típicos: HUD (ocultar indicador), sistema de audio.
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

    // — Timers de efectos activos —

    private float reducedInertiaTimer;
    private float magnetTimer;

    // — Parámetros del imán activo —
    // Guardados al activar para no depender de referencia externa durante el tick.

    private float activeMagnetRadius;
    private float activeMagnetAttractionSpeed;

    // — Buffer pre-allocado para el imán —
    // Se reutiliza en cada FixedUpdate para evitar GC allocation en hot path.

    private readonly Collider[] _magnetBuffer = new Collider[MagnetBufferSize];

    #endregion

    #region Properties

    /// <summary><c>true</c> mientras el efecto de Inercia Reducida está activo.</summary>
    public bool IsReducedInertiaActive => reducedInertiaTimer > 0f;

    /// <summary><c>true</c> mientras el efecto de Imán está activo.</summary>
    public bool IsMagnetActive => magnetTimer > 0f;

    /// <summary>Tiempo restante de Inercia Reducida en segundos.</summary>
    public float ReducedInertiaRemainingTime => reducedInertiaTimer;

    /// <summary>Tiempo restante del Imán en segundos.</summary>
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

    #region Public API — Activación de Efectos

    /// <summary>
    /// Activa el efecto de Imán.
    /// Si el efecto ya está activo, reinicia su timer con la nueva duración.
    /// Llamado por <see cref="PowerUpMagnet.ApplyEffect"/>.
    /// </summary>
    /// <param name="duration">Duración del efecto en segundos.</param>
    /// <param name="radius">Radio de atracción en metros.</param>
    /// <param name="attractionSpeed">Velocidad de movimiento de monedas en m/s.</param>
    public void ActivateMagnet(float duration, float radius, float attractionSpeed)
    {
        activeMagnetRadius        = radius;
        activeMagnetAttractionSpeed = attractionSpeed;
        magnetTimer               = duration;

        OnPowerUpCollected?.Invoke(CollectiblePowerUpType.Magnet, duration);
    }

    /// <summary>
    /// Activa el efecto de Inercia Reducida.
    /// Si el efecto ya está activo, reinicia su timer y reaplica los modificadores.
    /// Llamado por <see cref="PowerUpReducedInertia.ApplyEffect"/>.
    /// </summary>
    /// <param name="duration">Duración del efecto en segundos.</param>
    /// <param name="frictionMult">Multiplicador sobre la fricción pasiva [0 = sin fricción, 1 = normal].</param>
    /// <param name="groundStickMult">Multiplicador sobre la adhesión al suelo [0 = sin adhesión, 1 = normal].</param>
    /// <param name="steeringBoostMult">Multiplicador sobre la velocidad de steering [1 = normal, >1 = más reactivo].</param>
    public void ActivateReducedInertia(
        float duration,
        float frictionMult,
        float groundStickMult,
        float steeringBoostMult)
    {
        reducedInertiaTimer = duration;
        movementMotor?.SetInertiaReduction(frictionMult, groundStickMult, steeringBoostMult);

        OnPowerUpCollected?.Invoke(CollectiblePowerUpType.ReducedInertia, duration);
    }

    #endregion

    #region Tick — Timers

    private void TickReducedInertia()
    {
        if (!IsReducedInertiaActive) return;

        reducedInertiaTimer -= Time.deltaTime;

        if (reducedInertiaTimer > 0f) return;

        reducedInertiaTimer = 0f;
        movementMotor?.ClearInertiaReduction();
        OnPowerUpExpired?.Invoke(CollectiblePowerUpType.ReducedInertia);
    }

    private void TickMagnet()
    {
        if (!IsMagnetActive) return;

        magnetTimer -= Time.deltaTime;

        if (magnetTimer > 0f) return;

        magnetTimer                 = 0f;
        activeMagnetRadius          = 0f;
        activeMagnetAttractionSpeed = 0f;
        OnPowerUpExpired?.Invoke(CollectiblePowerUpType.Magnet);
    }

    #endregion

    #region Magnet Logic

    /// <summary>
    /// Mueve las monedas cercanas hacia la bola en cada FixedUpdate mientras el imán está activo.
    ///
    /// Usa <c>OverlapSphereNonAlloc</c> con buffer pre-allocado para evitar GC allocation
    /// en el hot path de FixedUpdate (50 Hz). La recolección real ocurre cuando la moneda
    /// entra en el collider trigger normal de la bola.
    ///
    /// Las monedas no tienen Rigidbody, por lo que se mueve <c>Transform.position</c>
    /// directamente sin conflicto con el physics engine.
    /// </summary>
    private void AttractCoins()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            activeMagnetRadius,
            _magnetBuffer,
            coinLayer,
            QueryTriggerInteraction.Collide);

        float stepDistance = activeMagnetAttractionSpeed * Time.fixedDeltaTime;

        for (int i = 0; i < count; i++)
        {
            if (_magnetBuffer[i] == null) continue;

            Transform coinTransform = _magnetBuffer[i].transform;
            Vector3   toCenter      = transform.position - coinTransform.position;
            float     distance      = toCenter.magnitude;

            if (distance < 0.05f) continue;

            coinTransform.position += toCenter.normalized * Mathf.Min(stepDistance, distance);
        }
    }

    #endregion
}