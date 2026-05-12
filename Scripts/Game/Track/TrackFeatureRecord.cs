using System;
using UnityEngine;

/// <summary>
/// Registro liviano de una feature generada para futuros sistemas.
/// </summary>
[Serializable]
public struct TrackFeatureRecord
{
    /// <summary>
    /// Tipo de feature generada.
    /// </summary>
    public TrackFeatureType FeatureType;

    /// <summary>
    /// Tipo de estructura física de la sección.
    /// </summary>
    public TrackStructureType StructureType;

    /// <summary>
    /// Distancia inicial de la feature.
    /// </summary>
    public float StartDistance;

    /// <summary>
    /// Distancia final de la feature.
    /// </summary>
    public float EndDistance;

    /// <summary>
    /// Posición inicial.
    /// </summary>
    public Vector3 StartPosition;

    /// <summary>
    /// Posición final.
    /// </summary>
    public Vector3 EndPosition;

    /// <summary>
    /// Posición central aproximada.
    /// </summary>
    public Vector3 CenterPosition;

    /// <summary>
    /// Estado lateral resultante.
    /// </summary>
    public TrackLateralState LateralState;

    /// <summary>
    /// Estado vertical resultante.
    /// </summary>
    public TrackVerticalState VerticalState;

    /// <summary>
    /// Relación de ancho resultante.
    /// </summary>
    public float WidthRatio;

    /// <summary>
    /// Indica si la sección tiene superficie física.
    /// </summary>
    public bool HasSurface;
}