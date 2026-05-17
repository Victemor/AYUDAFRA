using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orquestador central de la progresión infinita de niveles.
///
/// Responsabilidades:
/// - Configurar dificultad procedural por nivel.
/// - Generar pista, contenido, barreras, checkpoints y power-ups.
/// - Reposicionar la bola al inicio real del nivel.
/// - Guardar correctamente el respawn inicial del nivel.
/// - Reconstruir pistas decorativas anterior/siguiente sin gameplay.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class InfiniteLevelManager : MonoBehaviour
{
    #region Inspector

    [Header("Progresión")]
    [SerializeField]
    [Tooltip("SO con valores iniciales de dificultad y semilla base.")]
    private InfiniteProgressionSettings progressionSettings;

    [Header("Generadores")]
    [SerializeField]
    [Tooltip("Controlador procedural de la pista.")]
    private TrackGeneratorController trackGenerator;

    [SerializeField]
    [Tooltip("Generador de obstáculos, monedas y meta.")]
    private TrackContentGenerator contentGenerator;

    [SerializeField]
    [Tooltip("Generador de barreras laterales.")]
    private TrackBarrierGenerator barrierGenerator;

    [SerializeField]
    [Tooltip("Catálogo global de contenido. Sus valores Base son el máximo de dificultad.")]
    private TrackContentGenerationProfile contentProfile;

    [Header("Decoración de niveles adyacentes")]
    [SerializeField]
    [Tooltip("Generador de pistas decorativas anterior/siguiente. No afecta el gameplay.")]
    private AdjacentTrackPreviewManager adjacentTrackPreviewManager;

    [Header("Nivel Bonus")]
    [SerializeField]
    [Tooltip("Profile de generación con materiales especiales para los niveles bonus.")]
    private TrackGenerationProfile bonusTrackProfile;

    [Header("Jugador")]
    [SerializeField]
    [Tooltip("Motor de movimiento de la bola.")]
    private BallMovementMotor ballMovementMotor;

    [SerializeField]
    [Tooltip("Controlador de estado de la bola.")]
    private BallStateController ballStateController;

    [SerializeField]
    [Tooltip("Controlador de respawn de la bola.")]
    private BallRespawnController ballRespawnController;

    [SerializeField]
    [Tooltip("Distancia vertical sobre la superficie de la pista donde spawneará la bola.")]
    private float ballSpawnHeightOffset = 1f;

    [Header("Checkpoints y Vidas")]
    [SerializeField]
    [Tooltip("Gestor de checkpoints.")]
    private SpawnPointManager spawnPointManager;

    [SerializeField]
    [Tooltip("Controlador de vidas del jugador.")]
    private LivesController livesController;

    [Header("Power-Ups")]
    [SerializeField]
    [Tooltip("Generador de power-ups.")]
    private PowerUpGenerator powerUpGenerator;

    [Header("Transición")]
    [SerializeField]
    [Tooltip("Segundos entre que la bola toca la meta y se genera el siguiente nivel.")]
    private float levelTransitionDelay = 0.8f;

    [SerializeField]
    [Tooltip("Si está activo, el nivel NO avanza automáticamente al llegar a la meta.\n" +
             "Requiere que el jugador pulse el botón en el panel de PlaytestLevelController.\n" +
             "Útil para probar mecánicas sin presión de tiempo entre niveles.")]
    private bool requireInputToAdvance = false;

    [Header("Estado Actual — Solo Lectura en Runtime")]
    [SerializeField]
    [Tooltip("Índice del nivel activo. Comienza en 1.")]
    private int currentLevelIndex = 1;

    [SerializeField]
    [Tooltip("Semilla activa = BaseSeed + LevelIndex.")]
    private int currentActiveSeed;

    [SerializeField]
    [Tooltip("Factor de progresión [0 = inicio, 1 = dificultad máxima].")]
    [Range(0f, 1f)]
    private float currentProgressionT;

    [SerializeField]
    [Tooltip("¿El nivel actual es un nivel bonus?")]
    private bool currentIsBonus;

    #endregion

    #region Runtime

    private LevelGenerationSettings runtimeTrackSettings;
    private LevelContentGenerationSettings runtimeContentSettings;
    private Coroutine levelTransitionCoroutine;
    private bool pendingLevelAdvance;

    #endregion

    #region Properties

    /// <summary>
    /// Nivel activo actual.
    /// </summary>
    public int CurrentLevelIndex => currentLevelIndex;

    /// <summary>
    /// Semilla activa actual.
    /// </summary>
    public int CurrentActiveSeed => currentActiveSeed;

    /// <summary>
    /// Indica si el nivel actual es bonus.
    /// </summary>
    public bool CurrentIsBonus => currentIsBonus;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!ValidateReferences())
        {
            return;
        }

        runtimeTrackSettings = ScriptableObject.CreateInstance<LevelGenerationSettings>();
        runtimeContentSettings = ScriptableObject.CreateInstance<LevelContentGenerationSettings>();

        trackGenerator.DisableAutoGeneration();
        contentGenerator.DisableAutoGeneration();

        if (powerUpGenerator != null)
        {
            powerUpGenerator.DisableAutoGeneration();
        }
    }

    private void Start()
    {
        if (!ValidateReferences())
        {
            return;
        }

        GenerateInitialLevel();
    }

    private void OnEnable()
    {
        GameEvents.OnGoalReached += HandleGoalReached;
    }

    private void OnDisable()
    {
        GameEvents.OnGoalReached -= HandleGoalReached;
    }

    private void OnDestroy()
    {
        if (runtimeTrackSettings != null)
        {
            Destroy(runtimeTrackSettings);
        }

        if (runtimeContentSettings != null)
        {
            Destroy(runtimeContentSettings);
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Reinicia la progresión al nivel 1.
    /// </summary>
    [ContextMenu("Restart To Level One")]
    public void RestartToLevelOne()
    {
        if (levelTransitionCoroutine != null)
        {
            StopCoroutine(levelTransitionCoroutine);
            levelTransitionCoroutine = null;
        }

        currentLevelIndex = 1;

        ConfigureAndGenerate();
        RepositionBallAtLevelStart();

        Debug.Log("[INFINITE LEVEL] Juego reiniciado al nivel 1.", this);
    }

    /// <summary>
    /// Regenera el nivel actual manteniendo el mismo índice.
    /// </summary>
    [ContextMenu("Regenerate Current Level")]
    public void RegenerateCurrentLevel()
    {
        if (levelTransitionCoroutine != null)
        {
            StopCoroutine(levelTransitionCoroutine);
            levelTransitionCoroutine = null;
        }

        ConfigureAndGenerate();
        RepositionBallAtLevelStart();
    }

    /// <summary>
    /// Avanza manualmente al siguiente nivel.
    /// Solo tiene efecto si <c>requireInputToAdvance</c> es <c>true</c>
    /// y el jugador ya llegó a la meta.
    /// Llamado por <see cref="PlaytestLevelController.OnNextLevelButtonPressed"/>.
    /// </summary>
    public void AdvanceToNextLevel()
    {
        if (!pendingLevelAdvance) return;

        pendingLevelAdvance = false;

        if (levelTransitionCoroutine != null)
            StopCoroutine(levelTransitionCoroutine);

        levelTransitionCoroutine = StartCoroutine(LevelTransitionRoutine());
    }

    #endregion

    #region Level Flow

    /// <summary>
    /// Genera el primer nivel.
    /// </summary>
    private void GenerateInitialLevel()
    {
        ConfigureAndGenerate();
        RepositionBallAtLevelStart();
    }

    /// <summary>
    /// Maneja llegada a meta.
    /// </summary>
    private void HandleGoalReached()
    {
        if (levelTransitionCoroutine != null)
            StopCoroutine(levelTransitionCoroutine);

        if (requireInputToAdvance)
        {
            pendingLevelAdvance = true;
            return;
        }

        levelTransitionCoroutine = StartCoroutine(LevelTransitionRoutine());
    }

    /// <summary>
    /// Transición hacia el siguiente nivel.
    /// </summary>
    private IEnumerator LevelTransitionRoutine()
    {
        yield return new WaitForSeconds(levelTransitionDelay);

        currentLevelIndex++;

        ConfigureAndGenerate();
        RepositionBallAtLevelStart();

        levelTransitionCoroutine = null;
    }

    /// <summary>
    /// Configura y genera todos los sistemas del nivel activo.
    /// </summary>
    private void ConfigureAndGenerate()
    {
        currentProgressionT = ComputeProgressionT(
            currentLevelIndex,
            progressionSettings.LevelCountToReachMax);

        currentActiveSeed = progressionSettings.BaseSeed + currentLevelIndex;
        currentIsBonus = IsBonusLevel(currentLevelIndex);

        TrackGenerationProfile trackProfile = trackGenerator.GenerationProfile;

        ConfigureTrack(trackProfile);
        ConfigureContent();
        ConfigureSpawnPoints();
        ConfigurePowerUps(trackProfile);
        ConfigureAdjacentPreviews();
        PrintLevelLog();
    }

    #endregion

    #region Track

    /// <summary>
    /// Configura y genera la pista funcional del nivel actual.
    /// </summary>
    private void ConfigureTrack(TrackGenerationProfile trackProfile)
    {
        runtimeTrackSettings.ConfigureForLevel(
            progressionSettings,
            trackProfile,
            currentLevelIndex);

        if (currentIsBonus)
        {
            runtimeTrackSettings.ApplyBonusLevelOverrides();
        }

        ConfigureBarrierProbability(trackProfile);

        if (currentIsBonus && bonusTrackProfile != null)
        {
            trackGenerator.SetVisualProfileOverride(bonusTrackProfile);
        }
        else
        {
            trackGenerator.ClearVisualProfileOverride();
        }

        trackGenerator.GenerateLevel(runtimeTrackSettings);
    }

    #endregion

    #region Content

    /// <summary>
    /// Configura y genera contenido funcional del nivel actual.
    /// </summary>
    private void ConfigureContent()
    {
        runtimeContentSettings.ConfigureForLevel(
            progressionSettings,
            contentProfile,
            currentLevelIndex);

        if (currentIsBonus)
        {
            runtimeContentSettings.ConfigureForBonusLevel(
                progressionSettings.BonusCoinCount);
        }

        contentGenerator.GenerateContent(runtimeContentSettings);
    }

    #endregion

    #region Spawn Points

    /// <summary>
    /// Genera checkpoints funcionales del nivel actual.
    /// </summary>
    private void ConfigureSpawnPoints()
    {
        if (spawnPointManager == null ||
            ballRespawnController == null ||
            livesController == null)
        {
            return;
        }

        spawnPointManager.PlaceSpawnPoints(
            trackGenerator.GeneratedMap,
            ballRespawnController,
            livesController);
    }

    #endregion

    #region Power-Ups

    /// <summary>
    /// Genera power-ups funcionales del nivel actual.
    /// </summary>
    private void ConfigurePowerUps(TrackGenerationProfile trackProfile)
    {
        if (powerUpGenerator == null) return;

        float safeStart = progressionSettings.SafeStartLengthOverride > 0f
            ? progressionSettings.SafeStartLengthOverride
            : trackProfile.SafeStartLength;

        float safeEnd = progressionSettings.SafeEndLengthOverride > 0f
            ? progressionSettings.SafeEndLengthOverride
            : trackProfile.SafeEndLength;

        powerUpGenerator.GeneratePowerUps(
            trackGenerator.GeneratedMap,
            currentActiveSeed,
            contentGenerator.ReservationMap,
            currentLevelIndex,
            currentProgressionT,
            safeStart,
            safeEnd);
    }

    #endregion

    #region Adjacent Previews

    /// <summary>
    /// Reconstruye las pistas decorativas anterior/siguiente.
    /// Debe ejecutarse después de generar el track funcional actual.
    /// </summary>
    private void ConfigureAdjacentPreviews()
    {
        if (adjacentTrackPreviewManager == null)
        {
            return;
        }

        adjacentTrackPreviewManager.RebuildPreviews(currentLevelIndex);
    }

    #endregion

    #region Respawn

    /// <summary>
    /// Posiciona la bola al inicio real del nivel actual.
    /// También actualiza el respawn inicial para evitar que reaparezca en una posición vieja.
    /// </summary>
    private void RepositionBallAtLevelStart()
    {
        if (ballMovementMotor == null ||
            ballStateController == null ||
            trackGenerator == null)
        {
            return;
        }

        if (!TryResolveInitialSpawnTransform(out Vector3 spawnPosition, out Quaternion spawnRotation))
        {
            spawnPosition = trackGenerator.transform.position + Vector3.up * ballSpawnHeightOffset;

            float generatorYaw = trackGenerator.transform.eulerAngles.y;
            spawnRotation = Quaternion.Euler(0f, generatorYaw, 0f);

            Debug.LogWarning(
                "[INFINITE LEVEL] No se pudo resolver el spawn inicial desde GeneratedMap. " +
                "Se usará el origen del TrackGenerator como fallback.",
                this);
        }

        ballMovementMotor.TeleportTo(spawnPosition, spawnRotation);
        ballStateController.ResetState();

        if (ballRespawnController != null)
        {
            ballRespawnController.SetRespawnPosition(spawnPosition, spawnRotation);
        }

        if (livesController != null)
        {
            livesController.InitializeForLevel(spawnPosition, spawnRotation);
        }

        if (spawnPointManager != null)
        {
            spawnPointManager.ResetAllSpawnPoints();
        }
    }

    /// <summary>
    /// Resuelve el spawn inicial usando la mitad de la zona segura inicial de la pista generada.
    /// </summary>
    private bool TryResolveInitialSpawnTransform(out Vector3 spawnPosition, out Quaternion spawnRotation)
    {
        spawnPosition = Vector3.zero;
        spawnRotation = Quaternion.identity;

        TrackRuntimeMap runtimeMap = trackGenerator.GeneratedMap;

        if (runtimeMap == null || runtimeMap.PathSampler == null)
        {
            return false;
        }

        TrackGenerationProfile trackProfile = trackGenerator.GenerationProfile;

        if (trackProfile == null)
        {
            return false;
        }

        float safeStartLength = ResolveSafeStartLength(trackProfile);

        // TrackPathSampler ya implementa la misma lógica de forma canónica.
        // No hay razón para duplicarla en InfiniteLevelManager.
        TrackSample startSample = runtimeMap.PathSampler.SampleAtDistance(safeStartLength);

        Vector3 horizontalForward = new Vector3(startSample.Forward.x, 0f, startSample.Forward.z);

        spawnPosition = startSample.Position + Vector3.up * ballSpawnHeightOffset;
        spawnRotation = horizontalForward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(horizontalForward.normalized, Vector3.up)
            : Quaternion.identity;

        return true;
    }

    /// <summary>
    /// Resuelve la longitud real de la zona segura inicial usando la progresión activa.
    /// </summary>
    private float ResolveSafeStartLength(TrackGenerationProfile trackProfile)
    {
        if (progressionSettings != null && progressionSettings.SafeStartLengthOverride > 0f)
        {
            return progressionSettings.SafeStartLengthOverride;
        }

        return trackProfile.SafeStartLength;
    }

    #endregion

    #region Barriers

    /// <summary>
    /// Configura la cobertura de barreras del nivel actual.
    /// Los primeros niveles y los bonus fuerzan cobertura completa.
    /// </summary>
    private void ConfigureBarrierProbability(TrackGenerationProfile trackProfile)
    {
        if (barrierGenerator == null)
        {
            return;
        }

        float safeStartLength = progressionSettings.SafeStartLengthOverride > 0f
            ? progressionSettings.SafeStartLengthOverride
            : trackProfile.SafeStartLength;

        float safeEndLength = progressionSettings.SafeEndLengthOverride > 0f
            ? progressionSettings.SafeEndLengthOverride
            : trackProfile.SafeEndLength;

        barrierGenerator.SetSafeZoneLengths(safeStartLength, safeEndLength);

        if (currentLevelIndex <= progressionSettings.GuaranteedFullBarrierLevels || currentIsBonus)
        {
            barrierGenerator.ForceFullBarriers();
            return;
        }

        float resolvedCoverage = Mathf.Lerp(
            barrierGenerator.GeneralCoverageRatio,
            progressionSettings.MinBarrierCoverageRatio,
            currentProgressionT);

        barrierGenerator.SetBarrierProbability(resolvedCoverage);
    }

    #endregion

    #region Logging

    /// <summary>
    /// Imprime resumen del nivel generado.
    /// </summary>
    private void PrintLevelLog()
    {
        string label = currentIsBonus
            ? $"{currentLevelIndex} BONUS"
            : $"{currentLevelIndex}";

        string diffLabel = currentProgressionT >= 1f
            ? "DIFICULTAD MÁXIMA"
            : $"máx en nivel {progressionSettings.LevelCountToReachMax}";

        Debug.Log(
            $"[INFINITE LEVEL] ══════════════════════════════\n" +
            $"  Nivel         : {label}\n" +
            $"  Semilla       : {currentActiveSeed}  (base {progressionSettings.BaseSeed} + {currentLevelIndex})\n" +
            $"  Progresión    : {currentProgressionT:P0}  ({diffLabel})\n" +
            $"  Obstáculos    : máx {runtimeContentSettings.MaxObstacleCount}\n" +
            $"  Monedas       : {runtimeContentSettings.FixedCoinCount}\n" +
            $"══════════════════════════════",
            this);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Indica si el nivel indicado es bonus.
    /// </summary>
    private bool IsBonusLevel(int levelIndex)
    {
        int interval = progressionSettings.BonusLevelInterval;
        return interval > 0 && levelIndex % interval == 0;
    }

    /// <summary>
    /// Calcula el factor de progresión de dificultad.
    /// </summary>
    private static float ComputeProgressionT(int levelIndex, int levelCountToReachMax)
    {
        if (levelCountToReachMax <= 1)
        {
            return 1f;
        }

        return Mathf.Clamp01((float)(levelIndex - 1) / (levelCountToReachMax - 1));
    }

    #endregion

    #region Validation

    /// <summary>
    /// Valida referencias obligatorias.
    /// </summary>
    private bool ValidateReferences()
    {
        bool isValid = true;

        if (progressionSettings == null)
        {
            Debug.LogError("[INFINITE LEVEL] progressionSettings no asignado.", this);
            isValid = false;
        }

        if (trackGenerator == null)
        {
            Debug.LogError("[INFINITE LEVEL] trackGenerator no asignado.", this);
            isValid = false;
        }

        if (contentGenerator == null)
        {
            Debug.LogError("[INFINITE LEVEL] contentGenerator no asignado.", this);
            isValid = false;
        }

        if (contentProfile == null)
        {
            Debug.LogError("[INFINITE LEVEL] contentProfile no asignado.", this);
            isValid = false;
        }

        if (ballMovementMotor == null)
        {
            Debug.LogError("[INFINITE LEVEL] ballMovementMotor no asignado.", this);
            isValid = false;
        }

        if (ballStateController == null)
        {
            Debug.LogError("[INFINITE LEVEL] ballStateController no asignado.", this);
            isValid = false;
        }

        if (ballRespawnController == null)
        {
            Debug.LogWarning(
                "[INFINITE LEVEL] ballRespawnController no asignado. El respawn inicial no podrá guardarse explícitamente.",
                this);
        }

        if (livesController == null)
        {
            Debug.LogWarning("[INFINITE LEVEL] livesController no asignado.", this);
        }

        if (adjacentTrackPreviewManager == null)
        {
            Debug.LogWarning(
                "[INFINITE LEVEL] adjacentTrackPreviewManager no asignado. Las pistas anterior/siguiente decorativas no se generarán.",
                this);
        }

        return isValid;
    }

    #endregion
}