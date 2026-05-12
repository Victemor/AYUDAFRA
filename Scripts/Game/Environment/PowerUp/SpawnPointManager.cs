using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera y gestiona los checkpoints del nivel.
/// Los checkpoints nunca se colocan sobre gaps ni sobre rieles.
/// Además, impide que un checkpoint anterior sobrescriba el respawn si el jugador ya activó uno más avanzado.
/// </summary>
public sealed class SpawnPointManager : MonoBehaviour
{
    #region Constants

    private const string RootName = "GeneratedSpawnPoints";
    private const float DefaultRespawnHeightOffset = 1.2f;
    private const float MinimumTriggerHeight = 0.1f;
    private const float MinimumTriggerDepth = 0.05f;
    private const float MinimumTrackWidth = 0.1f;
    private const float MinimumValidSectionLength = 0.001f;
    private const float CheckpointDistanceTolerance = 0.01f;

    #endregion

    #region Inspector

    [Header("Prefab")]
    [SerializeField]
    [Tooltip("Prefab del checkpoint. Debe tener un Collider con Is Trigger activo. Si no tiene SpawnPointTrigger, se agrega automáticamente.")]
    private GameObject spawnPointPrefab;

    [Header("Configuración")]
    [SerializeField]
    [Tooltip("Fracciones de la pista donde se colocan los checkpoints. Valores entre 0 y 1. Por defecto: 25%, 50% y 75%.")]
    private float[] spawnFractions = { 0.25f, 0.5f, 0.75f };

    [SerializeField]
    [Min(MinimumTriggerHeight)]
    [Tooltip("Altura del volumen trigger del checkpoint.")]
    private float triggerHeight = 3f;

    [SerializeField]
    [Min(MinimumTriggerDepth)]
    [Tooltip("Profundidad del trigger en la dirección de avance.")]
    private float triggerDepth = 0.5f;

    [SerializeField]
    [Tooltip("Elevación del trigger sobre la superficie de la pista.")]
    private float triggerElevation = 0.1f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Altura adicional sobre la superficie donde reaparece la bola al usar este checkpoint.")]
    private float respawnHeightOffset = DefaultRespawnHeightOffset;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Distancia mínima desde el inicio de una sección válida para evitar colocar el checkpoint exactamente en una unión.")]
    private float sectionStartPadding = 1f;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("Si está activo, imprime logs útiles de generación.")]
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
    /// Coloca los checkpoints a lo largo de la pista generada.
    /// Nunca los coloca sobre rieles ni gaps.
    /// </summary>
    public void PlaceSpawnPoints(
        TrackRuntimeMap map,
        float trackWidth,
        BallRespawnController respawnController,
        LivesController livesController)
    {
        ClearSpawnPoints();

        if (!CanPlaceSpawnPoints(map, respawnController, livesController))
        {
            return;
        }

        highestActivatedDistance = -1f;
        generatedRoot = CreateRoot();

        float totalDistance = map.PathSampler.TotalDistance;
        float resolvedTrackWidth = Mathf.Max(MinimumTrackWidth, trackWidth);

        int skippedFractions = 0;

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

            TrackSample sample = map.PathSampler.SampleAtDistance(resolvedDistance);

            SpawnPointTrigger trigger = CreateSpawnPoint(
                sample,
                resolvedTrackWidth,
                normalizedFraction,
                resolvedDistance,
                i,
                respawnController,
                livesController);

            activeTriggers.Add(trigger);
        }

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[SPAWN POINTS] {activeTriggers.Count} checkpoints colocados. " +
                $"Fracciones omitidas: {skippedFractions}.",
                this);
        }
    }

    /// <summary>
    /// Resetea todos los checkpoints a no visitado.
    /// Esto sucede cuando el jugador se queda sin vidas y vuelve al inicio del nivel.
    /// </summary>
    public void ResetAllSpawnPoints()
    {
        highestActivatedDistance = -1f;

        for (int i = 0; i < activeTriggers.Count; i++)
        {
            if (activeTriggers[i] != null)
            {
                activeTriggers[i].ResetTrigger();
            }
        }
    }

    /// <summary>
    /// Destruye todos los checkpoints generados previamente.
    /// </summary>
    public void ClearSpawnPoints()
    {
        activeTriggers.Clear();
        highestActivatedDistance = -1f;

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
    }

    /// <summary>
    /// Indica si un checkpoint puede activarse.
    /// Evita que checkpoints anteriores sobrescriban el respawn cuando el jugador ya avanzó más.
    /// </summary>
    public bool CanActivateCheckpoint(float checkpointDistance)
    {
        return checkpointDistance + CheckpointDistanceTolerance >= highestActivatedDistance;
    }

    /// <summary>
    /// Registra la activación de un checkpoint como el checkpoint más avanzado.
    /// </summary>
    public void RegisterCheckpointActivation(float checkpointDistance)
    {
        highestActivatedDistance = Mathf.Max(highestActivatedDistance, checkpointDistance);
    }

    #endregion

    #region Distance Resolution

    private bool TryResolveValidSpawnDistance(
        TrackRuntimeMap map,
        float requestedDistance,
        out float resolvedDistance)
    {
        resolvedDistance = requestedDistance;

        IReadOnlyList<TrackSectionDefinition> sections = map.Sections;

        if (sections == null || sections.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < sections.Count; i++)
        {
            TrackSectionDefinition section = sections[i];

            if (requestedDistance < section.StartDistance || requestedDistance > section.EndDistance)
            {
                continue;
            }

            if (IsValidSpawnSection(section))
            {
                resolvedDistance = Mathf.Clamp(
                    requestedDistance,
                    section.StartDistance + sectionStartPadding,
                    section.EndDistance);

                return true;
            }

            return TryFindNextValidSection(sections, i + 1, out resolvedDistance);
        }

        return TryFindNextValidSection(sections, 0, out resolvedDistance);
    }

    private bool TryFindNextValidSection(
        IReadOnlyList<TrackSectionDefinition> sections,
        int startIndex,
        out float resolvedDistance)
    {
        resolvedDistance = 0f;

        for (int i = Mathf.Max(0, startIndex); i < sections.Count; i++)
        {
            TrackSectionDefinition section = sections[i];

            if (!IsValidSpawnSection(section))
            {
                continue;
            }

            resolvedDistance = Mathf.Min(
                section.EndDistance,
                section.StartDistance + sectionStartPadding);

            return true;
        }

        return false;
    }

    private static bool IsValidSpawnSection(TrackSectionDefinition section)
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

    #endregion

    #region Creation

    private SpawnPointTrigger CreateSpawnPoint(
        TrackSample sample,
        float trackWidth,
        float normalizedFraction,
        float resolvedDistance,
        int index,
        BallRespawnController respawnController,
        LivesController livesController)
    {
        Vector3 triggerPosition = sample.Position + Vector3.up * triggerElevation;
        Vector3 respawnPosition = sample.Position + Vector3.up * respawnHeightOffset;
        Quaternion respawnRotation = Quaternion.LookRotation(sample.Forward, Vector3.up);

        GameObject triggerObject = Instantiate(
            spawnPointPrefab,
            triggerPosition,
            respawnRotation,
            generatedRoot);

        int percentage = Mathf.RoundToInt(normalizedFraction * 100f);
        triggerObject.name = $"SpawnPoint_{index + 1:D2}_{percentage}pct_{resolvedDistance:F0}m";

        triggerObject.transform.localScale = new Vector3(
            trackWidth,
            triggerHeight,
            triggerDepth);

        EnsureTriggerCollider(triggerObject);

        SpawnPointTrigger trigger = triggerObject.GetComponent<SpawnPointTrigger>();

        if (trigger == null)
        {
            trigger = triggerObject.AddComponent<SpawnPointTrigger>();
        }

        trigger.Initialize(
            this,
            respawnController,
            livesController,
            respawnPosition,
            respawnRotation,
            resolvedDistance);

        return trigger;
    }

    private static void EnsureTriggerCollider(GameObject triggerObject)
    {
        Collider collider = triggerObject.GetComponent<Collider>();

        if (collider == null)
        {
            BoxCollider boxCollider = triggerObject.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            return;
        }

        collider.isTrigger = true;
    }

    private Transform CreateRoot()
    {
        GameObject root = new GameObject(RootName);
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        return root.transform;
    }

    #endregion

    #region Validation

    private bool CanPlaceSpawnPoints(
        TrackRuntimeMap map,
        BallRespawnController respawnController,
        LivesController livesController)
    {
        if (spawnPointPrefab == null)
        {
            Debug.LogWarning("[SPAWN POINTS] spawnPointPrefab no asignado.", this);
            return false;
        }

        if (map == null)
        {
            Debug.LogWarning("[SPAWN POINTS] TrackRuntimeMap es null.", this);
            return false;
        }

        if (map.PathSampler == null)
        {
            Debug.LogWarning("[SPAWN POINTS] PathSampler es null.", this);
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
        triggerHeight = Mathf.Max(MinimumTriggerHeight, triggerHeight);
        triggerDepth = Mathf.Max(MinimumTriggerDepth, triggerDepth);
        respawnHeightOffset = Mathf.Max(0f, respawnHeightOffset);
        sectionStartPadding = Mathf.Max(0f, sectionStartPadding);

        if (spawnFractions == null)
        {
            return;
        }

        for (int i = 0; i < spawnFractions.Length; i++)
        {
            spawnFractions[i] = Mathf.Clamp01(spawnFractions[i]);
        }
    }

    #endregion

    #region Event Handlers

    private void HandleLevelReset()
    {
        ResetAllSpawnPoints();
    }

    #endregion
}