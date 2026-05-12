using System.Reflection;
using UnityEngine;

/// <summary>
/// Genera pistas decorativas adyacentes al nivel actual:
/// - Nivel anterior conectado visualmente detrás.
/// - Nivel siguiente conectado visualmente delante.
///
/// Importante:
/// Estas pistas son solo visuales. No tienen colliders, rigidbodies, triggers ni scripts funcionales.
///
/// Reglas actuales:
/// - Preview anterior: sin cajas, sin muros, sin pelotas, sin monedas, sin meta. Sí permite ventiladores y barreras.
/// - Preview siguiente: contenido visual normal, pero sin pared inicial de barrera.
/// </summary>
public sealed class AdjacentTrackPreviewManager : MonoBehaviour
{
    #region Constants

    private const BindingFlags FieldFlags =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    private const string PreviewRootName = "GeneratedAdjacentTrackPreviews";
    private const string PreviousPreviewName = "DecorativePreviousLevel";
    private const string NextPreviewName = "DecorativeNextLevel";

    #endregion

    #region Inspector

    [Header("References")]
    [SerializeField]
    [Tooltip("Generador principal funcional del nivel actual. Se usa como plantilla para clonar pistas visuales.")]
    private TrackGeneratorController trackGeneratorTemplate;

    [SerializeField]
    [Tooltip("Generador de contenido funcional. Se usa como plantilla para clonar contenido decorativo.")]
    private TrackContentGenerator contentGeneratorTemplate;

    [SerializeField]
    [Tooltip("Generador de barreras funcional. Se usa como plantilla para clonar barreras decorativas.")]
    private TrackBarrierGenerator barrierGeneratorTemplate;

    [SerializeField]
    [Tooltip("SO de progresión infinita. Permite reconstruir la semilla y dificultad de niveles anterior/siguiente.")]
    private InfiniteProgressionSettings progressionSettings;

    [SerializeField]
    [Tooltip("Catálogo global de contenido. Necesario para configurar contenido decorativo con la misma progresión.")]
    private TrackContentGenerationProfile contentProfile;

    [SerializeField]
    [Tooltip("Profile visual especial usado en niveles bonus. Puede ser null.")]
    private TrackGenerationProfile bonusTrackProfile;

    [Header("Generation")]
    [SerializeField]
    [Tooltip("Genera el nivel anterior decorativo cuando el nivel actual es mayor a 1.")]
    private bool generatePreviousLevel = true;

    [SerializeField]
    [Tooltip("Genera el nivel siguiente decorativo al frente del nivel actual.")]
    private bool generateNextLevel = true;

    [SerializeField]
    [Tooltip("Genera contenido decorativo: obstáculos, monedas y meta, pero sanitizados sin física ni scripts.")]
    private bool generateDecorativeContent = true;

    [SerializeField]
    [Tooltip("Genera barreras decorativas visuales sin colliders.")]
    private bool generateDecorativeBarriers = true;

    [SerializeField]
    [Tooltip("Distancia visual entre el final/inicio de pistas decorativas y la pista actual.")]
    [Min(0f)]
    private float connectionGap = 0f;

    [Header("Previous Preview Content")]
    [SerializeField]
    [Tooltip("Si está activo, el nivel anterior visual puede generar ventiladores decorativos.")]
    private bool allowFansOnPreviousPreview = true;

    [SerializeField]
    [Tooltip("Si está activo, el nivel anterior visual genera meta decorativa. Recomendado: false.")]
    private bool allowGoalOnPreviousPreview = false;

    [Header("Next Preview Barriers")]
    [SerializeField]
    [Tooltip("Si está activo, el nivel siguiente visual genera la pared inicial. Recomendado: false.")]
    private bool generateStartWallOnNextPreview = false;

    [Header("Sanitization")]
    [SerializeField]
    [Tooltip("Si está activo, sanitiza automáticamente todos los objetos decorativos después de generarlos.")]
    private bool sanitizeAfterGeneration = true;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("Imprime logs básicos de generación decorativa.")]
    private bool enableDebugLogs;

    #endregion

    #region Runtime

    private Transform previewRoot;

    #endregion

    #region Public API

    /// <summary>
    /// Reconstruye las pistas decorativas adyacentes al nivel actual.
    /// </summary>
    /// <param name="currentLevelIndex">Nivel activo actual.</param>
    public void RebuildPreviews(int currentLevelIndex)
    {
        ClearPreviews();

        if (!CanBuildPreviews())
        {
            return;
        }

        TrackRuntimeMap currentMap = trackGeneratorTemplate.GeneratedMap;

        if (currentMap == null || currentMap.PathSampler == null)
        {
            Debug.LogWarning("[ADJACENT PREVIEW] El mapa actual no está generado.", this);
            return;
        }

        previewRoot = CreatePreviewRoot();

        TrackSample currentStart = currentMap.PathSampler.SampleAtDistance(0f);
        TrackSample currentEnd = currentMap.PathSampler.SampleAtDistance(currentMap.PathSampler.TotalDistance);

        if (generatePreviousLevel && currentLevelIndex > 1)
        {
            BuildPreviousPreview(currentLevelIndex - 1, currentStart);
        }

        if (generateNextLevel)
        {
            BuildNextPreview(currentLevelIndex + 1, currentEnd);
        }

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[ADJACENT PREVIEW] Previews reconstruidos para nivel actual {currentLevelIndex}.",
                this);
        }
    }

    /// <summary>
    /// Elimina todas las pistas decorativas generadas previamente.
    /// </summary>
    [ContextMenu("Clear Adjacent Previews")]
    public void ClearPreviews()
    {
        Transform existingRoot = transform.Find(PreviewRootName);

        if (existingRoot == null)
        {
            previewRoot = null;
            return;
        }

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

        previewRoot = null;
    }

    #endregion

    #region Preview Build

    /// <summary>
    /// Construye visualmente el nivel anterior y alinea su final con el inicio del nivel actual.
    /// </summary>
    private void BuildPreviousPreview(int previewLevelIndex, TrackSample currentStart)
    {
        GameObject wrapper = CreatePreviewWrapper(PreviousPreviewName, previewLevelIndex);

        PreviewBuildResult preview = BuildPreviewLevel(
            previewLevelIndex,
            wrapper.transform,
            PreviewSide.Previous);

        if (!preview.IsValid)
        {
            DestroyPreviewWrapper(wrapper);
            return;
        }

        TrackSample previewEnd = preview.RuntimeMap.PathSampler.SampleAtDistance(
            preview.RuntimeMap.PathSampler.TotalDistance);

        Vector3 targetForward = FlattenDirection(currentStart.Forward);
        Vector3 targetEndPosition = currentStart.Position - targetForward * connectionGap;

        AlignPreviewBySample(
            wrapper.transform,
            previewEnd.Position,
            previewEnd.Forward,
            targetEndPosition,
            targetForward);

        FinalizePreview(wrapper.transform);
    }

    /// <summary>
    /// Construye visualmente el nivel siguiente y alinea su inicio con el final del nivel actual.
    /// </summary>
    private void BuildNextPreview(int previewLevelIndex, TrackSample currentEnd)
    {
        GameObject wrapper = CreatePreviewWrapper(NextPreviewName, previewLevelIndex);

        PreviewBuildResult preview = BuildPreviewLevel(
            previewLevelIndex,
            wrapper.transform,
            PreviewSide.Next);

        if (!preview.IsValid)
        {
            DestroyPreviewWrapper(wrapper);
            return;
        }

        TrackSample previewStart = preview.RuntimeMap.PathSampler.SampleAtDistance(0f);

        Vector3 targetForward = FlattenDirection(currentEnd.Forward);
        Vector3 targetStartPosition = currentEnd.Position + targetForward * connectionGap;

        AlignPreviewBySample(
            wrapper.transform,
            previewStart.Position,
            previewStart.Forward,
            targetStartPosition,
            targetForward);

        FinalizePreview(wrapper.transform);
    }

    /// <summary>
    /// Genera una pista decorativa completa usando clones de los generadores existentes.
    /// </summary>
    private PreviewBuildResult BuildPreviewLevel(
        int previewLevelIndex,
        Transform wrapper,
        PreviewSide previewSide)
    {
        LevelGenerationSettings trackSettings = ScriptableObject.CreateInstance<LevelGenerationSettings>();
        LevelContentGenerationSettings contentSettings = ScriptableObject.CreateInstance<LevelContentGenerationSettings>();

        try
        {
            TrackGenerationProfile baseTrackProfile = trackGeneratorTemplate.GenerationProfile;

            trackSettings.ConfigureForLevel(
                progressionSettings,
                baseTrackProfile,
                previewLevelIndex);

            bool isBonus = IsBonusLevel(previewLevelIndex);

            if (isBonus)
            {
                trackSettings.ApplyBonusLevelOverrides();
            }

            TrackGeneratorController previewTrackGenerator =
                CreateDecorativeTrackGeneratorClone(previewLevelIndex, wrapper);

            if (previewTrackGenerator == null)
            {
                return PreviewBuildResult.Invalid;
            }

            if (isBonus && bonusTrackProfile != null)
            {
                previewTrackGenerator.SetVisualProfileOverride(bonusTrackProfile);
            }
            else
            {
                previewTrackGenerator.ClearVisualProfileOverride();
            }

            previewTrackGenerator.GenerateLevel(trackSettings);

            TrackRuntimeMap previewMap = previewTrackGenerator.GeneratedMap;

            if (previewMap == null || previewMap.PathSampler == null)
            {
                return PreviewBuildResult.Invalid;
            }

            if (generateDecorativeContent && contentGeneratorTemplate != null && contentProfile != null)
            {
                contentSettings.ConfigureForLevel(
                    progressionSettings,
                    contentProfile,
                    previewLevelIndex);

                if (isBonus)
                {
                    contentSettings.ConfigureForBonusLevel(progressionSettings.BonusCoinCount);
                }

                ApplyPreviewContentRules(contentSettings, previewSide);

                CreateDecorativeContentClone(
                    previewLevelIndex,
                    wrapper,
                    previewTrackGenerator,
                    contentSettings);
            }

            if (generateDecorativeBarriers && barrierGeneratorTemplate != null)
            {
                CreateDecorativeBarrierClone(
                    previewLevelIndex,
                    wrapper,
                    previewTrackGenerator,
                    trackSettings,
                    isBonus,
                    previewSide);
            }

            return new PreviewBuildResult(true, previewMap);
        }
        finally
        {
            if (trackSettings != null)
            {
                Destroy(trackSettings);
            }

            if (contentSettings != null)
            {
                Destroy(contentSettings);
            }
        }
    }

    #endregion

    #region Preview Rules

    /// <summary>
    /// Aplica reglas específicas de contenido según si la preview es anterior o siguiente.
    /// </summary>
    private void ApplyPreviewContentRules(
        LevelContentGenerationSettings contentSettings,
        PreviewSide previewSide)
    {
        if (contentSettings == null)
        {
            return;
        }

        if (previewSide == PreviewSide.Previous)
        {
            // Nivel anterior visual:
            // Sin obstáculos principales, sin monedas.
            // Ventiladores permitidos para mantener dinamismo visual.
            SetPrivateField(contentSettings, "enableBoxes", false);
            SetPrivateField(contentSettings, "enableWalls", false);
            SetPrivateField(contentSettings, "enableBalls", false);
            SetPrivateField(contentSettings, "enableCoins", false);
            SetPrivateField(contentSettings, "enableFans", allowFansOnPreviousPreview);
            SetPrivateField(contentSettings, "enableGoal", allowGoalOnPreviousPreview);

            // Refuerzo defensivo por si alguna versión de LevelContentGenerationSettings
            // usa chances aunque los toggles estén apagados.
            SetPrivateField(contentSettings, "boxSpawnChance", 0f);
            SetPrivateField(contentSettings, "wallSpawnChance", 0f);
            SetPrivateField(contentSettings, "ballFlatSpawnChance", 0f);
            SetPrivateField(contentSettings, "ballNarrowSpawnChance", 0f);
            SetPrivateField(contentSettings, "ballRailSpawnChance", 0f);
            SetPrivateField(contentSettings, "ballBeforeDownSlopeChance", 0f);

            SetPrivateField(contentSettings, "useRandomCoinCount", false);
            SetPrivateField(contentSettings, "fixedCoinCount", 0);
            SetPrivateField(contentSettings, "minRandomCoinCount", 0);
            SetPrivateField(contentSettings, "maxRandomCoinCount", 0);

            if (!allowFansOnPreviousPreview)
            {
                SetPrivateField(contentSettings, "fanFlatSpawnChance", 0f);
                SetPrivateField(contentSettings, "fanStraightRailSpawnChance", 0f);
            }

            return;
        }

        // Nivel siguiente visual:
        // De momento conserva contenido decorativo normal.
    }

    #endregion

    #region Clone Creation

    /// <summary>
    /// Crea un clon del TrackGeneratorController preparado para generar solo visual.
    /// </summary>
    private TrackGeneratorController CreateDecorativeTrackGeneratorClone(
        int previewLevelIndex,
        Transform parent)
    {
        TrackGeneratorController clone = Instantiate(trackGeneratorTemplate, parent);

        clone.gameObject.name = $"DecorativeTrackGenerator_Level_{previewLevelIndex:D3}";

        ResetTransformToOrigin(clone.transform);
        ClearChildren(clone.transform);

        SetPrivateField(clone, "generateOnStart", false);
        SetPrivateField(clone, "generateMeshColliders", false);
        SetPrivateField(clone, "voidZoneGenerator", null);
        SetPrivateField(clone, "trackBarrierGenerator", null);
        SetPrivateField(clone, "generatedRootName", $"GeneratedDecorativeTrack_{previewLevelIndex:D3}");
        SetPrivateField(clone, "enableDebugLogs", false);

        clone.DisableAutoGeneration();

        return clone;
    }

    /// <summary>
    /// Crea un clon del TrackContentGenerator y genera contenido decorativo sobre el mapa clonado.
    /// </summary>
    private void CreateDecorativeContentClone(
        int previewLevelIndex,
        Transform parent,
        TrackGeneratorController previewTrackGenerator,
        LevelContentGenerationSettings contentSettings)
    {
        TrackContentGenerator clone = Instantiate(contentGeneratorTemplate, parent);

        clone.gameObject.name = $"DecorativeContentGenerator_Level_{previewLevelIndex:D3}";

        ResetTransformToOrigin(clone.transform);
        ClearChildren(clone.transform);

        SetPrivateField(clone, "trackGenerator", previewTrackGenerator);
        SetPrivateField(clone, "generatedRootName", $"GeneratedDecorativeContent_{previewLevelIndex:D3}");
        SetPrivateField(clone, "poolRootName", $"_DecorativePool_{previewLevelIndex:D3}");
        SetPrivateField(clone, "generateOnStart", false);
        SetPrivateField(clone, "enableDebugLogs", false);

        clone.DisableAutoGeneration();
        clone.GenerateContent(contentSettings);
    }

    /// <summary>
    /// Crea un clon del TrackBarrierGenerator y genera barreras visuales sin colliders.
    /// </summary>
    private void CreateDecorativeBarrierClone(
        int previewLevelIndex,
        Transform parent,
        TrackGeneratorController previewTrackGenerator,
        LevelGenerationSettings trackSettings,
        bool isBonus,
        PreviewSide previewSide)
    {
        TrackBarrierGenerator clone = Instantiate(barrierGeneratorTemplate, parent);

        clone.gameObject.name = $"DecorativeBarrierGenerator_Level_{previewLevelIndex:D3}";

        ResetTransformToOrigin(clone.transform);
        ClearChildren(clone.transform);

        SetPrivateField(clone, "trackGenerator", previewTrackGenerator);
        SetPrivateField(clone, "generatedRootName", $"GeneratedDecorativeBarriers_{previewLevelIndex:D3}");
        SetPrivateField(clone, "generateColliders", false);
        SetPrivateField(clone, "rebuildOnStart", false);
        SetPrivateField(clone, "enableCoverageLogs", false);

        if (previewSide == PreviewSide.Next)
        {
            SetPrivateField(clone, "generateStartWall", generateStartWallOnNextPreview);
        }

        float safeStartLength = progressionSettings.SafeStartLengthOverride > 0f
            ? progressionSettings.SafeStartLengthOverride
            : previewTrackGenerator.GenerationProfile.SafeStartLength;

        float safeEndLength = progressionSettings.SafeEndLengthOverride > 0f
            ? progressionSettings.SafeEndLengthOverride
            : previewTrackGenerator.GenerationProfile.SafeEndLength;

        clone.SetSafeZoneLengths(safeStartLength, safeEndLength);

        // Para previews decorativas conviene llenar visualmente con barreras completas,
        // pero sin colliders.
        clone.ForceFullBarriers();

        clone.Rebuild(previewTrackGenerator.GeneratedMap, trackSettings);
    }

    #endregion

    #region Alignment

    /// <summary>
    /// Alinea una pista decorativa usando un sample de origen y un destino.
    /// </summary>
    private static void AlignPreviewBySample(
        Transform wrapper,
        Vector3 sourceSamplePosition,
        Vector3 sourceSampleForward,
        Vector3 targetSamplePosition,
        Vector3 targetSampleForward)
    {
        Vector3 sourceForward = FlattenDirection(sourceSampleForward);
        Vector3 targetForward = FlattenDirection(targetSampleForward);

        Quaternion rotation = Quaternion.FromToRotation(sourceForward, targetForward);

        wrapper.rotation = rotation;
        wrapper.position = targetSamplePosition - rotation * sourceSamplePosition;
    }

    /// <summary>
    /// Resuelve una dirección horizontal segura.
    /// </summary>
    private static Vector3 FlattenDirection(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return direction.normalized;
    }

    #endregion

    #region Finalization

    /// <summary>
    /// Sanitiza la jerarquía decorativa al final de la generación.
    /// </summary>
    private void FinalizePreview(Transform wrapper)
    {
        if (wrapper == null)
        {
            return;
        }

        if (sanitizeAfterGeneration)
        {
            DecorativeGeneratedObjectSanitizer.SanitizeHierarchy(wrapper);
        }
    }

    #endregion

    #region Root Helpers

    /// <summary>
    /// Crea la raíz general de previews.
    /// </summary>
    private Transform CreatePreviewRoot()
    {
        GameObject root = new GameObject(PreviewRootName);
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        return root.transform;
    }

    /// <summary>
    /// Crea un wrapper para un nivel decorativo.
    /// </summary>
    private GameObject CreatePreviewWrapper(string baseName, int levelIndex)
    {
        GameObject wrapper = new GameObject($"{baseName}_Level_{levelIndex:D3}");
        wrapper.transform.SetParent(previewRoot);
        wrapper.transform.localPosition = Vector3.zero;
        wrapper.transform.localRotation = Quaternion.identity;
        wrapper.transform.localScale = Vector3.one;

        return wrapper;
    }

    /// <summary>
    /// Destruye un wrapper si la generación falla.
    /// </summary>
    private static void DestroyPreviewWrapper(GameObject wrapper)
    {
        if (wrapper == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object.DestroyImmediate(wrapper);
        }
        else
        {
            Object.Destroy(wrapper);
        }
#else
        Object.Destroy(wrapper);
#endif
    }

    /// <summary>
    /// Reinicia el transform local de un clon para que la generación se haga desde origen.
    /// Luego el wrapper se encarga de alinear todo.
    /// </summary>
    private static void ResetTransformToOrigin(Transform target)
    {
        if (target == null)
        {
            return;
        }

        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    /// <summary>
    /// Limpia hijos clonados accidentalmente, como GeneratedTrack del nivel actual.
    /// </summary>
    private static void ClearChildren(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(child.gameObject);
            }
            else
            {
                Object.Destroy(child.gameObject);
            }
#else
            Object.Destroy(child.gameObject);
#endif
        }
    }

    #endregion

    #region Reflection

    /// <summary>
    /// Cambia un campo privado por nombre.
    /// Se usa para preparar clones decorativos sin modificar tus generadores funcionales.
    /// </summary>
    private void SetPrivateField(Object target, string fieldName, object value)
    {
        if (target == null || string.IsNullOrWhiteSpace(fieldName))
        {
            return;
        }

        FieldInfo field = target.GetType().GetField(fieldName, FieldFlags);

        if (field == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning(
                    $"[ADJACENT PREVIEW] No se encontró el campo '{fieldName}' en {target.GetType().Name}.",
                    this);
            }

            return;
        }

        field.SetValue(target, value);
    }

    #endregion

    #region Validation

    /// <summary>
    /// Valida referencias mínimas.
    /// </summary>
    private bool CanBuildPreviews()
    {
        if (trackGeneratorTemplate == null)
        {
            Debug.LogWarning("[ADJACENT PREVIEW] trackGeneratorTemplate no asignado.", this);
            return false;
        }

        if (progressionSettings == null)
        {
            Debug.LogWarning("[ADJACENT PREVIEW] progressionSettings no asignado.", this);
            return false;
        }

        if (trackGeneratorTemplate.GenerationProfile == null)
        {
            Debug.LogWarning("[ADJACENT PREVIEW] TrackGenerationProfile no asignado en TrackGeneratorController.", this);
            return false;
        }

        if (generateDecorativeContent && contentGeneratorTemplate == null)
        {
            Debug.LogWarning("[ADJACENT PREVIEW] generateDecorativeContent está activo pero no hay contentGeneratorTemplate.", this);
        }

        if (generateDecorativeContent && contentProfile == null)
        {
            Debug.LogWarning("[ADJACENT PREVIEW] generateDecorativeContent está activo pero no hay contentProfile.", this);
        }

        return true;
    }

    /// <summary>
    /// Indica si un nivel es bonus según la progresión.
    /// </summary>
    private bool IsBonusLevel(int levelIndex)
    {
        int interval = progressionSettings.BonusLevelInterval;
        return interval > 0 && levelIndex % interval == 0;
    }

    #endregion

    #region Private Types

    /// <summary>
    /// Lado decorativo respecto al nivel jugable actual.
    /// </summary>
    private enum PreviewSide
    {
        Previous = 0,
        Next = 1
    }

    /// <summary>
    /// Resultado interno de construir una preview.
    /// </summary>
    private readonly struct PreviewBuildResult
    {
        public static PreviewBuildResult Invalid => new PreviewBuildResult(false, null);

        public bool IsValid { get; }
        public TrackRuntimeMap RuntimeMap { get; }

        public PreviewBuildResult(bool isValid, TrackRuntimeMap runtimeMap)
        {
            IsValid = isValid;
            RuntimeMap = runtimeMap;
        }
    }

    #endregion
}