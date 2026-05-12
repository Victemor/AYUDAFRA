using UnityEngine;

/// <summary>
/// Punto continuo intermedio de la trayectoria del track.
/// 
/// Responsabilidades:
/// - Representar la trayectoria ya resuelta de una sección.
/// - Conservar metadatos necesarios para sampleo posterior.
/// - Servir como base para construir chunks de superficie.
/// </summary>
public readonly struct TrackSplinePoint
{
    /// <summary>
    /// Posición mundial del punto.
    /// </summary>
    public Vector3 Position { get; }

    /// <summary>
    /// Tangente principal de avance en este punto.
    /// </summary>
    public Vector3 Forward { get; }

    /// <summary>
    /// Vector lateral horizontal del track en este punto.
    /// </summary>
    public Vector3 Right { get; }

    /// <summary>
    /// Distancia acumulada lógica correspondiente a este punto.
    /// </summary>
    public float Distance { get; }

    /// <summary>
    /// Ancho del track en este punto.
    /// </summary>
    public float Width { get; }

    /// <summary>
    /// Tipo de estructura física de este punto.
    /// </summary>
    public TrackStructureType StructureType { get; }

    /// <summary>
    /// Indica si este punto pertenece a una superficie física.
    /// </summary>
    public bool HasSurface { get; }

    /// <summary>
    /// Separación entre railes en este punto.
    /// </summary>
    public float RailSeparation { get; }

    /// <summary>
    /// Ancho de riel en este punto.
    /// </summary>
    public float RailWidth { get; }

    /// <summary>
    /// Índice de la sección lógica a la que pertenece el punto.
    /// </summary>
    public int SectionIndex { get; }

    /// <summary>
    /// Crea un nuevo punto continuo del track.
    /// </summary>
    public TrackSplinePoint(
        Vector3 position,
        Vector3 forward,
        Vector3 right,
        float distance,
        float width,
        TrackStructureType structureType,
        bool hasSurface,
        float railSeparation,
        float railWidth,
        int sectionIndex)
    {
        Position = position;
        Forward = forward;
        Right = right;
        Distance = distance;
        Width = width;
        StructureType = structureType;
        HasSurface = hasSurface;
        RailSeparation = railSeparation;
        RailWidth = railWidth;
        SectionIndex = sectionIndex;
    }
}