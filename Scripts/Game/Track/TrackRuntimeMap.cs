using System.Collections.Generic;

/// <summary>
/// Resultado runtime final de la generación del nivel.
/// </summary>
public sealed class TrackRuntimeMap
{
    /// <summary>
    /// Semilla efectiva usada para construir el nivel.
    /// </summary>
    public int GeneratedSeed { get; }

    /// <summary>
    /// Secciones lógicas/espaciales generadas.
    /// </summary>
    public IReadOnlyList<TrackSectionDefinition> Sections => sections;

    /// <summary>
    /// Registro de features generadas.
    /// </summary>
    public IReadOnlyList<TrackFeatureRecord> Features => features;

    /// <summary>
    /// Chunks de superficie sólida del nivel.
    /// </summary>
    public IReadOnlyList<TrackSurfaceChunkDefinition> SurfaceChunks => surfaceChunks;

    /// <summary>
    /// Sampler runtime del path final.
    /// </summary>
    public TrackPathSampler PathSampler { get; }

    private readonly List<TrackSectionDefinition> sections;
    private readonly List<TrackFeatureRecord> features;
    private readonly List<TrackSurfaceChunkDefinition> surfaceChunks;

    /// <summary>
    /// Crea un nuevo mapa runtime generado.
    /// </summary>
    public TrackRuntimeMap(
        int generatedSeed,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features,
        List<TrackSurfaceChunkDefinition> surfaceChunks,
        TrackPathSampler pathSampler)
    {
        GeneratedSeed = generatedSeed;
        this.sections = sections;
        this.features = features;
        this.surfaceChunks = surfaceChunks;
        PathSampler = pathSampler;
    }
}