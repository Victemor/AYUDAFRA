using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controlador principal del generador procedural de track.
///
/// Genera dos tipos de pista:
/// - Pista jugable principal: guarda GeneratedMap, genera colliders, barreras y zona de muerte.
/// - Pista decorativa: genera solo geometría visual, sin colliders, sin barreras, sin contenido y sin modificar GeneratedMap.
/// </summary>
public sealed class TrackGeneratorController : MonoBehaviour
{
    #region Nested Types

    /// <summary>
    /// Resultado de una generación decorativa de track.
    /// </summary>
    public readonly struct DecorativeTrackBuildResult
    {
        public bool IsValid { get; }
        public GameObject RootObject { get; }
        public TrackRuntimeMap RuntimeMap { get; }

        public DecorativeTrackBuildResult(
            bool isValid,
            GameObject rootObject,
            TrackRuntimeMap runtimeMap)
        {
            IsValid = isValid;
            RootObject = rootObject;
            RuntimeMap = runtimeMap;
        }
    }

    #endregion

    #region Inspector

    [Header("Configuration")]
    [SerializeField]
    [Tooltip("Perfil base de generación del track. Define los valores máximos de todas las probabilidades.")]
    private TrackGenerationProfile generationProfile;

    [Header("Generated Root")]
    [SerializeField]
    [Tooltip("Nombre del contenedor raíz donde se instancian los chunks generados.")]
    private string generatedRootName = "GeneratedTrack";

    [SerializeField]
    [Tooltip("Prefijo de nombre para cada chunk visual generado.")]
    private string chunkObjectNamePrefix = "TrackChunk_";

    [SerializeField]
    [Tooltip("Layer asignada automáticamente a todos los objetos generados de pista, rieles y colliders.")]
    private string generatedTrackLayerName = "Ground";

    [SerializeField]
    [Tooltip("Si está activo, se genera collider físico para cada chunk sólido jugable.")]
    private bool generateMeshColliders = true;

    [Header("Rail Physics")]
    [SerializeField]
    [Tooltip("Si está activo, los chunks de rail usan CapsuleCollider por segmento para mejorar la estabilidad física.")]
    private bool usePrimitiveRailColliders = true;

    [SerializeField]
    [Tooltip("Multiplicador aplicado al radio físico de cada riel. Mantenerlo cerca de 1 evita que la bola se atasque entre rieles.")]
    private float railColliderRadiusMultiplier = 1f;

    [SerializeField]
    [Tooltip("Radio físico mínimo de seguridad para los CapsuleCollider de rail.")]
    private float minimumRailColliderRadius = 0.05f;

    [SerializeField]
    [Tooltip("Material físico opcional usado por los colliders primitivos de rail.")]
    private PhysicsMaterial railPhysicMaterial;

    [Header("Optional Generators")]
    [SerializeField]
    [Tooltip("Generador opcional de zona de muerte que replica el recorrido del track.")]
    private VoidZoneGenerator voidZoneGenerator;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("Si está activo, genera automáticamente al cargar la escena. InfiniteLevelManager lo desactiva en su Awake.")]
    private bool generateOnStart = true;

    [SerializeField]
    [Tooltip("Si está activo, imprime logs básicos de generación.")]
    private bool enableDebugLogs;

    [SerializeField]
    [Tooltip("Generador opcional de bordes laterales del track.")]
    private TrackBarrierGenerator trackBarrierGenerator;

    #endregion

    #region Runtime

    private readonly TrackRuleEvaluator ruleEvaluator = new TrackRuleEvaluator();

    private TrackRuntimeMap generatedMap;
    private LevelGenerationSettings activeSettings;
    private TrackGenerationProfile activeVisualProfile;

    #endregion

    #region Properties

    /// <summary>
    /// Mapa runtime generado actualmente para la pista jugable principal.
    /// </summary>
    public TrackRuntimeMap GeneratedMap => generatedMap;

    /// <summary>
    /// Perfil base de generación asignado al generador.
    /// </summary>
    public TrackGenerationProfile GenerationProfile => generationProfile;

    /// <summary>
    /// Generador de barreras asociado a la pista principal.
    /// </summary>
    public TrackBarrierGenerator BarrierGenerator => trackBarrierGenerator;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (generateOnStart)
        {
            Debug.LogWarning(
                "[TRACK] generateOnStart está activo pero no hay settings inyectados. " +
                "Asigna un InfiniteLevelManager en la escena o desactiva generateOnStart.",
                this);
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Desactiva la generación automática al inicio de escena.
    /// </summary>
    public void DisableAutoGeneration()
    {
        generateOnStart = false;
    }

    /// <summary>
    /// Inyecta los settings del nivel actual y genera la pista jugable completa.
    /// </summary>
    public void GenerateLevel(LevelGenerationSettings settings)
    {
        if (settings == null)
        {
            Debug.LogWarning("[TRACK] Se llamó GenerateLevel sin settings. Generación cancelada.", this);
            return;
        }

        activeSettings = settings;
        GenerateLevel();
    }

    /// <summary>
    /// Sobreescribe temporalmente el profile visual para la próxima generación jugable.
    /// </summary>
    public void SetVisualProfileOverride(TrackGenerationProfile profile)
    {
        activeVisualProfile = profile;
    }

    /// <summary>
    /// Elimina el override de profile visual.
    /// </summary>
    public void ClearVisualProfileOverride()
    {
        activeVisualProfile = null;
    }

    /// <summary>
    /// Genera el nivel jugable usando los settings activos.
    /// </summary>
    [ContextMenu("Generate Level")]
    public void GenerateLevel()
    {
        if (!CanGeneratePlayableLevel())
        {
            return;
        }

        ValidateInspectorData();
        LogConfigurationWarnings();
        ClearGeneratedVisuals();

        TrackRuntimeMap runtimeMap = BuildRuntimeMap(
            activeSettings,
            transform.position,
            ResolveHorizontalForward(transform.forward));

        if (runtimeMap == null)
        {
            return;
        }

        Transform root = GetOrCreateGeneratedRoot();

        BuildGeneratedVisuals(
            runtimeMap.SurfaceChunks,
            root,
            shouldGenerateColliders: generateMeshColliders,
            visualProfileOverride: activeVisualProfile);

        generatedMap = runtimeMap;

        if (trackBarrierGenerator != null)
        {
            trackBarrierGenerator.Rebuild(generatedMap, activeSettings);
        }

        if (voidZoneGenerator != null)
        {
            voidZoneGenerator.Rebuild();
        }

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[TRACK] Generated playable level with seed {runtimeMap.GeneratedSeed}. " +
                $"Sections: {runtimeMap.Sections.Count}. " +
                $"Features: {runtimeMap.Features.Count}. " +
                $"Chunks: {runtimeMap.SurfaceChunks.Count}.",
                this);
        }
    }

    /// <summary>
    /// Genera una pista decorativa visual sin afectar GeneratedMap.
    /// No genera colliders, barreras, zona de muerte, contenido, checkpoints ni power-ups.
    /// </summary>
    public DecorativeTrackBuildResult GenerateDecorativeLevel(
        LevelGenerationSettings settings,
        Transform parent,
        string rootName,
        TrackGenerationProfile visualProfileOverride = null)
    {
        if (settings == null)
        {
            Debug.LogWarning("[TRACK] No se puede generar pista decorativa sin LevelGenerationSettings.", this);
            return new DecorativeTrackBuildResult(false, null, null);
        }

        if (generationProfile == null)
        {
            Debug.LogWarning("[TRACK] No se puede generar pista decorativa sin generationProfile.", this);
            return new DecorativeTrackBuildResult(false, null, null);
        }

        ValidateInspectorData();

        TrackRuntimeMap runtimeMap = BuildRuntimeMap(
            settings,
            Vector3.zero,
            Vector3.forward);

        if (runtimeMap == null)
        {
            return new DecorativeTrackBuildResult(false, null, null);
        }

        GameObject rootObject = new GameObject(string.IsNullOrWhiteSpace(rootName) ? "DecorativeTrack" : rootName);

        if (parent != null)
        {
            rootObject.transform.SetParent(parent);
        }

        rootObject.transform.localPosition = Vector3.zero;
        rootObject.transform.localRotation = Quaternion.identity;
        rootObject.transform.localScale = Vector3.one;

        BuildGeneratedVisuals(
            runtimeMap.SurfaceChunks,
            rootObject.transform,
            shouldGenerateColliders: false,
            visualProfileOverride: visualProfileOverride);

        return new DecorativeTrackBuildResult(true, rootObject, runtimeMap);
    }

    /// <summary>
    /// Elimina todo lo generado visualmente en la pista jugable principal.
    /// </summary>
    [ContextMenu("Clear Generated Level")]
    public void ClearGeneratedLevel()
    {
        ClearGeneratedVisuals();
        generatedMap = null;
    }

    #endregion

    #region Runtime Map Build

    /// <summary>
    /// Construye un mapa runtime completo sin crear GameObjects.
    /// </summary>
    private TrackRuntimeMap BuildRuntimeMap(
        LevelGenerationSettings settings,
        Vector3 startPosition,
        Vector3 startForward)
    {
        if (settings == null || generationProfile == null)
        {
            return null;
        }

        int resolvedSeed = ResolveSeed(settings);
        System.Random random = new System.Random(resolvedSeed);

        float targetLength = generationProfile.TargetTrackLength * settings.LengthMultiplier;

        float minTrackHeight = settings.OverrideMinHeight
            ? settings.MinHeightOverride
            : generationProfile.MinTrackHeight;

        float maxTrackHeight = settings.OverrideMaxHeight
            ? settings.MaxHeightOverride
            : generationProfile.MaxTrackHeight;

        List<TrackSectionDefinition> sections = new List<TrackSectionDefinition>();
        List<TrackFeatureRecord> features = new List<TrackFeatureRecord>();

        TrackGenerationState state = CreateInitialState(startPosition, startForward);

        GenerateSafeStart(ref state, sections, features, generationProfile, settings);

        float safeEndLength = ResolveSafeEndLength(settings);

        while (state.GeneratedLength < targetLength - safeEndLength)
        {
            TrackGenerationDecision decision = ruleEvaluator.EvaluateNextDecision(
                ref state,
                generationProfile,
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
            TrackSplineBuilder.BuildSplinePoints(sections, generationProfile);

        List<TrackSurfaceChunkDefinition> surfaceChunks =
            TrackLayoutBuilder.BuildSurfaceChunks(splinePoints);

        TrackPathSampler pathSampler = new TrackPathSampler();
        pathSampler.Rebuild(surfaceChunks);

        return new TrackRuntimeMap(
            resolvedSeed,
            sections,
            features,
            surfaceChunks,
            pathSampler);
    }

    #endregion

    #region Generation Core

    /// <summary>
    /// Crea el estado inicial del generador.
    /// </summary>
    private static TrackGenerationState CreateInitialState(
        Vector3 startPosition,
        Vector3 startForward)
    {
        Vector3 resolvedForward = ResolveHorizontalForward(startForward);

        return new TrackGenerationState
        {
            CurrentPosition = startPosition,
            CurrentForward = resolvedForward,
            CurrentHeight = startPosition.y,
            GeneratedLength = 0f,
            CurrentLateralState = TrackLateralState.Center,
            CurrentVerticalState = TrackVerticalState.Flat,
            CurrentStructureType = TrackStructureType.SolidTrack,
            CurrentWidthRatio = 1f,
            CurrentYawOffsetDegrees = 0f,
            DistanceSinceLastLateralChange = 999f,
            DistanceSinceLastVerticalChange = 999f,
            DistanceSinceLastWidthChange = 999f,
            DistanceSinceLastGap = 999f,
            DistanceSinceLastRail = 999f,
            IsInsideSafeStartZone = true,
            IsInsideSafeEndZone = false,
            IsInsideRailSequence = false,
            CurrentRailSectionCount = 0
        };
    }

    /// <summary>
    /// Genera la zona recta segura inicial.
    /// </summary>
    private void GenerateSafeStart(
        ref TrackGenerationState state,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features,
        TrackGenerationProfile profile,
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

    /// <summary>
    /// Genera la zona recta segura final.
    /// </summary>
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

        state.CurrentStructureType = TrackStructureType.SolidTrack;
        state.IsInsideRailSequence = false;
        state.CurrentRailSectionCount = 0;
        state.CurrentWidthRatio = 1f;

        AddStraightSection(
            ref state,
            remaining,
            TrackFeatureType.Finish,
            TrackStructureType.SolidTrack,
            sections,
            features);
    }

    /// <summary>
    /// Aplica una decisión al estado actual y genera sus secciones correspondientes.
    /// </summary>
    private void ApplyDecision(
        ref TrackGenerationState state,
        TrackGenerationDecision decision,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        switch (decision.FeatureType)
        {
            case TrackFeatureType.Straight:
                AddStraightSection(
                    ref state,
                    decision.ChangeLength,
                    TrackFeatureType.Straight,
                    state.CurrentStructureType,
                    sections,
                    features);
                break;

            case TrackFeatureType.LateralEnterLeft45:
            case TrackFeatureType.LateralEnterLeft90:
            case TrackFeatureType.LateralEnterRight45:
            case TrackFeatureType.LateralEnterRight90:
            case TrackFeatureType.LateralReturnToCenterFromLeft45:
            case TrackFeatureType.LateralReturnToCenterFromLeft90:
            case TrackFeatureType.LateralReturnToCenterFromRight45:
            case TrackFeatureType.LateralReturnToCenterFromRight90:
                AddLateralSection(
                    ref state,
                    decision.ChangeLength,
                    decision.FeatureType,
                    state.CurrentStructureType,
                    sections,
                    features);
                break;

            case TrackFeatureType.SlopeUp:
            case TrackFeatureType.SlopeDown:
                AddVerticalSection(
                    ref state,
                    decision.ChangeLength,
                    decision.FeatureType,
                    decision.VerticalDelta,
                    sections,
                    features);
                break;

            case TrackFeatureType.NarrowStart:
                AddNarrowStartSection(
                    ref state,
                    decision.ChangeLength,
                    decision.TargetWidthRatio,
                    sections,
                    features);
                break;

            case TrackFeatureType.NarrowEnd:
                AddNarrowEndSection(
                    ref state,
                    decision.ChangeLength,
                    sections,
                    features);
                break;

            case TrackFeatureType.Gap:
                AddPreGapRampSection(
                    ref state,
                    decision.PreGapRampLength,
                    decision.PreGapRampHeight,
                    sections,
                    features);

                AddGapSection(
                    ref state,
                    decision.ChangeLength,
                    sections,
                    features);
                break;

            case TrackFeatureType.RailStart:
                AddRailStartSection(
                    ref state,
                    decision.ChangeLength,
                    decision.RailSeparation,
                    decision.RailWidth,
                    sections,
                    features);
                break;

            case TrackFeatureType.RailSegment:
                AddRailSegmentSection(
                    ref state,
                    decision.ChangeLength,
                    decision.RailSeparation,
                    decision.RailWidth,
                    sections,
                    features);
                break;

            case TrackFeatureType.RailEnd:
                AddRailEndSection(
                    ref state,
                    decision.ChangeLength,
                    decision.RailSeparation,
                    decision.RailWidth,
                    sections,
                    features);
                break;

            case TrackFeatureType.Finish:
                AddStraightSection(
                    ref state,
                    decision.ChangeLength,
                    TrackFeatureType.Finish,
                    TrackStructureType.SolidTrack,
                    sections,
                    features);
                break;
        }
    }

    /// <summary>
    /// Agrega recta de recuperación posterior a un cambio.
    /// </summary>
    private void AddStraightRecovery(
        ref TrackGenerationState state,
        float recoveryLength,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        AddStraightSection(
            ref state,
            recoveryLength,
            TrackFeatureType.Straight,
            state.CurrentStructureType,
            sections,
            features);
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
        Vector3 endPosition = startPosition + state.CurrentForward * length;
        endPosition.y = state.CurrentHeight;

        float startWidth = ResolveWidthFromRatio(state.CurrentWidthRatio);

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType = featureType,
            StructureType = structureType,
            StartDistance = state.GeneratedLength,
            EndDistance = state.GeneratedLength + length,
            Length = length,
            StartPosition = startPosition,
            EndPosition = endPosition,
            StartForward = state.CurrentForward,
            EndForward = state.CurrentForward,
            StartHeight = state.CurrentHeight,
            EndHeight = state.CurrentHeight,
            StartWidth = startWidth,
            EndWidth = startWidth,
            TargetWidthRatio = state.CurrentWidthRatio,
            LateralStateBefore = state.CurrentLateralState,
            LateralStateAfter = state.CurrentLateralState,
            VerticalStateBefore = state.CurrentVerticalState,
            VerticalStateAfter = TrackVerticalState.Flat,
            HasSurface = structureType != TrackStructureType.Gap,
            TurnAngleDegrees = 0f,
            TurnRadius = 0f,
            SlopeHeightDelta = 0f,
            RampHeightDelta = 0f,
            RailSeparation = structureType == TrackStructureType.RailTrack ? generationProfile.RailSeparation : 0f,
            RailWidth = structureType == TrackStructureType.RailTrack ? generationProfile.RailWidth : 0f,
            StartsFromCutCenter = false,
            EndsAtCutCenter = false
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

        float signedAngle = TrackFeatureUtility.GetSignedTurnAngle(featureType);
        float endYawOffset = GetEndYawOffsetForFeature(featureType, state.CurrentYawOffsetDegrees);
        TrackLateralState endLateralState = GetEndLateralStateForYaw(endYawOffset);

        float radius = generationProfile.CurveRadius;

        Vector3 startPosition = state.CurrentPosition;
        Vector3 startForward = ResolveHorizontalForward(state.CurrentForward);
        Vector3 startRight = Vector3.Cross(Vector3.up, startForward).normalized;

        float turnSign = Mathf.Sign(signedAngle);
        Vector3 center = startPosition + startRight * radius * turnSign;
        Vector3 radialStart = startPosition - center;
        Vector3 radialEnd = Quaternion.AngleAxis(signedAngle, Vector3.up) * radialStart;

        Vector3 endPosition = center + radialEnd;
        endPosition.y = state.CurrentHeight;

        Vector3 endForward = Quaternion.AngleAxis(signedAngle, Vector3.up) * startForward;
        endForward = ResolveHorizontalForward(endForward);

        float width = ResolveWidthFromRatio(state.CurrentWidthRatio);

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType = featureType,
            StructureType = structureType,
            StartDistance = state.GeneratedLength,
            EndDistance = state.GeneratedLength + length,
            Length = length,
            StartPosition = startPosition,
            EndPosition = endPosition,
            StartForward = startForward,
            EndForward = endForward,
            StartHeight = state.CurrentHeight,
            EndHeight = state.CurrentHeight,
            StartWidth = width,
            EndWidth = width,
            TargetWidthRatio = state.CurrentWidthRatio,
            LateralStateBefore = state.CurrentLateralState,
            LateralStateAfter = endLateralState,
            VerticalStateBefore = state.CurrentVerticalState,
            VerticalStateAfter = TrackVerticalState.Flat,
            HasSurface = structureType != TrackStructureType.Gap,
            TurnAngleDegrees = signedAngle,
            TurnRadius = radius,
            SlopeHeightDelta = 0f,
            RampHeightDelta = 0f,
            RailSeparation = structureType == TrackStructureType.RailTrack ? generationProfile.RailSeparation : 0f,
            RailWidth = structureType == TrackStructureType.RailTrack ? generationProfile.RailWidth : 0f,
            StartsFromCutCenter = false,
            EndsAtCutCenter = false
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentLateralState = endLateralState;
        state.CurrentYawOffsetDegrees = endYawOffset;
        state.CurrentPosition = endPosition;
        state.CurrentForward = endForward;
        state.GeneratedLength += length;
        state.DistanceSinceLastLateralChange = 0f;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange += length;
        state.DistanceSinceLastGap += length;
        state.DistanceSinceLastRail += length;
        state.CurrentVerticalState = TrackVerticalState.Flat;
        state.CurrentStructureType = structureType;

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

        Vector3 startPosition = state.CurrentPosition;
        float endHeight = state.CurrentHeight + verticalDelta;

        Vector3 endPosition = startPosition + state.CurrentForward * length;
        endPosition.y = endHeight;

        float width = ResolveWidthFromRatio(state.CurrentWidthRatio);

        TrackVerticalState endVerticalState = verticalDelta >= 0f
            ? TrackVerticalState.Ascending
            : TrackVerticalState.Descending;

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType = featureType,
            StructureType = structureType,
            StartDistance = state.GeneratedLength,
            EndDistance = state.GeneratedLength + length,
            Length = length,
            StartPosition = startPosition,
            EndPosition = endPosition,
            StartForward = state.CurrentForward,
            EndForward = state.CurrentForward,
            StartHeight = state.CurrentHeight,
            EndHeight = endHeight,
            StartWidth = width,
            EndWidth = width,
            TargetWidthRatio = state.CurrentWidthRatio,
            LateralStateBefore = state.CurrentLateralState,
            LateralStateAfter = state.CurrentLateralState,
            VerticalStateBefore = state.CurrentVerticalState,
            VerticalStateAfter = endVerticalState,
            HasSurface = true,
            TurnAngleDegrees = 0f,
            TurnRadius = 0f,
            SlopeHeightDelta = verticalDelta,
            RampHeightDelta = 0f,
            RailSeparation = structureType == TrackStructureType.RailTrack ? generationProfile.RailSeparation : 0f,
            RailWidth = structureType == TrackStructureType.RailTrack ? generationProfile.RailWidth : 0f,
            StartsFromCutCenter = false,
            EndsAtCutCenter = false
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentHeight = endHeight;
        state.CurrentPosition = endPosition;
        state.GeneratedLength += length;
        state.DistanceSinceLastLateralChange += length;
        state.DistanceSinceLastVerticalChange = 0f;
        state.DistanceSinceLastWidthChange += length;
        state.DistanceSinceLastGap += length;
        state.DistanceSinceLastRail += length;
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
        length = Mathf.Max(length, generationProfile.NarrowTransitionLength);

        Vector3 startPosition = state.CurrentPosition;
        Vector3 endPosition = startPosition + state.CurrentForward * length;
        endPosition.y = state.CurrentHeight;

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType = TrackFeatureType.NarrowStart,
            StructureType = TrackStructureType.SolidTrack,
            StartDistance = state.GeneratedLength,
            EndDistance = state.GeneratedLength + length,
            Length = length,
            StartPosition = startPosition,
            EndPosition = endPosition,
            StartForward = state.CurrentForward,
            EndForward = state.CurrentForward,
            StartHeight = state.CurrentHeight,
            EndHeight = state.CurrentHeight,
            StartWidth = ResolveWidthFromRatio(state.CurrentWidthRatio),
            EndWidth = ResolveWidthFromRatio(targetWidthRatio),
            TargetWidthRatio = targetWidthRatio,
            LateralStateBefore = state.CurrentLateralState,
            LateralStateAfter = state.CurrentLateralState,
            VerticalStateBefore = state.CurrentVerticalState,
            VerticalStateAfter = TrackVerticalState.Flat,
            HasSurface = true,
            TurnAngleDegrees = 0f,
            TurnRadius = 0f,
            SlopeHeightDelta = 0f,
            RampHeightDelta = 0f,
            RailSeparation = 0f,
            RailWidth = 0f,
            StartsFromCutCenter = false,
            EndsAtCutCenter = false
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentWidthRatio = targetWidthRatio;
        state.CurrentPosition = endPosition;
        state.GeneratedLength += length;
        state.DistanceSinceLastLateralChange += length;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange = 0f;
        state.DistanceSinceLastGap += length;
        state.DistanceSinceLastRail += length;
        state.CurrentVerticalState = TrackVerticalState.Flat;
        state.CurrentStructureType = TrackStructureType.SolidTrack;
    }

    private void AddNarrowEndSection(
        ref TrackGenerationState state,
        float length,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        length = Mathf.Max(length, generationProfile.NarrowTransitionLength);

        Vector3 startPosition = state.CurrentPosition;
        Vector3 endPosition = startPosition + state.CurrentForward * length;
        endPosition.y = state.CurrentHeight;

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType = TrackFeatureType.NarrowEnd,
            StructureType = TrackStructureType.SolidTrack,
            StartDistance = state.GeneratedLength,
            EndDistance = state.GeneratedLength + length,
            Length = length,
            StartPosition = startPosition,
            EndPosition = endPosition,
            StartForward = state.CurrentForward,
            EndForward = state.CurrentForward,
            StartHeight = state.CurrentHeight,
            EndHeight = state.CurrentHeight,
            StartWidth = ResolveWidthFromRatio(state.CurrentWidthRatio),
            EndWidth = ResolveWidthFromRatio(1f),
            TargetWidthRatio = 1f,
            LateralStateBefore = state.CurrentLateralState,
            LateralStateAfter = state.CurrentLateralState,
            VerticalStateBefore = state.CurrentVerticalState,
            VerticalStateAfter = TrackVerticalState.Flat,
            HasSurface = true,
            TurnAngleDegrees = 0f,
            TurnRadius = 0f,
            SlopeHeightDelta = 0f,
            RampHeightDelta = 0f,
            RailSeparation = 0f,
            RailWidth = 0f,
            StartsFromCutCenter = false,
            EndsAtCutCenter = false
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentWidthRatio = 1f;
        state.CurrentPosition = endPosition;
        state.GeneratedLength += length;
        state.DistanceSinceLastLateralChange += length;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange = 0f;
        state.DistanceSinceLastGap += length;
        state.DistanceSinceLastRail += length;
        state.CurrentVerticalState = TrackVerticalState.Flat;
        state.CurrentStructureType = TrackStructureType.SolidTrack;
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

        AddVerticalSection(
            ref state,
            rampLength,
            TrackFeatureType.SlopeUp,
            rampHeight,
            sections,
            features);
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

        Vector3 startPosition = state.CurrentPosition;
        Vector3 endPosition = startPosition + state.CurrentForward * length;
        endPosition.y = state.CurrentHeight;

        float width = ResolveWidthFromRatio(state.CurrentWidthRatio);

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType = TrackFeatureType.Gap,
            StructureType = TrackStructureType.Gap,
            StartDistance = state.GeneratedLength,
            EndDistance = state.GeneratedLength + length,
            Length = length,
            StartPosition = startPosition,
            EndPosition = endPosition,
            StartForward = state.CurrentForward,
            EndForward = state.CurrentForward,
            StartHeight = state.CurrentHeight,
            EndHeight = state.CurrentHeight,
            StartWidth = width,
            EndWidth = width,
            TargetWidthRatio = state.CurrentWidthRatio,
            LateralStateBefore = state.CurrentLateralState,
            LateralStateAfter = state.CurrentLateralState,
            VerticalStateBefore = TrackVerticalState.Flat,
            VerticalStateAfter = TrackVerticalState.Flat,
            HasSurface = false,
            TurnAngleDegrees = 0f,
            TurnRadius = 0f,
            SlopeHeightDelta = 0f,
            RampHeightDelta = 0f,
            RailSeparation = 0f,
            RailWidth = 0f,
            StartsFromCutCenter = true,
            EndsAtCutCenter = true
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentPosition = endPosition;
        state.GeneratedLength += length;
        state.DistanceSinceLastLateralChange += length;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange += length;
        state.DistanceSinceLastGap = 0f;
        state.DistanceSinceLastRail += length;
        state.CurrentVerticalState = TrackVerticalState.Flat;
        state.CurrentStructureType = TrackStructureType.SolidTrack;
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

        Vector3 startPosition = state.CurrentPosition;
        Vector3 endPosition = startPosition + state.CurrentForward * length;
        endPosition.y = state.CurrentHeight;

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType = TrackFeatureType.RailStart,
            StructureType = TrackStructureType.RailTrack,
            StartDistance = state.GeneratedLength,
            EndDistance = state.GeneratedLength + length,
            Length = length,
            StartPosition = startPosition,
            EndPosition = endPosition,
            StartForward = state.CurrentForward,
            EndForward = state.CurrentForward,
            StartHeight = state.CurrentHeight,
            EndHeight = state.CurrentHeight,
            StartWidth = generationProfile.NormalTrackWidth,
            EndWidth = generationProfile.NormalTrackWidth,
            TargetWidthRatio = state.CurrentWidthRatio,
            LateralStateBefore = state.CurrentLateralState,
            LateralStateAfter = state.CurrentLateralState,
            VerticalStateBefore = state.CurrentVerticalState,
            VerticalStateAfter = TrackVerticalState.Flat,
            HasSurface = true,
            TurnAngleDegrees = 0f,
            TurnRadius = 0f,
            SlopeHeightDelta = 0f,
            RampHeightDelta = 0f,
            RailSeparation = railSeparation,
            RailWidth = railWidth,
            StartsFromCutCenter = true,
            EndsAtCutCenter = false
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentPosition = endPosition;
        state.GeneratedLength += length;
        state.DistanceSinceLastLateralChange += length;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange += length;
        state.DistanceSinceLastGap += length;
        state.DistanceSinceLastRail = 0f;
        state.CurrentVerticalState = TrackVerticalState.Flat;
        state.CurrentStructureType = TrackStructureType.RailTrack;
        state.IsInsideRailSequence = true;
        state.CurrentRailSectionCount = 1;
        state.CurrentWidthRatio = 1f;
    }

    private void AddRailSegmentSection(
        ref TrackGenerationState state,
        float length,
        float railSeparation,
        float railWidth,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        AddStraightSection(
            ref state,
            length,
            TrackFeatureType.RailSegment,
            TrackStructureType.RailTrack,
            sections,
            features);

        TrackSectionDefinition last = sections[sections.Count - 1];
        last.RailSeparation = railSeparation;
        last.RailWidth = railWidth;
        sections[sections.Count - 1] = last;

        TrackFeatureRecord lastFeature = features[features.Count - 1];
        lastFeature.StructureType = TrackStructureType.RailTrack;
        features[features.Count - 1] = lastFeature;

        state.CurrentStructureType = TrackStructureType.RailTrack;
        state.IsInsideRailSequence = true;
        state.CurrentRailSectionCount++;
        state.DistanceSinceLastRail = 0f;
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

        Vector3 startPosition = state.CurrentPosition;
        Vector3 endPosition = startPosition + state.CurrentForward * length;
        endPosition.y = state.CurrentHeight;

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType = TrackFeatureType.RailEnd,
            StructureType = TrackStructureType.RailTrack,
            StartDistance = state.GeneratedLength,
            EndDistance = state.GeneratedLength + length,
            Length = length,
            StartPosition = startPosition,
            EndPosition = endPosition,
            StartForward = state.CurrentForward,
            EndForward = state.CurrentForward,
            StartHeight = state.CurrentHeight,
            EndHeight = state.CurrentHeight,
            StartWidth = generationProfile.NormalTrackWidth,
            EndWidth = generationProfile.NormalTrackWidth,
            TargetWidthRatio = 1f,
            LateralStateBefore = state.CurrentLateralState,
            LateralStateAfter = state.CurrentLateralState,
            VerticalStateBefore = state.CurrentVerticalState,
            VerticalStateAfter = TrackVerticalState.Flat,
            HasSurface = true,
            TurnAngleDegrees = 0f,
            TurnRadius = 0f,
            SlopeHeightDelta = 0f,
            RampHeightDelta = 0f,
            RailSeparation = railSeparation,
            RailWidth = railWidth,
            StartsFromCutCenter = false,
            EndsAtCutCenter = true
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentPosition = endPosition;
        state.GeneratedLength += length;
        state.DistanceSinceLastLateralChange += length;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange += length;
        state.DistanceSinceLastGap += length;
        state.DistanceSinceLastRail = 0f;
        state.CurrentVerticalState = TrackVerticalState.Flat;
        state.CurrentStructureType = TrackStructureType.SolidTrack;
        state.IsInsideRailSequence = false;
        state.CurrentRailSectionCount = 0;
        state.CurrentWidthRatio = 1f;
    }

    #endregion

    #region Visual Build

    private void BuildGeneratedVisuals(
        IReadOnlyList<TrackSurfaceChunkDefinition> surfaceChunks,
        Transform root,
        bool shouldGenerateColliders,
        TrackGenerationProfile visualProfileOverride)
    {
        if (surfaceChunks == null || surfaceChunks.Count == 0 || root == null)
        {
            return;
        }

        TrackGenerationProfile resolvedVisualProfile = visualProfileOverride != null
            ? visualProfileOverride
            : generationProfile;

        for (int i = 0; i < surfaceChunks.Count; i++)
        {
            CreateChunkObject(
                surfaceChunks[i],
                root,
                shouldGenerateColliders,
                resolvedVisualProfile);
        }
    }

    private void CreateChunkObject(
        TrackSurfaceChunkDefinition chunk,
        Transform parent,
        bool shouldGenerateColliders,
        TrackGenerationProfile visualProfile)
    {
        TrackMeshBuilder.TrackMeshBuildResult result =
            TrackMeshBuilder.BuildChunkMesh(chunk, visualProfile);

        GameObject chunkObject = new GameObject($"{chunkObjectNamePrefix}{chunk.ChunkIndex:D2}");
        AssignGeneratedLayer(chunkObject);

        chunkObject.transform.SetParent(parent);
        chunkObject.transform.localPosition = Vector3.zero;
        chunkObject.transform.localRotation = Quaternion.identity;
        chunkObject.transform.localScale = Vector3.one;

        MeshFilter meshFilter = chunkObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = chunkObject.AddComponent<MeshRenderer>();

        meshFilter.sharedMesh = result.Mesh;
        meshRenderer.sharedMaterials = result.Materials;

        if (shouldGenerateColliders && result.Mesh != null)
        {
            CreateChunkPhysics(chunk, chunkObject, result.Mesh);
        }
    }

    private void CreateChunkPhysics(
        TrackSurfaceChunkDefinition chunk,
        GameObject chunkObject,
        Mesh mesh)
    {
        if (chunk.StructureType == TrackStructureType.RailTrack && usePrimitiveRailColliders)
        {
            CreateRailPrimitiveColliders(chunk, chunkObject.transform);
            return;
        }

        MeshCollider meshCollider = chunkObject.AddComponent<MeshCollider>();
        meshCollider.cookingOptions =
            MeshColliderCookingOptions.EnableMeshCleaning |
            MeshColliderCookingOptions.WeldColocatedVertices |
            MeshColliderCookingOptions.CookForFasterSimulation;

        meshCollider.sharedMesh = mesh;
    }

    private void CreateRailPrimitiveColliders(
        TrackSurfaceChunkDefinition chunk,
        Transform parent)
    {
        if (chunk == null || chunk.Samples == null || chunk.Samples.Count < 2 || parent == null)
        {
            return;
        }

        CreateSingleRailPrimitiveColliders(chunk, parent, -1f, "Left");
        CreateSingleRailPrimitiveColliders(chunk, parent, 1f, "Right");
    }

    private void CreateSingleRailPrimitiveColliders(
        TrackSurfaceChunkDefinition chunk,
        Transform parent,
        float sideSign,
        string sideName)
    {
        for (int i = 0; i < chunk.Samples.Count - 1; i++)
        {
            TrackLayoutSamplePoint startSample = chunk.Samples[i];
            TrackLayoutSamplePoint endSample = chunk.Samples[i + 1];

            ResolveRailColliderFrame(
                startSample,
                sideSign,
                out Vector3 startCenter,
                out float startRadius);

            ResolveRailColliderFrame(
                endSample,
                sideSign,
                out Vector3 endCenter,
                out float endRadius);

            Vector3 segment = endCenter - startCenter;
            float segmentLength = segment.magnitude;

            if (segmentLength <= 0.05f)
            {
                continue;
            }

            float radius = Mathf.Max(
                minimumRailColliderRadius,
                Mathf.Min(startRadius, endRadius) * railColliderRadiusMultiplier);

            CreateRailCapsuleCollider(
                parent,
                startCenter,
                endCenter,
                radius,
                segmentLength,
                $"RailCollider_{sideName}_{i:D3}");
        }
    }

    private static void ResolveRailColliderFrame(
        TrackLayoutSamplePoint sample,
        float sideSign,
        out Vector3 center,
        out float radius)
    {
        float separation = Mathf.Max(0f, sample.RailSeparation);
        float railWidth = Mathf.Max(0.01f, sample.RailWidth);

        Vector3 right = ResolveSafeRight(sample.Right, sample.Forward);

        center = sample.Position + right * (separation * 0.5f * sideSign);
        radius = railWidth * 0.5f;
    }

    private void CreateRailCapsuleCollider(
        Transform parent,
        Vector3 startCenter,
        Vector3 endCenter,
        float radius,
        float segmentLength,
        string objectName)
    {
        Vector3 direction = (endCenter - startCenter).normalized;
        Vector3 midPoint = (startCenter + endCenter) * 0.5f;

        GameObject capsuleObject = new GameObject(objectName);
        AssignGeneratedLayer(capsuleObject);

        capsuleObject.transform.SetParent(parent);
        capsuleObject.transform.position = midPoint;
        capsuleObject.transform.rotation = ResolveCapsuleRotation(direction);
        capsuleObject.transform.localScale = Vector3.one;

        CapsuleCollider capsule = capsuleObject.AddComponent<CapsuleCollider>();
        capsule.radius = radius;
        capsule.height = segmentLength + radius * 2f;
        capsule.direction = 2;

        if (railPhysicMaterial != null)
        {
            capsule.sharedMaterial = railPhysicMaterial;
        }
    }

    private Transform GetOrCreateGeneratedRoot()
    {
        Transform existingRoot = transform.Find(generatedRootName);

        if (existingRoot != null)
        {
            return existingRoot;
        }

        GameObject root = new GameObject(generatedRootName);
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        return root.transform;
    }

    private void ClearGeneratedVisuals()
    {
        Transform existingRoot = transform.Find(generatedRootName);

        if (existingRoot == null)
        {
            return;
        }

        List<GameObject> children = new List<GameObject>();

        for (int i = 0; i < existingRoot.childCount; i++)
        {
            children.Add(existingRoot.GetChild(i).gameObject);
        }

        for (int i = 0; i < children.Count; i++)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(children[i]);
            }
            else
            {
                Destroy(children[i]);
            }
#else
            Destroy(children[i]);
#endif
        }
    }

    #endregion

    #region Helpers

    private bool CanGeneratePlayableLevel()
    {
        if (generationProfile == null)
        {
            Debug.LogWarning("[TRACK] generationProfile no asignado.", this);
            return false;
        }

        if (activeSettings == null)
        {
            Debug.LogWarning("[TRACK] No hay settings activos. Llama GenerateLevel(LevelGenerationSettings) primero.", this);
            return false;
        }

        return true;
    }

    private void AddFeatureRecord(
        TrackSectionDefinition section,
        List<TrackFeatureRecord> features)
    {
        features.Add(new TrackFeatureRecord
        {
            FeatureType = section.FeatureType,
            StructureType = section.StructureType,
            StartDistance = section.StartDistance,
            EndDistance = section.EndDistance,
            StartPosition = section.StartPosition,
            EndPosition = section.EndPosition,
            CenterPosition = Vector3.Lerp(section.StartPosition, section.EndPosition, 0.5f),
            LateralState = section.LateralStateAfter,
            VerticalState = section.VerticalStateAfter,
            WidthRatio = section.TargetWidthRatio,
            HasSurface = section.HasSurface
        });
    }

    private void AdvanceStateStraight(
        ref TrackGenerationState state,
        float length,
        Vector3 endPosition,
        TrackStructureType structureType)
    {
        state.CurrentPosition = endPosition;
        state.GeneratedLength += length;
        state.DistanceSinceLastLateralChange += length;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange += length;
        state.DistanceSinceLastGap += length;
        state.DistanceSinceLastRail += length;
        state.CurrentVerticalState = TrackVerticalState.Flat;
        state.CurrentStructureType = structureType;

        if (structureType == TrackStructureType.RailTrack)
        {
            state.IsInsideRailSequence = true;
            state.CurrentRailSectionCount++;
            state.DistanceSinceLastRail = 0f;
        }
    }

    private float ResolveWidthFromRatio(float widthRatio)
    {
        return generationProfile.NormalTrackWidth * Mathf.Max(0.01f, widthRatio);
    }

    private static int ResolveSeed(LevelGenerationSettings settings)
    {
        if (settings.UseFixedSeed)
        {
            return settings.FixedSeed;
        }

        return System.Environment.TickCount;
    }

    private float ResolveSafeEndLength(LevelGenerationSettings settings)
    {
        if (settings.SafeEndLengthOverride > 0f)
        {
            return settings.SafeEndLengthOverride;
        }

        return generationProfile.SafeEndLength;
    }

    private void AssignGeneratedLayer(GameObject generatedObject)
    {
        if (generatedObject == null)
        {
            return;
        }

        int layer = LayerMask.NameToLayer(generatedTrackLayerName);

        if (layer < 0)
        {
            Debug.LogWarning(
                $"[TRACK] La layer '{generatedTrackLayerName}' no existe. Se usará la layer actual del objeto.",
                this);

            return;
        }

        generatedObject.layer = layer;
    }

    private void ValidateInspectorData()
    {
        generatedRootName = string.IsNullOrWhiteSpace(generatedRootName)
            ? "GeneratedTrack"
            : generatedRootName;

        chunkObjectNamePrefix = string.IsNullOrWhiteSpace(chunkObjectNamePrefix)
            ? "TrackChunk_"
            : chunkObjectNamePrefix;

        generatedTrackLayerName = string.IsNullOrWhiteSpace(generatedTrackLayerName)
            ? "Ground"
            : generatedTrackLayerName;

        railColliderRadiusMultiplier = Mathf.Max(0.1f, railColliderRadiusMultiplier);
        minimumRailColliderRadius = Mathf.Max(0.01f, minimumRailColliderRadius);
    }

    private void LogConfigurationWarnings()
    {
        TrackGenerationValidationUtility.LogWarnings(
            this,
            TrackGenerationValidationUtility.CollectProfileWarnings(generationProfile));

        TrackGenerationValidationUtility.LogWarnings(
            this,
            TrackGenerationValidationUtility.CollectLevelSettingsWarnings(activeSettings));
    }

    private static float GetEndYawOffsetForFeature(
        TrackFeatureType featureType,
        float currentYawOffset)
    {
        return featureType switch
        {
            TrackFeatureType.LateralEnterLeft45 => -45f,
            TrackFeatureType.LateralEnterLeft90 => -90f,
            TrackFeatureType.LateralEnterRight45 => 45f,
            TrackFeatureType.LateralEnterRight90 => 90f,
            TrackFeatureType.LateralReturnToCenterFromLeft45 => 0f,
            TrackFeatureType.LateralReturnToCenterFromLeft90 => 0f,
            TrackFeatureType.LateralReturnToCenterFromRight45 => 0f,
            TrackFeatureType.LateralReturnToCenterFromRight90 => 0f,
            _ => currentYawOffset
        };
    }

    private static TrackLateralState GetEndLateralStateForYaw(float yawOffset)
    {
        if (Mathf.Approximately(yawOffset, 0f))
        {
            return TrackLateralState.Center;
        }

        return yawOffset < 0f
            ? TrackLateralState.Left
            : TrackLateralState.Right;
    }

    private static Vector3 ResolveHorizontalForward(Vector3 forward)
    {
        Vector3 horizontalForward = new Vector3(forward.x, 0f, forward.z);

        if (horizontalForward.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return horizontalForward.normalized;
    }

    private static Vector3 ResolveSafeRight(Vector3 right, Vector3 forward)
    {
        if (right.sqrMagnitude >= 0.0001f)
        {
            return right.normalized;
        }

        Vector3 horizontalForward = new Vector3(forward.x, 0f, forward.z);

        if (horizontalForward.sqrMagnitude < 0.0001f)
        {
            return Vector3.right;
        }

        horizontalForward.Normalize();

        Vector3 resolvedRight = Vector3.Cross(Vector3.up, horizontalForward);

        return resolvedRight.sqrMagnitude < 0.0001f
            ? Vector3.right
            : resolvedRight.normalized;
    }

    private static Quaternion ResolveCapsuleRotation(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return Quaternion.identity;
        }

        Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.95f
            ? Vector3.forward
            : Vector3.up;

        return Quaternion.LookRotation(direction, up);
    }

    #endregion
}