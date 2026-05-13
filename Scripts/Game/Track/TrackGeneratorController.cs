using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orquestador del pipeline de generación procedural de track.
///
/// Responsabilidades (exclusivamente de coordinación):
/// <list type="bullet">
///   <item>Delegar la construcción del mapa de datos a <see cref="TrackMapBuilder"/>.</item>
///   <item>Delegar la instanciación visual y física a <see cref="TrackChunkInstantiator"/>.</item>
///   <item>Gestionar el root de GameObjects generados y su limpieza.</item>
///   <item>Coordinar los sub-generadores opcionales (barreras, void zone).</item>
///   <item>Exponer la API pública estable para el resto de sistemas.</item>
/// </list>
///
/// La lógica de construcción del mapa vive en <see cref="TrackMapBuilder"/>.
/// La lógica de instanciación visual/física vive en <see cref="TrackChunkInstantiator"/>.
/// </summary>
public sealed class TrackGeneratorController : MonoBehaviour
{
    #region Nested Types

    /// <summary>
    /// Resultado de una generación decorativa de track.
    /// </summary>
    public readonly struct DecorativeTrackBuildResult
    {
        public bool             IsValid     { get; }
        public GameObject       RootObject  { get; }
        public TrackRuntimeMap  RuntimeMap  { get; }

        public DecorativeTrackBuildResult(bool isValid, GameObject rootObject, TrackRuntimeMap runtimeMap)
        {
            IsValid    = isValid;
            RootObject = rootObject;
            RuntimeMap = runtimeMap;
        }
    }

    #endregion

    #region Inspector

    [Header("Configuration")]
    [SerializeField]
    [Tooltip("Perfil base de generación del track.")]
    private TrackGenerationProfile generationProfile;

    [Header("Generated Root")]
    [SerializeField]
    [Tooltip("Nombre del objeto contenedor raíz de los chunks generados.")]
    private string generatedRootName = "GeneratedTrack";

    [SerializeField]
    [Tooltip("Prefijo de nombre para cada chunk visual generado.")]
    private string chunkObjectNamePrefix = "TrackChunk_";

    [SerializeField]
    [Tooltip("Layer asignada a todos los objetos de pista generados.")]
    private string generatedTrackLayerName = "Ground";

    [SerializeField]
    [Tooltip("Si está activo, genera collider físico para cada chunk sólido jugable.")]
    private bool generateMeshColliders = true;

    [Header("Rail Physics")]
    [SerializeField]
    [Tooltip("Si está activo, los chunks de rail usan CapsuleColliders primitivos por segmento.")]
    private bool usePrimitiveRailColliders = true;

    [SerializeField]
    [Tooltip("Multiplicador del radio físico de cada CapsuleCollider de rail.")]
    private float railColliderRadiusMultiplier = 1f;

    [SerializeField]
    [Tooltip("Radio físico mínimo de seguridad para los CapsuleColliders de rail.")]
    private float minimumRailColliderRadius = 0.05f;

    [SerializeField]
    [Tooltip("PhysicsMaterial opcional para los colliders primitivos de rail.")]
    private PhysicsMaterial railPhysicMaterial;

    [Header("Optional Generators")]
    [SerializeField]
    [Tooltip("Generador opcional de zona de muerte.")]
    private VoidZoneGenerator voidZoneGenerator;

    [SerializeField]
    [Tooltip("Generador opcional de bordes laterales del track.")]
    private TrackBarrierGenerator trackBarrierGenerator;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("Si está activo, genera automáticamente al iniciar. InfiniteLevelManager lo desactiva en su Awake.")]
    private bool generateOnStart = true;

    [SerializeField]
    [Tooltip("Imprime logs básicos de generación.")]
    private bool enableDebugLogs;

    #endregion

    #region Runtime

    private TrackRuntimeMap             generatedMap;
    private LevelGenerationSettings     activeSettings;
    private TrackGenerationProfile      activeVisualProfile;

    #endregion

    #region Properties

    /// <summary>Mapa runtime de la pista jugable actualmente generada.</summary>
    public TrackRuntimeMap GeneratedMap => generatedMap;

    /// <summary>Perfil base de generación.</summary>
    public TrackGenerationProfile GenerationProfile => generationProfile;

    /// <summary>Generador de barreras asociado.</summary>
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

    /// <summary>Desactiva la generación automática al inicio de escena.</summary>
    public void DisableAutoGeneration()
    {
        generateOnStart = false;
    }

    /// <summary>Inyecta los settings del nivel e inicia la generación jugable completa.</summary>
    public void GenerateLevel(LevelGenerationSettings settings)
    {
        if (settings == null)
        {
            Debug.LogWarning("[TRACK] GenerateLevel llamado sin settings. Cancelado.", this);
            return;
        }

        activeSettings = settings;
        GenerateLevel();
    }

    /// <summary>Sobreescribe el profile visual para la próxima generación jugable.</summary>
    public void SetVisualProfileOverride(TrackGenerationProfile profile)
    {
        activeVisualProfile = profile;
    }

    /// <summary>Elimina el override de profile visual.</summary>
    public void ClearVisualProfileOverride()
    {
        activeVisualProfile = null;
    }

    /// <summary>Genera el nivel jugable usando los settings activos.</summary>
    [ContextMenu("Generate Level")]
    public void GenerateLevel()
    {
        if (!CanGeneratePlayableLevel())
        {
            return;
        }

        SanitizeInspectorData();
        LogConfigurationWarnings();
        ClearGeneratedVisuals();

        TrackRuntimeMap runtimeMap = BuildMap(
            activeSettings,
            transform.position,
            TrackMapBuilder.ResolveHorizontalForward(transform.forward));

        if (runtimeMap == null)
        {
            return;
        }

        Transform root = GetOrCreateGeneratedRoot();

        TrackGenerationProfile resolvedVisualProfile = activeVisualProfile != null
            ? activeVisualProfile
            : generationProfile;

        TrackChunkInstantiator.InstantiateChunks(
            runtimeMap.SurfaceChunks,
            root,
            BuildInstantiatorSettings(generateMeshColliders),
            resolvedVisualProfile);

        generatedMap = runtimeMap;

        trackBarrierGenerator?.Rebuild(generatedMap, activeSettings);
        voidZoneGenerator?.Rebuild();

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[TRACK] Generated playable level. Seed: {runtimeMap.GeneratedSeed} | " +
                $"Sections: {runtimeMap.Sections.Count} | " +
                $"Chunks: {runtimeMap.SurfaceChunks.Count}.",
                this);
        }
    }

    /// <summary>
    /// Genera una pista decorativa visual sin afectar GeneratedMap.
    /// Sin colliders, barreras, void zone, contenido ni checkpoints.
    /// </summary>
    public DecorativeTrackBuildResult GenerateDecorativeLevel(
        LevelGenerationSettings settings,
        Transform parent,
        string rootName,
        TrackGenerationProfile visualProfileOverride = null)
    {
        if (settings == null || generationProfile == null)
        {
            Debug.LogWarning("[TRACK] GenerateDecorativeLevel: faltan settings o generationProfile.", this);
            return new DecorativeTrackBuildResult(false, null, null);
        }

        SanitizeInspectorData();

        TrackRuntimeMap runtimeMap = BuildMap(settings, Vector3.zero, Vector3.forward);

        if (runtimeMap == null)
        {
            return new DecorativeTrackBuildResult(false, null, null);
        }

        string resolvedName = string.IsNullOrWhiteSpace(rootName) ? "DecorativeTrack" : rootName;
        GameObject rootObject = new GameObject(resolvedName);

        if (parent != null)
        {
            rootObject.transform.SetParent(parent);
        }

        rootObject.transform.localPosition = Vector3.zero;
        rootObject.transform.localRotation = Quaternion.identity;
        rootObject.transform.localScale    = Vector3.one;

        TrackGenerationProfile resolvedVisualProfile = visualProfileOverride != null
            ? visualProfileOverride
            : generationProfile;

        TrackChunkInstantiator.InstantiateChunks(
            runtimeMap.SurfaceChunks,
            rootObject.transform,
            BuildInstantiatorSettings(generateColliders: false),
            resolvedVisualProfile);

        return new DecorativeTrackBuildResult(true, rootObject, runtimeMap);
    }

    /// <summary>Elimina todo el contenido visual generado y borra el mapa activo.</summary>
    [ContextMenu("Clear Generated Level")]
    public void ClearGeneratedLevel()
    {
        ClearGeneratedVisuals();
        generatedMap = null;
    }

    #endregion

    #region Internal Pipeline

    /// <summary>
    /// Crea un TrackMapBuilder fresco y construye el mapa de datos.
    /// El builder se crea por generación para garantizar estado limpio.
    /// </summary>
    private TrackRuntimeMap BuildMap(
        LevelGenerationSettings settings,
        Vector3 startPosition,
        Vector3 startForward)
    {
        return new TrackMapBuilder(generationProfile).Build(settings, startPosition, startForward);
    }

    private TrackChunkInstantiatorSettings BuildInstantiatorSettings(bool generateColliders)
    {
        int layer = LayerMask.NameToLayer(generatedTrackLayerName);

        return new TrackChunkInstantiatorSettings(
            generateColliders,
            usePrimitiveRailColliders,
            railColliderRadiusMultiplier,
            minimumRailColliderRadius,
            railPhysicMaterial,
            chunkObjectNamePrefix,
            layer);
    }

    #endregion

    #region Visual Root Management

    private Transform GetOrCreateGeneratedRoot()
    {
        Transform existing = transform.Find(generatedRootName);

        if (existing != null)
        {
            return existing;
        }

        GameObject root = new GameObject(generatedRootName);
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale    = Vector3.one;

        return root.transform;
    }

    /// <summary>
    /// Elimina todos los hijos del root generado.
    /// Itera en reversa para evitar allocations y corrupción de índices.
    /// </summary>
    private void ClearGeneratedVisuals()
    {
        Transform existingRoot = transform.Find(generatedRootName);

        if (existingRoot == null)
        {
            return;
        }

        for (int i = existingRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = existingRoot.GetChild(i).gameObject;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(child);
            }
            else
            {
                Object.Destroy(child);
            }
#else
            Object.Destroy(child);
#endif
        }
    }

    #endregion

    #region Guards & Validation

    private bool CanGeneratePlayableLevel()
    {
        if (generationProfile == null)
        {
            Debug.LogWarning("[TRACK] generationProfile no asignado.", this);
            return false;
        }

        if (activeSettings == null)
        {
            Debug.LogWarning("[TRACK] No hay settings activos. Llama GenerateLevel(settings) primero.", this);
            return false;
        }

        return true;
    }

    private void SanitizeInspectorData()
    {
        if (string.IsNullOrWhiteSpace(generatedRootName))       generatedRootName       = "GeneratedTrack";
        if (string.IsNullOrWhiteSpace(chunkObjectNamePrefix))   chunkObjectNamePrefix   = "TrackChunk_";
        if (string.IsNullOrWhiteSpace(generatedTrackLayerName)) generatedTrackLayerName = "Ground";

        railColliderRadiusMultiplier = Mathf.Max(0.1f, railColliderRadiusMultiplier);
        minimumRailColliderRadius    = Mathf.Max(0.01f, minimumRailColliderRadius);
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

    #endregion
}