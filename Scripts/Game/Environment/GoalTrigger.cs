using UnityEngine;

/// <summary>
/// Trigger de meta que marca el final del nivel cuando el jugador entra en su volumen.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class GoalTrigger : MonoBehaviour
{
    [Header("Filtro")]
    [Tooltip("Capas válidas para activar la meta.")]
    [SerializeField] private LayerMask playerLayers;

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsInLayerMask(other.gameObject.layer, playerLayers))
        {
            return;
        }

        if (other.TryGetComponent(out BallStateController state))
        {
            state.ReachGoal();
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