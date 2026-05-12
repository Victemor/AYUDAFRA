using System;
using UnityEngine;

/// <summary>
/// Define una sección lógica y espacial del track generado.
/// </summary>
[Serializable]
public struct TrackSectionDefinition
{
    public TrackFeatureType FeatureType;
    public TrackStructureType StructureType;

    public float StartDistance;
    public float EndDistance;
    public float Length;

    public Vector3 StartPosition;
    public Vector3 EndPosition;
    public Vector3 StartForward;
    public Vector3 EndForward;

    public float StartHeight;
    public float EndHeight;

    public float StartWidth;
    public float EndWidth;

    public float TargetWidthRatio;

    public TrackLateralState LateralStateBefore;
    public TrackLateralState LateralStateAfter;

    public TrackVerticalState VerticalStateBefore;
    public TrackVerticalState VerticalStateAfter;

    public bool HasSurface;

    public float TurnAngleDegrees;
    public float TurnRadius;

    public float SlopeHeightDelta;
    public float RampHeightDelta;
    public float RailSeparation;
    public float RailWidth;

    public bool StartsFromCutCenter;
    public bool EndsAtCutCenter;
}