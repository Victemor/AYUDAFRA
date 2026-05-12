using UnityEngine;

/// <summary>
/// Sample espacial de una superficie de track ya resuelta.
/// 
/// Contiene la información mínima necesaria para:
/// - construir malla,
/// - construir colliders,
/// - samplear trayectoria runtime,
/// - posicionar futuros objetos.
/// </summary>
public readonly struct TrackLayoutSamplePoint
{
    /// <summary>
    /// Posición mundial del sample.
    /// </summary>
    public Vector3 Position { get; }

    /// <summary>
    /// Forward local del track en este sample.
    /// </summary>
    public Vector3 Forward { get; }

    /// <summary>
    /// Right local del track en este sample.
    /// </summary>
    public Vector3 Right { get; }

    /// <summary>
    /// Ancho total del track sólido en este sample.
    /// En rail no representa el ancho de la plataforma, sino el ancho base de referencia.
    /// </summary>
    public float Width { get; }

    /// <summary>
    /// Distancia acumulada global correspondiente al sample.
    /// </summary>
    public float Distance { get; }

    /// <summary>
    /// Tipo de estructura física del sample.
    /// </summary>
    public TrackStructureType StructureType { get; }

    /// <summary>
    /// Separación entre rieles si este sample pertenece a una estructura rail.
    /// </summary>
    public float RailSeparation { get; }

    /// <summary>
    /// Ancho individual de cada riel si este sample pertenece a una estructura rail.
    /// </summary>
    public float RailWidth { get; }

    /// <summary>
    /// Crea un nuevo sample espacial de track.
    /// </summary>
    public TrackLayoutSamplePoint(
        Vector3 position,
        Vector3 forward,
        Vector3 right,
        float width,
        float distance,
        TrackStructureType structureType,
        float railSeparation,
        float railWidth)
    {
        Position = position;
        Forward = forward;
        Right = right;
        Width = width;
        Distance = distance;
        StructureType = structureType;
        RailSeparation = railSeparation;
        RailWidth = railWidth;
    }
}