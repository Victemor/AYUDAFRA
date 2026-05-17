using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera power-ups sobre la pista procedural.
///
/// Dos categorías independientes:
///
/// <list type="bullet">
///   <item>
///     <b>SpeedBoost (track gen)</b>: se coloca en oportunidades específicas de gameplay
///     (inicio de pendiente ascendente, justo antes de un gap). Lógica idéntica a la anterior.
///   </item>
///   <item>
///     <b>Collectibles</b>: distribuidos a lo largo de toda la pista con un intervalo variable.
///     Respetan el <see cref="TrackSpawnReservationMap"/> compartido con <see cref="TrackContentGenerator"/>
///     para evitar solapamiento con monedas y obstáculos.
///     La probabilidad y el intervalo escalan con <c>progressionT</c> y el nivel actual.
///   </item>
/// </list>
/// </summary>
public sealed class PowerUpGenerator : MonoBehaviour
{
    #region Constants

    private const string RootName                  = "GeneratedPowerUps";
    private const int    SeedOffset                = 777;
    private const float  DefaultInvalidDistance    = -999f;
    private const float  MinimumValidSectionLength = 0.001f;
    private const float  GapEdgeSafetyOffset       = 0.08f;

    #endregion

    #region Inspector

    [Header("Prefabs")]
    [SerializeField]
    [Tooltip("Prefab de segmento de aceleración. El eje Z debe coincidir con la dirección de avance " +
             "y su longitud base debe ser 1 unidad.")]
    private GameObject speedBoostZonePrefab;

    [Header("Reglas de Generación — SpeedBoost")]
    [SerializeField]
    [Tooltip("Permite generar SpeedBoost en pendientes ascendentes sobre pista sólida.")]
    private bool enableSlopeOpportunities = true;

    [SerializeField]
    [Tooltip("Permite generar SpeedBoost justo antes de gaps desde pista sólida.")]
    private bool enableGapOpportunities = true;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Probabilidad de generar SpeedBoost al inicio de una pendiente ascendente.")]
    private float slopeOpportunityChance = 0.65f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Probabilidad de generar SpeedBoost justo antes de un gap.")]
    private float gapOpportunityChance = 0.85f;

    [Header("Espaciado — SpeedBoost")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("Distancia mínima entre dos oportunidades aceptadas de SpeedBoost.")]
    private float minDistanceBetweenSpeedBoosts = 14f;

    [Header("Speed Boost — Visual")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("Offset vertical sobre la pista para evitar z-fighting.")]
    private float speedBoostSurfaceOffset = 0.01f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Distancia adicional antes del inicio de la sección desde donde empieza el SpeedBoost.")]
    private float speedBoostLeadInDistance = 0.75f;

    [SerializeField]
    [Min(0.25f)]
    [Tooltip("Longitud aproximada de cada segmento de SpeedBoost. Valores menores siguen mejor la pista.")]
    private float speedBoostSegmentLength = 1.25f;

    [SerializeField]
    [Min(0.1f)]
    [Tooltip("Longitud mínima visual de cada segmento de SpeedBoost.")]
    private float minimumSpeedBoostSegmentVisualLength = 0.25f;

    [Header("Coleccionables")]
    [SerializeField]
    [Tooltip("Lista de power-ups coleccionables disponibles para spawnear en el track.\n" +
             "Cada entrada apunta a su prefab con CollectiblePowerUpBase y define\n" +
             "cuándo y con qué probabilidad aparece.")]
    private CollectiblePowerUpSpawnEntry[] collectiblePowerUps;

    [SerializeField]
    [Min(0)]
    [Tooltip("Cantidad mínima de power-ups coleccionables por nivel. " +
             "0 = puede que no aparezca ninguno (especialmente en niveles iniciales).")]
    private int minCollectiblesPerLevel = 0;

    [SerializeField]
    [Min(0)]
    [Tooltip("Cantidad máxima de power-ups coleccionables por nivel. " +
             "La cantidad real se elige aleatoriamente entre min y max.")]
    private int maxCollectiblesPerLevel = 3;

    [SerializeField]
    [Min(5f)]
    [Tooltip("Distancia mínima en metros entre dos coleccionables. " +
             "Evita que aparezcan agrupados aunque la selección aleatoria los ponga cerca.")]
    private float minDistanceBetweenCollectibles = 20f;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("Si está activo, imprime resumen de generación en consola.")]
    private bool enableDebugLogs = true;

    [SerializeField]
    [Tooltip("SOLO DEBUG. Si está activo, ignora reglas de oportunidad para SpeedBoost " +
             "y distribuye coleccionables con intervalo mínimo. " +
             "Aun así evita rieles y gaps.")]
    private bool debugGenerateAnywhere = false;

    [SerializeField]
    [Min(1)]
    [Tooltip("SOLO DEBUG. Máximo de SpeedBoosts en modo Debug Generate Anywhere.")]
    private int debugMaxSpeedBoosts = 10;

    [SerializeField]
    [Min(0f)]
    [Tooltip("SOLO DEBUG. Distancia mínima entre SpeedBoosts en modo Debug Generate Anywhere.")]
    private float debugMinDistanceBetweenSpeedBoosts = 8f;

    #endregion

    #region Runtime

    private Transform generatedRoot;
    private float     lastAcceptedSpeedBoostDistance = DefaultInvalidDistance;

    #endregion

    #region Public API

    /// <summary>Mantiene compatibilidad con el flujo del InfiniteLevelManager.</summary>
    public void DisableAutoGeneration() { }

    /// <summary>
    /// Genera SpeedBoosts y coleccionables sobre el mapa procedural.
    /// </summary>
    /// <param name="map">Mapa runtime de la pista.</param>
    /// <param name="seed">Semilla de generación.</param>
    /// <param name="sharedReservationMap">
    ///   Mapa de reservas compartido con <see cref="TrackContentGenerator"/>.
    ///   Si es <c>null</c>, los coleccionables se colocan sin verificar solapamientos.
    /// </param>
    /// <param name="levelIndex">Índice del nivel actual (comienza en 1).</param>
    /// <param name="progressionT">Factor de progresión [0 = inicio, 1 = dificultad máxima].</param>
    /// <param name="safeStartLength">Metros iniciales sin power-ups.</param>
    /// <param name="safeEndLength">Metros finales sin power-ups.</param>
    public void GeneratePowerUps(
        TrackRuntimeMap map,
        int seed,
        TrackSpawnReservationMap sharedReservationMap,
        int levelIndex,
        float progressionT,
        float safeStartLength = 0f,
        float safeEndLength   = 0f)
    {
        ClearPowerUps();

        if (!CanGenerate(map))
        {
            return;
        }

        generatedRoot = CreateRoot();

        if (debugGenerateAnywhere)
        {
            GenerateDebugPowerUps(map, seed, sharedReservationMap, levelIndex, progressionT);
            return;
        }

        GenerateGameplayPowerUps(map, seed, sharedReservationMap, levelIndex, progressionT, safeStartLength, safeEndLength);
    }

    /// <summary>Elimina todos los power-ups generados previamente.</summary>
    public void ClearPowerUps()
    {
        Transform existingRoot = transform.Find(RootName);

        if (existingRoot != null)
        {
            existingRoot.SetParent(null);

#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(existingRoot.gameObject);
            else
                Destroy(existingRoot.gameObject);
#else
            Destroy(existingRoot.gameObject);
#endif
        }

        generatedRoot                    = null;
        lastAcceptedSpeedBoostDistance   = DefaultInvalidDistance;
    }

    #endregion

    #region Gameplay Generation

    private void GenerateGameplayPowerUps(
        TrackRuntimeMap          map,
        int                      seed,
        TrackSpawnReservationMap sharedReservationMap,
        int                      levelIndex,
        float                    progressionT,
        float                    safeStartLength,
        float                    safeEndLength)
    {
        System.Random random = new System.Random(seed + SeedOffset);
        IReadOnlyList<TrackSectionDefinition> sections = map.Sections;

        float totalDistance = map.PathSampler.TotalDistance;
        float usableStart   = Mathf.Max(0f, safeStartLength);
        float usableEnd     = Mathf.Max(usableStart, totalDistance - Mathf.Max(0f, safeEndLength));

        lastAcceptedSpeedBoostDistance = usableStart;

        int speedBoostPathsPlaced    = 0;
        int speedBoostSegmentsPlaced = 0;
        int collectiblesPlaced       = 0;
        int rejectedRailSections     = 0;

        // — SpeedBoost: solo en oportunidades de pendiente/gap —
        for (int i = 0; i < sections.Count; i++)
        {
            TrackSectionDefinition section = sections[i];

            if (section.StructureType == TrackStructureType.RailTrack)
            {
                rejectedRailSections++;
                continue;
            }

            if (!CanUseGameplaySection(section, usableStart, usableEnd)) continue;

            bool isSlopeOpportunity = enableSlopeOpportunities && IsSlopeUpSection(section);
            bool isGapOpportunity   = enableGapOpportunities   && IsImmediatePreGapSection(section, sections, i);

            if (!isSlopeOpportunity && !isGapOpportunity) continue;

            PowerUpOpportunityKind opportunityKind = isGapOpportunity
                ? PowerUpOpportunityKind.BeforeGap
                : PowerUpOpportunityKind.SlopeStart;

            float opportunityDistance = ResolveOpportunityDistance(section, opportunityKind);

            if (opportunityDistance < usableStart || opportunityDistance > usableEnd) continue;
            if (opportunityDistance - lastAcceptedSpeedBoostDistance < minDistanceBetweenSpeedBoosts) continue;

            float opportunityChance = opportunityKind == PowerUpOpportunityKind.BeforeGap
                ? gapOpportunityChance
                : slopeOpportunityChance;

            if (random.NextDouble() > opportunityChance) continue;
            if (speedBoostZonePrefab == null) continue;

            float startDistance = Mathf.Max(usableStart, section.StartDistance - speedBoostLeadInDistance);
            float endDistance   = ResolveSpeedBoostEndDistance(section, opportunityKind, usableEnd);
            int   createdSegs   = PlaceSpeedBoostPath(map, startDistance, endDistance, opportunityKind);

            if (createdSegs > 0)
            {
                speedBoostPathsPlaced++;
                speedBoostSegmentsPlaced         += createdSegs;
                lastAcceptedSpeedBoostDistance    = opportunityDistance;
            }
        }

        // — Coleccionables: distribuidos a lo largo de toda la pista —
        collectiblesPlaced = GenerateCollectibles(
            map, random, sharedReservationMap, sections, levelIndex, progressionT, usableStart, usableEnd);

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[POWER UPS] Generación gameplay completa.\n" +
                $"  Sections total              : {sections.Count}\n" +
                $"  Rail sections rejected      : {rejectedRailSections}\n" +
                $"  Speed Boost paths placed    : {speedBoostPathsPlaced}\n" +
                $"  Speed Boost segments        : {speedBoostSegmentsPlaced}\n" +
                $"  Collectibles placed         : {collectiblesPlaced}\n" +
                $"  Usable range                : {usableStart:F1}m – {usableEnd:F1}m\n" +
                $"  Level index / progressionT  : {levelIndex} / {progressionT:F2}",
                this);
        }
    }

    #endregion

    #region Collectible Generation

    /// <summary>
    /// Distribuye power-ups coleccionables a lo largo del track.
    ///
    /// La cantidad total se elige aleatoriamente entre <see cref="minCollectiblesPerLevel"/>
    /// y <see cref="maxCollectiblesPerLevel"/>. Las posiciones se distribuyen uniformemente
    /// con variación aleatoria y se validan contra el mapa de reservas para no solapar
    /// con monedas ni obstáculos.
    /// </summary>
    private int GenerateCollectibles(
        TrackRuntimeMap                      map,
        System.Random                        random,
        TrackSpawnReservationMap             reservationMap,
        IReadOnlyList<TrackSectionDefinition> sections,
        int                                  levelIndex,
        float                                progressionT,
        float                                usableStart,
        float                                usableEnd)
    {
        List<CollectiblePowerUpSpawnEntry> eligible = GetEligibleCollectibles(levelIndex, progressionT);
        if (eligible.Count == 0) return 0;

        // Determinar cuántos colocar este nivel.
        int clampedMax    = Mathf.Max(minCollectiblesPerLevel, maxCollectiblesPerLevel);
        int targetCount   = random.Next(minCollectiblesPerLevel, clampedMax + 1);
        if (targetCount <= 0) return 0;

        float range = usableEnd - usableStart;
        if (range <= 0f) return 0;

        // Distribuir los slots uniformemente y con variación aleatoria.
        // Cada slot ocupa range/targetCount metros; dentro del slot se escoge una posición al azar.
        float slotSize    = range / targetCount;
        int   placed      = 0;
        float lastPlaced  = usableStart - minDistanceBetweenCollectibles; // permite colocar desde el inicio

        for (int i = 0; i < targetCount; i++)
        {
            float slotStart = usableStart + slotSize * i;
            float slotEnd   = slotStart + slotSize;

            // Posición candidata aleatoria dentro del slot.
            float candidateDistance = slotStart + (float)random.NextDouble() * (slotEnd - slotStart);
            candidateDistance       = Mathf.Clamp(candidateDistance, usableStart, usableEnd);

            // Respetar distancia mínima entre coleccionables.
            if (candidateDistance - lastPlaced < minDistanceBetweenCollectibles) continue;

            // Seleccionar tipo elegible ponderado.
            if (!TryPickWeightedCollectible(eligible, random, progressionT, out CollectiblePowerUpSpawnEntry selectedEntry)) continue;

            // Evaluar probabilidad individual del tipo seleccionado.
            if (random.NextDouble() > selectedEntry.EvaluateSpawnChance(progressionT)) continue;

            // Verificar sección sólida.
            if (!IsValidSolidDistanceForCollectible(sections, candidateDistance)) continue;

            // Intentar reservar espacio.
            float acceptedDistance = TryReserveCollectibleSlot(
                reservationMap, selectedEntry, candidateDistance,
                usableStart, usableEnd, random, sections);

            if (acceptedDistance < 0f) continue;

            PlaceCollectible(map, acceptedDistance, selectedEntry);
            lastPlaced = acceptedDistance;
            placed++;
        }

        return placed;
    }

    /// <summary>
    /// Intenta reservar un slot para el coleccionable, probando posiciones alternativas si la
    /// principal está ocupada. Devuelve la distancia aceptada, o -1 si no fue posible.
    /// </summary>
    private float TryReserveCollectibleSlot(
        TrackSpawnReservationMap reservationMap,
        CollectiblePowerUpSpawnEntry entry,
        float                    preferredDistance,
        float                    usableStart,
        float                    usableEnd,
        System.Random            random,
        IReadOnlyList<TrackSectionDefinition> sections)
    {
        // Sin mapa de reservas: colocar sin verificar.
        if (reservationMap == null)
            return preferredDistance;

        float[] candidates =
        {
            preferredDistance,
            preferredDistance + 1.5f,
            preferredDistance - 1.5f,
            preferredDistance + 3f,
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            float dist = candidates[i];

            if (dist < usableStart || dist > usableEnd) continue;
            if (!IsValidSolidDistanceForCollectible(sections, dist)) continue;

            if (reservationMap.TryReserve(
                    dist,
                    0f,
                    entry.ReservationLength,
                    entry.ReservationWidth,
                    TrackSpawnPriority.Low))
            {
                return dist;
            }
        }

        return -1f;
    }

    /// <summary>
    /// Instancia el prefab de coleccionable en la pista y lo inicializa con los datos dados.
    ///
    /// Después de instanciar alinea el borde inferior del collider con la superficie de la pista.
    /// Esto resuelve el problema del pivot: una esfera tiene su pivot en el centro,
    /// por lo que sin alineación quedaría enterrada la mitad en el suelo.
    /// </summary>
    private void PlaceCollectible(TrackRuntimeMap map, float distance, CollectiblePowerUpSpawnEntry entry)
    {
        if (entry.Prefab == null) return;

        TrackSample sample = map.PathSampler.SampleAtDistance(distance);
        Quaternion rotation = Quaternion.LookRotation(ResolveSafeForward(sample.Forward), Vector3.up);

        // Instanciar a altura provisional; el offset real se lee del componente.
        GameObject instance = Instantiate(
            entry.Prefab,
            sample.Position + Vector3.up * 0.5f,
            rotation,
            generatedRoot);
        instance.name = $"Collectible_{entry.Prefab.name}_{distance:F0}m";

        // El SurfaceOffset correcto vive en el componente del prefab,
        // permitiendo que cada tipo de power-up tenga su propia altura de spawn.
        CollectiblePowerUpBase powerUp = instance.GetComponent<CollectiblePowerUpBase>();
        float surfaceOffset = powerUp != null ? powerUp.SurfaceOffset : 0.5f;

        instance.transform.position = sample.Position + Vector3.up * surfaceOffset;
        AlignCollectibleBottomToSurface(instance, sample.Position.y + surfaceOffset);
    }

    /// <summary>
    /// Eleva el objeto hasta que el punto más bajo de su collider quede en <paramref name="targetSurfaceY"/>.
    ///
    /// Necesario porque los prefabs pueden tener el pivot en el centro (e.g. una esfera)
    /// en lugar de en la base. Sin este ajuste, la mitad del objeto quedaría bajo el suelo.
    /// </summary>
    private static void AlignCollectibleBottomToSurface(GameObject instance, float targetSurfaceY)
    {
        Collider col = instance.GetComponentInChildren<Collider>(true);
        if (col == null) return;

        float bottomY = col.bounds.min.y;
        float delta   = targetSurfaceY - bottomY;

        if (Mathf.Abs(delta) > 0.001f)
            instance.transform.position += new Vector3(0f, delta, 0f);
    }

    /// <summary>
    /// Filtra y devuelve los coleccionables elegibles para el nivel y progresión actuales.
    /// </summary>
    private List<CollectiblePowerUpSpawnEntry> GetEligibleCollectibles(int levelIndex, float progressionT)
    {
        var eligible = new List<CollectiblePowerUpSpawnEntry>();

        if (collectiblePowerUps == null) return eligible;

        for (int i = 0; i < collectiblePowerUps.Length; i++)
        {
            CollectiblePowerUpSpawnEntry entry = collectiblePowerUps[i];
            if (entry.Prefab == null) continue;
            if (!entry.IsEligibleForLevel(levelIndex)) continue;
            if (entry.EvaluateSpawnChance(progressionT) <= 0f) continue;
            eligible.Add(entry);
        }

        return eligible;
    }

    /// <summary>
    /// Selecciona un coleccionable ponderado por spawnWeight × spawnChance del nivel actual.
    /// Usa patrón bool + out porque <see cref="CollectiblePowerUpSpawnEntry"/> es un struct
    /// y no puede ser null. Devuelve <c>false</c> si la lista está vacía o el peso total es cero.
    /// </summary>
    private static bool TryPickWeightedCollectible(
        List<CollectiblePowerUpSpawnEntry> eligible,
        System.Random                      random,
        float                              progressionT,
        out CollectiblePowerUpSpawnEntry   selected)
    {
        selected = default;

        if (eligible.Count == 0) return false;

        if (eligible.Count == 1)
        {
            selected = eligible[0];
            return true;
        }

        float totalWeight = 0f;
        for (int i = 0; i < eligible.Count; i++)
            totalWeight += eligible[i].SpawnWeight * eligible[i].EvaluateSpawnChance(progressionT);

        if (totalWeight <= 0f) return false;

        float roll    = (float)random.NextDouble() * totalWeight;
        float current = 0f;

        for (int i = 0; i < eligible.Count; i++)
        {
            current += eligible[i].SpawnWeight * eligible[i].EvaluateSpawnChance(progressionT);
            if (roll <= current)
            {
                selected = eligible[i];
                return true;
            }
        }

        // Fallback: cubre imprecisiones de punto flotante en la acumulación de pesos.
        selected = eligible[eligible.Count - 1];
        return true;
    }

    /// <summary>
    /// Indica si la distancia dada cae sobre una sección sólida válida para coleccionables.
    /// Rechaza rieles, gaps y cualquier sección sin superficie.
    /// </summary>
    private static bool IsValidSolidDistanceForCollectible(
        IReadOnlyList<TrackSectionDefinition> sections,
        float distance)
    {
        for (int i = 0; i < sections.Count; i++)
        {
            TrackSectionDefinition section = sections[i];
            if (distance < section.StartDistance || distance > section.EndDistance) continue;

            return section.HasSurface
                && section.StructureType == TrackStructureType.SolidTrack;
        }

        return false;
    }

    #endregion

    #region Debug Generation

    private void GenerateDebugPowerUps(
        TrackRuntimeMap          map,
        int                      seed,
        TrackSpawnReservationMap sharedReservationMap,
        int                      levelIndex,
        float                    progressionT)
    {
        System.Random random = new System.Random(seed + SeedOffset);
        IReadOnlyList<TrackSectionDefinition> sections = map.Sections;

        // Debug SpeedBoosts
        int   placed      = 0;
        float lastDistance = 0f;

        for (int i = 0; i < sections.Count; i++)
        {
            if (placed >= debugMaxSpeedBoosts) break;

            TrackSectionDefinition section = sections[i];
            if (section.StructureType == TrackStructureType.RailTrack) continue;
            if (!CanUseDebugSection(section)) continue;

            float distance = GetSectionCenterDistance(section);
            if (distance - lastDistance < debugMinDistanceBetweenSpeedBoosts) continue;

            if (speedBoostZonePrefab != null)
            {
                PlaceSpeedBoostPath(map, section.StartDistance, section.EndDistance, PowerUpOpportunityKind.Debug);
                placed++;
            }

            lastDistance = distance;
        }

        // Debug Collectibles: a intervalo mínimo a lo largo de toda la pista
        float totalDistance = map.PathSampler.TotalDistance;
        int   collectiblesPlaced = GenerateCollectibles(
            map, random, sharedReservationMap, sections, levelIndex, 1f, 0f, totalDistance);

        Debug.LogWarning(
            $"[POWER UPS DEBUG] Debug Generate Anywhere activo. " +
            $"SpeedBoosts: {placed} | Collectibles: {collectiblesPlaced}.",
            this);
    }

    #endregion

    #region Speed Boost Placement

    private int PlaceSpeedBoostPath(
        TrackRuntimeMap        map,
        float                  startDistance,
        float                  endDistance,
        PowerUpOpportunityKind opportunityKind)
    {
        if (speedBoostZonePrefab == null) return 0;

        startDistance = Mathf.Max(0f, startDistance);
        endDistance   = Mathf.Max(startDistance, endDistance);
        float totalLength = endDistance - startDistance;

        if (totalLength <= MinimumValidSectionLength) return 0;

        GameObject pathRootObject = new GameObject(
            $"SpeedBoostPath_{opportunityKind}_{startDistance:F0}m_{endDistance:F0}m");
        pathRootObject.transform.SetParent(generatedRoot);
        pathRootObject.transform.localPosition = Vector3.zero;
        pathRootObject.transform.localRotation = Quaternion.identity;
        pathRootObject.transform.localScale    = Vector3.one;
        Transform pathRoot = pathRootObject.transform;

        int   segmentCount          = Mathf.Max(1, Mathf.CeilToInt(totalLength / speedBoostSegmentLength));
        float resolvedSegmentLength = totalLength / segmentCount;
        int   createdSegments       = 0;

        for (int i = 0; i < segmentCount; i++)
        {
            float segStart = startDistance + resolvedSegmentLength * i;
            float segEnd   = i == segmentCount - 1 ? endDistance : segStart + resolvedSegmentLength;

            if (CreateSpeedBoostSegment(map, segStart, segEnd, pathRoot, i))
                createdSegments++;
        }

        if (createdSegments == 0)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(pathRootObject);
            else Destroy(pathRootObject);
#else
            Destroy(pathRootObject);
#endif
        }

        return createdSegments;
    }

    private bool CreateSpeedBoostSegment(
        TrackRuntimeMap map,
        float           startDistance,
        float           endDistance,
        Transform       parent,
        int             segmentIndex)
    {
        TrackSample startSample = map.PathSampler.SampleAtDistance(startDistance);
        TrackSample endSample   = map.PathSampler.SampleAtDistance(endDistance);

        Vector3 startPos = startSample.Position + Vector3.up * speedBoostSurfaceOffset;
        Vector3 endPos   = endSample.Position   + Vector3.up * speedBoostSurfaceOffset;

        Vector3 segment       = endPos - startPos;
        float   segmentLength = segment.magnitude;

        if (segmentLength <= MinimumValidSectionLength) return false;

        Vector3    position = (startPos + endPos) * 0.5f;
        Quaternion rotation = Quaternion.LookRotation(segment.normalized, Vector3.up);

        GameObject instance = Instantiate(speedBoostZonePrefab, position, rotation, parent);
        instance.name = $"SpeedBoostSegment_{segmentIndex:D2}_{startDistance:F1}m_{endDistance:F1}m";

        Vector3 scale = instance.transform.localScale;
        scale.z = Mathf.Max(minimumSpeedBoostSegmentVisualLength, segmentLength);
        instance.transform.localScale = scale;

        return true;
    }

    #endregion

    #region Filtering

    private bool CanGenerate(TrackRuntimeMap map)
    {
        if (map == null)
        {
            Debug.LogWarning("[POWER UPS] TrackRuntimeMap es null.", this);
            return false;
        }

        if (map.PathSampler == null)
        {
            Debug.LogWarning("[POWER UPS] PathSampler es null.", this);
            return false;
        }

        if (map.Sections == null || map.Sections.Count == 0)
        {
            Debug.LogWarning("[POWER UPS] No hay secciones disponibles.", this);
            return false;
        }

        bool hasSpeedBoost = speedBoostZonePrefab != null;
        bool hasCollectibles = collectiblePowerUps != null && collectiblePowerUps.Length > 0;

        if (!hasSpeedBoost && !hasCollectibles)
        {
            Debug.LogWarning(
                "[POWER UPS] Ni speedBoostZonePrefab ni collectiblePowerUps están asignados.", this);
            return false;
        }

        return true;
    }

    private static bool CanUseGameplaySection(
        TrackSectionDefinition section,
        float usableStart,
        float usableEnd)
    {
        if (section.EndDistance - section.StartDistance <= MinimumValidSectionLength) return false;
        if (section.StartDistance < usableStart || section.EndDistance > usableEnd) return false;
        if (!section.HasSurface) return false;
        return section.StructureType == TrackStructureType.SolidTrack;
    }

    private static bool CanUseDebugSection(TrackSectionDefinition section)
    {
        if (section.EndDistance - section.StartDistance <= MinimumValidSectionLength) return false;
        if (!section.HasSurface) return false;
        return section.StructureType == TrackStructureType.SolidTrack;
    }

    private static bool IsSlopeUpSection(TrackSectionDefinition section)
    {
        return section.FeatureType   == TrackFeatureType.SlopeUp
            && section.HasSurface
            && section.StructureType == TrackStructureType.SolidTrack;
    }

    private static bool IsImmediatePreGapSection(
        TrackSectionDefinition               section,
        IReadOnlyList<TrackSectionDefinition> sections,
        int                                  sectionIndex)
    {
        if (section.StructureType != TrackStructureType.SolidTrack) return false;
        if (!section.HasSurface) return false;

        int nextIndex = sectionIndex + 1;
        if (nextIndex >= sections.Count) return false;

        TrackSectionDefinition nextSection = sections[nextIndex];

        bool nextIsGap = !nextSection.HasSurface
            || nextSection.StructureType == TrackStructureType.RailTrack;

        if (!nextIsGap) return false;

        float gap = nextSection.StartDistance - section.EndDistance;
        return gap <= GapEdgeSafetyOffset;
    }

    private static float ResolveOpportunityDistance(
        TrackSectionDefinition section,
        PowerUpOpportunityKind opportunityKind)
    {
        return opportunityKind == PowerUpOpportunityKind.BeforeGap
            ? section.EndDistance
            : section.StartDistance;
    }

    private float ResolveSpeedBoostEndDistance(
        TrackSectionDefinition section,
        PowerUpOpportunityKind opportunityKind,
        float                  usableEnd)
    {
        if (opportunityKind == PowerUpOpportunityKind.BeforeGap)
            return Mathf.Min(section.EndDistance, usableEnd);

        return Mathf.Min(section.EndDistance, usableEnd);
    }

    private static float GetSectionCenterDistance(TrackSectionDefinition section)
    {
        return (section.StartDistance + section.EndDistance) * 0.5f;
    }

    #endregion

    #region Helpers

    private Transform CreateRoot()
    {
        GameObject root = new GameObject(RootName);
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale    = Vector3.one;
        return root.transform;
    }

    private static Vector3 ResolveSafeForward(Vector3 forward)
    {
        return forward.sqrMagnitude <= 0.0001f ? Vector3.forward : forward.normalized;
    }

    private void OnValidate()
    {
        slopeOpportunityChance  = Mathf.Clamp01(slopeOpportunityChance);
        gapOpportunityChance    = Mathf.Clamp01(gapOpportunityChance);

        minDistanceBetweenSpeedBoosts    = Mathf.Max(0f, minDistanceBetweenSpeedBoosts);
        speedBoostSurfaceOffset          = Mathf.Max(0f, speedBoostSurfaceOffset);
        speedBoostLeadInDistance         = Mathf.Max(0f, speedBoostLeadInDistance);
        speedBoostSegmentLength          = Mathf.Max(0.25f, speedBoostSegmentLength);
        minimumSpeedBoostSegmentVisualLength = Mathf.Max(0.1f, minimumSpeedBoostSegmentVisualLength);

        minCollectiblesPerLevel        = Mathf.Max(0, minCollectiblesPerLevel);
        maxCollectiblesPerLevel        = Mathf.Max(minCollectiblesPerLevel, maxCollectiblesPerLevel);
        minDistanceBetweenCollectibles = Mathf.Max(5f, minDistanceBetweenCollectibles);

        debugMaxSpeedBoosts              = Mathf.Max(1, debugMaxSpeedBoosts);
        debugMinDistanceBetweenSpeedBoosts = Mathf.Max(0f, debugMinDistanceBetweenSpeedBoosts);
    }

    #endregion

    #region Nested Types

    private enum PowerUpOpportunityKind
    {
        Debug      = 0,
        SlopeStart = 1,
        BeforeGap  = 2,
    }

    #endregion
}