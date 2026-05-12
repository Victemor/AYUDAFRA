using UnityEngine;

/// <summary>
/// Estado vivo del generador durante la construcción del nivel.
/// </summary>
public struct TrackGenerationState
{
    /// <summary>
    /// Posición actual del generador.
    /// </summary>
    public Vector3 CurrentPosition;

    /// <summary>
    /// Forward actual del generador.
    /// </summary>
    public Vector3 CurrentForward;

    /// <summary>
    /// Altura actual del generador.
    /// </summary>
    public float CurrentHeight;

    /// <summary>
    /// Longitud acumulada generada hasta el momento.
    /// </summary>
    public float GeneratedLength;

    /// <summary>
    /// Estado lateral relativo actual.
    /// </summary>
    public TrackLateralState CurrentLateralState;

    /// <summary>
    /// Estado vertical actual.
    /// </summary>
    public TrackVerticalState CurrentVerticalState;

    /// <summary>
    /// Estructura física actual.
    /// </summary>
    public TrackStructureType CurrentStructureType;

    /// <summary>
    /// Relación de ancho actual respecto al ancho normal.
    /// </summary>
    public float CurrentWidthRatio;

    /// <summary>
    /// Offset angular lateral acumulado respecto al eje base.
    /// Valores válidos típicos: -90, -45, 0, 45, 90.
    /// </summary>
    public float CurrentYawOffsetDegrees;

    /// <summary>
    /// Distancia transcurrida desde el último cambio lateral.
    /// </summary>
    public float DistanceSinceLastLateralChange;

    /// <summary>
    /// Distancia transcurrida desde el último cambio vertical.
    /// </summary>
    public float DistanceSinceLastVerticalChange;

    /// <summary>
    /// Distancia transcurrida desde el último cambio de ancho.
    /// </summary>
    public float DistanceSinceLastWidthChange;

    /// <summary>
    /// Distancia transcurrida desde el último gap.
    /// </summary>
    public float DistanceSinceLastGap;

    /// <summary>
    /// Distancia transcurrida desde la última secuencia rail.
    /// </summary>
    public float DistanceSinceLastRail;

    /// <summary>
    /// Indica si el generador sigue dentro de la zona segura inicial.
    /// </summary>
    public bool IsInsideSafeStartZone;

    /// <summary>
    /// Indica si el generador ya entró en la zona segura final.
    /// </summary>
    public bool IsInsideSafeEndZone;

    /// <summary>
    /// Indica si el generador está dentro de una secuencia rail.
    /// </summary>
    public bool IsInsideRailSequence;

    /// <summary>
    /// Cantidad de secciones rail consecutivas generadas en la secuencia actual.
    /// </summary>
    public int CurrentRailSectionCount;
}