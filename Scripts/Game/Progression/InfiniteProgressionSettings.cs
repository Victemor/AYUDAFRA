using UnityEngine;

/// <summary>
/// Define los valores de inicio para la progresión infinita de niveles.
///
/// Parámetros de pista y contenido: el valor aquí es el INICIO (nivel 1), el MÁXIMO viene
/// de los profiles de generación.
/// Barreras: funcionan al REVÉS — el máximo es el Inspector de <see cref="TrackBarrierGenerator"/>,
/// y aquí se define el MÍNIMO al que se llega con la progresión.
/// </summary>
[CreateAssetMenu(fileName = "InfiniteProgressionSettings",
    menuName = "Game/Progression/Infinite Progression Settings")]
public sealed class InfiniteProgressionSettings : ScriptableObject
{
    #region Seed

    [Header("Semilla")]
    [SerializeField]
    [Tooltip("Semilla base compartida con los jugadores.\n" +
             "Semilla activa de cada nivel = BaseSeed + LevelIndex.\n\n" +
             "Usa el botón 'Randomize Base Seed' (clic derecho → Context) para obtener una nueva.")]
    private int baseSeed = 100;

    [SerializeField]
    [Tooltip("Niveles necesarios para que todos los parámetros alcancen los valores máximos de los profiles.")]
    private int levelCountToReachMax = 10;

    #endregion

    #region Bonus Levels

    [Header("Niveles Bonus")]
    [SerializeField]
    [Tooltip("Cada X niveles se genera un nivel BONUS (solo monedas, barreras completas, texturas especiales).\n" +
             "Ejemplo: 5 → niveles 5, 10, 15, 20... son bonus.\n" +
             "Usa 0 para desactivar los niveles bonus.")]
    private int bonusLevelInterval = 5;

    [SerializeField]
    [Tooltip("Cantidad fija de monedas a generar en un nivel BONUS.\n" +
             "Independiente de la progresión normal. Mínimo absoluto: 10.\n" +
             "Recomendado: un valor alto (ej: 60-100) para recompensar al jugador.")]
    private int bonusCoinCount = 80;

    #endregion

    #region Track — Starting Values

    [Header("Pista — Valores Iniciales (nivel 1)")]
    [Space(4)]

    [SerializeField]
    [Tooltip("Longitud mínima de la pista en el nivel 1 (en unidades).\n" +
             "Máximo = TrackGenerationProfile.TargetTrackLength.")]
    private float startTrackLength = 60f;

    [SerializeField]
    [Range(0.01f, 1f)]
    [Tooltip("Multiplicador de dificultad global al inicio. Máximo implícito = 1.0.")]
    private float startDifficultyMultiplier = 0.5f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Multiplicador de giro lateral al inicio. Máximo implícito = 1.0.")]
    private float startLateralChanceMultiplier = 0.2f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Multiplicador de cambio vertical al inicio. Máximo implícito = 1.0.")]
    private float startVerticalChanceMultiplier = 0.2f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Multiplicador de estrechamiento al inicio. Máximo implícito = 1.0.")]
    private float startNarrowChanceMultiplier = 0.1f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Multiplicador de gap al inicio. 0 = sin huecos en los primeros niveles.")]
    private float startGapChanceMultiplier = 0f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Multiplicador de railes al inicio. 0 = sin railes en los primeros niveles.")]
    private float startRailChanceMultiplier = 0f;

    [SerializeField]
    [Tooltip("Valor mínimo inicial del delta de pendiente en el nivel 1 (SlopeHeightStepMax).\n" +
             "Máximo = TrackGenerationProfile.SlopeHeightStepMax.\n\n" +
             "RANGO DESBLOQUEADO: el techo crece con la progresión. En cada nivel se elige\n" +
             "aleatoriamente dentro del rango desbloqueado.")]
    private float startSlopeHeightStepMax = 0.5f;

    #endregion

    #region Track — Static Config

    [Header("Zonas Seguras (estáticas)")]
    [SerializeField] [Tooltip("Override longitud zona segura inicial. -1 = usa el profile.")]
    private float safeStartLengthOverride = -1f;

    [SerializeField] [Tooltip("Override longitud zona segura final. -1 = usa el profile.")]
    private float safeEndLengthOverride = -1f;

    [SerializeField] private bool generateStartSafeZoneBarriers = true;
    [SerializeField] private bool generateEndSafeZoneBarriers = true;

    [Header("Altura (estática)")]
    [SerializeField] private bool overrideMinHeight;
    [SerializeField] private float minHeightOverride = -4f;
    [SerializeField] private bool overrideMaxHeight;
    [SerializeField] private float maxHeightOverride = 8f;

    #endregion

    #region Barriers

    [Header("Barreras — Comportamiento")]
    [Space(4)]

    [SerializeField]
    [Tooltip("Niveles iniciales con barreras completas en TODA la pista.\n" +
             "Ejemplo: 3 → niveles 1, 2 y 3 tienen barreras al 100%.\n" +
             "Los niveles bonus SIEMPRE tienen barreras, sin importar este valor.")]
    [Min(0)]
    private int guaranteedFullBarrierLevels = 3;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Porcentaje MÍNIMO de secciones con barreras fuera de las zonas seguras " +
             "cuando la dificultad llega al máximo.\n" +
             "0 = sin barreras generales en niveles avanzados (solo zonas seguras).\n" +
             "El MÁXIMO viene de TrackBarrierGenerator → General Coverage Ratio.")]
    private float minBarrierCoverageRatio = 0f;

    #endregion

    #region Content — Toggles

    [Header("Contenido — Categorías (estáticas)")]
    [SerializeField] private bool enableBoxes = true;
    [SerializeField] private bool enableWalls = true;
    [SerializeField] private bool enableBalls = true;
    [SerializeField] private bool enableFans = true;
    [SerializeField] private bool enableCoins = true;
    [SerializeField] private bool enableGoal = true;

    #endregion

    #region Content — Starting Values

    [Header("Contenido — Valores Iniciales (nivel 1)")]
    [Space(4)]

    [SerializeField]
    [Tooltip("Cantidad mínima de obstáculos en el nivel 1.\nMáximo = TrackContentGenerationProfile.BaseMaxObstacleCount.")]
    private int startObstacleCount = 3;

    [SerializeField]
    [Tooltip("Cantidad de monedas en el nivel 1. Escala hasta BaseMaxCoinCount. Mínimo absoluto: 10.")]
    private int startCoinCount = 10;

    [SerializeField] [Range(0f, 1f)] private float startBoxSpawnChance = 0.06f;
    [SerializeField] [Range(0f, 1f)] private float startWallSpawnChance = 0.01f;
    [SerializeField] [Range(0f, 1f)] private float startBallFlatSpawnChance = 0.01f;
    [SerializeField] [Range(0f, 1f)] private float startBallNarrowSpawnChance = 0.02f;
    [SerializeField] [Range(0f, 1f)] private float startBallRailSpawnChance = 0f;
    [SerializeField] [Range(0f, 1f)] private float startBallBeforeDownSlopeChance = 0.05f;
    [SerializeField] [Range(0f, 1f)] private float startFanFlatSpawnChance = 0f;
    [SerializeField] [Range(0f, 1f)] private float startFanStraightRailSpawnChance = 0f;

    #endregion

    #region Properties — Seed

    public int BaseSeed => baseSeed;
    public int LevelCountToReachMax => Mathf.Max(1, levelCountToReachMax);
    public int BonusLevelInterval => Mathf.Max(0, bonusLevelInterval);
    public int BonusCoinCount => Mathf.Max(10, bonusCoinCount);

    #endregion

    #region Properties — Track

    public float StartTrackLength => Mathf.Max(1f, startTrackLength);
    public float StartDifficultyMultiplier => startDifficultyMultiplier;
    public float StartLateralChanceMultiplier => startLateralChanceMultiplier;
    public float StartVerticalChanceMultiplier => startVerticalChanceMultiplier;
    public float StartNarrowChanceMultiplier => startNarrowChanceMultiplier;
    public float StartGapChanceMultiplier => startGapChanceMultiplier;
    public float StartRailChanceMultiplier => startRailChanceMultiplier;
    public float StartSlopeHeightStepMax => Mathf.Max(0.01f, startSlopeHeightStepMax);

    public float SafeStartLengthOverride => safeStartLengthOverride;
    public float SafeEndLengthOverride => safeEndLengthOverride;
    public bool GenerateStartSafeZoneBarriers => generateStartSafeZoneBarriers;
    public bool GenerateEndSafeZoneBarriers => generateEndSafeZoneBarriers;
    public bool OverrideMinHeight => overrideMinHeight;
    public float MinHeightOverride => minHeightOverride;
    public bool OverrideMaxHeight => overrideMaxHeight;
    public float MaxHeightOverride => maxHeightOverride;

    #endregion

    #region Properties — Barriers

    public int GuaranteedFullBarrierLevels => Mathf.Max(0, guaranteedFullBarrierLevels);
    public float MinBarrierCoverageRatio => minBarrierCoverageRatio;

    #endregion

    #region Properties — Content

    public bool EnableBoxes => enableBoxes;
    public bool EnableWalls => enableWalls;
    public bool EnableBalls => enableBalls;
    public bool EnableFans => enableFans;
    public bool EnableCoins => enableCoins;
    public bool EnableGoal => enableGoal;

    public int StartObstacleCount => Mathf.Max(0, startObstacleCount);
    public int StartCoinCount => Mathf.Max(0, startCoinCount);

    public float StartBoxSpawnChance => startBoxSpawnChance;
    public float StartWallSpawnChance => startWallSpawnChance;
    public float StartBallFlatSpawnChance => startBallFlatSpawnChance;
    public float StartBallNarrowSpawnChance => startBallNarrowSpawnChance;
    public float StartBallRailSpawnChance => startBallRailSpawnChance;
    public float StartBallBeforeDownSlopeChance => startBallBeforeDownSlopeChance;
    public float StartFanFlatSpawnChance => startFanFlatSpawnChance;
    public float StartFanStraightRailSpawnChance => startFanStraightRailSpawnChance;

    #endregion

    #region Editor Tools

#if UNITY_EDITOR
    /// <summary>
    /// Genera una semilla base aleatoria. Úsalo cuando quieras una generación completamente nueva.
    /// </summary>
    [ContextMenu("Randomize Base Seed")]
    private void RandomizeBaseSeed()
    {
        baseSeed = UnityEngine.Random.Range(int.MinValue / 2, int.MaxValue / 2);
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[PROGRESSION] Semilla base aleatorizada: {baseSeed}", this);
    }
#endif

    #endregion

    private void OnValidate()
    {
        levelCountToReachMax = Mathf.Max(1, levelCountToReachMax);
        bonusLevelInterval = Mathf.Max(0, bonusLevelInterval);
        bonusCoinCount = Mathf.Max(10, bonusCoinCount);
        startTrackLength = Mathf.Max(1f, startTrackLength);
        startDifficultyMultiplier = Mathf.Clamp(startDifficultyMultiplier, 0.01f, 1f);
        startLateralChanceMultiplier = Mathf.Clamp01(startLateralChanceMultiplier);
        startVerticalChanceMultiplier = Mathf.Clamp01(startVerticalChanceMultiplier);
        startNarrowChanceMultiplier = Mathf.Clamp01(startNarrowChanceMultiplier);
        startGapChanceMultiplier = Mathf.Clamp01(startGapChanceMultiplier);
        startRailChanceMultiplier = Mathf.Clamp01(startRailChanceMultiplier);
        startSlopeHeightStepMax = Mathf.Max(0.01f, startSlopeHeightStepMax);
        guaranteedFullBarrierLevels = Mathf.Max(0, guaranteedFullBarrierLevels);
        startObstacleCount = Mathf.Max(0, startObstacleCount);
        startCoinCount = Mathf.Max(0, startCoinCount);
    }
}