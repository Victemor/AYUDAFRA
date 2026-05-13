using UnityEngine;

/// <summary>
/// Zona de vacío que elimina al jugador al entrar en su volumen trigger.
///
/// Responsabilidades:
/// <list type="bullet">
///   <item>Filtrar colisiones por <see cref="LayerMask"/>.</item>
///   <item>Resolver el <see cref="BallStateController"/> del objeto entrante.</item>
///   <item>Disparar <see cref="BallStateController.Die"/> una vez validado.</item>
///   <item>Actuar como receptor central de sus proxies hijos (<see cref="VoidZoneTriggerProxy"/>)
///         a través de <see cref="ProcessTriggerEnter"/>.</item>
/// </list>
///
/// Configuración en runtime: <see cref="VoidZoneGenerator"/> usa <see cref="CopySettingsFrom"/>
/// para propagar la configuración del VoidZone origen hacia el generado proceduralmente.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class VoidZone : MonoBehaviour
{
    #region Inspector

    [Header("Filtro")]
    [SerializeField]
    [Tooltip("Capas cuyos colliders pueden activar la muerte del jugador. " +
             "Debe incluir al menos la capa del jugador; dejar vacío desactiva silenciosamente la zona.")]
    private LayerMask playerLayers;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("Activa logs de depuración paso a paso de cada entrada al trigger.")]
    private bool enableDebugLogs;

    [SerializeField]
    [Tooltip("Emite una advertencia en Awake si el Collider local no está configurado como Trigger.")]
    private bool validateTriggerConfiguration = true;

    #endregion

    #region Properties

    /// <summary>Capas válidas para activar la zona de vacío.</summary>
    public LayerMask PlayerLayers => playerLayers;

    /// <summary>Indica si los logs de depuración están activos.</summary>
    public bool EnableDebugLogs => enableDebugLogs;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        ValidateConfiguration();
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessTriggerEnter(other, gameObject.name);
    }

    #endregion

    #region Public API — Trigger Entry

    /// <summary>
    /// Procesa una posible entrada a la zona de muerte.
    /// Puede ser invocado por el propio Collider o por un <see cref="VoidZoneTriggerProxy"/> hijo.
    /// </summary>
    /// <param name="other">Collider que entró en el trigger.</param>
    /// <param name="sourceName">Nombre del objeto que origina el evento. Usado solo en logs de debug.</param>
    public void ProcessTriggerEnter(Collider other, string sourceName)
    {
        if (other == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[VOID ZONE] Null collider recibido desde '{sourceName}'.", this);
            }

            return;
        }

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[VOID ZONE] Trigger detectado. Source='{sourceName}', " +
                $"Other='{other.name}', Layer='{LayerMask.LayerToName(other.gameObject.layer)}'.",
                this);
        }

        if (!IsInLayerMask(other.gameObject.layer, playerLayers))
        {
            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[VOID ZONE] Ignorado '{other.name}' — capa no incluida en PlayerLayers.",
                    this);
            }

            return;
        }

        BallStateController stateController = other.GetComponentInParent<BallStateController>();

        if (stateController == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning(
                    $"[VOID ZONE] '{other.name}' está en capa válida pero no tiene " +
                    $"BallStateController en su jerarquía padre.",
                    this);
            }

            return;
        }

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[VOID ZONE] BallStateController encontrado en '{stateController.name}'. " +
                $"Estado actual: {stateController.CurrentState}. Llamando Die().",
                this);
        }

        stateController.Die();
    }

    #endregion

    #region Public API — Generation Setup

    /// <summary>
    /// Copia los parámetros de configuración de otro <see cref="VoidZone"/> a este.
    /// Usado exclusivamente por <see cref="VoidZoneGenerator"/> para propagar la configuración
    /// del VoidZone origen al objeto generado proceduralmente.
    /// No llames este método durante el gameplay normal.
    /// </summary>
    /// <param name="source">VoidZone origen desde donde copiar la configuración.</param>
    public void CopySettingsFrom(VoidZone source)
    {
        if (source == null)
        {
            return;
        }

        playerLayers   = source.playerLayers;
        enableDebugLogs = source.enableDebugLogs;
    }

    #endregion

    #region Validation

    private void ValidateConfiguration()
    {
        if (validateTriggerConfiguration)
        {
            Collider ownCollider = GetComponent<Collider>();

            if (ownCollider != null && !ownCollider.isTrigger)
            {
                Debug.LogWarning(
                    $"[VOID ZONE] El Collider en '{name}' no está configurado como Trigger. " +
                    $"La zona de muerte no funcionará correctamente.",
                    this);
            }
        }

        if (playerLayers.value == 0)
        {
            Debug.LogWarning(
                $"[VOID ZONE] PlayerLayers en '{name}' está vacío (ninguna capa seleccionada). " +
                $"La zona de muerte ignorará todos los colliders silenciosamente.",
                this);
        }
    }

    private void OnValidate()
    {
        if (playerLayers.value == 0)
        {
            Debug.LogWarning(
                $"[VOID ZONE] PlayerLayers en '{name}' está vacío. " +
                $"Asigna al menos la capa del jugador para que la zona funcione.",
                this);
        }
    }

    #endregion

    #region Helpers

    private static bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    #endregion
}