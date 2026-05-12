using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Evalúa qué tipo de cambio puede generarse a continuación
/// en función del estado actual del nivel y las reglas configuradas.
/// </summary>
public sealed class TrackRuleEvaluator
{
    #region Nested Types

    /// <summary>
    /// Candidato interno usado para selección ponderada.
    /// </summary>
    private readonly struct Candidate
    {
        public TrackFeatureType FeatureType { get; }
        public float Weight { get; }

        public Candidate(TrackFeatureType featureType, float weight)
        {
            FeatureType = featureType;
            Weight = weight;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Selecciona la siguiente decisión válida del generador.
    /// </summary>
    public TrackGenerationDecision EvaluateNextDecision(
        ref TrackGenerationState state,
        TrackGenerationProfile profile,
        LevelGenerationSettings levelSettings,
        System.Random random,
        float targetLength,
        float minTrackHeight,
        float maxTrackHeight)
    {
        float safeStartLength = ResolveSafeStartLength(profile, levelSettings);

        if (state.GeneratedLength < safeStartLength)
        {
            return CreateStraightDecision(
                Mathf.Max(0f, safeStartLength - state.GeneratedLength),
                0f,
                state.CurrentWidthRatio);
        }

        float safeEndLength = ResolveSafeEndLength(profile, levelSettings);
        float remainingToEnd = Mathf.Max(0f, targetLength - state.GeneratedLength);

        if (state.CurrentStructureType == TrackStructureType.RailTrack
            && remainingToEnd <= safeEndLength + profile.MinimumSectionLength + profile.MinStraightAfterRail)
        {
            return CreateRailEndDecision(profile, random, state.CurrentWidthRatio);
        }

        if (state.GeneratedLength >= targetLength - safeEndLength)
        {
            state.IsInsideSafeEndZone = true;

            return CreateFinishDecision(
                Mathf.Max(0f, targetLength - state.GeneratedLength),
                state.CurrentWidthRatio);
        }

        List<Candidate> candidates = BuildCandidates(
            state,
            profile,
            levelSettings,
            targetLength,
            minTrackHeight,
            maxTrackHeight);

        if (candidates.Count == 0)
        {
            return CreateStraightDecision(
                RandomSolidLength(profile, random),
                0f,
                state.CurrentWidthRatio);
        }

        TrackFeatureType selectedFeature = PickWeighted(candidates, random);

        return BuildDecisionPayload(
            selectedFeature,
            state,
            profile,
            levelSettings,
            random,
            minTrackHeight,
            maxTrackHeight);
    }

    #endregion

    #region Candidate Build

    /// <summary>
    /// Construye la lista de candidatos válidos para el siguiente cambio.
    /// </summary>
    private List<Candidate> BuildCandidates(
        TrackGenerationState state,
        TrackGenerationProfile profile,
        LevelGenerationSettings levelSettings,
        float targetLength,
        float minTrackHeight,
        float maxTrackHeight)
    {
        List<Candidate> candidates = new List<Candidate>();

        if (state.CurrentStructureType == TrackStructureType.RailTrack)
        {
            BuildRailCandidates(
                candidates,
                state,
                profile,
                levelSettings,
                targetLength,
                minTrackHeight,
                maxTrackHeight);

            return candidates;
        }

        BuildSolidCandidates(
            candidates,
            state,
            profile,
            levelSettings,
            targetLength,
            minTrackHeight,
            maxTrackHeight);

        if (candidates.Count == 0)
        {
            candidates.Add(new Candidate(TrackFeatureType.Straight, 1f));
        }

        return candidates;
    }

    /// <summary>
    /// Construye candidatos válidos mientras el generador está sobre pista sólida.
    /// </summary>
    private void BuildSolidCandidates(
        List<Candidate> candidates,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        LevelGenerationSettings levelSettings,
        float targetLength,
        float minTrackHeight,
        float maxTrackHeight)
    {
        candidates.Add(new Candidate(TrackFeatureType.Straight, 1f));

        AddSolidLateralCandidates(candidates, state, profile, levelSettings);
        AddVerticalCandidates(candidates, state, profile, levelSettings, minTrackHeight, maxTrackHeight);
        AddWidthCandidates(candidates, state, profile, levelSettings);
        AddGapCandidate(candidates, state, profile, levelSettings, targetLength);
        AddRailStartCandidate(candidates, state, profile, levelSettings, targetLength);
    }

    /// <summary>
    /// Construye candidatos válidos mientras el generador está dentro de una secuencia rail.
    /// Los rieles pueden seguir recto, girar, bajar o finalizar, pero nunca subir.
    /// </summary>
    private void BuildRailCandidates(
        List<Candidate> candidates,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        LevelGenerationSettings levelSettings,
        float targetLength,
        float minTrackHeight,
        float maxTrackHeight)
    {
        if (state.CurrentRailSectionCount >= profile.RailMaxConsecutiveSections)
        {
            candidates.Add(new Candidate(TrackFeatureType.RailEnd, 1f));
            return;
        }

        candidates.Add(new Candidate(TrackFeatureType.RailSegment, 1f));
        candidates.Add(new Candidate(TrackFeatureType.RailEnd, 0.35f));

        AddRailLateralCandidates(candidates, state, profile, levelSettings);
        AddRailDownSlopeCandidate(candidates, state, profile, levelSettings, minTrackHeight);

        float remainingToEnd = targetLength - state.GeneratedLength;

        if (remainingToEnd <= profile.SafeEndLength + profile.MinimumSectionLength + profile.MinStraightAfterRail)
        {
            candidates.Clear();
            candidates.Add(new Candidate(TrackFeatureType.RailEnd, 1f));
        }
    }

    /// <summary>
    /// Agrega únicamente pendiente descendente dentro de una secuencia rail.
    /// Por decisión de diseño, los rieles nunca pueden generar pendientes ascendentes.
    /// </summary>
    private void AddRailDownSlopeCandidate(
        List<Candidate> candidates,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        LevelGenerationSettings levelSettings,
        float minTrackHeight)
    {
        if (state.DistanceSinceLastVerticalChange < profile.MinStraightAfterVerticalChange)
        {
            return;
        }

        float verticalChance =
            profile.VerticalChangeChance
            * levelSettings.VerticalChanceMultiplier
            * levelSettings.DifficultyMultiplier
            * profile.RailVerticalChanceMultiplier;

        if (verticalChance <= 0f)
        {
            return;
        }

        float nextDownMinimum = state.CurrentHeight - profile.SlopeHeightStepMin;

        if (nextDownMinimum < minTrackHeight)
        {
            return;
        }

        candidates.Add(new Candidate(
            TrackFeatureType.SlopeDown,
            verticalChance * profile.SlopeDownWeight));
    }

    /// <summary>
    /// Agrega candidatos laterales válidos en pista sólida.
    /// </summary>
    private void AddSolidLateralCandidates(
        List<Candidate> candidates,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        LevelGenerationSettings levelSettings)
    {
        if (state.DistanceSinceLastLateralChange < profile.MinStraightAfterLateralChange)
        {
            return;
        }

        float lateralChance =
            profile.LateralChangeChance
            * levelSettings.LateralChanceMultiplier
            * levelSettings.DifficultyMultiplier;

        if (lateralChance <= 0f)
        {
            return;
        }

        AddLateralCandidatesForState(
            candidates,
            state.CurrentLateralState,
            state.CurrentYawOffsetDegrees,
            lateralChance,
            profile.LeftTurnWeight,
            profile.RightTurnWeight,
            profile.Turn45Weight,
            profile.Turn90Weight);
    }

    /// <summary>
    /// Agrega candidatos laterales válidos en estructura rail.
    /// </summary>
    private void AddRailLateralCandidates(
        List<Candidate> candidates,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        LevelGenerationSettings levelSettings)
    {
        if (state.DistanceSinceLastLateralChange < profile.MinStraightAfterLateralChange)
        {
            return;
        }

        float lateralChance =
            profile.LateralChangeChance
            * levelSettings.LateralChanceMultiplier
            * levelSettings.DifficultyMultiplier
            * profile.RailTurnChanceMultiplier;

        if (lateralChance <= 0f)
        {
            return;
        }

        AddLateralCandidatesForState(
            candidates,
            state.CurrentLateralState,
            state.CurrentYawOffsetDegrees,
            lateralChance,
            profile.LeftTurnWeight,
            profile.RightTurnWeight,
            profile.Turn45Weight,
            profile.Turn90Weight);
    }

    /// <summary>
    /// Agrega candidatos de giro según el estado lateral actual.
    /// </summary>
    private void AddLateralCandidatesForState(
        List<Candidate> candidates,
        TrackLateralState currentLateralState,
        float currentYawOffsetDegrees,
        float lateralChance,
        float leftTurnWeight,
        float rightTurnWeight,
        float turn45Weight,
        float turn90Weight)
    {
        float left45Weight = lateralChance * leftTurnWeight * turn45Weight;
        float left90Weight = lateralChance * leftTurnWeight * turn90Weight;
        float right45Weight = lateralChance * rightTurnWeight * turn45Weight;
        float right90Weight = lateralChance * rightTurnWeight * turn90Weight;

        switch (currentLateralState)
        {
            case TrackLateralState.Center:
                candidates.Add(new Candidate(TrackFeatureType.LateralEnterLeft45, left45Weight));
                candidates.Add(new Candidate(TrackFeatureType.LateralEnterLeft90, left90Weight));
                candidates.Add(new Candidate(TrackFeatureType.LateralEnterRight45, right45Weight));
                candidates.Add(new Candidate(TrackFeatureType.LateralEnterRight90, right90Weight));
                break;

            case TrackLateralState.Left:
                if (Mathf.Approximately(currentYawOffsetDegrees, -45f))
                {
                    candidates.Add(new Candidate(TrackFeatureType.LateralReturnToCenterFromLeft45, lateralChance));
                }
                else if (Mathf.Approximately(currentYawOffsetDegrees, -90f))
                {
                    candidates.Add(new Candidate(TrackFeatureType.LateralReturnToCenterFromLeft90, lateralChance));
                }
                break;

            case TrackLateralState.Right:
                if (Mathf.Approximately(currentYawOffsetDegrees, 45f))
                {
                    candidates.Add(new Candidate(TrackFeatureType.LateralReturnToCenterFromRight45, lateralChance));
                }
                else if (Mathf.Approximately(currentYawOffsetDegrees, 90f))
                {
                    candidates.Add(new Candidate(TrackFeatureType.LateralReturnToCenterFromRight90, lateralChance));
                }
                break;
        }
    }

    /// <summary>
    /// Agrega candidatos de pendiente válidos en pista sólida.
    /// </summary>
    private void AddVerticalCandidates(
        List<Candidate> candidates,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        LevelGenerationSettings levelSettings,
        float minTrackHeight,
        float maxTrackHeight)
    {
        if (state.DistanceSinceLastVerticalChange < profile.MinStraightAfterVerticalChange)
        {
            return;
        }

        if (state.CurrentStructureType != TrackStructureType.SolidTrack)
        {
            return;
        }

        float verticalChance =
            profile.VerticalChangeChance
            * levelSettings.VerticalChanceMultiplier
            * levelSettings.DifficultyMultiplier;

        if (verticalChance <= 0f)
        {
            return;
        }

        float nextUpMinimum = state.CurrentHeight + profile.SlopeHeightStepMin;
        float nextDownMinimum = state.CurrentHeight - profile.SlopeHeightStepMin;

        if (nextUpMinimum <= maxTrackHeight)
        {
            candidates.Add(new Candidate(
                TrackFeatureType.SlopeUp,
                verticalChance * profile.SlopeUpWeight));
        }

        if (nextDownMinimum >= minTrackHeight)
        {
            candidates.Add(new Candidate(
                TrackFeatureType.SlopeDown,
                verticalChance * profile.SlopeDownWeight));
        }
    }

    /// <summary>
    /// Agrega candidatos de estrechamiento válidos.
    /// </summary>
    private void AddWidthCandidates(
        List<Candidate> candidates,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        LevelGenerationSettings levelSettings)
    {
        if (state.DistanceSinceLastWidthChange < profile.MinStraightAfterWidthChange)
        {
            return;
        }

        if (state.CurrentStructureType != TrackStructureType.SolidTrack)
        {
            return;
        }

        float widthChance =
            profile.NarrowChance
            * levelSettings.NarrowChanceMultiplier
            * levelSettings.DifficultyMultiplier;

        if (widthChance <= 0f)
        {
            return;
        }

        if (Mathf.Approximately(state.CurrentWidthRatio, 1f))
        {
            candidates.Add(new Candidate(TrackFeatureType.NarrowStart, widthChance));
        }
        else
        {
            candidates.Add(new Candidate(TrackFeatureType.NarrowEnd, widthChance));
        }
    }

    /// <summary>
    /// Agrega candidato de gap si es válido.
    /// </summary>
    private void AddGapCandidate(
        List<Candidate> candidates,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        LevelGenerationSettings levelSettings,
        float targetLength)
    {
        if (state.DistanceSinceLastGap < profile.MinStraightAfterGap)
        {
            return;
        }

        if (state.CurrentStructureType != TrackStructureType.SolidTrack)
        {
            return;
        }

        if (!Mathf.Approximately(state.CurrentWidthRatio, 1f))
        {
            return;
        }

        if (state.CurrentVerticalState != TrackVerticalState.Flat)
        {
            return;
        }

        if (state.GeneratedLength < profile.ForbidGapNearStartDistance)
        {
            return;
        }

        if (state.GeneratedLength > targetLength - profile.ForbidGapNearEndDistance)
        {
            return;
        }

        if (state.DistanceSinceLastLateralChange < profile.ForbidGapAfterRecentHardChangeDistance)
        {
            return;
        }

        float gapChance =
            profile.GapChance
            * levelSettings.GapChanceMultiplier
            * levelSettings.DifficultyMultiplier;

        if (gapChance <= 0f)
        {
            return;
        }

        candidates.Add(new Candidate(TrackFeatureType.Gap, gapChance));
    }

    /// <summary>
    /// Agrega candidato para comenzar una secuencia rail.
    /// </summary>
    private void AddRailStartCandidate(
        List<Candidate> candidates,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        LevelGenerationSettings levelSettings,
        float targetLength)
    {
        if (state.CurrentStructureType != TrackStructureType.SolidTrack)
        {
            return;
        }

        if (!Mathf.Approximately(state.CurrentWidthRatio, 1f))
        {
            return;
        }

        if (state.CurrentVerticalState != TrackVerticalState.Flat)
        {
            return;
        }

        if (state.GeneratedLength < profile.ForbidRailNearStartDistance)
        {
            return;
        }

        if (state.GeneratedLength > targetLength - profile.ForbidRailNearEndDistance)
        {
            return;
        }

        if (state.DistanceSinceLastRail < profile.MinStraightAfterRail)
        {
            return;
        }

        float railChance =
            profile.RailGenerationChance
            * levelSettings.RailChanceMultiplier
            * levelSettings.DifficultyMultiplier;

        if (railChance <= 0f)
        {
            return;
        }

        candidates.Add(new Candidate(TrackFeatureType.RailStart, railChance));
    }

    #endregion

    #region Decision Payload

    /// <summary>
    /// Construye el payload final de una decisión seleccionada.
    /// </summary>
    private TrackGenerationDecision BuildDecisionPayload(
        TrackFeatureType selectedFeature,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        LevelGenerationSettings levelSettings,
        System.Random random,
        float minTrackHeight,
        float maxTrackHeight)
    {
        if (selectedFeature == TrackFeatureType.Straight)
        {
            return CreateStraightDecision(
                RandomSolidLength(profile, random),
                0f,
                state.CurrentWidthRatio);
        }

        if (TrackFeatureUtility.IsLateralFeature(selectedFeature))
        {
            return CreateLateralDecision(selectedFeature, profile, state.CurrentWidthRatio);
        }

        if (selectedFeature == TrackFeatureType.SlopeUp || selectedFeature == TrackFeatureType.SlopeDown)
        {
            if (state.CurrentStructureType == TrackStructureType.RailTrack
                && selectedFeature == TrackFeatureType.SlopeUp)
            {
                return CreateRailSegmentDecision(profile, random, state.CurrentWidthRatio);
            }

            return CreateVerticalDecision(
                selectedFeature,
                state,
                profile,
                levelSettings,
                random,
                minTrackHeight,
                maxTrackHeight);
        }

        if (selectedFeature == TrackFeatureType.NarrowStart)
        {
            return CreateNarrowStartDecision(profile, random);
        }

        if (selectedFeature == TrackFeatureType.NarrowEnd)
        {
            return CreateNarrowEndDecision(profile);
        }

        if (selectedFeature == TrackFeatureType.Gap)
        {
            return CreateGapDecision(profile, random, state.CurrentWidthRatio);
        }

        if (selectedFeature == TrackFeatureType.RailStart)
        {
            return CreateRailStartDecision(profile, random, state.CurrentWidthRatio);
        }

        if (selectedFeature == TrackFeatureType.RailSegment)
        {
            return CreateRailSegmentDecision(profile, random, state.CurrentWidthRatio);
        }

        if (selectedFeature == TrackFeatureType.RailEnd)
        {
            return CreateRailEndDecision(profile, random, state.CurrentWidthRatio);
        }

        if (selectedFeature == TrackFeatureType.Finish)
        {
            return CreateFinishDecision(RandomSolidLength(profile, random), state.CurrentWidthRatio);
        }

        return CreateStraightDecision(
            RandomSolidLength(profile, random),
            0f,
            state.CurrentWidthRatio);
    }

    /// <summary>
    /// Crea una decisión de recta normal.
    /// </summary>
    private TrackGenerationDecision CreateStraightDecision(
        float changeLength,
        float recoveryLength,
        float currentWidthRatio)
    {
        return new TrackGenerationDecision(
            TrackFeatureType.Straight,
            Mathf.Max(0f, changeLength),
            Mathf.Max(0f, recoveryLength),
            currentWidthRatio,
            0f,
            0f,
            0f,
            0f,
            0f);
    }

    /// <summary>
    /// Crea una decisión de cierre final.
    /// </summary>
    private TrackGenerationDecision CreateFinishDecision(
        float changeLength,
        float currentWidthRatio)
    {
        return new TrackGenerationDecision(
            TrackFeatureType.Finish,
            Mathf.Max(0f, changeLength),
            0f,
            currentWidthRatio,
            0f,
            0f,
            0f,
            0f,
            0f);
    }

    /// <summary>
    /// Crea una decisión lateral.
    /// </summary>
    private TrackGenerationDecision CreateLateralDecision(
        TrackFeatureType featureType,
        TrackGenerationProfile profile,
        float currentWidthRatio)
    {
        float angle = Mathf.Abs(TrackFeatureUtility.GetSignedTurnAngle(featureType));
        float radians = angle * Mathf.Deg2Rad;
        float arcLength = profile.CurveRadius * radians;

        return new TrackGenerationDecision(
            featureType,
            arcLength,
            profile.MinStraightAfterLateralChange,
            currentWidthRatio,
            0f,
            0f,
            0f,
            0f,
            0f);
    }

    /// <summary>
    /// Crea una decisión vertical con delta aleatorio dentro del rango desbloqueado por progresión.
    /// </summary>
    private TrackGenerationDecision CreateVerticalDecision(
        TrackFeatureType featureType,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        LevelGenerationSettings levelSettings,
        System.Random random,
        float minTrackHeight,
        float maxTrackHeight)
    {
        float effectiveSlopeMax = levelSettings.HasSlopeHeightStepMaxOverride
            ? Mathf.Clamp(
                levelSettings.SlopeHeightStepMaxOverride,
                profile.SlopeHeightStepMin,
                profile.SlopeHeightStepMax)
            : profile.SlopeHeightStepMax;

        float requestedDelta = RandomRange(profile.SlopeHeightStepMin, effectiveSlopeMax, random);
        float changeLength = RandomSolidLength(profile, random);
        float maxDeltaByAngle = changeLength * Mathf.Tan(profile.MaxSlopeAngleDegrees * Mathf.Deg2Rad);

        float finalDelta;

        if (featureType == TrackFeatureType.SlopeUp)
        {
            float allowedMax = Mathf.Max(0f, maxTrackHeight - state.CurrentHeight);
            finalDelta = Mathf.Min(requestedDelta, allowedMax);
            finalDelta = Mathf.Min(finalDelta, maxDeltaByAngle);
        }
        else
        {
            float allowedMax = Mathf.Max(0f, state.CurrentHeight - minTrackHeight);
            finalDelta = Mathf.Min(requestedDelta, allowedMax);
            finalDelta = Mathf.Min(finalDelta, maxDeltaByAngle);
            finalDelta *= -1f;
        }

        return new TrackGenerationDecision(
            featureType,
            changeLength,
            profile.MinStraightAfterVerticalChange,
            state.CurrentWidthRatio,
            finalDelta,
            0f,
            0f,
            0f,
            0f);
    }

    /// <summary>
    /// Crea una decisión de inicio de estrechamiento.
    /// </summary>
    private TrackGenerationDecision CreateNarrowStartDecision(
        TrackGenerationProfile profile,
        System.Random random)
    {
        float widthRatio = RandomRange(profile.NarrowWidthRatioMin, profile.NarrowWidthRatioMax, random);

        return new TrackGenerationDecision(
            TrackFeatureType.NarrowStart,
            RandomSolidLength(profile, random),
            profile.MinStraightAfterWidthChange,
            widthRatio,
            0f,
            0f,
            0f,
            0f,
            0f);
    }

    /// <summary>
    /// Crea una decisión de fin de estrechamiento.
    /// </summary>
    private TrackGenerationDecision CreateNarrowEndDecision(
        TrackGenerationProfile profile)
    {
        return new TrackGenerationDecision(
            TrackFeatureType.NarrowEnd,
            profile.NarrowTransitionLength,
            profile.MinStraightAfterWidthChange,
            1f,
            0f,
            0f,
            0f,
            0f,
            0f);
    }

    /// <summary>
    /// Crea una decisión de gap con mini rampa previa.
    /// </summary>
    private TrackGenerationDecision CreateGapDecision(
        TrackGenerationProfile profile,
        System.Random random,
        float currentWidthRatio)
    {
        float gapLength = RandomRange(profile.GapLengthMin, profile.GapLengthMax, random);
        float rampLength = RandomRange(profile.PreGapRampLengthMin, profile.PreGapRampLengthMax, random);
        float rampHeight = RandomRange(profile.PreGapRampHeightMin, profile.PreGapRampHeightMax, random);

        return new TrackGenerationDecision(
            TrackFeatureType.Gap,
            gapLength,
            profile.MinStraightAfterGap,
            currentWidthRatio,
            0f,
            rampLength,
            rampHeight,
            0f,
            0f);
    }

    /// <summary>
    /// Crea una decisión de entrada a rail.
    /// </summary>
    private TrackGenerationDecision CreateRailStartDecision(
        TrackGenerationProfile profile,
        System.Random random,
        float currentWidthRatio)
    {
        return new TrackGenerationDecision(
            TrackFeatureType.RailStart,
            RandomRange(profile.RailSectionLengthMin, profile.RailSectionLengthMax, random),
            0f,
            currentWidthRatio,
            0f,
            0f,
            0f,
            profile.RailSeparation,
            profile.RailWidth);
    }

    /// <summary>
    /// Crea una decisión de continuación dentro de rail.
    /// </summary>
    private TrackGenerationDecision CreateRailSegmentDecision(
        TrackGenerationProfile profile,
        System.Random random,
        float currentWidthRatio)
    {
        return new TrackGenerationDecision(
            TrackFeatureType.RailSegment,
            RandomRange(profile.RailSectionLengthMin, profile.RailSectionLengthMax, random),
            0f,
            currentWidthRatio,
            0f,
            0f,
            0f,
            profile.RailSeparation,
            profile.RailWidth);
    }

    /// <summary>
    /// Crea una decisión de salida de rail.
    /// </summary>
    private TrackGenerationDecision CreateRailEndDecision(
        TrackGenerationProfile profile,
        System.Random random,
        float currentWidthRatio)
    {
        return new TrackGenerationDecision(
            TrackFeatureType.RailEnd,
            RandomRange(profile.RailSectionLengthMin, profile.RailSectionLengthMax, random),
            profile.MinStraightAfterRail,
            currentWidthRatio,
            0f,
            0f,
            0f,
            profile.RailSeparation,
            profile.RailWidth);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Selecciona un candidato por pesos ponderados.
    /// </summary>
    private TrackFeatureType PickWeighted(List<Candidate> candidates, System.Random random)
    {
        float totalWeight = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            totalWeight += Mathf.Max(0.0001f, candidates[i].Weight);
        }

        double pick = random.NextDouble() * totalWeight;
        float cumulative = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += Mathf.Max(0.0001f, candidates[i].Weight);

            if (pick <= cumulative)
            {
                return candidates[i].FeatureType;
            }
        }

        return candidates[candidates.Count - 1].FeatureType;
    }

    /// <summary>
    /// Devuelve una longitud aleatoria de pista sólida.
    /// </summary>
    private float RandomSolidLength(TrackGenerationProfile profile, System.Random random)
    {
        return RandomRange(profile.MinimumSectionLength, profile.MaximumSectionLength, random);
    }

    /// <summary>
    /// Devuelve un valor aleatorio dentro del rango indicado.
    /// </summary>
    private float RandomRange(float min, float max, System.Random random)
    {
        if (Mathf.Approximately(min, max))
        {
            return min;
        }

        double t = random.NextDouble();
        return Mathf.Lerp(min, max, (float)t);
    }

    /// <summary>
    /// Resuelve la longitud segura inicial efectiva.
    /// </summary>
    private float ResolveSafeStartLength(
        TrackGenerationProfile profile,
        LevelGenerationSettings levelSettings)
    {
        return levelSettings.SafeStartLengthOverride > 0f
            ? levelSettings.SafeStartLengthOverride
            : profile.SafeStartLength;
    }

    /// <summary>
    /// Resuelve la longitud segura final efectiva.
    /// </summary>
    private float ResolveSafeEndLength(
        TrackGenerationProfile profile,
        LevelGenerationSettings levelSettings)
    {
        return levelSettings.SafeEndLengthOverride > 0f
            ? levelSettings.SafeEndLengthOverride
            : profile.SafeEndLength;
    }

    #endregion
}