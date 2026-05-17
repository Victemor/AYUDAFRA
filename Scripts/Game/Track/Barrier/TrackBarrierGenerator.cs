using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera barreras cilíndricas laterales y una pared inicial usando el mapa runtime final.
/// 
/// Reglas principales:
/// - Las zonas seguras inicial y final siempre pueden tener barreras en ambos lados.
/// - Los niveles iniciales o bonus pueden forzar barreras completas en toda la pista.
/// - La cobertura general se calcula por distancia jugable real, excluyendo zona segura inicial y final.
/// - Después de los niveles forzados, las barreras generales pueden generarse en un solo lado.
/// - En giros, se prioriza el lado exterior del giro.
/// </summary>
public sealed class TrackBarrierGenerator : MonoBehaviour
{
    #region Constants

    private const string DefaultGeneratedRootName = "GeneratedTrackBarriers";
    private const string DefaultBarrierLayerName = "Barrier";

    private const float MinimumDistanceRangeLength = 0.05f;
    private const float DistanceMergeTolerance = 0.25f;
    private const float SampleDistanceTolerance = 0.001f;

    #endregion

    #region Inspector

    [Header("References")]
    [SerializeField]
    [Tooltip("Generador principal del track. Necesario para reconstruir desde context menu o Start.")]
    private TrackGeneratorController trackGenerator;

    [Header("Generated Root")]
    [SerializeField]
    [Tooltip("Nombre del contenedor raíz de las barreras generadas.")]
    private string generatedRootName = DefaultGeneratedRootName;

    [SerializeField]
    [Tooltip("Layer asignada automáticamente a todas las barreras generadas.")]
    private string barrierLayerName = DefaultBarrierLayerName;

    [Header("Cylindrical Side Barriers")]
    [SerializeField]
    [Tooltip("Material propio usado por las barreras cilíndricas laterales.")]
    private Material cylindricalBarrierMaterial;

    [SerializeField]
    [Tooltip("Radio visual de las barreras cilíndricas laterales.")]
    private float cylindricalBarrierRadius = 0.18f;

    [SerializeField]
    [Tooltip("Cantidad de segmentos radiales de las barreras cilíndricas.")]
    private int cylindricalBarrierRadialSegments = 12;

    [SerializeField]
    [Tooltip("Distancia lateral desde el borde de referencia hasta el centro del cilindro.")]
    private float cylindricalBarrierLateralOffset = 0.18f;

    [SerializeField]
    [Tooltip("Offset vertical del centro del cilindro respecto a la superficie del track.")]
    private float cylindricalBarrierVerticalOffset = 0.45f;

    [SerializeField]
    [Tooltip("Iteraciones de suavizado aplicadas a los anchors de la barrera cilíndrica.")]
    private int cylindricalBarrierSmoothingIterations = 2;

    [Header("Cylindrical Barrier End Posts")]
    [SerializeField]
    [Tooltip("Si está activo, cada tramo de barrera genera postes verticales al inicio y al final.")]
    private bool generateCylindricalBarrierEndPosts = true;

    [SerializeField]
    [Tooltip("Offset vertical de la base de los postes de inicio y final.")]
    private float cylindricalBarrierPostBaseVerticalOffset = 0.02f;

    [Header("Start Wall")]
    [SerializeField]
    [Tooltip("Si está activo, se genera una pared inicial independiente al inicio del nivel.")]
    private bool generateStartWall = true;

    [SerializeField]
    [Tooltip("Material propio usado por la pared inicial.")]
    private Material startWallMaterial;

    [SerializeField]
    [Tooltip("Ancho de la pared inicial. Si está en 0 o menos, usa el ancho del primer sample.")]
    private float startWallWidthOverride = -1f;

    [SerializeField]
    [Tooltip("Altura de la pared inicial.")]
    private float startWallHeight = 2f;

    [SerializeField]
    [Tooltip("Grosor de la pared inicial en dirección del track.")]
    private float startWallThickness = 0.35f;

    [SerializeField]
    [Tooltip("Offset longitudinal desde el primer sample. Valores negativos la colocan antes del inicio.")]
    private float startWallForwardOffset = -0.25f;

    [SerializeField]
    [Tooltip("Offset vertical de la base de la pared inicial.")]
    private float startWallVerticalOffset = 0f;

    [Header("General Distribution")]
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Probabilidad legacy. Se conserva por compatibilidad, pero la cobertura real usa General Coverage Ratio.")]
    private float generalBarrierChance = 0.55f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Porcentaje máximo de la distancia jugable general que puede recibir barreras laterales.")]
    private float generalCoverageRatio = 0.5f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Probabilidad de que una barrera general se genere en ambos lados. Si falla, se genera solo en un lado priorizado por giro.")]
    private float bothSidesChance = 0.35f;

    [Header("Physics")]
    [SerializeField]
    [Tooltip("Si está activo, las barreras generan colliders.")]
    private bool generateColliders = true;

    [SerializeField]
    [Tooltip("Si está activo, las barreras cilíndricas usan CapsuleCollider por segmento.")]
    private bool usePrimitiveCylindricalBarrierColliders = true;

    [SerializeField]
    [Tooltip("Multiplicador del radio físico de las barreras cilíndricas.")]
    private float cylindricalBarrierColliderRadiusMultiplier = 1.35f;

    [SerializeField]
    [Tooltip("Radio físico mínimo de seguridad para los colliders cilíndricos.")]
    private float minimumCylindricalBarrierColliderRadius = 0.12f;

    [SerializeField]
    [Tooltip("Material físico opcional para las barreras cilíndricas.")]
    private PhysicsMaterial cylindricalBarrierPhysicMaterial;

    [SerializeField]
    [Tooltip("Material físico opcional para la pared inicial.")]
    private PhysicsMaterial startWallPhysicMaterial;

    [Header("Build")]
    [SerializeField]
    [Tooltip("Si está activo, reconstruye barreras automáticamente en Start usando el mapa actual del TrackGeneratorController.")]
    private bool rebuildOnStart = false;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("Si está activo, imprime el resumen de cobertura resuelta.")]
    private bool enableCoverageLogs = true;

    #endregion

    #region Runtime

    private bool activeGenerateStartSafeZoneBarriers = true;
    private bool activeGenerateEndSafeZoneBarriers = true;

    private float activeSafeZoneStartLength = 0f;
    private float activeSafeZoneEndLength = 0f;

    /// <summary>
    /// Cobertura inyectada por InfiniteLevelManager.
    /// -1 = usar GeneralCoverageRatio del inspector.
    /// </summary>
    private float activeBarrierCoverageRatio = -1f;

    #endregion

    #region Properties

    /// <summary>
    /// Coverage ratio base del Inspector. Actúa como máximo de barreras generales.
    /// </summary>
    public float GeneralCoverageRatio => Mathf.Clamp01(generalCoverageRatio);

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (rebuildOnStart && trackGenerator != null)
        {
            RebuildFromTrackGenerator();
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Inyecta las longitudes reales de zona segura calculadas por el manager.
    /// </summary>
    public void SetSafeZoneLengths(float startLength, float endLength)
    {
        activeSafeZoneStartLength = Mathf.Max(0f, startLength);
        activeSafeZoneEndLength = Mathf.Max(0f, endLength);
    }

    /// <summary>
    /// Inyecta la cobertura general del siguiente rebuild.
    /// 0 = solo zonas seguras.
    /// 1 = toda la pista válida.
    /// </summary>
    public void SetBarrierProbability(float coverageRatio)
    {
        activeBarrierCoverageRatio = Mathf.Clamp01(coverageRatio);
    }

    /// <summary>
    /// Fuerza barreras completas en toda la pista válida.
    /// Usado para niveles iniciales garantizados y niveles bonus.
    /// </summary>
    public void ForceFullBarriers()
    {
        activeBarrierCoverageRatio = 1f;
    }

    /// <summary>
    /// Reconstruye usando el mapa actual del TrackGeneratorController.
    /// </summary>
    [ContextMenu("Rebuild Barriers From Track Generator")]
    public void RebuildFromTrackGenerator()
    {
        if (trackGenerator == null)
        {
            Debug.LogWarning("[TRACK BARRIERS] TrackGeneratorController no está asignado.", this);
            return;
        }

        Rebuild(trackGenerator.GeneratedMap);
    }

    /// <summary>
    /// Reconstruye usando el mapa runtime y los settings activos.
    /// </summary>
    public void Rebuild(TrackRuntimeMap runtimeMap, LevelGenerationSettings settings)
    {
        if (settings != null)
        {
            activeGenerateStartSafeZoneBarriers = settings.GenerateStartSafeZoneBarriers;
            activeGenerateEndSafeZoneBarriers = settings.GenerateEndSafeZoneBarriers;

            if (settings.SafeStartLengthOverride > 0f)
            {
                activeSafeZoneStartLength = settings.SafeStartLengthOverride;
            }

            if (settings.SafeEndLengthOverride > 0f)
            {
                activeSafeZoneEndLength = settings.SafeEndLengthOverride;
            }
        }

        Rebuild(runtimeMap);
    }

    /// <summary>
    /// Reconstruye todas las barreras a partir del mapa runtime generado.
    /// </summary>
    public void Rebuild(TrackRuntimeMap runtimeMap)
    {
        ClearGeneratedBarriers();
        ValidateInspectorData();

        if (runtimeMap == null ||
            runtimeMap.SurfaceChunks == null ||
            runtimeMap.SurfaceChunks.Count == 0 ||
            runtimeMap.Sections == null ||
            runtimeMap.Sections.Count == 0)
        {
            return;
        }

        float resolvedCoverageRatio = activeBarrierCoverageRatio >= 0f
            ? activeBarrierCoverageRatio
            : generalCoverageRatio;

        resolvedCoverageRatio = Mathf.Clamp01(resolvedCoverageRatio);

        BarrierBuildPlan plan = BuildBarrierPlan(runtimeMap, resolvedCoverageRatio);

        Transform root = GetOrCreateGeneratedRoot();

        BuildBarrierRuns(runtimeMap, plan.LeftRuns, TrackBarrierSide.Left, root);
        BuildBarrierRuns(runtimeMap, plan.RightRuns, TrackBarrierSide.Right, root);

        if (generateStartWall)
        {
            BuildStartWall(runtimeMap.SurfaceChunks, root);
        }

        if (enableCoverageLogs)
        {
            Debug.Log(
                $"[TRACK BARRIERS] Cobertura resuelta.\n" +
                $"  Total distance              : {plan.TotalDistance:F1}\n" +
                $"  Safe start length           : {plan.SafeStartLength:F1}\n" +
                $"  Safe end length             : {plan.SafeEndLength:F1}\n" +
                $"  Playable general distance   : {plan.GeneralPlayableDistance:F1}\n" +
                $"  Target general coverage     : {resolvedCoverageRatio:P0}\n" +
                $"  Target general distance     : {plan.TargetGeneralDistance:F1}\n" +
                $"  Selected general distance   : {plan.SelectedGeneralDistance:F1}\n" +
                $"  Safe ranges selected        : {plan.SafeRangeCount}\n" +
                $"  General ranges selected     : {plan.GeneralRangeCount}\n" +
                $"  Left runs                   : {plan.LeftRuns.Count}\n" +
                $"  Right runs                  : {plan.RightRuns.Count}",
                this);
        }
    }

    /// <summary>
    /// Elimina todas las barreras generadas.
    /// </summary>
    [ContextMenu("Clear Generated Barriers")]
    public void ClearGeneratedBarriers()
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

    #region Build Plan

    private BarrierBuildPlan BuildBarrierPlan(TrackRuntimeMap runtimeMap, float coverageRatio)
    {
        BarrierBuildPlan plan = new BarrierBuildPlan();

        float totalDistance = ResolveTotalDistance(runtimeMap);
        float safeStartLength = Mathf.Clamp(activeSafeZoneStartLength, 0f, totalDistance);
        float safeEndLength = Mathf.Clamp(activeSafeZoneEndLength, 0f, totalDistance);

        float generalStart = safeStartLength;
        float generalEnd = Mathf.Max(generalStart, totalDistance - safeEndLength);

        plan.TotalDistance = totalDistance;
        plan.SafeStartLength = safeStartLength;
        plan.SafeEndLength = safeEndLength;

        List<BarrierDistanceRange> validRanges = BuildValidSurfaceRanges(runtimeMap.Sections);

        bool forceFullCoverage = coverageRatio >= 0.999f;

        if (forceFullCoverage)
        {
            for (int i = 0; i < validRanges.Count; i++)
            {
                AddRangeToBothSides(plan, validRanges[i], isSafeRange: true);
            }

            plan.GeneralPlayableDistance = SumClippedLength(validRanges, generalStart, generalEnd);
            plan.TargetGeneralDistance = plan.GeneralPlayableDistance;
            plan.SelectedGeneralDistance = plan.GeneralPlayableDistance;
            plan.LeftRuns = MergeRanges(plan.LeftRuns);
            plan.RightRuns = MergeRanges(plan.RightRuns);
            return plan;
        }

        if (activeGenerateStartSafeZoneBarriers && safeStartLength > 0f)
        {
            List<BarrierDistanceRange> safeStartRanges = ClipRanges(validRanges, 0f, safeStartLength);

            for (int i = 0; i < safeStartRanges.Count; i++)
            {
                AddRangeToBothSides(plan, safeStartRanges[i], isSafeRange: true);
            }
        }

        if (activeGenerateEndSafeZoneBarriers && safeEndLength > 0f)
        {
            List<BarrierDistanceRange> safeEndRanges = ClipRanges(validRanges, totalDistance - safeEndLength, totalDistance);

            for (int i = 0; i < safeEndRanges.Count; i++)
            {
                AddRangeToBothSides(plan, safeEndRanges[i], isSafeRange: true);
            }
        }

        List<BarrierDistanceRange> generalRanges = ClipRanges(validRanges, generalStart, generalEnd);

        float generalPlayableDistance = SumLength(generalRanges);
        float targetGeneralDistance = generalPlayableDistance * coverageRatio;

        plan.GeneralPlayableDistance = generalPlayableDistance;
        plan.TargetGeneralDistance = targetGeneralDistance;

        if (targetGeneralDistance > 0f && generalRanges.Count > 0)
        {
            List<BarrierDistanceRange> selectedGeneralRanges = PickGeneralRanges(
                runtimeMap,
                generalRanges,
                targetGeneralDistance);

            System.Random sideRandom = new System.Random(runtimeMap.GeneratedSeed + 44191);

            for (int i = 0; i < selectedGeneralRanges.Count; i++)
            {
                BarrierDistanceRange range = selectedGeneralRanges[i];
                plan.SelectedGeneralDistance += range.Length;
                plan.GeneralRangeCount++;

                TrackBarrierSidePreference preference = ResolveSidePreference(runtimeMap.Sections, range);

                bool useBothSides = sideRandom.NextDouble() <= bothSidesChance;

                if (useBothSides || preference == TrackBarrierSidePreference.Both)
                {
                    plan.LeftRuns.Add(range);
                    plan.RightRuns.Add(range);
                    continue;
                }

                if (preference == TrackBarrierSidePreference.Left)
                {
                    plan.LeftRuns.Add(range);
                }
                else
                {
                    plan.RightRuns.Add(range);
                }
            }
        }

        plan.LeftRuns = MergeRanges(plan.LeftRuns);
        plan.RightRuns = MergeRanges(plan.RightRuns);

        return plan;
    }

    private static List<BarrierDistanceRange> BuildValidSurfaceRanges(IReadOnlyList<TrackSectionDefinition> sections)
    {
        List<BarrierDistanceRange> ranges = new List<BarrierDistanceRange>();

        if (sections == null)
        {
            return ranges;
        }

        for (int i = 0; i < sections.Count; i++)
        {
            TrackSectionDefinition section = sections[i];

            if (!IsSectionValidForBarrier(section))
            {
                continue;
            }

            ranges.Add(new BarrierDistanceRange(section.StartDistance, section.EndDistance));
        }

        return MergeRanges(ranges);
    }

    private static bool IsSectionValidForBarrier(TrackSectionDefinition section)
    {
        if (!section.HasSurface)
        {
            return false;
        }

        if (section.StructureType == TrackStructureType.Gap)
        {
            return false;
        }

        return section.EndDistance - section.StartDistance > MinimumDistanceRangeLength;
    }

    private static List<BarrierDistanceRange> ClipRanges(
        IReadOnlyList<BarrierDistanceRange> ranges,
        float clipStart,
        float clipEnd)
    {
        List<BarrierDistanceRange> clipped = new List<BarrierDistanceRange>();

        if (ranges == null || clipEnd <= clipStart)
        {
            return clipped;
        }

        for (int i = 0; i < ranges.Count; i++)
        {
            float start = Mathf.Max(ranges[i].StartDistance, clipStart);
            float end = Mathf.Min(ranges[i].EndDistance, clipEnd);

            if (end - start <= MinimumDistanceRangeLength)
            {
                continue;
            }

            clipped.Add(new BarrierDistanceRange(start, end));
        }

        return clipped;
    }

    private static List<BarrierDistanceRange> PickGeneralRanges(
        TrackRuntimeMap runtimeMap,
        List<BarrierDistanceRange> candidateRanges,
        float targetDistance)
    {
        List<BarrierDistanceRange> result = new List<BarrierDistanceRange>();

        if (candidateRanges == null || candidateRanges.Count == 0 || targetDistance <= 0f)
        {
            return result;
        }

        List<BarrierDistanceRange> shuffled = new List<BarrierDistanceRange>(candidateRanges);
        System.Random random = new System.Random(runtimeMap.GeneratedSeed + 99173);

        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);
            BarrierDistanceRange temp = shuffled[i];
            shuffled[i] = shuffled[j];
            shuffled[j] = temp;
        }

        float selectedDistance = 0f;

        for (int i = 0; i < shuffled.Count; i++)
        {
            if (selectedDistance >= targetDistance)
            {
                break;
            }

            BarrierDistanceRange range = shuffled[i];
            float remaining = targetDistance - selectedDistance;

            if (range.Length <= remaining + MinimumDistanceRangeLength)
            {
                result.Add(range);
                selectedDistance += range.Length;
                continue;
            }

            float partialLength = Mathf.Max(MinimumDistanceRangeLength, remaining);
            float maxStart = Mathf.Max(range.StartDistance, range.EndDistance - partialLength);
            float start = RandomRange(random, range.StartDistance, maxStart);
            float end = Mathf.Min(range.EndDistance, start + partialLength);

            if (end - start > MinimumDistanceRangeLength)
            {
                result.Add(new BarrierDistanceRange(start, end));
                selectedDistance += end - start;
            }
        }

        return MergeRanges(result);
    }

    private static void AddRangeToBothSides(
        BarrierBuildPlan plan,
        BarrierDistanceRange range,
        bool isSafeRange)
    {
        if (range.Length <= MinimumDistanceRangeLength)
        {
            return;
        }

        plan.LeftRuns.Add(range);
        plan.RightRuns.Add(range);

        if (isSafeRange)
        {
            plan.SafeRangeCount++;
        }
    }

    private static List<BarrierDistanceRange> MergeRanges(List<BarrierDistanceRange> ranges)
    {
        List<BarrierDistanceRange> merged = new List<BarrierDistanceRange>();

        if (ranges == null || ranges.Count == 0)
        {
            return merged;
        }

        ranges.Sort((a, b) => a.StartDistance.CompareTo(b.StartDistance));

        BarrierDistanceRange current = ranges[0];

        for (int i = 1; i < ranges.Count; i++)
        {
            BarrierDistanceRange next = ranges[i];

            if (next.StartDistance <= current.EndDistance + DistanceMergeTolerance)
            {
                current = new BarrierDistanceRange(
                    current.StartDistance,
                    Mathf.Max(current.EndDistance, next.EndDistance));
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }

        merged.Add(current);
        return merged;
    }

    private static float SumLength(IReadOnlyList<BarrierDistanceRange> ranges)
    {
        float total = 0f;

        if (ranges == null)
        {
            return total;
        }

        for (int i = 0; i < ranges.Count; i++)
        {
            total += ranges[i].Length;
        }

        return total;
    }

    private static float SumClippedLength(
        IReadOnlyList<BarrierDistanceRange> ranges,
        float clipStart,
        float clipEnd)
    {
        return SumLength(ClipRanges(ranges, clipStart, clipEnd));
    }

    #endregion

    #region Side Preference

    private static TrackBarrierSidePreference ResolveSidePreference(
        IReadOnlyList<TrackSectionDefinition> sections,
        BarrierDistanceRange range)
    {
        float centerDistance = (range.StartDistance + range.EndDistance) * 0.5f;

        if (!TryFindSectionAtDistance(sections, centerDistance, out TrackSectionDefinition section))
        {
            return TrackBarrierSidePreference.Both;
        }

        if (section.TurnAngleDegrees > 0.001f)
        {
            return TrackBarrierSidePreference.Right;
        }

        if (section.TurnAngleDegrees < -0.001f)
        {
            return TrackBarrierSidePreference.Left;
        }

        return TrackBarrierSidePreference.Both;
    }

    private static bool TryFindSectionAtDistance(
        IReadOnlyList<TrackSectionDefinition> sections,
        float distance,
        out TrackSectionDefinition resolvedSection)
    {
        resolvedSection = default;

        if (sections == null || sections.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < sections.Count; i++)
        {
            TrackSectionDefinition section = sections[i];

            if (distance >= section.StartDistance && distance <= section.EndDistance)
            {
                resolvedSection = section;
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Build Barrier Runs

    private void BuildBarrierRuns(
        TrackRuntimeMap runtimeMap,
        IReadOnlyList<BarrierDistanceRange> runs,
        TrackBarrierSide side,
        Transform root)
    {
        if (runs == null)
        {
            return;
        }

        for (int i = 0; i < runs.Count; i++)
        {
            BuildSingleDistanceRun(runtimeMap, runs[i], side, root, i);
        }
    }

    private void BuildSingleDistanceRun(
        TrackRuntimeMap runtimeMap,
        BarrierDistanceRange run,
        TrackBarrierSide side,
        Transform root,
        int runIndex)
    {
        List<TrackSurfaceChunkDefinition> clippedChunks = BuildClippedChunks(runtimeMap.SurfaceChunks, run);

        if (clippedChunks.Count == 0)
        {
            return;
        }

        Mesh mesh = TrackBarrierMeshBuilder.BuildBarrierMesh(
            clippedChunks,
            0,
            clippedChunks.Count - 1,
            clippedChunks.Count,
            side,
            cylindricalBarrierLateralOffset,
            cylindricalBarrierRadius,
            cylindricalBarrierVerticalOffset,
            cylindricalBarrierRadialSegments,
            cylindricalBarrierSmoothingIterations);

        if (mesh == null || mesh.vertexCount == 0)
        {
            return;
        }

        GameObject barrierObject = new GameObject(
            $"CylindricalBarrier_{side}_{runIndex:D3}_{run.StartDistance:F0}m_{run.EndDistance:F0}m");

        AssignLayer(barrierObject);

        barrierObject.transform.SetParent(root);
        barrierObject.transform.localPosition = Vector3.zero;
        barrierObject.transform.localRotation = Quaternion.identity;
        barrierObject.transform.localScale = Vector3.one;

        MeshFilter meshFilter = barrierObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = barrierObject.AddComponent<MeshRenderer>();

        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = cylindricalBarrierMaterial;

        if (!generateColliders)
        {
            return;
        }

        if (usePrimitiveCylindricalBarrierColliders)
        {
            CreateCylindricalBarrierPrimitiveColliders(clippedChunks, side, barrierObject.transform);

            if (generateCylindricalBarrierEndPosts)
            {
                CreateCylindricalBarrierEndPostColliders(clippedChunks, side, barrierObject.transform);
            }

            return;
        }

        MeshCollider meshCollider = barrierObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
        meshCollider.sharedMaterial = cylindricalBarrierPhysicMaterial;
    }

    private static List<TrackSurfaceChunkDefinition> BuildClippedChunks(
        IReadOnlyList<TrackSurfaceChunkDefinition> sourceChunks,
        BarrierDistanceRange run)
    {
        List<TrackSurfaceChunkDefinition> result = new List<TrackSurfaceChunkDefinition>();

        if (sourceChunks == null)
        {
            return result;
        }

        int chunkIndex = 0;

        for (int i = 0; i < sourceChunks.Count; i++)
        {
            TrackSurfaceChunkDefinition sourceChunk = sourceChunks[i];

            if (sourceChunk == null ||
                sourceChunk.Samples == null ||
                sourceChunk.Samples.Count < 2)
            {
                continue;
            }

            if (sourceChunk.StructureType == TrackStructureType.Gap)
            {
                continue;
            }

            if (sourceChunk.EndDistance < run.StartDistance ||
                sourceChunk.StartDistance > run.EndDistance)
            {
                continue;
            }

            List<TrackLayoutSamplePoint> samples = ClipChunkSamples(sourceChunk, run.StartDistance, run.EndDistance);

            if (samples.Count < 2)
            {
                continue;
            }

            float startDistance = samples[0].Distance;
            float endDistance = samples[samples.Count - 1].Distance;

            if (endDistance - startDistance <= MinimumDistanceRangeLength)
            {
                continue;
            }

            result.Add(new TrackSurfaceChunkDefinition(
                chunkIndex,
                startDistance,
                endDistance,
                sourceChunk.StructureType,
                samples));

            chunkIndex++;
        }

        return result;
    }

    private static List<TrackLayoutSamplePoint> ClipChunkSamples(
        TrackSurfaceChunkDefinition chunk,
        float clipStart,
        float clipEnd)
    {
        List<TrackLayoutSamplePoint> samples = new List<TrackLayoutSamplePoint>();

        TrackLayoutSamplePoint startSample = TrackChunkSampler.SampleAtDistance(chunk, Mathf.Max(chunk.StartDistance, clipStart));
        TrackLayoutSamplePoint endSample = TrackChunkSampler.SampleAtDistance(chunk, Mathf.Min(chunk.EndDistance, clipEnd));

        AddSampleIfDistinct(samples, startSample);

        for (int i = 0; i < chunk.Samples.Count; i++)
        {
            TrackLayoutSamplePoint sample = chunk.Samples[i];

            if (sample.Distance <= clipStart + SampleDistanceTolerance)
            {
                continue;
            }

            if (sample.Distance >= clipEnd - SampleDistanceTolerance)
            {
                continue;
            }

            AddSampleIfDistinct(samples, sample);
        }

        AddSampleIfDistinct(samples, endSample);

        return samples;
    }

    private static void AddSampleIfDistinct(
        List<TrackLayoutSamplePoint> samples,
        TrackLayoutSamplePoint sample)
    {
        if (sample.Forward.sqrMagnitude <= 0.0001f ||
            sample.Right.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (samples.Count == 0)
        {
            samples.Add(sample);
            return;
        }

        TrackLayoutSamplePoint last = samples[samples.Count - 1];

        if (Mathf.Abs(last.Distance - sample.Distance) <= SampleDistanceTolerance ||
            Vector3.Distance(last.Position, sample.Position) <= SampleDistanceTolerance)
        {
            return;
        }

        samples.Add(sample);
    }

    #endregion

    #region Primitive Colliders

    private void CreateCylindricalBarrierPrimitiveColliders(
        IReadOnlyList<TrackSurfaceChunkDefinition> chunks,
        TrackBarrierSide side,
        Transform parent)
    {
        List<Vector3> centers = CollectCylindricalBarrierCenters(chunks, side);

        if (centers.Count < 2)
        {
            return;
        }

        float radius = ResolvePhysicalBarrierRadius();

        for (int i = 0; i < centers.Count - 1; i++)
        {
            Vector3 start = centers[i];
            Vector3 end = centers[i + 1];
            Vector3 segment = end - start;
            float segmentLength = segment.magnitude;

            if (segmentLength <= 0.05f)
            {
                continue;
            }

            CreateCapsuleColliderSegment(
                parent,
                start,
                end,
                radius,
                segmentLength,
                $"CylindricalBarrierCollider_{side}_{i:D3}");
        }
    }

    private void CreateCylindricalBarrierEndPostColliders(
        IReadOnlyList<TrackSurfaceChunkDefinition> chunks,
        TrackBarrierSide side,
        Transform parent)
    {
        List<Vector3> centers = CollectCylindricalBarrierCenters(chunks, side);

        if (centers.Count < 2)
        {
            return;
        }

        float radius = ResolvePhysicalBarrierRadius();

        CreateVerticalPostCollider(parent, centers[0], radius, $"CylindricalBarrierStartPostCollider_{side}");
        CreateVerticalPostCollider(parent, centers[centers.Count - 1], radius, $"CylindricalBarrierEndPostCollider_{side}");
    }

    private void CreateVerticalPostCollider(Transform parent, Vector3 topCenter, float radius, string objectName)
    {
        float bottomY = topCenter.y - cylindricalBarrierVerticalOffset + cylindricalBarrierPostBaseVerticalOffset;
        Vector3 bottomCenter = new Vector3(topCenter.x, bottomY, topCenter.z);

        Vector3 segment = topCenter - bottomCenter;
        float segmentLength = segment.magnitude;

        if (segmentLength <= 0.05f)
        {
            return;
        }

        CreateCapsuleColliderSegment(parent, bottomCenter, topCenter, radius, segmentLength, objectName);
    }

    private List<Vector3> CollectCylindricalBarrierCenters(
        IReadOnlyList<TrackSurfaceChunkDefinition> chunks,
        TrackBarrierSide side)
    {
        List<Vector3> centers = new List<Vector3>();

        if (chunks == null || chunks.Count == 0)
        {
            return centers;
        }

        float sideSign = side == TrackBarrierSide.Right ? 1f : -1f;

        for (int i = 0; i < chunks.Count; i++)
        {
            TrackSurfaceChunkDefinition chunk = chunks[i];

            if (chunk == null || chunk.Samples == null)
            {
                continue;
            }

            for (int j = 0; j < chunk.Samples.Count; j++)
            {
                TrackLayoutSamplePoint sample = chunk.Samples[j];

                Vector3 right = ResolveSafeRight(sample.Right, sample.Forward);
                float halfReferenceWidth = ResolveHalfReferenceWidth(sample);
                float resolvedOffset = halfReferenceWidth + cylindricalBarrierLateralOffset;

                Vector3 center = sample.Position
                                 + right * resolvedOffset * sideSign
                                 + Vector3.up * cylindricalBarrierVerticalOffset;

                if (centers.Count > 0 &&
                    Vector3.Distance(centers[centers.Count - 1], center) <= 0.001f)
                {
                    continue;
                }

                centers.Add(center);
            }
        }

        SmoothColliderCenters(centers, cylindricalBarrierSmoothingIterations);

        return centers;
    }

    private static void SmoothColliderCenters(List<Vector3> centers, int iterations)
    {
        if (centers == null || centers.Count < 3 || iterations <= 0)
        {
            return;
        }

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            List<Vector3> copy = new List<Vector3>(centers);

            for (int i = 1; i < centers.Count - 1; i++)
            {
                centers[i] = (copy[i - 1] + copy[i] + copy[i + 1]) / 3f;
            }
        }
    }

    private void CreateCapsuleColliderSegment(
        Transform parent,
        Vector3 start,
        Vector3 end,
        float radius,
        float segmentLength,
        string objectName)
    {
        Vector3 direction = (end - start).normalized;
        Vector3 midPoint = (start + end) * 0.5f;

        GameObject capsuleObject = new GameObject(objectName);
        AssignLayer(capsuleObject);

        capsuleObject.transform.SetParent(parent);
        capsuleObject.transform.position = midPoint;
        capsuleObject.transform.rotation = ResolveCapsuleRotation(direction);
        capsuleObject.transform.localScale = Vector3.one;

        CapsuleCollider capsule = capsuleObject.AddComponent<CapsuleCollider>();
        capsule.radius = radius;
        capsule.height = segmentLength + radius * 2f;
        capsule.direction = 2;

        if (cylindricalBarrierPhysicMaterial != null)
        {
            capsule.sharedMaterial = cylindricalBarrierPhysicMaterial;
        }
    }

    #endregion

    #region Start Wall

    private void BuildStartWall(IReadOnlyList<TrackSurfaceChunkDefinition> chunks, Transform root)
    {
        if (!TryGetFirstValidSample(chunks, out TrackLayoutSamplePoint firstSample))
        {
            return;
        }

        GameObject wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallObject.name = "StartWall";
        AssignLayer(wallObject);

        wallObject.transform.SetParent(root);

        Vector3 forward = ResolveSafeForward(firstSample.Forward);

        float wallWidth = startWallWidthOverride > 0f
            ? startWallWidthOverride
            : firstSample.Width;

        Vector3 position = firstSample.Position
                           + forward * startWallForwardOffset
                           + Vector3.up * (startWallVerticalOffset + startWallHeight * 0.5f);

        wallObject.transform.position = position;
        wallObject.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        wallObject.transform.localScale = new Vector3(
            Mathf.Max(0.01f, wallWidth),
            Mathf.Max(0.01f, startWallHeight),
            Mathf.Max(0.01f, startWallThickness));

        MeshRenderer meshRenderer = wallObject.GetComponent<MeshRenderer>();

        if (meshRenderer != null)
        {
            meshRenderer.sharedMaterial = startWallMaterial;
        }

        Collider collider = wallObject.GetComponent<Collider>();

        if (collider == null)
        {
            return;
        }

        if (!generateColliders)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(collider);
            }
            else
            {
                Destroy(collider);
            }
#else
            Destroy(collider);
#endif
            return;
        }

        collider.sharedMaterial = startWallPhysicMaterial;
    }

    #endregion

    #region Helpers

    private static bool TryGetFirstValidSample(
        IReadOnlyList<TrackSurfaceChunkDefinition> chunks,
        out TrackLayoutSamplePoint sample)
    {
        sample = default;

        if (chunks == null)
        {
            return false;
        }

        for (int i = 0; i < chunks.Count; i++)
        {
            TrackSurfaceChunkDefinition chunk = chunks[i];

            if (chunk == null || chunk.Samples == null || chunk.Samples.Count == 0)
            {
                continue;
            }

            sample = chunk.Samples[0];
            return true;
        }

        return false;
    }

    private static float ResolveTotalDistance(TrackRuntimeMap runtimeMap)
    {
        if (runtimeMap.PathSampler != null)
        {
            return runtimeMap.PathSampler.TotalDistance;
        }

        IReadOnlyList<TrackSurfaceChunkDefinition> chunks = runtimeMap.SurfaceChunks;

        if (chunks == null || chunks.Count == 0)
        {
            return 0f;
        }

        return chunks[chunks.Count - 1].EndDistance;
    }

    private Transform GetOrCreateGeneratedRoot()
    {
        Transform existingRoot = transform.Find(generatedRootName);

        if (existingRoot != null)
        {
            return existingRoot;
        }

        GameObject root = new GameObject(generatedRootName);
        AssignLayer(root);

        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        return root.transform;
    }

    private void AssignLayer(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        int layer = LayerMask.NameToLayer(barrierLayerName);

        if (layer < 0)
        {
            Debug.LogWarning($"[TRACK BARRIERS] La layer '{barrierLayerName}' no existe.", this);
            return;
        }

        target.layer = layer;
    }

    private static float ResolveHalfReferenceWidth(TrackLayoutSamplePoint sample)
    {
        if (sample.StructureType == TrackStructureType.RailTrack)
        {
            float railSeparation = Mathf.Max(0f, sample.RailSeparation);
            float railWidth = Mathf.Max(0f, sample.RailWidth);

            if (railSeparation <= 0.0001f || railWidth <= 0.0001f)
            {
                return Mathf.Max(0f, sample.Width) * 0.5f;
            }

            return railSeparation * 0.5f + railWidth * 0.5f;
        }

        return Mathf.Max(0f, sample.Width) * 0.5f;
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

    private static Vector3 ResolveSafeForward(Vector3 forward)
    {
        return forward.sqrMagnitude >= 0.0001f ? forward.normalized : Vector3.forward;
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

    private float ResolvePhysicalBarrierRadius()
    {
        return Mathf.Max(
            minimumCylindricalBarrierColliderRadius,
            cylindricalBarrierRadius * cylindricalBarrierColliderRadiusMultiplier);
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        if (max <= min)
        {
            return min;
        }

        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private void ValidateInspectorData()
    {
        generatedRootName = string.IsNullOrWhiteSpace(generatedRootName)
            ? DefaultGeneratedRootName
            : generatedRootName;

        barrierLayerName = string.IsNullOrWhiteSpace(barrierLayerName)
            ? DefaultBarrierLayerName
            : barrierLayerName;

        cylindricalBarrierRadius = Mathf.Max(0.01f, cylindricalBarrierRadius);
        cylindricalBarrierRadialSegments = Mathf.Max(3, cylindricalBarrierRadialSegments);
        cylindricalBarrierLateralOffset = Mathf.Max(0f, cylindricalBarrierLateralOffset);
        cylindricalBarrierSmoothingIterations = Mathf.Max(0, cylindricalBarrierSmoothingIterations);
        cylindricalBarrierPostBaseVerticalOffset = Mathf.Max(0f, cylindricalBarrierPostBaseVerticalOffset);
        cylindricalBarrierColliderRadiusMultiplier = Mathf.Max(0.1f, cylindricalBarrierColliderRadiusMultiplier);
        minimumCylindricalBarrierColliderRadius = Mathf.Max(0.01f, minimumCylindricalBarrierColliderRadius);

        startWallHeight = Mathf.Max(0.01f, startWallHeight);
        startWallThickness = Mathf.Max(0.01f, startWallThickness);

        generalBarrierChance = Mathf.Clamp01(generalBarrierChance);
        generalCoverageRatio = Mathf.Clamp01(generalCoverageRatio);
        bothSidesChance = Mathf.Clamp01(bothSidesChance);
    }

    #endregion

    #region Private Types

    private enum TrackBarrierSidePreference
    {
        Both = 0,
        Left = 1,
        Right = 2
    }

    private readonly struct BarrierDistanceRange
    {
        public float StartDistance { get; }
        public float EndDistance { get; }
        public float Length => Mathf.Max(0f, EndDistance - StartDistance);

        public BarrierDistanceRange(float startDistance, float endDistance)
        {
            StartDistance = Mathf.Min(startDistance, endDistance);
            EndDistance = Mathf.Max(startDistance, endDistance);
        }
    }

    private sealed class BarrierBuildPlan
    {
        public float TotalDistance;
        public float SafeStartLength;
        public float SafeEndLength;
        public float GeneralPlayableDistance;
        public float TargetGeneralDistance;
        public float SelectedGeneralDistance;
        public int SafeRangeCount;
        public int GeneralRangeCount;

        public List<BarrierDistanceRange> LeftRuns = new List<BarrierDistanceRange>();
        public List<BarrierDistanceRange> RightRuns = new List<BarrierDistanceRange>();
    }

    #endregion
}