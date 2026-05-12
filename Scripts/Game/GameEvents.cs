using System;

/// <summary>
/// Bus global de eventos del juego.
///
/// Responsabilidades:
/// - Centralizar eventos transversales de gameplay.
/// - Desacoplar emisores y receptores.
/// - Evitar referencias directas innecesarias entre sistemas.
///
/// Debe mantenerse liviano y orientado a eventos verdaderamente globales.
/// </summary>
public static class GameEvents
{
    #region Collectibles

    /// <summary>
    /// Evento disparado cuando se recolecta un collectible.
    /// </summary>
    public static event Action<CollectibleCollectedData> OnCollectibleCollected;

    /// <summary>
    /// Dispara el evento global de recolección de collectible.
    /// </summary>
    public static void RaiseCollectibleCollected(CollectibleCollectedData data)
    {
        OnCollectibleCollected?.Invoke(data);
    }

    #endregion

    #region Player Lifecycle

    /// <summary>
    /// Evento disparado cuando el jugador muere.
    /// </summary>
    public static event Action OnPlayerDied;

    /// <summary>
    /// Evento disparado cuando el jugador reaparece.
    /// </summary>
    public static event Action OnPlayerRespawned;

    /// <summary>
    /// Evento disparado cuando el jugador alcanza la meta.
    /// </summary>
    public static event Action OnGoalReached;

    /// <summary>
    /// Dispara el evento global de muerte del jugador.
    /// </summary>
    public static void RaisePlayerDied()
    {
        OnPlayerDied?.Invoke();
    }

    /// <summary>
    /// Dispara el evento global de respawn del jugador.
    /// </summary>
    public static void RaisePlayerRespawned()
    {
        OnPlayerRespawned?.Invoke();
    }

    /// <summary>
    /// Dispara el evento global de llegada a meta.
    /// </summary>
    public static void RaiseGoalReached()
    {
        OnGoalReached?.Invoke();
    }

    #endregion

    #region Lives

    /// <summary>
    /// Evento disparado cuando cambia la cantidad de vidas del jugador.
    /// Parámetro: número de vidas actuales.
    /// </summary>
    public static event Action<int> OnLivesChanged;

    /// <summary>
    /// Dispara el evento global de cambio de vidas.
    /// </summary>
    public static void RaiseLivesChanged(int currentLives)
    {
        OnLivesChanged?.Invoke(currentLives);
    }

    #endregion

    #region Level Reset

    /// <summary>
    /// Evento disparado cuando el jugador pierde todas las vidas y se reinicia al inicio del nivel.
    /// Notifica a sistemas como SpawnPointManager para que reseteen su estado.
    /// </summary>
    public static event Action OnLevelReset;

    /// <summary>
    /// Dispara el evento global de reinicio al inicio del nivel actual.
    /// </summary>
    public static void RaiseLevelReset()
    {
        OnLevelReset?.Invoke();
    }

    #endregion
}