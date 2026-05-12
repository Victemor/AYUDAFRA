using UnityEngine;

/// <summary>
/// Escucha eventos globales de recolección y procesa lógica de gameplay.
/// </summary>
public sealed class CollectibleListener : MonoBehaviour
{
    private void OnEnable()
    {
        GameEvents.OnCollectibleCollected += HandleCollected;
    }

    private void OnDisable()
    {
        GameEvents.OnCollectibleCollected -= HandleCollected;
    }

    /// <summary>
    /// Procesa una recolección global de collectible.
    /// </summary>
    private void HandleCollected(CollectibleCollectedData data)
    {
        Debug.Log($"Recolectado: {data.Type} | Valor: {data.Value}");

        // Aquí va:
        // - sumar monedas
        // - UI
        // - sonido
        // - partículas
    }
}