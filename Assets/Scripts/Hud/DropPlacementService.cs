using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Servicio responsable de decidir posiciones válidas para nuevas gotas.
/// No instancia objetos ni conoce progreso: solo calcula posiciones.
/// </summary>
[Serializable]
public class DropPlacementService
{
    [Header("Placement Bounds")]
    [SerializeField] private List<PlacementBoundsConfig> placementBounds = new();

    [Header("Spacing")]
    [SerializeField] private float minDistanceBetweenDrops = 1.5f;

    /// <summary>
    /// Genera una posición aleatoria válida dentro de los límites
    /// y respetando la distancia mínima entre gotas.
    /// </summary>
    public Vector3 GeneratePosition(
        int futureDropCount,
        IReadOnlyCollection<DropController> existingDrops
    )
    {
        PlacementBoundsConfig bounds = GetPlacementBounds(futureDropCount);

        if (bounds == null)
            return Vector3.zero;

        const int maxAttempts = 30;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float x = UnityEngine.Random.Range(bounds.xBound.x, bounds.xBound.y);
            float z = UnityEngine.Random.Range(bounds.zBound.x, bounds.zBound.y);

            Vector3 candidate = new Vector3(x, 0f, z);

            if (IsPositionValid(candidate, existingDrops))
                return candidate;
        }

        // Fallback (si está muy lleno)
        return new Vector3(
            UnityEngine.Random.Range(bounds.xBound.x, bounds.xBound.y),
            0f,
            UnityEngine.Random.Range(bounds.zBound.x, bounds.zBound.y)
        );
    }

    private bool IsPositionValid(
        Vector3 candidate,
        IReadOnlyCollection<DropController> existingDrops
    )
    {
        foreach (var drop in existingDrops)
        {
            float distance = Vector3.Distance(
                candidate,
                drop.transform.localPosition
            );

            if (distance < minDistanceBetweenDrops)
                return false;
        }

        return true;
    }

    private PlacementBoundsConfig GetPlacementBounds(int dropCount)
    {
        foreach (var config in placementBounds)
        {
            if (dropCount <= config.maxDrops)
                return config;
        }

        return placementBounds.Count > 0
            ? placementBounds[^1]
            : null;
    }
}

/// <summary>
/// Configuración de límites espaciales para la colocación de gotas
/// según la cantidad de fragmentos visibles.
/// </summary>
[Serializable]
public class PlacementBoundsConfig
{
    /// <summary>
    /// Número máximo de gotas permitido para este rango de límites.
    /// </summary>
    [Tooltip("Número máximo de gotas para este límite")]
    public int maxDrops;

    /// <summary>
    /// Rango permitido en el eje X para la colocación de gotas.
    /// </summary>
    [Tooltip("Límite visual en X")]
    public Vector2 xBound;

    /// <summary>
    /// Rango permitido en el eje Z para la colocación de gotas.
    /// </summary>
    [Tooltip("Límite visual en Z")]
    public Vector2 zBound;
}
