using UnityEngine;

/// <summary>
/// Utilidades para consultar propiedades semánticas de los features del track.
/// </summary>
public static class TrackFeatureUtility
{
    /// <summary>
    /// Indica si el feature representa un giro lateral.
    /// </summary>
    public static bool IsLateralFeature(TrackFeatureType featureType)
    {
        return featureType == TrackFeatureType.LateralEnterLeft45
               || featureType == TrackFeatureType.LateralEnterLeft90
               || featureType == TrackFeatureType.LateralEnterRight45
               || featureType == TrackFeatureType.LateralEnterRight90
               || featureType == TrackFeatureType.LateralReturnToCenterFromLeft45
               || featureType == TrackFeatureType.LateralReturnToCenterFromLeft90
               || featureType == TrackFeatureType.LateralReturnToCenterFromRight45
               || featureType == TrackFeatureType.LateralReturnToCenterFromRight90;
    }

    /// <summary>
    /// Indica si el feature pertenece al flujo de generación de rieles.
    /// </summary>
    public static bool IsRailFeature(TrackFeatureType featureType)
    {
        return featureType == TrackFeatureType.RailStart
               || featureType == TrackFeatureType.RailSegment
               || featureType == TrackFeatureType.RailEnd;
    }

    /// <summary>
    /// Devuelve el ángulo lateral firmado del feature.
    /// </summary>
    public static float GetSignedTurnAngle(TrackFeatureType featureType)
    {
        return featureType switch
        {
            TrackFeatureType.LateralEnterLeft45 => -45f,
            TrackFeatureType.LateralEnterLeft90 => -90f,
            TrackFeatureType.LateralEnterRight45 => 45f,
            TrackFeatureType.LateralEnterRight90 => 90f,
            TrackFeatureType.LateralReturnToCenterFromLeft45 => 45f,
            TrackFeatureType.LateralReturnToCenterFromLeft90 => 90f,
            TrackFeatureType.LateralReturnToCenterFromRight45 => -45f,
            TrackFeatureType.LateralReturnToCenterFromRight90 => -90f,
            _ => 0f
        };
    }

    /// <summary>
    /// Indica si el feature es un cambio vertical.
    /// </summary>
    public static bool IsVerticalFeature(TrackFeatureType featureType)
    {
        return featureType == TrackFeatureType.SlopeUp
               || featureType == TrackFeatureType.SlopeDown
               || featureType == TrackFeatureType.PreGapRamp;
    }

    /// <summary>
    /// Indica si el feature implica una transición de ancho.
    /// </summary>
    public static bool IsWidthFeature(TrackFeatureType featureType)
    {
        return featureType == TrackFeatureType.NarrowStart
               || featureType == TrackFeatureType.NarrowEnd;
    }
}