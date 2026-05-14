using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generador procedural de obstáculos, monedas y meta sobre el track.
///
/// En modo de progresión infinita, los settings de contenido se inyectan desde
/// <see cref="InfiniteLevelManager"/> mediante <see cref="GenerateContent(LevelContentGenerationSettings)"/>.
/// No existe ninguna referencia a LevelContentGenerationSettings en el Inspector de este componente.
/// </summary>
public sealed class TrackContentGenerator : MonoBehaviour
{
    #region Inspector

    [Header("References")]
    [Tooltip("Generador principal del track.")]
    [SerializeField] private TrackGeneratorController trackGenerator;

    [Tooltip("Catálogo global de prefabs y valores base. Sus valores Base son el máximo de dificultad.")]
    [SerializeField] private TrackContentGenerationProfile contentProfile;

    [Header("Output")]
    [Tooltip("Nombre del objeto raíz donde se instanciará todo el contenido generado.")]
    [SerializeField] private string generatedRootName = "GeneratedTrackContent";

    [Tooltip("Nombre del objeto hijo donde se guardarán los objetos inactivos del pool local.")]
    [SerializeField] private string poolRootName = "_Pool";

    [Tooltip("Si está activo, genera contenido automáticamente al iniciar la escena. InfiniteLevelManager lo desactiva en su Awake.")]
    [SerializeField] private bool generateOnStart = true;

    [Header("Placement Correction")]
    [Tooltip("Si está activo, alinea la parte inferior del collider del objeto con la superficie del track después de spawnearlo.")]
    [SerializeField] private bool alignSpawnedObjectsToTrackSurface = true;

    [Tooltip("Offset vertical mínimo aplicado después de alinear el collider con la pista. Útil para evitar clipping visual.")]
    [SerializeField, Min(0f)] private float defaultSurfaceContactOffset = 0.01f;

    [Tooltip("Si está activo, resetea velocidades de Rigidbody al spawnear objetos desde pool.")]
    [SerializeField] private bool resetRigidbodyOnSpawn = true;

    [Header("Debug")]
    [Tooltip("Imprime información básica de generación en consola.")]
    [SerializeField] private bool enableDebugLogs;

    #endregion

    #region Runtime

    private readonly TrackSpawnReservationMap reservationMap = new TrackSpawnReservationMap();

    private TrackGeneratedObjectPool objectPool;
    private Transform generatedRoot;
    private Transform poolRoot;
    private GoalSpawnData resolvedGoalData;

    /// <summary>
    /// Settings de contenido activos en la generación actual.
    /// Inyectados por <see cref="InfiniteLevelManager"/> vía <see cref="GenerateContent(LevelContentGenerationSettings)"/>.
    /// </summary>
    private LevelContentGenerationSettings levelSettings;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateContent();
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Desactiva la generación automática de contenido al inicio de escena.
    /// <see cref="InfiniteLevelManager"/> lo llama en su Awake para tomar control del ciclo.
    /// </summary>
    public void DisableAutoGeneration()
    {
        generateOnStart = false;
    }

    /// <summary>
    /// Inyecta los settings de contenido del nivel actual y genera todo el contenido procedural.
    /// Punto de entrada principal desde <see cref="InfiniteLevelManager"/>.
    /// </summary>
    /// <param name="settings">Settings de contenido configurados para el nivel actual.</param>
    public void GenerateContent(LevelContentGenerationSettings settings)
    {
        levelSettings = settings;
        GenerateContent();
    }

    /// <summary>
    /// Genera todo el contenido procedural sobre el track actual usando los settings activos.
    /// </summary>
    [ContextMenu("Generate Track Content")]
    public void GenerateContent()
    {
        if (!CanGenerate())
        {
            return;
        }

        EnsureRootsAndPool();
        ClearGeneratedContent();

        reservationMap.Clear();

        TrackRuntimeMap map = trackGenerator.GeneratedMap;
        System.Random random = new System.Random(map.GeneratedSeed + 931);

        resolvedGoalData = ResolveGoalSpawnData(map);

        ReserveGoalArea();
        GenerateObstacles(map, generatedRoot, random);
        GenerateCoins(map, generatedRoot, random);
        InstantiateGoal(generatedRoot);

        if (enableDebugLogs)
        {
            Debug.Log("[TRACK CONTENT] Content generated.", this);
        }
    }

    /// <summary>
    /// Desactiva todo el contenido activo generado por este componente.
    /// </summary>
    [ContextMenu("Clear Track Content")]
    public void ClearGeneratedContent()
    {
        EnsureRootsAndPool();
        objectPool.DespawnAllActive();
    }

    /// <summary>
    /// Expone el mapa de reservas del nivel generado.
    /// PowerUpGenerator lo consume para evitar solapar coleccionables con monedas y obstáculos.
    /// Solo es válido después de llamar a GenerateContent().
    /// </summary>
    public TrackSpawnReservationMap ReservationMap => reservationMap;

    #endregion

    #region Setup

    private void EnsureRootsAndPool()
    {
        generatedRoot = GetOrCreateChild(transform, generatedRootName);
        poolRoot = GetOrCreateChild(generatedRoot, poolRootName);

        if (objectPool == null)
        {
            objectPool = new TrackGeneratedObjectPool(poolRoot);
        }
    }

    private static Transform GetOrCreateChild(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);

        if (existing != null)
        {
            return existing;
        }

        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;

        return child.transform;
    }

    #endregion

    #region Obstacle Generation

    private void GenerateObstacles(TrackRuntimeMap map, Transform root, System.Random random)
    {
        IReadOnlyList<TrackSectionDefinition> sections = map.Sections;

        int obstacleLimit = levelSettings != null ? levelSettings.MaxObstacleCount : 0;
        int obstacleCount = 0;

        for (int i = 0; i < sections.Count; i++)
        {
            if (obstacleLimit > 0 && obstacleCount >= obstacleLimit)
            {
                break;
            }

            TrackSectionDefinition section = sections[i];

            if (!section.HasSurface || section.StructureType == TrackStructureType.Gap)
            {
                continue;
            }

            float start = Mathf.Max(section.StartDistance, contentProfile.StartContentPadding);
            float end = section.EndDistance - contentProfile.EndContentPadding;

            if (end <= start)
            {
                continue;
            }

            for (float distance = start; distance <= end; distance += contentProfile.ObstacleEvaluationStep)
            {
                if (obstacleLimit > 0 && obstacleCount >= obstacleLimit)
                {
                    break;
                }

                bool spawned = TryGenerateSectionContent(map, sections, i, section, distance, root, random);

                if (spawned)
                {
                    obstacleCount++;
                }
            }
        }
    }

    private bool TryGenerateSectionContent(
        TrackRuntimeMap map,
        IReadOnlyList<TrackSectionDefinition> sections,
        int sectionIndex,
        TrackSectionDefinition section,
        float distance,
        Transform root,
        System.Random random)
    {
        if (IsFlatSolid(section))
        {
            if (IsEnabled(Category.Walls)
                && HasPrefabsFor(Category.Walls)
                && TryRoll(random, GetWallChance()))
            {
                return TrySpawnCentered(
                    map,
                    distance,
                    GetEntries(Category.Walls),
                    GetOverrides(Category.Walls),
                    root,
                    random,
                    TrackSpawnPriority.High);
            }

            if (IsEnabled(Category.Boxes)
                && HasPrefabsFor(Category.Boxes)
                && TryRoll(random, GetBoxChance()))
            {
                return TrySpawnBoxRow(map, section, distance, root, random);
            }

            if (IsEnabled(Category.Balls)
                && HasPrefabsFor(Category.Balls)
                && TryRoll(random, GetBallFlatChance()))
            {
                return TrySpawnRandomInsideTrack(
                    map,
                    section,
                    distance,
                    GetEntries(Category.Balls),
                    GetOverrides(Category.Balls),
                    root,
                    random,
                    TrackSpawnPriority.Medium);
            }

            if (IsEnabled(Category.Fans)
                && HasPrefabsFor(Category.Fans)
                && TryRoll(random, GetFanFlatChance()))
            {
                return TrySpawnFan(map, section, distance, root, random);
            }
        }

        if (IsNarrowSolid(section))
        {
            if (IsEnabled(Category.Boxes)
                && HasPrefabsFor(Category.Boxes)
                && TryRoll(random, GetBoxChance()))
            {
                return TrySpawnSingleBoxAtCenter(map, distance, root, random);
            }

            if (IsEnabled(Category.Balls)
                && HasPrefabsFor(Category.Balls)
                && TryRoll(random, GetBallNarrowChance()))
            {
                return TrySpawnCentered(
                    map,
                    distance,
                    GetEntries(Category.Balls),
                    GetOverrides(Category.Balls),
                    root,
                    random,
                    TrackSpawnPriority.Medium);
            }
        }

        if (IsRail(section))
        {
            if (IsEnabled(Category.Balls)
                && HasPrefabsFor(Category.Balls)
                && TryRoll(random, GetBallRailChance()))
            {
                return TrySpawnCentered(
                    map,
                    distance,
                    GetEntries(Category.Balls),
                    GetOverrides(Category.Balls),
                    root,
                    random,
                    TrackSpawnPriority.Medium);
            }

            if (IsEnabled(Category.Fans)
                && HasPrefabsFor(Category.Fans)
                && IsStraightRail(section)
                && TryRoll(random, GetFanStraightRailChance()))
            {
                return TrySpawnFan(map, section, distance, root, random);
            }
        }

        if (IsEnabled(Category.Balls)
            && HasPrefabsFor(Category.Balls)
            && IsBeforeDownSlope(sections, sectionIndex, distance)
            && TryRoll(random, GetBallBeforeDownSlopeChance()))
        {
            return TrySpawnRandomInsideTrack(
                map,
                section,
                distance,
                GetEntries(Category.Balls),
                GetOverrides(Category.Balls),
                root,
                random,
                TrackSpawnPriority.Medium);
        }

        return false;
    }

    #endregion

    #region Coin Generation

    private void GenerateCoins(TrackRuntimeMap map, Transform root, System.Random random)
    {
        if (!IsEnabled(Category.Coins))
        {
            return;
        }

        IReadOnlyList<TrackSpawnPrefabEntry> coinEntries = GetEntries(Category.Coins);
        IReadOnlyList<LevelSpawnPrefabOverride> overrides = GetOverrides(Category.Coins);

        if (!HasUsablePrefabs(coinEntries, overrides, levelSettings))
        {
            return;
        }

        int requestedCoinCount = ResolveCoinCount(random);

        if (requestedCoinCount <= 0)
        {
            return;
        }

        float totalDistance = map.PathSampler.TotalDistance;
        float startDistance = contentProfile.StartContentPadding;
        float endDistance = totalDistance - contentProfile.EndContentPadding;

        if (endDistance <= startDistance)
        {
            return;
        }

        int placedCoins = 0;
        int patternCount = Mathf.Max(1, Mathf.CeilToInt(requestedCoinCount / (float)contentProfile.MaxCoinsPerPattern));
        float segmentLength = (endDistance - startDistance) / patternCount;

        for (int i = 0; i < patternCount && placedCoins < requestedCoinCount; i++)
        {
            float segmentStart = startDistance + segmentLength * i;
            float segmentEnd = segmentStart + segmentLength;
            int remainingCoins = requestedCoinCount - placedCoins;

            placedCoins += TrySpawnCoinPatternInSegment(map, segmentStart, segmentEnd, remainingCoins, root, random);
        }
    }

    private int TrySpawnCoinPatternInSegment(
        TrackRuntimeMap map,
        float startDistance,
        float endDistance,
        int remainingCoins,
        Transform root,
        System.Random random)
    {
        for (int attempt = 0; attempt < contentProfile.CoinPlacementAttempts; attempt++)
        {
            int patternCoinCount = random.Next(contentProfile.MinCoinsPerPattern, contentProfile.MaxCoinsPerPattern + 1);
            patternCoinCount = Mathf.Min(patternCoinCount, remainingCoins);

            if (patternCoinCount <= 0)
            {
                return 0;
            }

            float patternLength = Mathf.Max(0f, (patternCoinCount - 1) * contentProfile.CoinPatternDistanceSpacing);

            if (endDistance - startDistance < patternLength)
            {
                patternCoinCount = Mathf.Max(
                    1,
                    Mathf.FloorToInt((endDistance - startDistance) / contentProfile.CoinPatternDistanceSpacing));

                patternLength = Mathf.Max(0f, (patternCoinCount - 1) * contentProfile.CoinPatternDistanceSpacing);
            }

            float centerDistance = RandomRange(
                random,
                startDistance + patternLength * 0.5f,
                endDistance - patternLength * 0.5f);

            CoinPatternType patternType = PickCoinPattern(random);
            List<CoinSpawnPoint> points = BuildCoinPatternPoints(
                map,
                centerDistance,
                patternCoinCount,
                patternLength,
                patternType);

            if (points.Count == 0)
            {
                continue;
            }

            int placed = TryPlaceCoinPattern(map, points, root, random);

            if (placed > 0)
            {
                return placed;
            }
        }

        return 0;
    }

    private int TryPlaceCoinPattern(
        TrackRuntimeMap map,
        List<CoinSpawnPoint> points,
        Transform root,
        System.Random random)
    {
        int placed = 0;

        for (int i = 0; i < points.Count; i++)
        {
            CoinSpawnPoint point = points[i];
            TrackSectionDefinition section = FindSectionAtDistance(map.Sections, point.Distance);

            if (!CanSpawnCoinOnSection(section))
            {
                continue;
            }

            float halfWidth = ResolveSectionWidth(section) * 0.5f;
            float safeLimit = Mathf.Max(
                0f,
                halfWidth - contentProfile.CoinEdgePadding - contentProfile.CoinReservationWidth * 0.5f);

            float clampedLateral = Mathf.Clamp(point.Lateral, -safeLimit, safeLimit);

            if (!reservationMap.TryReserve(
                    point.Distance,
                    clampedLateral,
                    contentProfile.CoinReservationLength,
                    contentProfile.CoinReservationWidth,
                    TrackSpawnPriority.Low))
            {
                continue;
            }

            TrackSpawnPrefabEntry entry = PickWeighted(
                GetEntries(Category.Coins),
                GetOverrides(Category.Coins),
                levelSettings,
                random);

            if (entry == null)
            {
                continue;
            }

            TrackSample sample = map.PathSampler.SampleAtDistance(point.Distance);

            SpawnEntryAtSample(
                entry,
                sample,
                clampedLateral,
                root,
                alignToSurface: true);

            placed++;
        }

        return placed;
    }

    private List<CoinSpawnPoint> BuildCoinPatternPoints(
        TrackRuntimeMap map,
        float centerDistance,
        int count,
        float patternLength,
        CoinPatternType patternType)
    {
        List<CoinSpawnPoint> points = new List<CoinSpawnPoint>();

        if (count <= 0)
        {
            return points;
        }

        float startDistance = centerDistance - patternLength * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            float distance = startDistance + i * contentProfile.CoinPatternDistanceSpacing;
            TrackSectionDefinition section = FindSectionAtDistance(map.Sections, distance);

            if (!CanSpawnCoinOnSection(section))
            {
                continue;
            }

            float halfWidth = ResolveSectionWidth(section) * 0.5f;
            float safeLimit = Mathf.Max(
                0f,
                halfWidth - contentProfile.CoinEdgePadding - contentProfile.CoinReservationWidth * 0.5f);

            float amplitude = Mathf.Min(contentProfile.CoinPatternLateralAmplitude, safeLimit);
            float lateral = ResolveCoinPatternLateral(patternType, i, t, amplitude);

            points.Add(new CoinSpawnPoint(distance, lateral));
        }

        return points;
    }

    private static float ResolveCoinPatternLateral(CoinPatternType patternType, int index, float t, float amplitude)
    {
        return patternType switch
        {
            CoinPatternType.Single => 0f,
            CoinPatternType.CenterLine => 0f,
            CoinPatternType.ZigZag => index % 2 == 0 ? -amplitude : amplitude,
            CoinPatternType.LeftLine => -amplitude,
            CoinPatternType.RightLine => amplitude,
            CoinPatternType.Arc => Mathf.Lerp(-amplitude, amplitude, t),
            _ => 0f
        };
    }

    private static CoinPatternType PickCoinPattern(System.Random random)
    {
        int value = random.Next(0, 6);
        return (CoinPatternType)value;
    }

    #endregion

    #region Goal Generation

    private GoalSpawnData ResolveGoalSpawnData(TrackRuntimeMap map)
    {
        if (levelSettings != null && !levelSettings.EnableGoal)
        {
            return GoalSpawnData.Invalid;
        }

        if (contentProfile.GoalPrefab == null)
        {
            return GoalSpawnData.Invalid;
        }

        TrackSectionDefinition finishSection = FindFinishSection(map.Sections);

        float goalDistance = Mathf.Lerp(finishSection.StartDistance, finishSection.EndDistance, 0.5f);
        TrackSample sample = map.PathSampler.SampleAtDistance(goalDistance);

        Vector3 position = sample.Position + Vector3.up * contentProfile.GoalVerticalOffset;
        Quaternion rotation = Quaternion.LookRotation(sample.Forward, Vector3.up);

        return new GoalSpawnData(true, goalDistance, position, rotation);
    }

    private void ReserveGoalArea()
    {
        if (!resolvedGoalData.IsValid)
        {
            return;
        }

        reservationMap.TryReserve(
            resolvedGoalData.Distance,
            0f,
            contentProfile.GoalReservationLength,
            contentProfile.GoalReservationWidth,
            TrackSpawnPriority.Critical);
    }

    private void InstantiateGoal(Transform root)
    {
        if (!resolvedGoalData.IsValid)
        {
            return;
        }

        GameObject goal = objectPool.Spawn(
            contentProfile.GoalPrefab,
            resolvedGoalData.Position,
            resolvedGoalData.Rotation,
            root);

        PrepareSpawnedObject(goal, resolvedGoalData.Position.y, contentProfile.GoalVerticalOffset, alignToSurface: false);
    }

    #endregion

    #region Spawn Helpers

    private bool TrySpawnBoxRow(
        TrackRuntimeMap map,
        TrackSectionDefinition section,
        float distance,
        Transform root,
        System.Random random)
    {
        TrackSpawnPrefabEntry entry = PickWeighted(
            GetEntries(Category.Boxes),
            GetOverrides(Category.Boxes),
            levelSettings,
            random);

        if (entry == null)
        {
            return false;
        }

        float trackWidth = ResolveSectionWidth(section);
        float usableWidth = trackWidth - contentProfile.BoxEdgePadding * 2f;
        float slotWidth = entry.ReservationWidth + contentProfile.BoxLateralSpacing;

        int maxBoxesThatFit = Mathf.FloorToInt((usableWidth + contentProfile.BoxLateralSpacing) / slotWidth);
        maxBoxesThatFit = Mathf.Min(maxBoxesThatFit, contentProfile.MaxBoxesPerRow);

        if (maxBoxesThatFit <= 0)
        {
            return false;
        }

        int boxCount = contentProfile.AllowMultipleBoxesPerRow
            ? random.Next(1, maxBoxesThatFit + 1)
            : 1;

        List<float> lateralOffsets = BuildCenteredRowOffsets(
            boxCount,
            entry.ReservationWidth,
            contentProfile.BoxLateralSpacing);

        bool anyPlaced = false;

        for (int i = 0; i < lateralOffsets.Count; i++)
        {
            if (TrySpawnEntryAtLateral(map, distance, lateralOffsets[i], entry, root, TrackSpawnPriority.Medium))
            {
                anyPlaced = true;
            }
        }

        return anyPlaced;
    }

    private bool TrySpawnSingleBoxAtCenter(
        TrackRuntimeMap map,
        float distance,
        Transform root,
        System.Random random)
    {
        TrackSpawnPrefabEntry entry = PickWeighted(
            GetEntries(Category.Boxes),
            GetOverrides(Category.Boxes),
            levelSettings,
            random);

        if (entry == null)
        {
            return false;
        }

        return TrySpawnEntryAtLateral(map, distance, 0f, entry, root, TrackSpawnPriority.Medium);
    }

    private bool TrySpawnCentered(
        TrackRuntimeMap map,
        float distance,
        IReadOnlyList<TrackSpawnPrefabEntry> entries,
        IReadOnlyList<LevelSpawnPrefabOverride> overrides,
        Transform root,
        System.Random random,
        TrackSpawnPriority priority)
    {
        TrackSpawnPrefabEntry entry = PickWeighted(entries, overrides, levelSettings, random);

        if (entry == null)
        {
            return false;
        }

        return TrySpawnEntryAtLateral(map, distance, 0f, entry, root, priority);
    }

    private bool TrySpawnRandomInsideTrack(
        TrackRuntimeMap map,
        TrackSectionDefinition section,
        float distance,
        IReadOnlyList<TrackSpawnPrefabEntry> entries,
        IReadOnlyList<LevelSpawnPrefabOverride> overrides,
        Transform root,
        System.Random random,
        TrackSpawnPriority priority)
    {
        TrackSpawnPrefabEntry entry = PickWeighted(entries, overrides, levelSettings, random);

        if (entry == null)
        {
            return false;
        }

        float halfWidth = ResolveSectionWidth(section) * 0.5f;
        float safeLimit = Mathf.Max(
            0f,
            halfWidth - entry.ReservationWidth * 0.5f - contentProfile.BoxEdgePadding);

        float lateral = RandomRange(random, -safeLimit, safeLimit);

        return TrySpawnEntryAtLateral(map, distance, lateral, entry, root, priority);
    }

    private bool TrySpawnEntryAtLateral(
        TrackRuntimeMap map,
        float distance,
        float lateral,
        TrackSpawnPrefabEntry entry,
        Transform root,
        TrackSpawnPriority priority)
    {
        if (entry == null || entry.Prefab == null)
        {
            return false;
        }

        float reservationLength = entry.ReservationLength + contentProfile.ObstacleDistanceSpacing;

        if (!reservationMap.TryReserve(distance, lateral, reservationLength, entry.ReservationWidth, priority))
        {
            return false;
        }

        TrackSample sample = map.PathSampler.SampleAtDistance(distance);

        SpawnEntryAtSample(entry, sample, lateral, root, alignToSurface: true);

        return true;
    }

    private bool TrySpawnFan(
        TrackRuntimeMap map,
        TrackSectionDefinition section,
        float distance,
        Transform root,
        System.Random random)
    {
        TrackSpawnPrefabEntry entry = PickWeighted(
            GetEntries(Category.Fans),
            GetOverrides(Category.Fans),
            levelSettings,
            random);

        if (entry == null)
        {
            return false;
        }

        int side = contentProfile.RandomizeFanSide
            ? random.NextDouble() < 0.5 ? -1 : 1
            : contentProfile.FixedFanSide;

        float lateral =
            (ResolveSectionWidth(section) * 0.5f
             + contentProfile.FanOutsideOffset
             + entry.ReservationWidth * 0.5f)
            * side;

        return TrySpawnEntryAtLateral(map, distance, lateral, entry, root, TrackSpawnPriority.Medium);
    }

    /// <summary>
    /// Instancia una entrada del catálogo sobre un sample del track y corrige su altura física.
    /// </summary>
    private GameObject SpawnEntryAtSample(
        TrackSpawnPrefabEntry entry,
        TrackSample sample,
        float lateral,
        Transform root,
        bool alignToSurface)
    {
        Vector3 basePosition =
            sample.Position
            + sample.Right * lateral
            + Vector3.up * contentProfile.GlobalVerticalOffset;

        Quaternion rotation = Quaternion.LookRotation(sample.Forward, Vector3.up);

        GameObject spawnedObject = objectPool.Spawn(entry.Prefab, basePosition, rotation, root);

        PrepareSpawnedObject(
            spawnedObject,
            sample.Position.y,
            entry.VerticalOffset,
            alignToSurface);

        return spawnedObject;
    }

    /// <summary>
    /// Prepara un objeto recién spawneado: corrige altura y limpia su estado físico.
    /// </summary>
    private void PrepareSpawnedObject(
        GameObject spawnedObject,
        float trackSurfaceY,
        float extraVerticalOffset,
        bool alignToSurface)
    {
        if (spawnedObject == null)
        {
            return;
        }

        if (alignSpawnedObjectsToTrackSurface && alignToSurface)
        {
            AlignObjectBottomToTrackSurface(
                spawnedObject,
                trackSurfaceY,
                extraVerticalOffset + defaultSurfaceContactOffset);
        }

        if (resetRigidbodyOnSpawn)
        {
            ResetSpawnedRigidbodies(spawnedObject);
        }
    }

    /// <summary>
    /// Alinea la parte inferior del collider principal del objeto con la superficie del track.
    /// Esto evita que esferas, cubos y obstáculos aparezcan suspendidos o enterrados.
    /// </summary>
    private static void AlignObjectBottomToTrackSurface(
        GameObject spawnedObject,
        float trackSurfaceY,
        float extraVerticalOffset)
    {
        if (spawnedObject == null)
        {
            return;
        }

        Collider collider = spawnedObject.GetComponentInChildren<Collider>();

        if (collider == null)
        {
            return;
        }

        Physics.SyncTransforms();

        Bounds bounds = collider.bounds;
        float bottomY = bounds.min.y;
        float targetBottomY = trackSurfaceY + Mathf.Max(0f, extraVerticalOffset);
        float deltaY = targetBottomY - bottomY;

        spawnedObject.transform.position += Vector3.up * deltaY;

        Physics.SyncTransforms();
    }

    /// <summary>
    /// Resetea velocidades de todos los Rigidbody del objeto spawneado.
    /// Necesario cuando el objeto viene del pool o cuando se instancia con físicas activas.
    /// </summary>
    private static void ResetSpawnedRigidbodies(GameObject spawnedObject)
    {
        if (spawnedObject == null)
        {
            return;
        }

        Rigidbody[] rigidbodies = spawnedObject.GetComponentsInChildren<Rigidbody>();

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rb = rigidbodies[i];

            if (rb == null)
            {
                continue;
            }

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
            rb.WakeUp();
        }
    }

    #endregion

    #region Level Settings Accessors

    private bool IsEnabled(Category category)
    {
        if (levelSettings == null)
        {
            return true;
        }

        return category switch
        {
            Category.Boxes => levelSettings.EnableBoxes,
            Category.Walls => levelSettings.EnableWalls,
            Category.Balls => levelSettings.EnableBalls,
            Category.Fans => levelSettings.EnableFans,
            Category.Coins => levelSettings.EnableCoins,
            _ => true
        };
    }

    private bool HasPrefabsFor(Category category)
    {
        return HasUsablePrefabs(GetEntries(category), GetOverrides(category), levelSettings);
    }

    private IReadOnlyList<TrackSpawnPrefabEntry> GetEntries(Category category)
    {
        return category switch
        {
            Category.Boxes => contentProfile.BoxPrefabs,
            Category.Walls => contentProfile.WallPrefabs,
            Category.Balls => contentProfile.BallPrefabs,
            Category.Fans => contentProfile.FanPrefabs,
            Category.Coins => contentProfile.CoinPrefabs,
            _ => null
        };
    }

    private IReadOnlyList<LevelSpawnPrefabOverride> GetOverrides(Category category)
    {
        if (levelSettings == null)
        {
            return null;
        }

        return category switch
        {
            Category.Boxes => levelSettings.BoxOverrides,
            Category.Walls => levelSettings.WallOverrides,
            Category.Balls => levelSettings.BallOverrides,
            Category.Fans => levelSettings.FanOverrides,
            Category.Coins => levelSettings.CoinOverrides,
            _ => null
        };
    }

    private float GetBoxChance()
    {
        return levelSettings != null ? levelSettings.BoxSpawnChance : contentProfile.BaseBoxSpawnChance;
    }

    private float GetWallChance()
    {
        return levelSettings != null ? levelSettings.WallSpawnChance : contentProfile.BaseWallSpawnChance;
    }

    private float GetBallFlatChance()
    {
        return levelSettings != null ? levelSettings.BallFlatSpawnChance : contentProfile.BaseBallFlatSpawnChance;
    }

    private float GetBallNarrowChance()
    {
        return levelSettings != null ? levelSettings.BallNarrowSpawnChance : contentProfile.BaseBallNarrowSpawnChance;
    }

    private float GetBallRailChance()
    {
        return levelSettings != null ? levelSettings.BallRailSpawnChance : contentProfile.BaseBallRailSpawnChance;
    }

    private float GetBallBeforeDownSlopeChance()
    {
        return levelSettings != null
            ? levelSettings.BallBeforeDownSlopeChance
            : contentProfile.BaseBallBeforeDownSlopeChance;
    }

    private float GetFanFlatChance()
    {
        return levelSettings != null ? levelSettings.FanFlatSpawnChance : contentProfile.BaseFanFlatSpawnChance;
    }

    private float GetFanStraightRailChance()
    {
        return levelSettings != null
            ? levelSettings.FanStraightRailSpawnChance
            : contentProfile.BaseFanStraightRailSpawnChance;
    }

    private int ResolveCoinCount(System.Random random)
    {
        bool useRandom = levelSettings != null
            ? levelSettings.UseRandomCoinCount
            : contentProfile.BaseUseRandomCoinCount;

        if (!useRandom)
        {
            return levelSettings != null
                ? levelSettings.FixedCoinCount
                : contentProfile.BaseFixedCoinCount;
        }

        int min = levelSettings != null
            ? levelSettings.MinRandomCoinCount
            : contentProfile.BaseMinRandomCoinCount;

        int max = levelSettings != null
            ? levelSettings.MaxRandomCoinCount
            : contentProfile.BaseMaxRandomCoinCount;

        return random.Next(min, max + 1);
    }

    #endregion

    #region Weighted Selection

    private static TrackSpawnPrefabEntry PickWeighted(
        IReadOnlyList<TrackSpawnPrefabEntry> entries,
        IReadOnlyList<LevelSpawnPrefabOverride> overrides,
        LevelContentGenerationSettings settings,
        System.Random random)
    {
        if (!HasUsablePrefabs(entries, overrides, settings))
        {
            return null;
        }

        float totalWeight = 0f;

        for (int i = 0; i < entries.Count; i++)
        {
            float weight = ResolveWeight(entries[i], overrides, settings);
            totalWeight += Mathf.Max(0f, weight);
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        double pick = random.NextDouble() * totalWeight;
        float cumulative = 0f;

        for (int i = 0; i < entries.Count; i++)
        {
            TrackSpawnPrefabEntry entry = entries[i];
            float weight = ResolveWeight(entry, overrides, settings);

            if (entry == null || entry.Prefab == null || weight <= 0f)
            {
                continue;
            }

            cumulative += weight;

            if (pick <= cumulative)
            {
                return entry;
            }
        }

        return null;
    }

    private static float ResolveWeight(
        TrackSpawnPrefabEntry entry,
        IReadOnlyList<LevelSpawnPrefabOverride> overrides,
        LevelContentGenerationSettings settings)
    {
        if (entry == null || entry.Prefab == null || string.IsNullOrWhiteSpace(entry.Id))
        {
            return 0f;
        }

        if (settings == null || overrides == null)
        {
            return entry.BaseSelectionWeight;
        }

        LevelSpawnPrefabOverride overrideEntry = FindOverride(entry.Id, overrides);

        if (overrideEntry == null)
        {
            return settings.UseOverridesAsWhitelist ? 0f : entry.BaseSelectionWeight;
        }

        if (!overrideEntry.EnabledInLevel)
        {
            return 0f;
        }

        return overrideEntry.OverrideSelectionWeight
            ? overrideEntry.LevelSelectionWeight
            : entry.BaseSelectionWeight;
    }

    private static LevelSpawnPrefabOverride FindOverride(
        string id,
        IReadOnlyList<LevelSpawnPrefabOverride> overrides)
    {
        if (overrides == null)
        {
            return null;
        }

        for (int i = 0; i < overrides.Count; i++)
        {
            if (overrides[i] != null && overrides[i].PrefabId == id)
            {
                return overrides[i];
            }
        }

        return null;
    }

    private static bool HasUsablePrefabs(
        IReadOnlyList<TrackSpawnPrefabEntry> entries,
        IReadOnlyList<LevelSpawnPrefabOverride> overrides,
        LevelContentGenerationSettings settings)
    {
        if (entries == null || entries.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (ResolveWeight(entries[i], overrides, settings) > 0f)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Section Classification

    private static bool IsFlatSolid(TrackSectionDefinition section)
    {
        return section.StructureType == TrackStructureType.SolidTrack
               && section.HasSurface
               && Mathf.Abs(section.TurnAngleDegrees) <= 0.001f
               && Mathf.Approximately(section.SlopeHeightDelta, 0f)
               && Mathf.Approximately(section.RampHeightDelta, 0f)
               && !IsNarrowSolid(section);
    }

    private static bool IsNarrowSolid(TrackSectionDefinition section)
    {
        return section.StructureType == TrackStructureType.SolidTrack
               && section.HasSurface
               && section.TargetWidthRatio < 0.99f;
    }

    private static bool IsRail(TrackSectionDefinition section)
    {
        return section.StructureType == TrackStructureType.RailTrack && section.HasSurface;
    }

    private static bool IsStraightRail(TrackSectionDefinition section)
    {
        return IsRail(section)
               && Mathf.Abs(section.TurnAngleDegrees) <= 0.001f
               && Mathf.Approximately(section.SlopeHeightDelta, 0f)
               && Mathf.Approximately(section.RampHeightDelta, 0f);
    }

    private static bool CanSpawnCoinOnSection(TrackSectionDefinition section)
    {
        return section.HasSurface
               && section.StructureType != TrackStructureType.Gap
               && section.StructureType != TrackStructureType.RailTrack;
    }

    private bool IsBeforeDownSlope(
        IReadOnlyList<TrackSectionDefinition> sections,
        int currentIndex,
        float currentDistance)
    {
        for (int i = currentIndex + 1; i < sections.Count; i++)
        {
            TrackSectionDefinition next = sections[i];

            if (next.StartDistance - currentDistance > contentProfile.BeforeDownSlopeWindow)
            {
                return false;
            }

            if (next.SlopeHeightDelta < -0.001f)
            {
                return true;
            }
        }

        return false;
    }

    private static TrackSectionDefinition FindSectionAtDistance(
        IReadOnlyList<TrackSectionDefinition> sections,
        float distance)
    {
        for (int i = 0; i < sections.Count; i++)
        {
            if (distance >= sections[i].StartDistance && distance <= sections[i].EndDistance)
            {
                return sections[i];
            }
        }

        return sections[sections.Count - 1];
    }

    private static TrackSectionDefinition FindFinishSection(IReadOnlyList<TrackSectionDefinition> sections)
    {
        for (int i = sections.Count - 1; i >= 0; i--)
        {
            if (sections[i].FeatureType == TrackFeatureType.Finish)
            {
                return sections[i];
            }
        }

        return sections[sections.Count - 1];
    }

    private static float ResolveSectionWidth(TrackSectionDefinition section)
    {
        return Mathf.Min(section.StartWidth, section.EndWidth);
    }

    #endregion

    #region Misc Helpers

    private bool CanGenerate()
    {
        if (trackGenerator == null)
        {
            Debug.LogWarning("[TRACK CONTENT] TrackGeneratorController no asignado.", this);
            return false;
        }

        if (trackGenerator.GeneratedMap == null)
        {
            Debug.LogWarning("[TRACK CONTENT] No existe GeneratedMap. Genera primero la pista.", this);
            return false;
        }

        if (trackGenerator.GeneratedMap.PathSampler == null)
        {
            Debug.LogWarning("[TRACK CONTENT] PathSampler no disponible en GeneratedMap.", this);
            return false;
        }

        if (contentProfile == null)
        {
            Debug.LogWarning("[TRACK CONTENT] Content profile no asignado.", this);
            return false;
        }

        return true;
    }

    private static bool TryRoll(System.Random random, float chance)
    {
        return chance > 0f && (chance >= 1f || random.NextDouble() <= chance);
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        if (max <= min)
        {
            return min;
        }

        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private static List<float> BuildCenteredRowOffsets(
        int count,
        float reservationWidth,
        float spacing)
    {
        List<float> offsets = new List<float>();

        if (count <= 0)
        {
            return offsets;
        }

        if (count == 1)
        {
            offsets.Add(0f);
            return offsets;
        }

        float step = reservationWidth + spacing;
        float start = -step * (count - 1) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            offsets.Add(start + step * i);
        }

        return offsets;
    }

    private void OnValidate()
    {
        defaultSurfaceContactOffset = Mathf.Max(0f, defaultSurfaceContactOffset);
    }

    #endregion

    #region Private Types

    private enum Category
    {
        Boxes,
        Walls,
        Balls,
        Fans,
        Coins
    }

    private enum CoinPatternType
    {
        Single = 0,
        CenterLine = 1,
        ZigZag = 2,
        LeftLine = 3,
        RightLine = 4,
        Arc = 5
    }

    private readonly struct CoinSpawnPoint
    {
        public float Distance { get; }
        public float Lateral { get; }

        public CoinSpawnPoint(float distance, float lateral)
        {
            Distance = distance;
            Lateral = lateral;
        }
    }

    private readonly struct GoalSpawnData
    {
        public static GoalSpawnData Invalid => new GoalSpawnData(false, 0f, Vector3.zero, Quaternion.identity);

        public bool IsValid { get; }
        public float Distance { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }

        public GoalSpawnData(bool isValid, float distance, Vector3 position, Quaternion rotation)
        {
            IsValid = isValid;
            Distance = distance;
            Position = position;
            Rotation = rotation;
        }
    }

    #endregion
}