using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Catálogo global y configuración base para generación procedural de contenido sobre pista.
/// </summary>
[CreateAssetMenu(fileName = "TrackContentGenerationProfile", menuName = "Game/Track/Content Generation Profile")]
public sealed class TrackContentGenerationProfile : ScriptableObject
{
    [Header("Global Catalog - Obstacles")]
    [Tooltip("Catálogo global de cajas disponibles para todos los niveles.")]
    [SerializeField] private List<TrackSpawnPrefabEntry> boxPrefabs = new List<TrackSpawnPrefabEntry>();

    [Tooltip("Catálogo global de muros disponibles para todos los niveles.")]
    [SerializeField] private List<TrackSpawnPrefabEntry> wallPrefabs = new List<TrackSpawnPrefabEntry>();

    [Tooltip("Catálogo global de pelotas empujables disponibles para todos los niveles.")]
    [SerializeField] private List<TrackSpawnPrefabEntry> ballPrefabs = new List<TrackSpawnPrefabEntry>();

    [Tooltip("Catálogo global de ventiladores o aspas disponibles para todos los niveles.")]
    [SerializeField] private List<TrackSpawnPrefabEntry> fanPrefabs = new List<TrackSpawnPrefabEntry>();

    [Header("Global Catalog - Coins")]
    [Tooltip("Catálogo global de monedas disponibles para todos los niveles.")]
    [SerializeField] private List<TrackSpawnPrefabEntry> coinPrefabs = new List<TrackSpawnPrefabEntry>();

    [Header("Goal")]
    [Tooltip("Prefab de meta. Debe traer su collider trigger y GoalTrigger configurado.")]
    [SerializeField] private GameObject goalPrefab;

    [Tooltip("Largo reservado por la meta para impedir que otros objetos aparezcan cerca.")]
    [SerializeField] private float goalReservationLength = 5f;

    [Tooltip("Ancho reservado por la meta para impedir que otros objetos aparezcan cerca.")]
    [SerializeField] private float goalReservationWidth = 8f;

    [Header("Base Chances Fallback")]
    [Tooltip("Probabilidad base de cajas si el nivel no tiene configuración propia.")]
    [Range(0f, 1f)]
    [SerializeField] private float baseBoxSpawnChance = 0.18f;

    [Tooltip("Probabilidad base de muros si el nivel no tiene configuración propia.")]
    [Range(0f, 1f)]
    [SerializeField] private float baseWallSpawnChance = 0.04f;

    [Tooltip("Probabilidad base de pelotas en pista plana si el nivel no tiene configuración propia.")]
    [Range(0f, 1f)]
    [SerializeField] private float baseBallFlatSpawnChance = 0.04f;

    [Tooltip("Probabilidad base de pelotas en estrechamiento si el nivel no tiene configuración propia.")]
    [Range(0f, 1f)]
    [SerializeField] private float baseBallNarrowSpawnChance = 0.08f;

    [Tooltip("Probabilidad base de pelotas en railes si el nivel no tiene configuración propia.")]
    [Range(0f, 1f)]
    [SerializeField] private float baseBallRailSpawnChance = 0.025f;

    [Tooltip("Probabilidad base de pelotas antes de bajadas si el nivel no tiene configuración propia.")]
    [Range(0f, 1f)]
    [SerializeField] private float baseBallBeforeDownSlopeChance = 0.2f;

    [Tooltip("Probabilidad base de ventiladores en pista plana si el nivel no tiene configuración propia.")]
    [Range(0f, 1f)]
    [SerializeField] private float baseFanFlatSpawnChance = 0.015f;

    [Tooltip("Probabilidad base de ventiladores en rail recto si el nivel no tiene configuración propia.")]
    [Range(0f, 1f)]
    [SerializeField] private float baseFanStraightRailSpawnChance = 0.01f;

    [Header("Base Coins Fallback")]
    [Tooltip("Si no hay configuración de nivel, permite que la cantidad de monedas sea aleatoria.")]
    [SerializeField] private bool baseUseRandomCoinCount = true;

    [Tooltip("Cantidad fija de monedas usada como fallback.")]
    [SerializeField] private int baseFixedCoinCount = 20;

    [Tooltip("Mínimo de monedas usado como fallback cuando la cantidad es aleatoria.")]
    [SerializeField] private int baseMinRandomCoinCount = 10;

    [Tooltip("Máximo de monedas usado como fallback cuando la cantidad es aleatoria.")]
    [SerializeField] private int baseMaxRandomCoinCount = 30;

    [Header("General Placement")]
    [Tooltip("Offset vertical base aplicado a todos los spawns.")]
    [SerializeField] private float globalVerticalOffset = 0.05f;

    [Tooltip("Distancia inicial donde no se generará contenido.")]
    [SerializeField] private float startContentPadding = 8f;

    [Tooltip("Distancia final donde no se generarán obstáculos ni monedas.")]
    [SerializeField] private float endContentPadding = 8f;

    [Tooltip("Cada cuántas unidades de distancia se evalúa si aparece un obstáculo.")]
    [SerializeField] private float obstacleEvaluationStep = 3f;

    [Tooltip("Separación longitudinal extra entre obstáculos para evitar acumulación.")]
    [SerializeField] private float obstacleDistanceSpacing = 2f;

    [Header("Boxes Placement")]
    [Tooltip("Separación lateral mínima entre cajas de una misma fila.")]
    [SerializeField] private float boxLateralSpacing = 0.15f;

    [Tooltip("Margen mínimo contra los bordes de la pista para que las cajas no se salgan.")]
    [SerializeField] private float boxEdgePadding = 0.15f;

    [Tooltip("Cantidad máxima de cajas que se pueden generar en una misma fila.")]
    [SerializeField] private int maxBoxesPerRow = 4;

    [Tooltip("Permite que una misma fila pueda tener varias cajas.")]
    [SerializeField] private bool allowMultipleBoxesPerRow = true;

    [Header("Balls Placement")]
    [Tooltip("Distancia antes de una bajada donde puede aparecer una pelota empujable.")]
    [SerializeField] private float beforeDownSlopeWindow = 6f;

    [Header("Fans Placement")]
    [Tooltip("Distancia lateral extra desde el borde de la pista hacia afuera para colocar el centro del ventilador.")]
    [SerializeField] private float fanOutsideOffset = 1.25f;

    [Tooltip("Permite elegir aleatoriamente si el ventilador aparece a izquierda o derecha.")]
    [SerializeField] private bool randomizeFanSide = true;

    [Tooltip("Lado usado cuando Randomize Fan Side está desactivado. -1 izquierda, 1 derecha.")]
    [SerializeField] private int fixedFanSide = 1;

    [Header("Coins Placement")]
    [Tooltip("Margen lateral contra los bordes de la pista para generar monedas.")]
    [SerializeField] private float coinEdgePadding = 0.25f;

    [Tooltip("Largo reservado por cada moneda para evitar solapamiento con otros objetos.")]
    [SerializeField] private float coinReservationLength = 0.8f;

    [Tooltip("Ancho reservado por cada moneda para evitar solapamiento con otros objetos.")]
    [SerializeField] private float coinReservationWidth = 0.8f;

    [Tooltip("Intentos máximos para colocar un patrón de monedas antes de descartarlo.")]
    [SerializeField] private int coinPlacementAttempts = 8;

    [Tooltip("Cantidad mínima de monedas por patrón.")]
    [SerializeField] private int minCoinsPerPattern = 3;

    [Tooltip("Cantidad máxima de monedas por patrón.")]
    [SerializeField] private int maxCoinsPerPattern = 7;

    [Tooltip("Separación longitudinal entre monedas dentro de un patrón.")]
    [SerializeField] private float coinPatternDistanceSpacing = 1.25f;

    [Tooltip("Amplitud lateral usada por patrones como ZigZag o Arc.")]
    [SerializeField] private float coinPatternLateralAmplitude = 1.25f;

    [Header("Goal Placement")]
    [Tooltip("Offset vertical aplicado al prefab de meta.")]
    [SerializeField] private float goalVerticalOffset;

    [Header("Infinite Progression — Máximos")]
    [Tooltip("Cantidad máxima de obstáculos (cajas + muros + pelotas + ventiladores) " +
            "que se pueden generar en el nivel de máxima dificultad.\n" +
            "0 = sin límite.")]
    [SerializeField] private int baseMaxObstacleCount = 30;
    
    [Tooltip("Cantidad máxima de monedas en el nivel de máxima dificultad.\n" +
            "El valor mínimo inicial se configura en InfiniteProgressionSettings.StartCoinCount.")]
    [SerializeField] private int baseMaxCoinCount = 40;
    public IReadOnlyList<TrackSpawnPrefabEntry> BoxPrefabs => boxPrefabs;
    public IReadOnlyList<TrackSpawnPrefabEntry> WallPrefabs => wallPrefabs;
    public IReadOnlyList<TrackSpawnPrefabEntry> BallPrefabs => ballPrefabs;
    public IReadOnlyList<TrackSpawnPrefabEntry> FanPrefabs => fanPrefabs;
    public IReadOnlyList<TrackSpawnPrefabEntry> CoinPrefabs => coinPrefabs;
    public GameObject GoalPrefab => goalPrefab;
    public float GoalReservationLength => Mathf.Max(0.1f, goalReservationLength);
    public float GoalReservationWidth => Mathf.Max(0.1f, goalReservationWidth);

    public float BaseBoxSpawnChance => baseBoxSpawnChance;
    public float BaseWallSpawnChance => baseWallSpawnChance;
    public float BaseBallFlatSpawnChance => baseBallFlatSpawnChance;
    public float BaseBallNarrowSpawnChance => baseBallNarrowSpawnChance;
    public float BaseBallRailSpawnChance => baseBallRailSpawnChance;
    public float BaseBallBeforeDownSlopeChance => baseBallBeforeDownSlopeChance;
    public float BaseFanFlatSpawnChance => baseFanFlatSpawnChance;
    public float BaseFanStraightRailSpawnChance => baseFanStraightRailSpawnChance;

    public bool BaseUseRandomCoinCount => baseUseRandomCoinCount;
    public int BaseFixedCoinCount => Mathf.Max(0, baseFixedCoinCount);
    public int BaseMinRandomCoinCount => Mathf.Max(0, baseMinRandomCoinCount);
    public int BaseMaxRandomCoinCount => Mathf.Max(BaseMinRandomCoinCount, baseMaxRandomCoinCount);

    public float GlobalVerticalOffset => globalVerticalOffset;
    public float StartContentPadding => Mathf.Max(0f, startContentPadding);
    public float EndContentPadding => Mathf.Max(0f, endContentPadding);
    public float ObstacleEvaluationStep => Mathf.Max(0.5f, obstacleEvaluationStep);
    public float ObstacleDistanceSpacing => Mathf.Max(0f, obstacleDistanceSpacing);

    public float BoxLateralSpacing => Mathf.Max(0.05f, boxLateralSpacing);
    public float BoxEdgePadding => Mathf.Max(0.05f, boxEdgePadding);
    public int MaxBoxesPerRow => Mathf.Max(1, maxBoxesPerRow);
    public bool AllowMultipleBoxesPerRow => allowMultipleBoxesPerRow;

    public float BeforeDownSlopeWindow => Mathf.Max(0.5f, beforeDownSlopeWindow);

    public float FanOutsideOffset => Mathf.Max(0f, fanOutsideOffset);
    public bool RandomizeFanSide => randomizeFanSide;
    public int FixedFanSide => fixedFanSide < 0 ? -1 : 1;

    public float CoinEdgePadding => Mathf.Max(0.05f, coinEdgePadding);
    public float CoinReservationLength => Mathf.Max(0.1f, coinReservationLength);
    public float CoinReservationWidth => Mathf.Max(0.1f, coinReservationWidth);
    public int CoinPlacementAttempts => Mathf.Max(1, coinPlacementAttempts);
    public int MinCoinsPerPattern => Mathf.Max(1, minCoinsPerPattern);
    public int MaxCoinsPerPattern => Mathf.Max(MinCoinsPerPattern, maxCoinsPerPattern);
    public float CoinPatternDistanceSpacing => Mathf.Max(0.25f, coinPatternDistanceSpacing);
    public float CoinPatternLateralAmplitude => Mathf.Max(0f, coinPatternLateralAmplitude);

    public int BaseMaxObstacleCount => Mathf.Max(0, baseMaxObstacleCount);
    public int BaseMaxCoinCount => Mathf.Max(0, baseMaxCoinCount);

    public float GoalVerticalOffset => goalVerticalOffset;

    private void OnValidate()
    {
        goalReservationLength = Mathf.Max(0.1f, goalReservationLength);
        goalReservationWidth = Mathf.Max(0.1f, goalReservationWidth);

        baseFixedCoinCount = Mathf.Max(0, baseFixedCoinCount);
        baseMinRandomCoinCount = Mathf.Max(0, baseMinRandomCoinCount);
        baseMaxRandomCoinCount = Mathf.Max(baseMinRandomCoinCount, baseMaxRandomCoinCount);

        obstacleEvaluationStep = Mathf.Max(0.5f, obstacleEvaluationStep);
        obstacleDistanceSpacing = Mathf.Max(0f, obstacleDistanceSpacing);

        boxLateralSpacing = Mathf.Max(0.05f, boxLateralSpacing);
        boxEdgePadding = Mathf.Max(0.05f, boxEdgePadding);
        maxBoxesPerRow = Mathf.Max(1, maxBoxesPerRow);

        beforeDownSlopeWindow = Mathf.Max(0.5f, beforeDownSlopeWindow);

        fanOutsideOffset = Mathf.Max(0f, fanOutsideOffset);
        fixedFanSide = fixedFanSide < 0 ? -1 : 1;

        coinEdgePadding = Mathf.Max(0.05f, coinEdgePadding);
        coinReservationLength = Mathf.Max(0.1f, coinReservationLength);
        coinReservationWidth = Mathf.Max(0.1f, coinReservationWidth);
        coinPlacementAttempts = Mathf.Max(1, coinPlacementAttempts);
        minCoinsPerPattern = Mathf.Max(1, minCoinsPerPattern);
        maxCoinsPerPattern = Mathf.Max(minCoinsPerPattern, maxCoinsPerPattern);
        coinPatternDistanceSpacing = Mathf.Max(0.25f, coinPatternDistanceSpacing);
        coinPatternLateralAmplitude = Mathf.Max(0f, coinPatternLateralAmplitude);

        NormalizeEntries(boxPrefabs);
        NormalizeEntries(wallPrefabs);
        NormalizeEntries(ballPrefabs);
        NormalizeEntries(fanPrefabs);
        NormalizeEntries(coinPrefabs);

        baseMaxObstacleCount = Mathf.Max(0, baseMaxObstacleCount);
        baseMaxCoinCount = Mathf.Max(0, baseMaxCoinCount);
    }

    private static void NormalizeEntries(List<TrackSpawnPrefabEntry> entries)
    {
        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            entries[i]?.Normalize();
        }
    }
}