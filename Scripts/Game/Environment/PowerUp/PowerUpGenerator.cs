using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera power-ups únicamente en oportunidades válidas del track:
/// inicio de pendientes ascendentes y zonas inmediatamente previas a gaps.
/// Nunca genera power-ups sobre rieles, aunque el riel tenga pendiente.
/// </summary>
public sealed class PowerUpGenerator : MonoBehaviour
{
    #region Constants

    private const string RootName = "GeneratedPowerUps";
    private const int SeedOffset = 777;
    private const float DefaultInvalidDistance = -999f;
    private const float MinimumValidSectionLength = 0.001f;
    private const float GapEdgeSafetyOffset = 0.08f;

    #endregion

    #region Inspector

    [Header("Prefabs")]
    [SerializeField]
    [Tooltip("Prefab del trampolín. Requiere un Collider marcado como IsTrigger y un componente JumpPad.")]
    private GameObject jumpPadPrefab;

    [SerializeField]
    [Tooltip("Prefab de segmento de aceleración. El eje Z debe coincidir con la dirección de avance y su longitud base debe ser 1 unidad.")]
    private GameObject speedBoostZonePrefab;

    [Header("Reglas de Generación")]
    [SerializeField]
    [Tooltip("Permite generar power-ups en pendientes ascendentes sobre pista sólida.")]
    private bool enableSlopeOpportunities = true;

    [SerializeField]
    [Tooltip("Permite generar power-ups justo antes de gaps desde pista sólida.")]
    private bool enableGapOpportunities = true;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Probabilidad de generar algún power-up al inicio de una pendiente ascendente.")]
    private float slopeOpportunityChance = 0.65f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Probabilidad de generar algún power-up justo antes de un gap.")]
    private float gapOpportunityChance = 0.85f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Probabilidad de elegir SpeedBoost cuando también puede elegirse JumpPad. 0.5 = ambos tienen la misma probabilidad.")]
    private float speedBoostSelectionChance = 0.5f;

    [Header("Espaciado")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("Distancia mínima entre dos oportunidades aceptadas.")]
    private float minDistanceBetweenPowerUps = 14f;

    [Header("Jump Pad")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("Offset vertical mínimo para evitar que el JumpPad quede embebido en la pista.")]
    private float jumpPadSurfaceOffset = 0.08f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Distancia desde el inicio de la pendiente donde se coloca el JumpPad.")]
    private float jumpPadSlopeStartForwardOffset = 0.35f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Distancia antes del borde del gap donde se coloca el JumpPad.")]
    private float jumpPadBeforeGapOffset = 0.75f;

    [Header("Speed Boost")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("Offset vertical mínimo para evitar z-fighting. Debe ser bajo para quedar sobre la pista.")]
    private float speedBoostSurfaceOffset = 0.01f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Distancia adicional antes del inicio de la pendiente/rampa desde donde empieza el SpeedBoost.")]
    private float speedBoostLeadInDistance = 0.75f;

    [SerializeField]
    [Min(0.25f)]
    [Tooltip("Longitud aproximada de cada segmento de SpeedBoost. Valores menores siguen mejor la pista.")]
    private float speedBoostSegmentLength = 1.25f;

    [SerializeField]
    [Min(0.1f)]
    [Tooltip("Longitud mínima visual de cada segmento de SpeedBoost.")]
    private float minimumSpeedBoostSegmentVisualLength = 0.25f;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("Si está activo, imprime resumen de generación.")]
    private bool enableDebugLogs = true;

    [SerializeField]
    [Tooltip("SOLO DEBUG. Si está activo, ignora reglas de oportunidad, pero aun así evita rieles y gaps.")]
    private bool debugGenerateAnywhere = false;

    [SerializeField]
    [Min(1)]
    [Tooltip("SOLO DEBUG. Cantidad máxima de power-ups en modo Debug Generate Anywhere.")]
    private int debugMaxPowerUps = 20;

    [SerializeField]
    [Min(0f)]
    [Tooltip("SOLO DEBUG. Distancia mínima entre power-ups en modo Debug Generate Anywhere.")]
    private float debugMinDistanceBetweenPowerUps = 8f;

    #endregion

    #region Runtime

    private Transform generatedRoot;
    private float lastAcceptedPowerUpDistance = DefaultInvalidDistance;

    #endregion

    #region Public API

    /// <summary>
    /// Mantiene compatibilidad con el flujo del InfiniteLevelManager.
    /// </summary>
    public void DisableAutoGeneration()
    {
    }

    /// <summary>
    /// Genera los power-ups sobre el mapa procedural recibido.
    /// </summary>
    public void GeneratePowerUps(
        TrackRuntimeMap map,
        int seed,
        float safeStartLength = 0f,
        float safeEndLength = 0f)
    {
        ClearPowerUps();

        if (!CanGenerate(map))
        {
            return;
        }

        generatedRoot = CreateRoot();

        if (debugGenerateAnywhere)
        {
            GenerateDebugPowerUps(map, seed);
            return;
        }

        GenerateGameplayPowerUps(map, seed, safeStartLength, safeEndLength);
    }

    /// <summary>
    /// Elimina los power-ups generados previamente.
    /// </summary>
    public void ClearPowerUps()
    {
        Transform existingRoot = transform.Find(RootName);

        if (existingRoot != null)
        {
            existingRoot.SetParent(null);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(existingRoot.gameObject);
            }
            else
            {
                Destroy(existingRoot.gameObject);
            }
#else
            Destroy(existingRoot.gameObject);
#endif
        }

        generatedRoot = null;
        lastAcceptedPowerUpDistance = DefaultInvalidDistance;
    }

    #endregion

    #region Gameplay Generation

    /// <summary>
    /// Genera power-ups solamente en oportunidades válidas de gameplay.
    /// </summary>
    private void GenerateGameplayPowerUps(
        TrackRuntimeMap map,
        int seed,
        float safeStartLength,
        float safeEndLength)
    {
        System.Random random = new System.Random(seed + SeedOffset);
        IReadOnlyList<TrackSectionDefinition> sections = map.Sections;

        float totalDistance = map.PathSampler.TotalDistance;
        float usableStart = Mathf.Max(0f, safeStartLength);
        float usableEnd = Mathf.Max(usableStart, totalDistance - Mathf.Max(0f, safeEndLength));

        lastAcceptedPowerUpDistance = usableStart;

        int slopeOpportunities = 0;
        int gapOpportunities = 0;
        int acceptedOpportunities = 0;
        int jumpPadsPlaced = 0;
        int speedBoostPathsPlaced = 0;
        int speedBoostSegmentsPlaced = 0;
        int rejectedRailSections = 0;

        for (int i = 0; i < sections.Count; i++)
        {
            TrackSectionDefinition section = sections[i];

            if (section.StructureType == TrackStructureType.RailTrack)
            {
                rejectedRailSections++;
                continue;
            }

            if (!CanUseGameplaySection(section, usableStart, usableEnd))
            {
                continue;
            }

            bool isSlopeOpportunity = enableSlopeOpportunities && IsSlopeUpSection(section);
            bool isGapOpportunity = enableGapOpportunities && IsImmediatePreGapSection(section, sections, i);

            if (!isSlopeOpportunity && !isGapOpportunity)
            {
                continue;
            }

            PowerUpOpportunityKind opportunityKind = isGapOpportunity
                ? PowerUpOpportunityKind.BeforeGap
                : PowerUpOpportunityKind.SlopeStart;

            float opportunityDistance = ResolveOpportunityDistance(section, opportunityKind);

            if (opportunityDistance < usableStart || opportunityDistance > usableEnd)
            {
                continue;
            }

            if (opportunityDistance - lastAcceptedPowerUpDistance < minDistanceBetweenPowerUps)
            {
                continue;
            }

            if (opportunityKind == PowerUpOpportunityKind.SlopeStart)
            {
                slopeOpportunities++;
            }
            else
            {
                gapOpportunities++;
            }

            float opportunityChance = opportunityKind == PowerUpOpportunityKind.BeforeGap
                ? gapOpportunityChance
                : slopeOpportunityChance;

            if (random.NextDouble() > opportunityChance)
            {
                continue;
            }

            PowerUpSpawnType selectedType = ResolvePowerUpType(random);

            if (selectedType == PowerUpSpawnType.None)
            {
                continue;
            }

            if (selectedType == PowerUpSpawnType.SpeedBoost)
            {
                float startDistance = Mathf.Max(usableStart, section.StartDistance - speedBoostLeadInDistance);
                float endDistance = ResolveSpeedBoostEndDistance(section, opportunityKind, usableEnd);

                int createdSegments = PlaceSpeedBoostPath(map, startDistance, endDistance, opportunityKind);

                if (createdSegments > 0)
                {
                    speedBoostPathsPlaced++;
                    speedBoostSegmentsPlaced += createdSegments;
                    acceptedOpportunities++;
                    lastAcceptedPowerUpDistance = opportunityDistance;
                }

                continue;
            }

            if (selectedType == PowerUpSpawnType.JumpPad)
            {
                float jumpDistance = ResolveJumpPadDistance(section, opportunityKind);
                jumpDistance = Mathf.Clamp(jumpDistance, usableStart, usableEnd);

                PlaceJumpPad(map, jumpDistance, opportunityKind);

                jumpPadsPlaced++;
                acceptedOpportunities++;
                lastAcceptedPowerUpDistance = opportunityDistance;
            }
        }

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[POWER UPS] Generación gameplay completa.\n" +
                $"  Sections total           : {sections.Count}\n" +
                $"  Rail sections rejected   : {rejectedRailSections}\n" +
                $"  Slope opportunities      : {slopeOpportunities}\n" +
                $"  Gap opportunities        : {gapOpportunities}\n" +
                $"  Accepted opportunities   : {acceptedOpportunities}\n" +
                $"  Jump Pads placed         : {jumpPadsPlaced}\n" +
                $"  Speed Boost paths placed : {speedBoostPathsPlaced}\n" +
                $"  Speed Boost segments     : {speedBoostSegmentsPlaced}\n" +
                $"  Usable range             : {usableStart:F1} - {usableEnd:F1}",
                this);
        }
    }

    /// <summary>
    /// Decide si la oportunidad genera JumpPad o SpeedBoost, garantizando exclusión entre ambos.
    /// Si ambos prefabs existen, usa una probabilidad 50/50 por defecto.
    /// </summary>
    private PowerUpSpawnType ResolvePowerUpType(System.Random random)
    {
        bool canSpawnJump = jumpPadPrefab != null;
        bool canSpawnSpeed = speedBoostZonePrefab != null;

        if (!canSpawnJump && !canSpawnSpeed)
        {
            return PowerUpSpawnType.None;
        }

        if (canSpawnJump && !canSpawnSpeed)
        {
            return PowerUpSpawnType.JumpPad;
        }

        if (!canSpawnJump && canSpawnSpeed)
        {
            return PowerUpSpawnType.SpeedBoost;
        }

        return random.NextDouble() < speedBoostSelectionChance
            ? PowerUpSpawnType.SpeedBoost
            : PowerUpSpawnType.JumpPad;
    }

    #endregion

    #region Debug Generation

    /// <summary>
    /// Genera power-ups ignorando las oportunidades específicas.
    /// Aun en debug evita rieles y gaps para no validar un comportamiento inválido.
    /// </summary>
    private void GenerateDebugPowerUps(TrackRuntimeMap map, int seed)
    {
        System.Random random = new System.Random(seed + SeedOffset);
        IReadOnlyList<TrackSectionDefinition> sections = map.Sections;

        int placed = 0;
        int rejectedRailSections = 0;
        float lastDistance = 0f;

        for (int i = 0; i < sections.Count; i++)
        {
            if (placed >= debugMaxPowerUps)
            {
                break;
            }

            TrackSectionDefinition section = sections[i];

            if (section.StructureType == TrackStructureType.RailTrack)
            {
                rejectedRailSections++;
                continue;
            }

            if (!CanUseDebugSection(section))
            {
                continue;
            }

            float distance = GetSectionCenterDistance(section);

            if (distance - lastDistance < debugMinDistanceBetweenPowerUps)
            {
                continue;
            }

            if (jumpPadPrefab != null && (placed % 2 == 0 || speedBoostZonePrefab == null))
            {
                PlaceJumpPad(map, distance, PowerUpOpportunityKind.Debug);
            }
            else if (speedBoostZonePrefab != null)
            {
                PlaceSpeedBoostPath(map, section.StartDistance, section.EndDistance, PowerUpOpportunityKind.Debug);
            }

            placed++;
            lastDistance = distance;
        }

        Debug.LogWarning(
            $"[POWER UPS DEBUG] Debug Generate Anywhere está activo. " +
            $"Se generaron {placed} power-ups solo en SolidTrack. " +
            $"Rieles rechazados: {rejectedRailSections}.",
            this);
    }

    #endregion

    #region Placement

    /// <summary>
    /// Instancia un JumpPad puntual sobre la pista.
    /// </summary>
    private void PlaceJumpPad(
        TrackRuntimeMap map,
        float distance,
        PowerUpOpportunityKind opportunityKind)
    {
        TrackSample sample = map.PathSampler.SampleAtDistance(distance);

        Vector3 position = sample.Position + Vector3.up * jumpPadSurfaceOffset;
        Quaternion rotation = Quaternion.LookRotation(ResolveSafeForward(sample.Forward), Vector3.up);

        GameObject instance = Instantiate(jumpPadPrefab, position, rotation, generatedRoot);
        instance.name = $"JumpPad_{opportunityKind}_{distance:F0}m";
    }

    /// <summary>
    /// Crea una ruta de SpeedBoost segmentada para seguir curvatura, pendiente y rampas.
    /// </summary>
    private int PlaceSpeedBoostPath(
        TrackRuntimeMap map,
        float startDistance,
        float endDistance,
        PowerUpOpportunityKind opportunityKind)
    {
        startDistance = Mathf.Max(0f, startDistance);
        endDistance = Mathf.Max(startDistance, endDistance);

        float totalLength = endDistance - startDistance;

        if (totalLength <= MinimumValidSectionLength)
        {
            return 0;
        }

        GameObject pathRootObject = new GameObject($"SpeedBoostPath_{opportunityKind}_{startDistance:F0}m_{endDistance:F0}m");
        pathRootObject.transform.SetParent(generatedRoot);
        pathRootObject.transform.localPosition = Vector3.zero;
        pathRootObject.transform.localRotation = Quaternion.identity;
        pathRootObject.transform.localScale = Vector3.one;

        Transform pathRoot = pathRootObject.transform;

        int segmentCount = Mathf.Max(1, Mathf.CeilToInt(totalLength / speedBoostSegmentLength));
        float resolvedSegmentLength = totalLength / segmentCount;
        int createdSegments = 0;

        for (int i = 0; i < segmentCount; i++)
        {
            float segmentStartDistance = startDistance + resolvedSegmentLength * i;
            float segmentEndDistance = i == segmentCount - 1
                ? endDistance
                : segmentStartDistance + resolvedSegmentLength;

            if (CreateSpeedBoostSegment(map, segmentStartDistance, segmentEndDistance, pathRoot, i))
            {
                createdSegments++;
            }
        }

        if (createdSegments == 0)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(pathRootObject);
            }
            else
            {
                Destroy(pathRootObject);
            }
#else
            Destroy(pathRootObject);
#endif
        }

        return createdSegments;
    }

    /// <summary>
    /// Instancia un segmento individual de SpeedBoost entre dos muestras del PathSampler.
    /// </summary>
    private bool CreateSpeedBoostSegment(
        TrackRuntimeMap map,
        float startDistance,
        float endDistance,
        Transform parent,
        int segmentIndex)
    {
        TrackSample startSample = map.PathSampler.SampleAtDistance(startDistance);
        TrackSample endSample = map.PathSampler.SampleAtDistance(endDistance);

        Vector3 startPosition = startSample.Position + Vector3.up * speedBoostSurfaceOffset;
        Vector3 endPosition = endSample.Position + Vector3.up * speedBoostSurfaceOffset;

        Vector3 segment = endPosition - startPosition;
        float segmentLength = segment.magnitude;

        if (segmentLength <= MinimumValidSectionLength)
        {
            return false;
        }

        Vector3 position = (startPosition + endPosition) * 0.5f;
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

    /// <summary>
    /// Valida si el sistema tiene los datos mínimos necesarios para generar.
    /// </summary>
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

        if (jumpPadPrefab == null && speedBoostZonePrefab == null)
        {
            Debug.LogWarning("[POWER UPS] No hay prefabs asignados.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Valida si una sección puede participar en reglas de gameplay.
    /// Los power-ups solo pueden generarse sobre pista sólida, nunca sobre rieles ni gaps.
    /// </summary>
    private static bool CanUseGameplaySection(
        TrackSectionDefinition section,
        float usableStart,
        float usableEnd)
    {
        if (section.EndDistance - section.StartDistance <= MinimumValidSectionLength)
        {
            return false;
        }

        if (section.StartDistance < usableStart || section.EndDistance > usableEnd)
        {
            return false;
        }

        if (!section.HasSurface)
        {
            return false;
        }

        return section.StructureType == TrackStructureType.SolidTrack;
    }

    /// <summary>
    /// Valida una sección para modo debug.
    /// Incluso en debug evita rieles y gaps para probar solo comportamiento real de pista sólida.
    /// </summary>
    private static bool CanUseDebugSection(TrackSectionDefinition section)
    {
        if (section.EndDistance - section.StartDistance <= MinimumValidSectionLength)
        {
            return false;
        }

        if (!section.HasSurface)
        {
            return false;
        }

        return section.StructureType == TrackStructureType.SolidTrack;
    }

    /// <summary>
    /// Indica si una sección es una pendiente ascendente válida para power-ups.
    /// Aunque un riel suba, no se considera válido.
    /// </summary>
    private static bool IsSlopeUpSection(TrackSectionDefinition section)
    {
        return section.FeatureType == TrackFeatureType.SlopeUp
            && section.HasSurface
            && section.StructureType == TrackStructureType.SolidTrack;
    }

    /// <summary>
    /// Indica si la sección actual es inmediatamente anterior a un gap.
    /// No permite rieles como sección válida previa al gap.
    /// </summary>
    private static bool IsImmediatePreGapSection(
        TrackSectionDefinition section,
        IReadOnlyList<TrackSectionDefinition> sections,
        int index)
    {
        if (!section.HasSurface)
        {
            return false;
        }

        if (section.StructureType != TrackStructureType.SolidTrack)
        {
            return false;
        }

        if (index + 1 >= sections.Count)
        {
            return false;
        }

        TrackSectionDefinition nextSection = sections[index + 1];

        return nextSection.StructureType == TrackStructureType.Gap
            || nextSection.FeatureType == TrackFeatureType.Gap;
    }

    #endregion

    #region Distance Resolution

    /// <summary>
    /// Resuelve la distancia lógica de la oportunidad.
    /// </summary>
    private static float ResolveOpportunityDistance(
        TrackSectionDefinition section,
        PowerUpOpportunityKind opportunityKind)
    {
        return opportunityKind == PowerUpOpportunityKind.BeforeGap
            ? section.EndDistance
            : section.StartDistance;
    }

    /// <summary>
    /// Resuelve hasta dónde llega el SpeedBoost según el tipo de oportunidad.
    /// </summary>
    private static float ResolveSpeedBoostEndDistance(
        TrackSectionDefinition section,
        PowerUpOpportunityKind opportunityKind,
        float usableEnd)
    {
        if (opportunityKind == PowerUpOpportunityKind.BeforeGap)
        {
            return Mathf.Min(usableEnd, section.EndDistance - GapEdgeSafetyOffset);
        }

        return Mathf.Min(usableEnd, section.EndDistance);
    }

    /// <summary>
    /// Resuelve la distancia donde debe colocarse el JumpPad.
    /// </summary>
    private float ResolveJumpPadDistance(
        TrackSectionDefinition section,
        PowerUpOpportunityKind opportunityKind)
    {
        if (opportunityKind == PowerUpOpportunityKind.BeforeGap)
        {
            return Mathf.Max(section.StartDistance, section.EndDistance - jumpPadBeforeGapOffset);
        }

        return Mathf.Min(section.EndDistance, section.StartDistance + jumpPadSlopeStartForwardOffset);
    }

    /// <summary>
    /// Calcula la distancia central de una sección.
    /// </summary>
    private static float GetSectionCenterDistance(TrackSectionDefinition section)
    {
        return (section.StartDistance + section.EndDistance) * 0.5f;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Crea un nuevo contenedor raíz para los power-ups generados.
    /// </summary>
    private Transform CreateRoot()
    {
        GameObject root = new GameObject(RootName);
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        return root.transform;
    }

    /// <summary>
    /// Resuelve un forward seguro para rotaciones.
    /// </summary>
    private static Vector3 ResolveSafeForward(Vector3 forward)
    {
        return forward.sqrMagnitude <= 0.0001f
            ? Vector3.forward
            : forward.normalized;
    }

    private void OnValidate()
    {
        slopeOpportunityChance = Mathf.Clamp01(slopeOpportunityChance);
        gapOpportunityChance = Mathf.Clamp01(gapOpportunityChance);
        speedBoostSelectionChance = Mathf.Clamp01(speedBoostSelectionChance);

        minDistanceBetweenPowerUps = Mathf.Max(0f, minDistanceBetweenPowerUps);

        jumpPadSurfaceOffset = Mathf.Max(0f, jumpPadSurfaceOffset);
        jumpPadSlopeStartForwardOffset = Mathf.Max(0f, jumpPadSlopeStartForwardOffset);
        jumpPadBeforeGapOffset = Mathf.Max(0f, jumpPadBeforeGapOffset);

        speedBoostSurfaceOffset = Mathf.Max(0f, speedBoostSurfaceOffset);
        speedBoostLeadInDistance = Mathf.Max(0f, speedBoostLeadInDistance);
        speedBoostSegmentLength = Mathf.Max(0.25f, speedBoostSegmentLength);
        minimumSpeedBoostSegmentVisualLength = Mathf.Max(0.1f, minimumSpeedBoostSegmentVisualLength);

        debugMaxPowerUps = Mathf.Max(1, debugMaxPowerUps);
        debugMinDistanceBetweenPowerUps = Mathf.Max(0f, debugMinDistanceBetweenPowerUps);
    }

    #endregion

    #region Nested Types

    private enum PowerUpSpawnType
    {
        None = 0,
        JumpPad = 1,
        SpeedBoost = 2
    }

    private enum PowerUpOpportunityKind
    {
        Debug = 0,
        SlopeStart = 1,
        BeforeGap = 2
    }

    #endregion
}