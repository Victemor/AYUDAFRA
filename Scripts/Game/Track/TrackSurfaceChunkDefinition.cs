using System.Collections.Generic;

/// <summary>
/// Chunk continuo de superficie del track.
/// 
/// Cada chunk representa una porción sin gaps y sin cambio de estructura física.
/// Puede transformarse en una malla independiente con collider propio.
/// </summary>
public sealed class TrackSurfaceChunkDefinition
{
    /// <summary>
    /// Índice del chunk dentro del nivel generado.
    /// </summary>
    public int ChunkIndex { get; }

    /// <summary>
    /// Distancia inicial global del chunk.
    /// </summary>
    public float StartDistance { get; }

    /// <summary>
    /// Distancia final global del chunk.
    /// </summary>
    public float EndDistance { get; }

    /// <summary>
    /// Tipo de estructura del chunk.
    /// </summary>
    public TrackStructureType StructureType { get; }

    /// <summary>
    /// Samples espaciales del chunk.
    /// </summary>
    public IReadOnlyList<TrackLayoutSamplePoint> Samples => samples;

    private readonly List<TrackLayoutSamplePoint> samples;

    /// <summary>
    /// Crea un nuevo chunk continuo de superficie.
    /// </summary>
    public TrackSurfaceChunkDefinition(
        int chunkIndex,
        float startDistance,
        float endDistance,
        TrackStructureType structureType,
        List<TrackLayoutSamplePoint> samples)
    {
        ChunkIndex = chunkIndex;
        StartDistance = startDistance;
        EndDistance = endDistance;
        StructureType = structureType;
        this.samples = samples;
    }
}