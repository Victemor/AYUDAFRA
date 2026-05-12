using UnityEngine;

/// <summary>
/// Datos asociados a la recolección de un collectible.
/// </summary>
public readonly struct CollectibleCollectedData
{
    /// <summary>
    /// Tipo de collectible recolectado.
    /// </summary>
    public CollectibleType Type { get; }

    /// <summary>
    /// Valor numérico asociado al collectible.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Posición del collectible al momento de ser recolectado.
    /// </summary>
    public Vector3 WorldPosition { get; }

    /// <summary>
    /// GameObject del recolector.
    /// </summary>
    public GameObject Collector { get; }

    /// <summary>
    /// Crea una nueva instancia de datos de recolección.
    /// </summary>
    public CollectibleCollectedData(
        CollectibleType type,
        int value,
        Vector3 worldPosition,
        GameObject collector)
    {
        Type = type;
        Value = value;
        WorldPosition = worldPosition;
        Collector = collector;
    }
}