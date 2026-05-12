using UnityEngine;

/// <summary>
/// Gestiona el sistema de vidas del jugador.
///
/// Flujo:
/// - Al morir con vidas > 0: pierde 1 vida → respawn en último spawn point (BallRespawnController maneja el teleport).
/// - Al morir con 0 vidas: sobreescribe el punto de respawn al inicio del nivel y dispara
///   <see cref="GameEvents.OnLevelReset"/> para que los spawn points se reseteen.
///
/// Usa [DefaultExecutionOrder(-40)] para ejecutar ANTES que BallRespawnController (orden 0),
/// garantizando que el punto de respawn se actualiza antes de que la corutina lo lea.
/// </summary>
[DefaultExecutionOrder(-40)]
public sealed class LivesController : MonoBehaviour
{
    #region Inspector

    [Header("Configuración")]
    [SerializeField]
    [Tooltip("Cantidad máxima de vidas del jugador.")]
    [Min(1)]
    private int maxLives = 5;

    [Header("Referencias")]
    [SerializeField]
    [Tooltip("Controlador de respawn de la bola. Necesario para sobreescribir el punto\n" +
             "de respawn al inicio del nivel cuando las vidas llegan a 0.")]
    private BallRespawnController respawnController;

    [Header("Estado — Solo Lectura")]
    [SerializeField]
    [Tooltip("Vidas actuales del jugador.")]
    private int currentLives;

    #endregion

    #region Runtime

    private Vector3 levelStartPosition;
    private Quaternion levelStartRotation;

    #endregion

    #region Properties

    /// <summary>Vidas actuales.</summary>
    public int CurrentLives => currentLives;

    /// <summary>Máximo de vidas configurado.</summary>
    public int MaxLives => maxLives;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        GameEvents.OnPlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerDied -= HandlePlayerDied;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Inicializa el controlador con la posición de inicio del nivel actual y resetea las vidas al máximo.
    /// Llamar desde InfiniteLevelManager al comienzo de cada nuevo nivel.
    /// </summary>
    public void InitializeForLevel(Vector3 startPosition, Quaternion startRotation)
    {
        levelStartPosition = startPosition;
        levelStartRotation = startRotation;
        currentLives = maxLives;
        GameEvents.RaiseLivesChanged(currentLives);
    }

    /// <summary>
    /// Actualiza la posición de inicio del nivel sin cambiar las vidas.
    /// Llamar cuando el nivel se regenera pero las vidas deben conservarse.
    /// </summary>
    public void SetLevelStartPosition(Vector3 position, Quaternion rotation)
    {
        levelStartPosition = position;
        levelStartRotation = rotation;
    }

    /// <summary>
    /// Resetea las vidas al máximo. Llamar al avanzar de nivel.
    /// </summary>
    public void ResetLives()
    {
        currentLives = maxLives;
        GameEvents.RaiseLivesChanged(currentLives);
    }

    /// <summary>
    /// Añade una vida si no se ha alcanzado el máximo.
    /// Llamado por SpawnPointTrigger al pasar por un checkpoint no visitado.
    /// </summary>
    public void AddLife()
    {
        if (currentLives >= maxLives)
        {
            return;
        }

        currentLives++;
        GameEvents.RaiseLivesChanged(currentLives);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Ejecuta ANTES que BallRespawnController gracias a DefaultExecutionOrder(-40).
    /// La corutina de respawn de BallRespawnController ya está iniciada pero no ha
    /// teleportado al jugador aún (respawnDelay = 0.2s). Este método sincroniza el
    /// punto de respawn correcto antes de que ocurra el teleport.
    /// </summary>
    private void HandlePlayerDied()
    {
        if (currentLives > 0)
        {
            // Pierde una vida — el respawn ocurre en el último spawn point activado.
            currentLives--;
            GameEvents.RaiseLivesChanged(currentLives);
            return;
        }

        // Sin vidas → respawn al inicio del nivel con 0 vidas.
        // Sobreescribir el punto de respawn ANTES de que la corutina lea la posición.
        if (respawnController != null)
        {
            respawnController.SetRespawnPosition(levelStartPosition, levelStartRotation);
        }

        // Notificar a todos los sistemas (SpawnPointManager, etc.) para que se reseteen.
        GameEvents.RaiseLevelReset();
    }

    #endregion
}