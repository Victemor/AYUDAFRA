using UnityEngine;

/// <summary>
/// Proxy de trigger para zonas de muerte generadas por segmentos.
/// 
/// Responsabilidades:
/// - Recibir OnTriggerEnter desde colliders hijos.
/// - Reenviar el evento al VoidZone raíz.
/// - Recuperar automáticamente la referencia al VoidZone padre si no fue asignada.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class VoidZoneTriggerProxy : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]
    [Tooltip("Zona de vacío raíz que procesará la entrada.")]
    [SerializeField] private VoidZone rootVoidZone;

    [Header("Debug")]
    [Tooltip("Activa logs de depuración del proxy.")]
    [SerializeField] private bool enableDebugLogs;

    #endregion

    private void Reset()
    {
        Collider ownCollider = GetComponent<Collider>();
        ownCollider.isTrigger = true;

        TryResolveRootVoidZone();
    }

    private void Awake()
    {
        TryResolveRootVoidZone();
    }

    /// <summary>
    /// Configura el VoidZone raíz que recibirá los eventos.
    /// </summary>
    public void SetRootVoidZone(VoidZone root)
    {
        rootVoidZone = root;

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[VOID ZONE PROXY] RootVoidZone assigned to '{name}': {(rootVoidZone != null ? rootVoidZone.name : "NULL")}",
                this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (rootVoidZone == null)
        {
            TryResolveRootVoidZone();
        }

        if (rootVoidZone == null)
        {
            Debug.LogWarning(
                $"[VOID ZONE PROXY] No rootVoidZone assigned on '{name}'. Trigger cannot be forwarded.",
                this);
            return;
        }

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[VOID ZONE PROXY] Forwarding trigger from '{name}' to root '{rootVoidZone.name}'.",
                this);
        }

        rootVoidZone.ProcessTriggerEnter(other, name);
    }

    /// <summary>
    /// Intenta resolver automáticamente el VoidZone en la jerarquía padre.
    /// </summary>
    private void TryResolveRootVoidZone()
    {
        if (rootVoidZone != null)
        {
            return;
        }

        rootVoidZone = GetComponentInParent<VoidZone>();

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[VOID ZONE PROXY] Auto-resolve on '{name}': {(rootVoidZone != null ? rootVoidZone.name : "NULL")}.",
                this);
        }
    }
}