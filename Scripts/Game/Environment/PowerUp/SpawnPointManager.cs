using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera y gestiona los checkpoints del nivel.
///
/// El prefab se instancia tal cual, sin modificar escala, sin añadir componentes.
/// Responsabilidades del diseñador sobre el prefab:
/// - Collider con IsTrigger activo.
/// - Componente <see cref="SpawnPointTrigger"/> configurado.
/// - Visual base (siempre visible) y visual de vida asignados en el Inspector del prefab.
///
/// El manager solo posiciona y orienta el prefab; toda la lógica visual y de
/// detección vive en <see cref="SpawnPointTrigger"/>.
/// </summary>
public sealed class SpawnPointManager : MonoBehaviour
{
    #region Constants

    private const string RootName                  = "GeneratedSpawnPoints";
    private const float  DefaultRespawnHeightOffset = 1.2f;
    private const float  MinimumValidSectionLength  = 0.001f;
    private const float  CheckpointDistanceTolerance = 0.01f;

    #endregion

    #region Inspector

    [Header("Prefab")]
    [SerializeField]
    [Tooltip("Prefab del checkpoint.\n" +
             "Requisitos mínimos del prefab:\n" +
             "  • Un Collider con Is Trigger activo.\n" +
             "  • Un componente SpawnPointTrigger con sus visuales asignados.\n" +
             "El generador lo instancia tal cual: no modifica escala ni añade componentes.")]
    private GameObject spawnPointPrefab;

    [Header("Configuración de Posición")]
    [SerializeField]
    [Tooltip("Elevación del prefab sobre la superficie de la pista en metros.")]
    private float triggerElevation = 0.1f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Altura adicional sobre la superficie donde reaparece la bola al usar este checkpoint.")]
    private float respawnHeightOffset = DefaultRespawnHeightOffset;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Offset desde el inicio de una sección válida para no colocar el checkpoint\n" +
             "exactamente en la unión entre secciones.")]
    private float sectionStartPadding = 1f;

    [Header("Fracciones")]
    [SerializeField]
    [Tooltip("Fracciones normalizadas de la pista donde se colocan los checkpoints.\n" +
             "0 = inicio, 1 = fin. Valores recomendados: 0.25, 0.5, 0.75.")]
    private float[] spawnFractions = { 0.25f, 0.5f, 0.75f };

    [Header("Debug")]
    [SerializeField]
    [Tooltip("Si está activo, imprime logs de generación.")]
    private bool enableDebugLogs = true;

    #endregion

    #region Runtime

    private readonly List<SpawnPointTrigger> activeTriggers = new List<SpawnPointTrigger>();
    private Transform generatedRoot;
    private float highestActivatedDistance = -1f;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        GameEvents.OnLevelReset += HandleLevelReset;
    }

    private void OnDisable()
    {
        GameEvents.OnLevelReset -= HandleLevelReset;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Coloca checkpoints a lo largo de la pista generada.
    /// Solo los coloca en secciones sólidas con superficie; nunca sobre rieles ni gaps.
    /// El prefab se instancia sin modificar escala ni añadir componentes.
    /// </summary>
    public void PlaceSpawnPoints(
        TrackRuntimeMap       map,
        BallRespawnController respawnController,
        LivesController       livesController)
    {
        ClearSpawnPoints();

        if (!CanPlaceSpawnPoints(map, respawnController, livesController)) return;

        highestActivatedDistance = -1f;
        generatedRoot = CreateRoot();

        float totalDistance    = map.PathSampler.TotalDistance;
        int   skippedFractions = 0;

        for (int i = 0; i < spawnFractions.Length; i++)
        {
            float normalizedFraction = Mathf.Clamp01(spawnFractions[i]);

            if (normalizedFraction <= 0f || normalizedFraction >= 1f)
            {
                skippedFractions++;
                continue;
            }

            float requestedDistance = totalDistance * normalizedFraction;

            if (!TryResolveValidSpawnDistance(map, requestedDistance, out float resolvedDistance))
            {
                skippedFractions++;
                continue;
            }

            TrackSample       sample  = map.PathSampler.SampleAtDistance(resolvedDistance);
            SpawnPointTrigger trigger = CreateSpawnPoint(
                sample, normalizedFraction, resolvedDistance, i,
                respawnController, livesController);

            if (trigger != null)
            {
                activeTriggers.Add(trigger);
            }
            else
            {
                skippedFractions++;
            }
        }

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[SPAWN POINTS] {activeTriggers.Count} checkpoints colocados. " +
                $"Omitidos: {skippedFractions}.",
                this);
        }
    }

    /// <summary>
    /// Resetea todos los checkpoints a no visitado.
    /// Llamado cuando el jugador se queda sin vidas y vuelve al inicio del nivel.
    /// </summary>
    public void ResetAllSpawnPoints()
    {
        highestActivatedDistance = -1f;

        for (int i = 0; i < activeTriggers.Count; i++)
        {
            if (activeTriggers[i] != null)
                activeTriggers[i].ResetTrigger();
        }
    }

    /// <summary>Destruye todos los checkpoints generados previamente.</summary>
    public void ClearSpawnPoints()
    {
        activeTriggers.Clear();
        highestActivatedDistance = -1f;

        Transform existingRoot = transform.Find(RootName);
        if (existingRoot == null) { generatedRoot = null; return; }

        existingRoot.SetParent(null);

#if UNITY_EDITOR
        if (!Application.isPlaying) DestroyImmediate(existingRoot.gameObject);
        else                        Destroy(existingRoot.gameObject);
#else
        Destroy(existingRoot.gameObject);
#endif
        generatedRoot = null;
    }

    /// <summary>
    /// Indica si un checkpoint puede activarse.
    /// Previene que checkpoints anteriores sobrescriban el respawn cuando el jugador ya avanzó más.
    /// </summary>
    public bool CanActivateCheckpoint(float checkpointDistance)
    {
        return checkpointDistance + CheckpointDistanceTolerance >= highestActivatedDistance;
    }

    /// <summary>Registra la distancia de un checkpoint como la más avanzada activada.</summary>
    public void RegisterCheckpointActivation(float checkpointDistance)
    {
        if (checkpointDistance > highestActivatedDistance)
            highestActivatedDistance = checkpointDistance;
    }

    #endregion

    #region Creation

    /// <summary>
    /// Instancia el prefab en la posición correcta sin modificar su escala ni añadir componentes.
    /// Devuelve <c>null</c> si el prefab no tiene <see cref="SpawnPointTrigger"/>.
    /// </summary>
    private SpawnPointTrigger CreateSpawnPoint(
        TrackSample           sample,
        float                 normalizedFraction,
        float                 resolvedDistance,
        int                   index,
        BallRespawnController respawnController,
        LivesController       livesController)
    {
        Vector3    triggerPosition = sample.Position + Vector3.up * triggerElevation;
        Vector3    respawnPosition = sample.Position + Vector3.up * respawnHeightOffset;
        Quaternion rotation        = Quaternion.LookRotation(ResolveSafeForward(sample.Forward), Vector3.up);

        GameObject instance = Instantiate(spawnPointPrefab, triggerPosition, rotation, generatedRoot);

        int percentage = Mathf.RoundToInt(normalizedFraction * 100f);
        instance.name = $"SpawnPoint_{index + 1:D2}_{percentage}pct_{resolvedDistance:F0}m";

        // El prefab se coloca tal cual. Sin modificación de escala.
        // Sin adición automática de Collider ni SpawnPointTrigger:
        // son responsabilidad del diseñador sobre el prefab.
        SpawnPointTrigger trigger = instance.GetComponentInChildren<SpawnPointTrigger>(true);

        if (trigger == null)
        {
            Debug.LogWarning(
                $"[SPAWN POINTS] El prefab '{spawnPointPrefab.name}' no contiene un SpawnPointTrigger. " +
                $"Checkpoint omitido en {resolvedDistance:F0}m.",
                this);
            Destroy(instance);
            return null;
        }

        trigger.Initialize(
            this,
            respawnController,
            livesController,
            respawnPosition,
            rotation,
            resolvedDistance);

        return trigger;
    }

    private static Vector3 ResolveSafeForward(Vector3 forward)
        => forward.sqrMagnitude <= 0.0001f ? Vector3.forward : forward.normalized;

    private Transform CreateRoot()
    {
        GameObject root = new GameObject(RootName);
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale    = Vector3.one;
        return root.transform;
    }

    #endregion

    #region Path Sampling

    private bool TryResolveValidSpawnDistance(
        TrackRuntimeMap map,
        float           requestedDistance,
        out float       resolvedDistance)
    {
        resolvedDistance = requestedDistance;

        System.Collections.Generic.IReadOnlyList<TrackSectionDefinition> sections = map.Sections;

        for (int i = 0; i < sections.Count; i++)
        {
            TrackSectionDefinition section = sections[i];

            if (requestedDistance < section.StartDistance || requestedDistance > section.EndDistance)
                continue;

            if (IsValidSpawnSection(section))
            {
                resolvedDistance = Mathf.Clamp(requestedDistance, section.StartDistance + sectionStartPadding, section.EndDistance);
                return true;
            }

            // Sección inválida en la posición exacta → buscar hacia adelante
            for (int j = i + 1; j < sections.Count; j++)
            {
                TrackSectionDefinition candidate = sections[j];
                if (!IsValidSpawnSection(candidate)) continue;

                resolvedDistance = Mathf.Min(
                    candidate.EndDistance,
                    candidate.StartDistance + sectionStartPadding);
                return true;
            }

            return false;
        }

        return false;
    }

    private static bool IsValidSpawnSection(TrackSectionDefinition section)
    {
        if (section.EndDistance - section.StartDistance <= MinimumValidSectionLength) return false;
        if (!section.HasSurface) return false;
        return section.StructureType == TrackStructureType.SolidTrack;
    }

    #endregion

    #region Validation

    private bool CanPlaceSpawnPoints(
        TrackRuntimeMap       map,
        BallRespawnController respawnController,
        LivesController       livesController)
    {
        if (spawnPointPrefab == null)
        {
            Debug.LogWarning("[SPAWN POINTS] spawnPointPrefab no asignado.", this);
            return false;
        }
        if (map == null || map.PathSampler == null)
        {
            Debug.LogWarning("[SPAWN POINTS] TrackRuntimeMap o PathSampler es null.", this);
            return false;
        }
        if (map.Sections == null || map.Sections.Count == 0)
        {
            Debug.LogWarning("[SPAWN POINTS] No hay secciones en el mapa.", this);
            return false;
        }
        if (spawnFractions == null || spawnFractions.Length == 0)
        {
            Debug.LogWarning("[SPAWN POINTS] No hay fracciones configuradas.", this);
            return false;
        }
        if (respawnController == null)
        {
            Debug.LogWarning("[SPAWN POINTS] BallRespawnController no asignado.", this);
            return false;
        }
        if (livesController == null)
        {
            Debug.LogWarning("[SPAWN POINTS] LivesController no asignado.", this);
            return false;
        }
        return true;
    }

    private void OnValidate()
    {
        triggerElevation    = Mathf.Max(0f, triggerElevation);
        respawnHeightOffset = Mathf.Max(0f, respawnHeightOffset);
        sectionStartPadding = Mathf.Max(0f, sectionStartPadding);

        if (spawnFractions == null) return;
        for (int i = 0; i < spawnFractions.Length; i++)
            spawnFractions[i] = Mathf.Clamp01(spawnFractions[i]);
    }

    #endregion

    #region Event Handlers

    private void HandleLevelReset() => ResetAllSpawnPoints();

    #endregion
}