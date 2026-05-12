using UnityEngine;

/// <summary>
/// Parámetros de generación de pista activos en el nivel actual.
///
/// En modo de progresión infinita, todos los campos son configurados en runtime por
/// <see cref="InfiniteLevelManager"/> vía <see cref="ConfigureForLevel"/>.
/// </summary>
[CreateAssetMenu(fileName = "LevelGenerationSettings", menuName = "Game/Track/Level Generation Settings")]
public sealed class LevelGenerationSettings : ScriptableObject
{
    #region Inspector

    [Header("Seed")]
    [SerializeField]
    [Tooltip("Si está activo, el nivel usará una semilla fija reproducible.")]
    private bool useFixedSeed = true;

    [SerializeField]
    [Tooltip("Semilla fija. En progresión infinita InfiniteLevelManager la sobreescribe como BaseSeed + LevelIndex.")]
    private int fixedSeed = 12345;

    [Header("Difficulty")]
    [SerializeField]
    [Tooltip("Multiplicador global de dificultad. 1.0 = TrackGenerationProfile sin restricciones.")]
    private float difficultyMultiplier = 1f;

    [SerializeField]
    [Tooltip("Multiplicador de longitud total del track.")]
    private float lengthMultiplier = 1f;

    [SerializeField]
    [Tooltip("Multiplicador de probabilidad de cambio lateral.")]
    private float lateralChanceMultiplier = 1f;

    [SerializeField]
    [Tooltip("Multiplicador de probabilidad de cambio vertical.")]
    private float verticalChanceMultiplier = 1f;

    [SerializeField]
    [Tooltip("Multiplicador de probabilidad de estrechamiento.")]
    private float narrowChanceMultiplier = 1f;

    [SerializeField]
    [Tooltip("Multiplicador de probabilidad de gap.")]
    private float gapChanceMultiplier = 1f;

    [SerializeField]
    [Tooltip("Multiplicador de probabilidad de generación rail.")]
    private float railChanceMultiplier = 1f;

    [Header("Slope Override")]
    [SerializeField]
    [Tooltip("Techo efectivo del delta de pendiente para este nivel.\n" +
             "El generador elige aleatoriamente en [profile.SlopeHeightStepMin, este valor].\n" +
             "Valor ≤ 0 = sin override (usa profile.SlopeHeightStepMax directamente).")]
    private float slopeHeightStepMaxOverride = -1f;

    [Header("Safe Zones")]
    [SerializeField]
    [Tooltip("Si es mayor que cero, reemplaza la longitud segura inicial del perfil.")]
    private float safeStartLengthOverride = -1f;

    [SerializeField]
    [Tooltip("Si es mayor que cero, reemplaza la longitud segura final del perfil.")]
    private float safeEndLengthOverride = -1f;

    [Header("Safe Zone Barriers")]
    [SerializeField]
    [Tooltip("Si está activo, se generan bordes laterales en la zona segura inicial.")]
    private bool generateStartSafeZoneBarriers = true;

    [SerializeField]
    [Tooltip("Si está activo, se generan bordes laterales en la zona segura final.")]
    private bool generateEndSafeZoneBarriers = true;

    [Header("Height Overrides")]
    [SerializeField]
    [Tooltip("Si está activo, sobrescribe la altura mínima permitida.")]
    private bool overrideMinHeight;

    [SerializeField]
    [Tooltip("Altura mínima del nivel.")]
    private float minHeightOverride = -4f;

    [SerializeField]
    [Tooltip("Si está activo, sobrescribe la altura máxima permitida.")]
    private bool overrideMaxHeight;

    [SerializeField]
    [Tooltip("Altura máxima del nivel.")]
    private float maxHeightOverride = 8f;

    #endregion

    #region Properties

    public bool UseFixedSeed => useFixedSeed;
    public int FixedSeed => fixedSeed;
    public float DifficultyMultiplier => difficultyMultiplier;
    public float LengthMultiplier => lengthMultiplier;
    public float LateralChanceMultiplier => lateralChanceMultiplier;
    public float VerticalChanceMultiplier => verticalChanceMultiplier;
    public float NarrowChanceMultiplier => narrowChanceMultiplier;
    public float GapChanceMultiplier => gapChanceMultiplier;
    public float RailChanceMultiplier => railChanceMultiplier;

    /// <summary>
    /// Techo efectivo del delta de pendiente. Activo cuando > 0.
    /// <see cref="TrackRuleEvaluator"/> lo usa en lugar de <c>profile.SlopeHeightStepMax</c>
    /// para implementar el rango de pendiente desbloqueado por progresión.
    /// </summary>
    public float SlopeHeightStepMaxOverride => slopeHeightStepMaxOverride;

    /// <summary>Indica si el override de pendiente está activo.</summary>
    public bool HasSlopeHeightStepMaxOverride => slopeHeightStepMaxOverride > 0f;

    public float SafeStartLengthOverride => safeStartLengthOverride;
    public float SafeEndLengthOverride => safeEndLengthOverride;
    public bool GenerateStartSafeZoneBarriers => generateStartSafeZoneBarriers;
    public bool GenerateEndSafeZoneBarriers => generateEndSafeZoneBarriers;
    public bool OverrideMinHeight => overrideMinHeight;
    public float MinHeightOverride => minHeightOverride;
    public bool OverrideMaxHeight => overrideMaxHeight;
    public float MaxHeightOverride => maxHeightOverride;

    #endregion

    #region Infinite Progression API

    /// <summary>
    /// Configura todos los parámetros de pista para el nivel indicado.
    ///
    /// - Longitud: Lerp(startTrackLength, profile.TargetTrackLength, t) convertida a multiplicador.
    /// - Multiplicadores: Lerp(startValue, 1.0, t).
    /// - Pendiente: el techo efectivo = Lerp(startSlopeHeightStepMax, profile.SlopeHeightStepMax, t).
    ///   El generador elige aleatoriamente dentro de [profile.SlopeHeightStepMin, techo] en cada sección,
    ///   por lo que a dificultad máxima aún puede salir cualquier valor del rango.
    ///
    /// Solo modifica campos en memoria — no persiste al asset en disco.
    /// </summary>
    /// <param name="progression">SO con valores de inicio.</param>
    /// <param name="trackProfile">Profile base del track (necesario para calcular multiplicadores de longitud y pendiente).</param>
    /// <param name="levelIndex">Índice del nivel actual (comienza en 1).</param>
    public void ConfigureForLevel(
        InfiniteProgressionSettings progression,
        TrackGenerationProfile trackProfile,
        int levelIndex)
    {
        float t = ComputeProgressionT(levelIndex, progression.LevelCountToReachMax);

        useFixedSeed = true;
        fixedSeed = progression.BaseSeed + levelIndex;

        // Longitud: interpola en unidades reales y convierte a multiplicador.
        float resolvedLength = Mathf.Lerp(progression.StartTrackLength, trackProfile.TargetTrackLength, t);
        lengthMultiplier = resolvedLength / Mathf.Max(0.01f, trackProfile.TargetTrackLength);

        // Multiplicadores: lerp de valor inicial → 1.0 (= profile completo = máximo).
        difficultyMultiplier = Mathf.Lerp(progression.StartDifficultyMultiplier, 1f, t);
        lateralChanceMultiplier = Mathf.Lerp(progression.StartLateralChanceMultiplier, 1f, t);
        verticalChanceMultiplier = Mathf.Lerp(progression.StartVerticalChanceMultiplier, 1f, t);
        narrowChanceMultiplier = Mathf.Lerp(progression.StartNarrowChanceMultiplier, 1f, t);
        gapChanceMultiplier = Mathf.Lerp(progression.StartGapChanceMultiplier, 1f, t);
        railChanceMultiplier = Mathf.Lerp(progression.StartRailChanceMultiplier, 1f, t);

        // Pendiente: desbloquea el rango progresivamente.
        // El techo efectivo crece de startSlopeHeightStepMax → profile.SlopeHeightStepMax.
        // En cada nivel se elige un valor aleatorio dentro de [profile.SlopeHeightStepMin, techo],
        // lo que garantiza variedad incluso en dificultad máxima.
        slopeHeightStepMaxOverride = Mathf.Lerp(
            progression.StartSlopeHeightStepMax, trackProfile.SlopeHeightStepMax, t);

        // Zonas seguras (valores estáticos del SO de progresión).
        safeStartLengthOverride = progression.SafeStartLengthOverride;
        safeEndLengthOverride = progression.SafeEndLengthOverride;
        generateStartSafeZoneBarriers = progression.GenerateStartSafeZoneBarriers;
        generateEndSafeZoneBarriers = progression.GenerateEndSafeZoneBarriers;
        overrideMinHeight = progression.OverrideMinHeight;
        minHeightOverride = progression.MinHeightOverride;
        overrideMaxHeight = progression.OverrideMaxHeight;
        maxHeightOverride = progression.MaxHeightOverride;
    }

    private static float ComputeProgressionT(int levelIndex, int levelCountToReachMax)
    {
        if (levelCountToReachMax <= 1)
        {
            return 1f;
        }

        return Mathf.Clamp01((float)(levelIndex - 1) / (levelCountToReachMax - 1));
    }

    /// <summary>
    /// Aplica los overrides específicos de nivel bonus: sin gaps.
    /// Llamar inmediatamente después de <see cref="ConfigureForLevel"/> cuando el nivel es bonus.
    /// </summary>
    public void ApplyBonusLevelOverrides()
    {
        gapChanceMultiplier = 0f;
    }

    #endregion

    private void OnValidate()
    {
        difficultyMultiplier = Mathf.Max(0.1f, difficultyMultiplier);
        lengthMultiplier = Mathf.Max(0.1f, lengthMultiplier);
        lateralChanceMultiplier = Mathf.Max(0f, lateralChanceMultiplier);
        verticalChanceMultiplier = Mathf.Max(0f, verticalChanceMultiplier);
        narrowChanceMultiplier = Mathf.Max(0f, narrowChanceMultiplier);
        gapChanceMultiplier = Mathf.Max(0f, gapChanceMultiplier);
        railChanceMultiplier = Mathf.Max(0f, railChanceMultiplier);
    }
}