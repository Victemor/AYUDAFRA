using UnityEngine;

/// <summary>
/// Representa un objeto recolectable del escenario.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class Collectible : MonoBehaviour
{
    #region Inspector

    [Header("Configuración")]
    [Tooltip("Capas válidas que pueden recolectar este objeto.")]
    [SerializeField] private LayerMask collectorLayers;

    [Tooltip("Tipo de collectible.")]
    [SerializeField] private CollectibleType type = CollectibleType.Coin;

    [Tooltip("Valor asociado a este collectible.")]
    [SerializeField] private int value = 1;

    #endregion

    #region Runtime

    private bool hasBeenCollected;

    #endregion

    private void OnEnable()
    {
        hasBeenCollected = false;
    }

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasBeenCollected)
        {
            return;
        }

        if (!IsInLayerMask(other.gameObject.layer, collectorLayers))
        {
            return;
        }

        hasBeenCollected = true;

        CollectibleCollectedData data = new CollectibleCollectedData(
            type,
            value,
            transform.position,
            other.gameObject);

        GameEvents.RaiseCollectibleCollected(data);
        DespawnOrDestroy();
    }

    private void DespawnOrDestroy()
    {
        if (TryGetComponent(out TrackPoolableObject poolableObject))
        {
            poolableObject.Despawn();
            return;
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// Indica si una capa pertenece a un LayerMask dado.
    /// </summary>
    private static bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}