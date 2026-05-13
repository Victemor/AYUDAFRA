using UnityEngine;

/// <summary>
/// Configuración inmutable para <see cref="TrackChunkInstantiator"/>.
///
/// Agrupa todos los parámetros que antes vivían como campos serialized en
/// <see cref="TrackGeneratorController"/> y eran consumidos directamente por
/// los métodos privados de construcción visual.
/// </summary>
public readonly struct TrackChunkInstantiatorSettings
{
    /// <summary>Si está activo, se añade un collider físico a cada chunk sólido.</summary>
    public bool GenerateColliders { get; }

    /// <summary>Si está activo, los chunks de rail usan CapsuleColliders por segmento en lugar de MeshCollider.</summary>
    public bool UsePrimitiveRailColliders { get; }

    /// <summary>Multiplicador aplicado al radio de cada CapsuleCollider de rail.</summary>
    public float RailColliderRadiusMultiplier { get; }

    /// <summary>Radio físico mínimo de seguridad para CapsuleColliders de rail.</summary>
    public float MinimumRailColliderRadius { get; }

    /// <summary>PhysicsMaterial opcional aplicado a los colliders primitivos de rail.</summary>
    public PhysicsMaterial RailPhysicMaterial { get; }

    /// <summary>Prefijo de nombre para cada GameObject de chunk generado.</summary>
    public string ChunkObjectNamePrefix { get; }

    /// <summary>
    /// Layer (índice entero) asignada automáticamente a todos los GameObjects de chunk.
    /// Usa -1 para no asignar layer.
    /// </summary>
    public int GeneratedTrackLayer { get; }

    /// <summary>
    /// Crea una nueva instancia de la configuración.
    /// </summary>
    public TrackChunkInstantiatorSettings(
        bool generateColliders,
        bool usePrimitiveRailColliders,
        float railColliderRadiusMultiplier,
        float minimumRailColliderRadius,
        PhysicsMaterial railPhysicMaterial,
        string chunkObjectNamePrefix,
        int generatedTrackLayer)
    {
        GenerateColliders              = generateColliders;
        UsePrimitiveRailColliders      = usePrimitiveRailColliders;
        RailColliderRadiusMultiplier   = Mathf.Max(0.01f, railColliderRadiusMultiplier);
        MinimumRailColliderRadius      = Mathf.Max(0.001f, minimumRailColliderRadius);
        RailPhysicMaterial             = railPhysicMaterial;
        ChunkObjectNamePrefix          = string.IsNullOrWhiteSpace(chunkObjectNamePrefix)
            ? "TrackChunk_"
            : chunkObjectNamePrefix;
        GeneratedTrackLayer            = generatedTrackLayer;
    }
}