
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Construye un <see cref="TrackRuntimeMap"/> a partir de un perfil y sus settings.
///
/// Esta clase no es un MonoBehaviour y no tiene efectos secundarios sobre la escena.
/// Toda la lógica es pura de datos: recibe configuración, produce un mapa.
///
/// Se extrae de <see cref="TrackGeneratorController"/> para separar la responsabilidad
/// de "construir el mapa lógico" de "instanciar GameObjects visuales".
/// </summary>
internal sealed class TrackMapBuilder
{
    #region Fields

    private readonly TrackGenerationProfile profile;
    private readonly TrackRuleEvaluator ruleEvaluator;

    #endregion

    #region Constructor

    /// <summary>
    /// Crea una instancia del builder para el perfil dado.
    /// </summary>
    /// <param name="profile">Perfil base de generación. No puede ser null.</param>
    public TrackMapBuilder(TrackGenerationProfile profile)
    {
        this.profile     = profile;
        this.ruleEvaluator = new TrackRuleEvaluator();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Construye el mapa runtime completo sin crear GameObjects.
    /// </summary>
    /// <param name="settings">Configuración del nivel activo.</param>
    /// <param name="startPosition">Posición de inicio del track en world space.</param>
    /// <param name="startForward">Dirección de avance inicial (se proyecta al plano horizontal).</param>
    /// <returns>Mapa runtime listo para construir visuals o null si faltan referencias.</returns>
    public TrackRuntimeMap Build(
        LevelGenerationSettings settings,
        Vector3 startPosition,
        Vector3 startForward)
    {
        if (settings == null || profile == null)
        {
            return null;
        }

        int resolvedSeed = ResolveSeed(settings);
        System.Random random = new System.Random(resolvedSeed);

        float targetLength    = profile.TargetTrackLength * settings.LengthMultiplier;
        float minTrackHeight  = settings.OverrideMinHeight  ? settings.MinHeightOverride  : profile.MinTrackHeight;
        float maxTrackHeight  = settings.OverrideMaxHeight  ? settings.MaxHeightOverride  : profile.MaxTrackHeight;
        float safeEndLength   = ResolveSafeEndLength(settings);

        List<TrackSectionDefinition> sections = new List<TrackSectionDefinition>();
        List<TrackFeatureRecord>     features = new List<TrackFeatureRecord>();

        TrackGenerationState state = CreateInitialState(startPosition, startForward);

        GenerateSafeStart(ref state, sections, features, settings);

        while (state.GeneratedLength < targetLength - safeEndLength)
        {
            TrackGenerationDecision decision = ruleEvaluator.EvaluateNextDecision(
                ref state,
                profile,
                settings,
                random,
                targetLength,
                minTrackHeight,
                maxTrackHeight);

            ApplyDecision(ref state, decision, sections, features);

            if (decision.RecoveryLength > 0f)
            {
                AddStraightRecovery(ref state, decision.RecoveryLength, sections, features);
            }
        }

        GenerateSafeEnd(ref state, targetLength, sections, features);

        List<TrackSplinePoint> splinePoints =
            TrackSplineBuilder.BuildSplinePoints(sections, profile);

        List<TrackSurfaceChunkDefinition> surfaceChunks =
            TrackLayoutBuilder.BuildSurfaceChunks(splinePoints);

        TrackPathSampler pathSampler = new TrackPathSampler();
        pathSampler.Rebuild(surfaceChunks);

        return new TrackRuntimeMap(resolvedSeed, sections, features, surfaceChunks, pathSampler);
    }

    /// <summary>
    /// Proyecta un vector sobre el plano horizontal y lo normaliza.
    /// Si el resultado es cero, devuelve Vector3.forward como fallback seguro.
    /// </summary>
    public static Vector3 ResolveHorizontalForward(Vector3 forward)
    {
        Vector3 projected = forward;
        projected.y = 0f;

        return projected.sqrMagnitude > 0.0001f
            ? projected.normalized
            : Vector3.forward;
    }

    #endregion

    #region Seed & Safe Zones

    private static int ResolveSeed(LevelGenerationSettings settings)
    {
        return settings.UseFixedSeed ? settings.FixedSeed : System.Environment.TickCount;
    }

    private float ResolveSafeEndLength(LevelGenerationSettings settings)
    {
        return settings.SafeEndLengthOverride > 0f
            ? settings.SafeEndLengthOverride
            : profile.SafeEndLength;
    }

    #endregion

    #region State Initialization

    private static TrackGenerationState CreateInitialState(Vector3 startPosition, Vector3 startForward)
    {
        return new TrackGenerationState
        {
            CurrentPosition              = startPosition,
            CurrentForward               = ResolveHorizontalForward(startForward),
            CurrentHeight                = startPosition.y,
            GeneratedLength              = 0f,
            CurrentLateralState          = TrackLateralState.Center,
            CurrentVerticalState         = TrackVerticalState.Flat,
            CurrentStructureType         = TrackStructureType.SolidTrack,
            CurrentWidthRatio            = 1f,
            CurrentYawOffsetDegrees      = 0f,
            DistanceSinceLastLateralChange = 999f,
            DistanceSinceLastVerticalChange = 999f,
            DistanceSinceLastWidthChange   = 999f,
            DistanceSinceLastGap           = 999f,
            DistanceSinceLastRail          = 999f,
            IsInsideSafeStartZone          = true,
            IsInsideSafeEndZone            = false,
            IsInsideRailSequence           = false,
            CurrentRailSectionCount        = 0,
        };
    }

    #endregion

    #region Safe Zone Generation

    private void GenerateSafeStart(
        ref TrackGenerationState state,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features,
        LevelGenerationSettings settings)
    {
        float safeStartLength = settings.SafeStartLengthOverride > 0f
            ? settings.SafeStartLengthOverride
            : profile.SafeStartLength;

        if (safeStartLength <= 0f)
        {
            state.IsInsideSafeStartZone = false;
            return;
        }

        AddStraightSection(
            ref state,
            safeStartLength,
            TrackFeatureType.Straight,
            TrackStructureType.SolidTrack,
            sections,
            features);

        state.IsInsideSafeStartZone = false;
    }

    private void GenerateSafeEnd(
        ref TrackGenerationState state,
        float targetLength,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        float remaining = Mathf.Max(0f, targetLength - state.GeneratedLength);

        if (remaining <= 0f)
        {
            return;
        }

        state.CurrentStructureType      = TrackStructureType.SolidTrack;
        state.IsInsideRailSequence      = false;
        state.CurrentRailSectionCount   = 0;
        state.CurrentWidthRatio         = 1f;

        AddStraightSection(
            ref state,
            remaining,
            TrackFeatureType.Finish,
            TrackStructureType.SolidTrack,
            sections,
            features);
    }

    #endregion

    #region Decision Dispatch

    private void ApplyDecision(
        ref TrackGenerationState state,
        TrackGenerationDecision decision,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        switch (decision.FeatureType)
        {
            case TrackFeatureType.Straight:
                AddStraightSection(ref state, decision.ChangeLength, TrackFeatureType.Straight,
                    state.CurrentStructureType, sections, features);
                break;

            case TrackFeatureType.LateralEnterLeft45:
            case TrackFeatureType.LateralEnterLeft90:
            case TrackFeatureType.LateralEnterRight45:
            case TrackFeatureType.LateralEnterRight90:
            case TrackFeatureType.LateralReturnToCenterFromLeft45:
            case TrackFeatureType.LateralReturnToCenterFromLeft90:
            case TrackFeatureType.LateralReturnToCenterFromRight45:
            case TrackFeatureType.LateralReturnToCenterFromRight90:
                AddLateralSection(ref state, decision.ChangeLength, decision.FeatureType,
                    state.CurrentStructureType, sections, features);
                break;

            case TrackFeatureType.SlopeUp:
            case TrackFeatureType.SlopeDown:
                AddVerticalSection(ref state, decision.ChangeLength, decision.FeatureType,
                    decision.VerticalDelta, sections, features);
                break;

            case TrackFeatureType.NarrowStart:
                AddNarrowStartSection(ref state, decision.ChangeLength,
                    decision.TargetWidthRatio, sections, features);
                break;

            case TrackFeatureType.NarrowEnd:
                AddNarrowEndSection(ref state, decision.ChangeLength, sections, features);
                break;

            case TrackFeatureType.Gap:
                AddPreGapRampSection(ref state, decision.PreGapRampLength,
                    decision.PreGapRampHeight, sections, features);
                AddGapSection(ref state, decision.ChangeLength, sections, features);
                break;

            case TrackFeatureType.RailStart:
                AddRailStartSection(ref state, decision.ChangeLength,
                    decision.RailSeparation, decision.RailWidth, sections, features);
                break;

            case TrackFeatureType.RailSegment:
                AddRailSegmentSection(ref state, decision.ChangeLength,
                    decision.RailSeparation, decision.RailWidth, sections, features);
                break;

            case TrackFeatureType.RailEnd:
                AddRailEndSection(ref state, decision.ChangeLength,
                    decision.RailSeparation, decision.RailWidth, sections, features);
                break;

            case TrackFeatureType.Finish:
                AddStraightSection(ref state, decision.ChangeLength, TrackFeatureType.Finish,
                    TrackStructureType.SolidTrack, sections, features);
                break;
        }
    }

    private void AddStraightRecovery(
        ref TrackGenerationState state,
        float recoveryLength,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        AddStraightSection(ref state, recoveryLength, TrackFeatureType.Straight,
            state.CurrentStructureType, sections, features);
    }

    #endregion

    #region Section Builders

    private void AddStraightSection(
        ref TrackGenerationState state,
        float length,
        TrackFeatureType featureType,
        TrackStructureType structureType,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        length = Mathf.Max(0f, length);

        if (length <= 0.0001f)
        {
            return;
        }

        Vector3 startPosition = state.CurrentPosition;
        Vector3 endPosition   = startPosition + state.CurrentForward * length;
        endPosition.y         = state.CurrentHeight;

        float startWidth = ResolveWidthFromRatio(state.CurrentWidthRatio);

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType          = featureType,
            StructureType        = structureType,
            StartDistance        = state.GeneratedLength,
            EndDistance          = state.GeneratedLength + length,
            Length               = length,
            StartPosition        = startPosition,
            EndPosition          = endPosition,
            StartForward         = state.CurrentForward,
            EndForward           = state.CurrentForward,
            StartHeight          = state.CurrentHeight,
            EndHeight            = state.CurrentHeight,
            StartWidth           = startWidth,
            EndWidth             = startWidth,
            TargetWidthRatio     = state.CurrentWidthRatio,
            LateralStateBefore   = state.CurrentLateralState,
            LateralStateAfter    = state.CurrentLateralState,
            VerticalStateBefore  = state.CurrentVerticalState,
            VerticalStateAfter   = TrackVerticalState.Flat,
            HasSurface           = structureType != TrackStructureType.Gap,
            TurnAngleDegrees     = 0f,
            TurnRadius           = 0f,
            SlopeHeightDelta     = 0f,
            RampHeightDelta      = 0f,
            RailSeparation       = structureType == TrackStructureType.RailTrack ? profile.RailSeparation : 0f,
            RailWidth            = structureType == TrackStructureType.RailTrack ? profile.RailWidth : 0f,
            StartsFromCutCenter  = false,
            EndsAtCutCenter      = false,
        };

        sections.Add(section);
        AddFeatureRecord(section, features);
        AdvanceStateStraight(ref state, length, endPosition, structureType);
    }

    private void AddLateralSection(
        ref TrackGenerationState state,
        float length,
        TrackFeatureType featureType,
        TrackStructureType structureType,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        length = Mathf.Max(0f, length);

        if (length <= 0.0001f)
        {
            return;
        }

        float signedAngle      = TrackFeatureUtility.GetSignedTurnAngle(featureType);
        float endYawOffset     = GetEndYawOffsetForFeature(featureType, state.CurrentYawOffsetDegrees);
        TrackLateralState endLateralState = GetEndLateralStateForYaw(endYawOffset);

        float radius        = profile.CurveRadius;
        Vector3 startForward = ResolveHorizontalForward(state.CurrentForward);
        Vector3 startRight  = Vector3.Cross(Vector3.up, startForward).normalized;
        float turnSign      = Mathf.Sign(signedAngle);

        Vector3 center      = state.CurrentPosition + startRight * radius * turnSign;
        Vector3 radialStart = state.CurrentPosition - center;
        Vector3 radialEnd   = Quaternion.AngleAxis(signedAngle, Vector3.up) * radialStart;

        Vector3 endPosition = center + radialEnd;
        endPosition.y       = state.CurrentHeight;

        Vector3 endForward = ResolveHorizontalForward(
            Quaternion.AngleAxis(signedAngle, Vector3.up) * startForward);

        float width = ResolveWidthFromRatio(state.CurrentWidthRatio);

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType          = featureType,
            StructureType        = structureType,
            StartDistance        = state.GeneratedLength,
            EndDistance          = state.GeneratedLength + length,
            Length               = length,
            StartPosition        = state.CurrentPosition,
            EndPosition          = endPosition,
            StartForward         = startForward,
            EndForward           = endForward,
            StartHeight          = state.CurrentHeight,
            EndHeight            = state.CurrentHeight,
            StartWidth           = width,
            EndWidth             = width,
            TargetWidthRatio     = state.CurrentWidthRatio,
            LateralStateBefore   = state.CurrentLateralState,
            LateralStateAfter    = endLateralState,
            VerticalStateBefore  = state.CurrentVerticalState,
            VerticalStateAfter   = TrackVerticalState.Flat,
            HasSurface           = structureType != TrackStructureType.Gap,
            TurnAngleDegrees     = signedAngle,
            TurnRadius           = radius,
            SlopeHeightDelta     = 0f,
            RampHeightDelta      = 0f,
            RailSeparation       = structureType == TrackStructureType.RailTrack ? profile.RailSeparation : 0f,
            RailWidth            = structureType == TrackStructureType.RailTrack ? profile.RailWidth : 0f,
            StartsFromCutCenter  = false,
            EndsAtCutCenter      = false,
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentLateralState       = endLateralState;
        state.CurrentYawOffsetDegrees   = endYawOffset;
        state.CurrentPosition           = endPosition;
        state.CurrentForward            = endForward;
        state.GeneratedLength           += length;
        state.DistanceSinceLastLateralChange  = 0f;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange    += length;
        state.DistanceSinceLastGap            += length;
        state.DistanceSinceLastRail           += length;
        state.CurrentVerticalState   = TrackVerticalState.Flat;
        state.CurrentStructureType   = structureType;

        if (structureType == TrackStructureType.RailTrack)
        {
            state.CurrentRailSectionCount++;
            state.IsInsideRailSequence = true;
        }
    }

    private void AddVerticalSection(
        ref TrackGenerationState state,
        float length,
        TrackFeatureType featureType,
        float verticalDelta,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        length = Mathf.Max(0f, length);

        if (length <= 0.0001f)
        {
            return;
        }

        TrackStructureType structureType = state.CurrentStructureType;

        float endHeight     = state.CurrentHeight + verticalDelta;
        Vector3 endPosition = state.CurrentPosition + state.CurrentForward * length;
        endPosition.y       = endHeight;

        float width = ResolveWidthFromRatio(state.CurrentWidthRatio);

        TrackVerticalState endVerticalState = verticalDelta >= 0f
            ? TrackVerticalState.Ascending
            : TrackVerticalState.Descending;

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType          = featureType,
            StructureType        = structureType,
            StartDistance        = state.GeneratedLength,
            EndDistance          = state.GeneratedLength + length,
            Length               = length,
            StartPosition        = state.CurrentPosition,
            EndPosition          = endPosition,
            StartForward         = state.CurrentForward,
            EndForward           = state.CurrentForward,
            StartHeight          = state.CurrentHeight,
            EndHeight            = endHeight,
            StartWidth           = width,
            EndWidth             = width,
            TargetWidthRatio     = state.CurrentWidthRatio,
            LateralStateBefore   = state.CurrentLateralState,
            LateralStateAfter    = state.CurrentLateralState,
            VerticalStateBefore  = state.CurrentVerticalState,
            VerticalStateAfter   = endVerticalState,
            HasSurface           = true,
            TurnAngleDegrees     = 0f,
            TurnRadius           = 0f,
            SlopeHeightDelta     = verticalDelta,
            RampHeightDelta      = 0f,
            RailSeparation       = structureType == TrackStructureType.RailTrack ? profile.RailSeparation : 0f,
            RailWidth            = structureType == TrackStructureType.RailTrack ? profile.RailWidth : 0f,
            StartsFromCutCenter  = false,
            EndsAtCutCenter      = false,
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentHeight              = endHeight;
        state.CurrentPosition            = endPosition;
        state.GeneratedLength            += length;
        state.DistanceSinceLastLateralChange  += length;
        state.DistanceSinceLastVerticalChange  = 0f;
        state.DistanceSinceLastWidthChange    += length;
        state.DistanceSinceLastGap            += length;
        state.DistanceSinceLastRail           += length;
        state.CurrentVerticalState = endVerticalState;
        state.CurrentStructureType = structureType;
    }

    private void AddNarrowStartSection(
        ref TrackGenerationState state,
        float length,
        float targetWidthRatio,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        length = Mathf.Max(length, profile.NarrowTransitionLength);

        Vector3 endPosition = state.CurrentPosition + state.CurrentForward * length;
        endPosition.y       = state.CurrentHeight;

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType          = TrackFeatureType.NarrowStart,
            StructureType        = TrackStructureType.SolidTrack,
            StartDistance        = state.GeneratedLength,
            EndDistance          = state.GeneratedLength + length,
            Length               = length,
            StartPosition        = state.CurrentPosition,
            EndPosition          = endPosition,
            StartForward         = state.CurrentForward,
            EndForward           = state.CurrentForward,
            StartHeight          = state.CurrentHeight,
            EndHeight            = state.CurrentHeight,
            StartWidth           = ResolveWidthFromRatio(state.CurrentWidthRatio),
            EndWidth             = ResolveWidthFromRatio(targetWidthRatio),
            TargetWidthRatio     = targetWidthRatio,
            LateralStateBefore   = state.CurrentLateralState,
            LateralStateAfter    = state.CurrentLateralState,
            VerticalStateBefore  = state.CurrentVerticalState,
            VerticalStateAfter   = TrackVerticalState.Flat,
            HasSurface           = true,
            TurnAngleDegrees     = 0f,
            TurnRadius           = 0f,
            SlopeHeightDelta     = 0f,
            RampHeightDelta      = 0f,
            RailSeparation       = 0f,
            RailWidth            = 0f,
            StartsFromCutCenter  = false,
            EndsAtCutCenter      = false,
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentWidthRatio              = targetWidthRatio;
        state.CurrentPosition                = endPosition;
        state.GeneratedLength                += length;
        state.DistanceSinceLastLateralChange += length;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange   = 0f;
        state.DistanceSinceLastGap           += length;
        state.DistanceSinceLastRail          += length;
        state.CurrentVerticalState  = TrackVerticalState.Flat;
        state.CurrentStructureType  = TrackStructureType.SolidTrack;
    }

    private void AddNarrowEndSection(
        ref TrackGenerationState state,
        float length,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        length = Mathf.Max(length, profile.NarrowTransitionLength);

        Vector3 endPosition = state.CurrentPosition + state.CurrentForward * length;
        endPosition.y       = state.CurrentHeight;

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType          = TrackFeatureType.NarrowEnd,
            StructureType        = TrackStructureType.SolidTrack,
            StartDistance        = state.GeneratedLength,
            EndDistance          = state.GeneratedLength + length,
            Length               = length,
            StartPosition        = state.CurrentPosition,
            EndPosition          = endPosition,
            StartForward         = state.CurrentForward,
            EndForward           = state.CurrentForward,
            StartHeight          = state.CurrentHeight,
            EndHeight            = state.CurrentHeight,
            StartWidth           = ResolveWidthFromRatio(state.CurrentWidthRatio),
            EndWidth             = ResolveWidthFromRatio(1f),
            TargetWidthRatio     = 1f,
            LateralStateBefore   = state.CurrentLateralState,
            LateralStateAfter    = state.CurrentLateralState,
            VerticalStateBefore  = state.CurrentVerticalState,
            VerticalStateAfter   = TrackVerticalState.Flat,
            HasSurface           = true,
            TurnAngleDegrees     = 0f,
            TurnRadius           = 0f,
            SlopeHeightDelta     = 0f,
            RampHeightDelta      = 0f,
            RailSeparation       = 0f,
            RailWidth            = 0f,
            StartsFromCutCenter  = false,
            EndsAtCutCenter      = false,
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentWidthRatio              = 1f;
        state.CurrentPosition                = endPosition;
        state.GeneratedLength                += length;
        state.DistanceSinceLastLateralChange += length;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange   = 0f;
        state.DistanceSinceLastGap           += length;
        state.DistanceSinceLastRail          += length;
        state.CurrentVerticalState  = TrackVerticalState.Flat;
        state.CurrentStructureType  = TrackStructureType.SolidTrack;
    }

    private void AddPreGapRampSection(
        ref TrackGenerationState state,
        float rampLength,
        float rampHeight,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        if (rampLength <= 0f || Mathf.Abs(rampHeight) <= 0.001f)
        {
            return;
        }

        AddVerticalSection(ref state, rampLength, TrackFeatureType.SlopeUp, rampHeight, sections, features);
    }

    private void AddGapSection(
        ref TrackGenerationState state,
        float length,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        length = Mathf.Max(0f, length);

        if (length <= 0.0001f)
        {
            return;
        }

        float width         = ResolveWidthFromRatio(state.CurrentWidthRatio);
        Vector3 endPosition = state.CurrentPosition + state.CurrentForward * length;
        endPosition.y       = state.CurrentHeight;

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType          = TrackFeatureType.Gap,
            StructureType        = TrackStructureType.Gap,
            StartDistance        = state.GeneratedLength,
            EndDistance          = state.GeneratedLength + length,
            Length               = length,
            StartPosition        = state.CurrentPosition,
            EndPosition          = endPosition,
            StartForward         = state.CurrentForward,
            EndForward           = state.CurrentForward,
            StartHeight          = state.CurrentHeight,
            EndHeight            = state.CurrentHeight,
            StartWidth           = width,
            EndWidth             = width,
            TargetWidthRatio     = state.CurrentWidthRatio,
            LateralStateBefore   = state.CurrentLateralState,
            LateralStateAfter    = state.CurrentLateralState,
            VerticalStateBefore  = TrackVerticalState.Flat,
            VerticalStateAfter   = TrackVerticalState.Flat,
            HasSurface           = false,
            TurnAngleDegrees     = 0f,
            TurnRadius           = 0f,
            SlopeHeightDelta     = 0f,
            RampHeightDelta      = 0f,
            RailSeparation       = 0f,
            RailWidth            = 0f,
            StartsFromCutCenter  = true,
            EndsAtCutCenter      = true,
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentPosition                = endPosition;
        state.GeneratedLength                += length;
        state.DistanceSinceLastLateralChange += length;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange   += length;
        state.DistanceSinceLastGap           = 0f;
        state.DistanceSinceLastRail          += length;
        state.CurrentVerticalState  = TrackVerticalState.Flat;
        state.CurrentStructureType  = TrackStructureType.SolidTrack;
    }

    private void AddRailStartSection(
        ref TrackGenerationState state,
        float length,
        float railSeparation,
        float railWidth,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        length = Mathf.Max(0f, length);

        if (length <= 0.0001f)
        {
            return;
        }

        Vector3 endPosition = state.CurrentPosition + state.CurrentForward * length;
        endPosition.y       = state.CurrentHeight;

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType          = TrackFeatureType.RailStart,
            StructureType        = TrackStructureType.RailTrack,
            StartDistance        = state.GeneratedLength,
            EndDistance          = state.GeneratedLength + length,
            Length               = length,
            StartPosition        = state.CurrentPosition,
            EndPosition          = endPosition,
            StartForward         = state.CurrentForward,
            EndForward           = state.CurrentForward,
            StartHeight          = state.CurrentHeight,
            EndHeight            = state.CurrentHeight,
            StartWidth           = profile.NormalTrackWidth,
            EndWidth             = profile.NormalTrackWidth,
            TargetWidthRatio     = state.CurrentWidthRatio,
            LateralStateBefore   = state.CurrentLateralState,
            LateralStateAfter    = state.CurrentLateralState,
            VerticalStateBefore  = state.CurrentVerticalState,
            VerticalStateAfter   = TrackVerticalState.Flat,
            HasSurface           = true,
            TurnAngleDegrees     = 0f,
            TurnRadius           = 0f,
            SlopeHeightDelta     = 0f,
            RampHeightDelta      = 0f,
            RailSeparation       = railSeparation,
            RailWidth            = railWidth,
            StartsFromCutCenter  = true,
            EndsAtCutCenter      = false,
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentPosition                = endPosition;
        state.GeneratedLength                += length;
        state.DistanceSinceLastLateralChange += length;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange   += length;
        state.DistanceSinceLastGap           += length;
        state.DistanceSinceLastRail          = 0f;
        state.CurrentVerticalState  = TrackVerticalState.Flat;
        state.CurrentStructureType  = TrackStructureType.RailTrack;
        state.IsInsideRailSequence  = true;
        state.CurrentRailSectionCount = 1;
        state.CurrentWidthRatio     = 1f;
    }

    private void AddRailSegmentSection(
        ref TrackGenerationState state,
        float length,
        float railSeparation,
        float railWidth,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        AddStraightSection(ref state, length, TrackFeatureType.RailSegment,
            TrackStructureType.RailTrack, sections, features);

        /// Mutación post-add: la sección recta genérica no incluye los parámetros de rail
        /// específicos de esta decisión. Se actualizan ahora que el índice es conocido.
        TrackSectionDefinition last = sections[sections.Count - 1];
        last.RailSeparation = railSeparation;
        last.RailWidth      = railWidth;
        sections[sections.Count - 1] = last;

        TrackFeatureRecord lastFeature = features[features.Count - 1];
        lastFeature.StructureType = TrackStructureType.RailTrack;
        features[features.Count - 1] = lastFeature;

        state.CurrentStructureType    = TrackStructureType.RailTrack;
        state.IsInsideRailSequence    = true;
        state.CurrentRailSectionCount++;
        state.DistanceSinceLastRail   = 0f;
    }

    private void AddRailEndSection(
        ref TrackGenerationState state,
        float length,
        float railSeparation,
        float railWidth,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        length = Mathf.Max(0f, length);

        if (length <= 0.0001f)
        {
            return;
        }

        Vector3 endPosition = state.CurrentPosition + state.CurrentForward * length;
        endPosition.y       = state.CurrentHeight;

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType          = TrackFeatureType.RailEnd,
            StructureType        = TrackStructureType.RailTrack,
            StartDistance        = state.GeneratedLength,
            EndDistance          = state.GeneratedLength + length,
            Length               = length,
            StartPosition        = state.CurrentPosition,
            EndPosition          = endPosition,
            StartForward         = state.CurrentForward,
            EndForward           = state.CurrentForward,
            StartHeight          = state.CurrentHeight,
            EndHeight            = state.CurrentHeight,
            StartWidth           = profile.NormalTrackWidth,
            EndWidth             = profile.NormalTrackWidth,
            TargetWidthRatio     = 1f,
            LateralStateBefore   = state.CurrentLateralState,
            LateralStateAfter    = state.CurrentLateralState,
            VerticalStateBefore  = state.CurrentVerticalState,
            VerticalStateAfter   = TrackVerticalState.Flat,
            HasSurface           = true,
            TurnAngleDegrees     = 0f,
            TurnRadius           = 0f,
            SlopeHeightDelta     = 0f,
            RampHeightDelta      = 0f,
            RailSeparation       = railSeparation,
            RailWidth            = railWidth,
            StartsFromCutCenter  = false,
            EndsAtCutCenter      = true,
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentPosition                = endPosition;
        state.GeneratedLength                += length;
        state.DistanceSinceLastLateralChange += length;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange   += length;
        state.DistanceSinceLastGap           += length;
        state.DistanceSinceLastRail          = 0f;
        state.CurrentVerticalState           = TrackVerticalState.Flat;
        state.CurrentStructureType           = TrackStructureType.SolidTrack;
        state.IsInsideRailSequence           = false;
        state.CurrentRailSectionCount        = 0;
        state.CurrentWidthRatio              = 1f;
    }

    #endregion

    #region Helpers

    private void AdvanceStateStraight(
        ref TrackGenerationState state,
        float length,
        Vector3 endPosition,
        TrackStructureType structureType)
    {
        state.CurrentPosition                = endPosition;
        state.GeneratedLength                += length;
        state.DistanceSinceLastLateralChange += length;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange   += length;
        state.DistanceSinceLastGap           += length;
        state.DistanceSinceLastRail          += length;
        state.CurrentVerticalState           = TrackVerticalState.Flat;
        state.CurrentStructureType           = structureType;

        if (structureType == TrackStructureType.RailTrack)
        {
            state.IsInsideRailSequence    = true;
            state.CurrentRailSectionCount++;
            state.DistanceSinceLastRail   = 0f;
        }
    }

    private static void AddFeatureRecord(
        TrackSectionDefinition section,
        List<TrackFeatureRecord> features)
    {
        features.Add(new TrackFeatureRecord
        {
            FeatureType    = section.FeatureType,
            StructureType  = section.StructureType,
            StartDistance  = section.StartDistance,
            EndDistance    = section.EndDistance,
            StartPosition  = section.StartPosition,
            EndPosition    = section.EndPosition,
            CenterPosition = Vector3.Lerp(section.StartPosition, section.EndPosition, 0.5f),
            LateralState   = section.LateralStateAfter,
            VerticalState  = section.VerticalStateAfter,
            WidthRatio     = section.TargetWidthRatio,
            HasSurface     = section.HasSurface,
        });
    }

    private float ResolveWidthFromRatio(float widthRatio)
    {
        return profile.NormalTrackWidth * Mathf.Max(0.01f, widthRatio);
    }

    private static float GetEndYawOffsetForFeature(TrackFeatureType featureType, float currentYawOffset)
    {
        return featureType switch
        {
            TrackFeatureType.LateralEnterLeft45               => -45f,
            TrackFeatureType.LateralEnterLeft90               => -90f,
            TrackFeatureType.LateralEnterRight45              =>  45f,
            TrackFeatureType.LateralEnterRight90              =>  90f,
            TrackFeatureType.LateralReturnToCenterFromLeft45  =>   0f,
            TrackFeatureType.LateralReturnToCenterFromLeft90  =>   0f,
            TrackFeatureType.LateralReturnToCenterFromRight45 =>   0f,
            TrackFeatureType.LateralReturnToCenterFromRight90 =>   0f,
            _                                                 => currentYawOffset,
        };
    }

    private static TrackLateralState GetEndLateralStateForYaw(float yawOffset)
    {
        if (Mathf.Approximately(yawOffset, 0f))
        {
            return TrackLateralState.Center;
        }

        return yawOffset < 0f ? TrackLateralState.Left : TrackLateralState.Right;
    }

    #endregion
}