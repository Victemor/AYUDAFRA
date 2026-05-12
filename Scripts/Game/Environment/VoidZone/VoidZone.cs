using UnityEngine;

/// <summary>
/// Zona de vacío que elimina al jugador al entrar en su volumen trigger.
/// 
/// Responsabilidades:
/// - Validar capas que pueden activar muerte.
/// - Resolver el BallStateController del objeto que entró.
/// - Disparar la muerte del jugador.
/// - Exponer logs de depuración controlados.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class VoidZone : MonoBehaviour
{
    #region Inspector

    [Header("Filtro")]
    [Tooltip("Capas válidas para activar la zona de vacío.")]
    [SerializeField] private LayerMask playerLayers;

    [Header("Debug")]
    [Tooltip("Activa logs de depuración de la zona de vacío.")]
    [SerializeField] private bool enableDebugLogs;

    [Tooltip("Dibuja avisos si el collider no está configurado como trigger.")]
    [SerializeField] private bool validateTriggerConfiguration = true;

    #endregion

    #region Properties

    /// <summary>
    /// Capas válidas para activar la zona de vacío.
    /// </summary>
    public LayerMask PlayerLayers => playerLayers;

    /// <summary>
    /// Indica si los logs de depuración están activos.
    /// </summary>
    public bool EnableDebugLogs => enableDebugLogs;

    #endregion

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
    }

    private void Awake()
    {
        ValidateConfiguration();
    }

    /// <summary>
    /// Permite configurar por código las capas válidas.
    /// </summary>
    public void SetPlayerLayers(LayerMask layers)
    {
        playerLayers = layers;
    }

    /// <summary>
    /// Permite activar o desactivar logs por código.
    /// </summary>
    public void SetDebugLogs(bool enabled)
    {
        enableDebugLogs = enabled;
    }

    /// <summary>
    /// Procesa una posible entrada a la zona de muerte.
    /// 
    /// Este método puede ser llamado por el propio collider o por un proxy hijo.
    /// </summary>
    public void ProcessTriggerEnter(Collider other, string sourceName)
    {
        if (other == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[VOID ZONE] Trigger received null collider from '{sourceName}'.", this);
            }

            return;
        }

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[VOID ZONE] Trigger enter detected. " +
                $"Source='{sourceName}', Other='{other.name}', Layer='{LayerMask.LayerToName(other.gameObject.layer)}'.",
                this);
        }

        if (!IsInLayerMask(other.gameObject.layer, playerLayers))
        {
            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[VOID ZONE] Ignored collider '{other.name}' because its layer is not inside PlayerLayers.",
                    this);
            }

            return;
        }

        BallStateController state = other.GetComponentInParent<BallStateController>();

        if (state == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning(
                    $"[VOID ZONE] Collider '{other.name}' is in a valid layer but no BallStateController was found in parents.",
                    this);
            }

            return;
        }

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[VOID ZONE] BallStateController found on '{state.name}'. Calling Die().",
                this);
        }

        state.Die();
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessTriggerEnter(other, gameObject.name);
    }

    /// <summary>
    /// Valida configuración básica del collider local.
    /// </summary>
    private void ValidateConfiguration()
    {
        if (!validateTriggerConfiguration)
        {
            return;
        }

        Collider ownCollider = GetComponent<Collider>();
        if (ownCollider != null && !ownCollider.isTrigger)
        {
            Debug.LogWarning(
                $"[VOID ZONE] Collider on '{name}' is not configured as trigger. It should be trigger for correct behavior.",
                this);
        }
    }

    /// <summary>
    /// Indica si una capa pertenece a un LayerMask dado.
    /// </summary>
    private static bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }
}