using UnityEngine;

/// <summary>
/// Contador simple de collectibles recolectados.
/// </summary>
public sealed class CollectibleCounter : MonoBehaviour
{
    #region Runtime

    private int totalCollected;
    private int totalValue;

    #endregion

    #region Properties

    /// <summary>
    /// Cantidad total de collectibles recogidos.
    /// </summary>
    public int TotalCollected => totalCollected;

    /// <summary>
    /// Valor total acumulado.
    /// </summary>
    public int TotalValue => totalValue;

    #endregion

    private void OnEnable()
    {
        GameEvents.OnCollectibleCollected += HandleCollectibleCollected;
    }

    private void OnDisable()
    {
        GameEvents.OnCollectibleCollected -= HandleCollectibleCollected;
    }

    /// <summary>
    /// Procesa una recolección global.
    /// </summary>
    private void HandleCollectibleCollected(CollectibleCollectedData data)
    {
        totalCollected++;
        totalValue += data.Value;

        Debug.Log($"Recolectado: {data.Type} | Valor: {data.Value} | Total Valor: {totalValue}");
    }
}