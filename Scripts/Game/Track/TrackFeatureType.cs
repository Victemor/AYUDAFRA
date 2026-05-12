/// <summary>
/// Tipos de feature que el generador puede producir.
/// </summary>
public enum TrackFeatureType
{
    Straight = 0,

    LateralEnterLeft45 = 1,
    LateralEnterLeft90 = 2,
    LateralEnterRight45 = 3,
    LateralEnterRight90 = 4,

    LateralReturnToCenterFromLeft45 = 5,
    LateralReturnToCenterFromLeft90 = 6,
    LateralReturnToCenterFromRight45 = 7,
    LateralReturnToCenterFromRight90 = 8,

    SlopeUp = 9,
    SlopeDown = 10,

    NarrowStart = 11,
    NarrowEnd = 12,

    PreGapRamp = 13,
    Gap = 14,

    RailStart = 15,
    RailSegment = 16,
    RailEnd = 17,

    Finish = 18
}