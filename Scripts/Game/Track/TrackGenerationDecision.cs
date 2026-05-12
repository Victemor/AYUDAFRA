/// <summary>
/// Decisión seleccionada por el evaluador para el siguiente cambio.
/// </summary>
public readonly struct TrackGenerationDecision
{
    /// <summary>
    /// Feature principal seleccionado.
    /// </summary>
    public TrackFeatureType FeatureType { get; }

    /// <summary>
    /// Longitud principal del cambio.
    /// </summary>
    public float ChangeLength { get; }

    /// <summary>
    /// Longitud de recuperación recta posterior.
    /// </summary>
    public float RecoveryLength { get; }

    /// <summary>
    /// Relación de ancho objetivo para cambios de estrechamiento.
    /// </summary>
    public float TargetWidthRatio { get; }

    /// <summary>
    /// Delta de altura aplicado a pendientes normales.
    /// </summary>
    public float VerticalDelta { get; }

    /// <summary>
    /// Longitud de la mini rampa previa al gap.
    /// </summary>
    public float PreGapRampLength { get; }

    /// <summary>
    /// Altura de la mini rampa previa al gap.
    /// </summary>
    public float PreGapRampHeight { get; }

    /// <summary>
    /// Separación entre rieles para estructuras rail.
    /// </summary>
    public float RailSeparation { get; }

    /// <summary>
    /// Ancho individual de cada riel.
    /// </summary>
    public float RailWidth { get; }

    /// <summary>
    /// Crea una decisión de generación.
    /// </summary>
    public TrackGenerationDecision(
        TrackFeatureType featureType,
        float changeLength,
        float recoveryLength,
        float targetWidthRatio,
        float verticalDelta,
        float preGapRampLength,
        float preGapRampHeight,
        float railSeparation,
        float railWidth)
    {
        FeatureType = featureType;
        ChangeLength = changeLength;
        RecoveryLength = recoveryLength;
        TargetWidthRatio = targetWidthRatio;
        VerticalDelta = verticalDelta;
        PreGapRampLength = preGapRampLength;
        PreGapRampHeight = preGapRampHeight;
        RailSeparation = railSeparation;
        RailWidth = railWidth;
    }
}