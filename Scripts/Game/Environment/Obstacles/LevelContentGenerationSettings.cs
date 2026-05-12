using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Parámetros de contenido activos en el nivel actual.
///
/// En modo de progresión infinita, este asset es configurado en runtime por
/// <see cref="InfiniteLevelManager"/> vía <see cref="ConfigureForLevel"/>.
///
/// Sistema de monedas: valor fijo por nivel con piso de 10 monedas mínimas.
/// Sistema de obstáculos: límite máximo de obstáculos (cajas + muros + pelotas + ventiladores)
/// que pueden generarse en el nivel, escalando de mínimo a máximo con la progresión.
/// </summary>
[CreateAssetMenu(fileName = "LevelContentGenerationSettings",
    menuName = "Game/Track/Level Content Generation Settings")]
public sealed class LevelContentGenerationSettings : ScriptableObject
{
    [Header("Catalog Reference")]
    [Tooltip("Catálogo global. No genera contenido por sí solo.")]
    [SerializeField] private TrackContentGenerationProfile contentCatalog;

    [Header("Override Behaviour")]
    [Tooltip("En modo infinito siempre es False — usa pesos base del catálogo.\n" +
             "True solo si usas este asset como configuración manual de nivel fijo.")]
    [SerializeField] private bool useOverridesAsWhitelist = false;

    [Header("Category Toggles")]
    [SerializeField] private bool enableBoxes = true;
    [SerializeField] private bool enableWalls = true;
    [SerializeField] private bool enableBalls = true;
    [SerializeField] private bool enableFans = true;
    [SerializeField] private bool enableCoins = true;
    [SerializeField] private bool enableGoal = true;

    [Header("Spawn Chances")]
    [Range(0f, 1f)] [SerializeField] private float boxSpawnChance = 0.18f;
    [Range(0f, 1f)] [SerializeField] private float wallSpawnChance = 0.04f;
    [Range(0f, 1f)] [SerializeField] private float ballFlatSpawnChance = 0.04f;
    [Range(0f, 1f)] [SerializeField] private float ballNarrowSpawnChance = 0.08f;
    [Range(0f, 1f)] [SerializeField] private float ballRailSpawnChance = 0.025f;
    [Range(0f, 1f)] [SerializeField] private float ballBeforeDownSlopeChance = 0.2f;
    [Range(0f, 1f)] [SerializeField] private float fanFlatSpawnChance = 0.015f;
    [Range(0f, 1f)] [SerializeField] private float fanStraightRailSpawnChance = 0.01f;

    [Header("Obstáculos — Cantidad Máxima")]
    [Tooltip("Máximo de obstáculos (cajas + muros + pelotas + ventiladores) que se generan en este nivel.\n" +
             "0 = sin límite (usa solo las probabilidades de spawn).\n" +
             "En modo infinito este valor es configurado automáticamente por progresión.")]
    [SerializeField] private int maxObstacleCount = 0;

    [Header("Coins — Cantidad Fija")]
    [Tooltip("Cantidad fija de monedas a generar en este nivel.\n" +
             "Mínimo absoluto: 10. En modo infinito este valor es configurado automáticamente.")]
    [SerializeField] private int fixedCoinCount = 20;

    // Campos legacy mantenidos para compatibilidad con fallback del contentProfile.
    // En modo infinito no se usan — solo se usa fixedCoinCount.
    [HideInInspector] [SerializeField] private bool useRandomCoinCount = false;
    [HideInInspector] [SerializeField] private int minRandomCoinCount = 10;
    [HideInInspector] [SerializeField] private int maxRandomCoinCount = 30;

    [Header("Allowed Prefabs")]
    [SerializeField] private List<LevelSpawnPrefabOverride> boxOverrides = new List<LevelSpawnPrefabOverride>();
    [SerializeField] private List<LevelSpawnPrefabOverride> wallOverrides = new List<LevelSpawnPrefabOverride>();
    [SerializeField] private List<LevelSpawnPrefabOverride> ballOverrides = new List<LevelSpawnPrefabOverride>();
    [SerializeField] private List<LevelSpawnPrefabOverride> fanOverrides = new List<LevelSpawnPrefabOverride>();
    [SerializeField] private List<LevelSpawnPrefabOverride> coinOverrides = new List<LevelSpawnPrefabOverride>();

    #region Properties

    public TrackContentGenerationProfile ContentCatalog => contentCatalog;
    public bool UseOverridesAsWhitelist => useOverridesAsWhitelist;

    public bool EnableBoxes => enableBoxes;
    public bool EnableWalls => enableWalls;
    public bool EnableBalls => enableBalls;
    public bool EnableFans => enableFans;
    public bool EnableCoins => enableCoins;
    public bool EnableGoal => enableGoal;

    public float BoxSpawnChance => boxSpawnChance;
    public float WallSpawnChance => wallSpawnChance;
    public float BallFlatSpawnChance => ballFlatSpawnChance;
    public float BallNarrowSpawnChance => ballNarrowSpawnChance;
    public float BallRailSpawnChance => ballRailSpawnChance;
    public float BallBeforeDownSlopeChance => ballBeforeDownSlopeChance;
    public float FanFlatSpawnChance => fanFlatSpawnChance;
    public float FanStraightRailSpawnChance => fanStraightRailSpawnChance;

    /// <summary>
    /// Máximo de obstáculos para este nivel. 0 = sin límite.
    /// </summary>
    public int MaxObstacleCount => Mathf.Max(0, maxObstacleCount);

    // Para compatibilidad con TrackContentGenerator que llama ResolveCoinCount.
    public bool UseRandomCoinCount => false;
    public int FixedCoinCount => Mathf.Max(10, fixedCoinCount);
    public int MinRandomCoinCount => Mathf.Max(0, minRandomCoinCount);
    public int MaxRandomCoinCount => Mathf.Max(MinRandomCoinCount, maxRandomCoinCount);

    public IReadOnlyList<LevelSpawnPrefabOverride> BoxOverrides => boxOverrides;
    public IReadOnlyList<LevelSpawnPrefabOverride> WallOverrides => wallOverrides;
    public IReadOnlyList<LevelSpawnPrefabOverride> BallOverrides => ballOverrides;
    public IReadOnlyList<LevelSpawnPrefabOverride> FanOverrides => fanOverrides;
    public IReadOnlyList<LevelSpawnPrefabOverride> CoinOverrides => coinOverrides;

    #endregion

    #region Infinite Progression API

    /// <summary>
    /// Configura el nivel actual interpolando desde los valores iniciales de <paramref name="progression"/>
    /// hasta los valores base de <paramref name="contentProfile"/>.
    ///
    /// Monedas: valor fijo = Lerp(startCoinCount, profile.BaseMaxCoinCount, t), piso de 10.
    /// Obstáculos: límite = Lerp(startObstacleCount, profile.BaseMaxObstacleCount, t).
    /// Spawns: Lerp(startChance, profile.BaseChance, t).
    ///
    /// useOverridesAsWhitelist siempre se fuerza a false — los prefabs se eligen
    /// por su peso base del catálogo (las listas de overrides del asset runtime están vacías).
    /// </summary>
    public void ConfigureForLevel(
        InfiniteProgressionSettings progression,
        TrackContentGenerationProfile contentProfile,
        int levelIndex)
    {
        float t = ComputeProgressionT(levelIndex, progression.LevelCountToReachMax);

        // Forzar false — en modo infinito no hay overrides configurados.
        useOverridesAsWhitelist = false;

        // Categorías.
        enableBoxes = progression.EnableBoxes;
        enableWalls = progression.EnableWalls;
        enableBalls = progression.EnableBalls;
        enableFans = progression.EnableFans;
        enableCoins = progression.EnableCoins;
        enableGoal = progression.EnableGoal;

        // Probabilidades de spawn.
        boxSpawnChance = Mathf.Lerp(progression.StartBoxSpawnChance, contentProfile.BaseBoxSpawnChance, t);
        wallSpawnChance = Mathf.Lerp(progression.StartWallSpawnChance, contentProfile.BaseWallSpawnChance, t);
        ballFlatSpawnChance = Mathf.Lerp(progression.StartBallFlatSpawnChance, contentProfile.BaseBallFlatSpawnChance, t);
        ballNarrowSpawnChance = Mathf.Lerp(progression.StartBallNarrowSpawnChance, contentProfile.BaseBallNarrowSpawnChance, t);
        ballRailSpawnChance = Mathf.Lerp(progression.StartBallRailSpawnChance, contentProfile.BaseBallRailSpawnChance, t);
        ballBeforeDownSlopeChance = Mathf.Lerp(progression.StartBallBeforeDownSlopeChance, contentProfile.BaseBallBeforeDownSlopeChance, t);
        fanFlatSpawnChance = Mathf.Lerp(progression.StartFanFlatSpawnChance, contentProfile.BaseFanFlatSpawnChance, t);
        fanStraightRailSpawnChance = Mathf.Lerp(progression.StartFanStraightRailSpawnChance, contentProfile.BaseFanStraightRailSpawnChance, t);

        // Obstáculos: límite máximo escala de mínimo → máximo del profile.
        int resolvedObstacles = Mathf.RoundToInt(
            Mathf.Lerp(progression.StartObstacleCount, contentProfile.BaseMaxObstacleCount, t));
        maxObstacleCount = Mathf.Max(0, resolvedObstacles);

        // Monedas: valor fijo por nivel con piso absoluto de 10.
        useRandomCoinCount = false;
        int resolvedCoins = Mathf.RoundToInt(
            Mathf.Lerp(progression.StartCoinCount, contentProfile.BaseMaxCoinCount, t));
        fixedCoinCount = Mathf.Max(10, resolvedCoins);
        minRandomCoinCount = fixedCoinCount;
        maxRandomCoinCount = fixedCoinCount;
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
    /// Configura el asset para un nivel bonus: solo monedas, sin obstáculos.
    /// Aplica su propio rango de monedas independiente de la progresión normal.
    /// Llamar inmediatamente después de <see cref="ConfigureForLevel"/> cuando el nivel es bonus.
    /// </summary>
    /// <param name="bonusCoinCount">Cantidad fija de monedas exclusiva del nivel bonus.</param>
    public void ConfigureForBonusLevel(int bonusCoinCount)
    {
        enableBoxes = false;
        enableWalls = false;
        enableBalls = false;
        enableFans = false;
        enableCoins = true;
        enableGoal = true;
        maxObstacleCount = 0;

        // El nivel bonus tiene su propio conteo de monedas —
        // no está limitado por la progresión normal.
        useRandomCoinCount = false;
        fixedCoinCount = Mathf.Max(10, bonusCoinCount);
        minRandomCoinCount = fixedCoinCount;
        maxRandomCoinCount = fixedCoinCount;
    }

    #endregion

    private void OnValidate()
    {
        fixedCoinCount = Mathf.Max(10, fixedCoinCount);
        minRandomCoinCount = Mathf.Max(0, minRandomCoinCount);
        maxRandomCoinCount = Mathf.Max(minRandomCoinCount, maxRandomCoinCount);
        maxObstacleCount = Mathf.Max(0, maxObstacleCount);

        NormalizeOverrides(boxOverrides);
        NormalizeOverrides(wallOverrides);
        NormalizeOverrides(ballOverrides);
        NormalizeOverrides(fanOverrides);
        NormalizeOverrides(coinOverrides);
    }

    private static void NormalizeOverrides(List<LevelSpawnPrefabOverride> overrides)
    {
        if (overrides == null)
        {
            return;
        }

        for (int i = 0; i < overrides.Count; i++)
        {
            overrides[i]?.Normalize();
        }
    }
}