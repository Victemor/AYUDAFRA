using UnityEngine;

/// <summary>
/// Componente encargado de configurar y disparar la música de una escena
/// desde el Inspector.
///
/// Responsabilidades:
/// - Solicitar al AudioManager la reproducción de una pista musical.
/// - Permitir decidir si la reproducción usa transición o no.
/// - Permitir decidir si la detención usa transición o no.
/// - Exponer una configuración simple y reutilizable para diseño.
///
/// Decisiones de diseño:
/// - Este componente solo orquesta intención de escena; no contiene lógica de audio.
/// - La validación operativa queda delegada al AudioManager.
/// - Se trabaja con una única pista de música global activa a la vez.
/// </summary>
public sealed class SceneMusicPlayer : MonoBehaviour
{
    #region Enums

    /// <summary>
    /// Define el modo de reproducción de la música.
    /// </summary>
    private enum PlayMode
    {
        NoTransition = 0,
        TransitionDefaultDuration = 1,
        TransitionCustomDuration = 2
    }

    /// <summary>
    /// Define el modo de detención de la música.
    /// </summary>
    private enum StopMode
    {
        NoTransition = 0,
        TransitionDefaultDuration = 1,
        TransitionCustomDuration = 2
    }

    #endregion

    #region Inspector

    [Header("Music")]

    [Tooltip("Identificador de la pista musical que este componente solicitará reproducir.")]
    [SerializeField] private SoundId musicId = SoundId.BackgroundMusic;

    [Tooltip("Multiplicador aplicado sobre el volumen base configurado en el AudioClipConfig.")]
    [Range(0f, 1f)]
    [SerializeField] private float volumeMultiplier = 1f;

    [Header("Play Settings")]

    [Tooltip("Si está activo, la música se reproducirá automáticamente al iniciar.")]
    [SerializeField] private bool playOnStart = true;

    [Tooltip("Modo de reproducción de la música.")]
    [SerializeField] private PlayMode playMode = PlayMode.TransitionDefaultDuration;

    [Tooltip("Si está activo, reinicia la pista aunque ya sea la misma que está sonando.")]
    [SerializeField] private bool restartIfAlreadyPlaying = false;

    [Tooltip("Duración personalizada del fade de entrada cuando el modo seleccionado lo requiera.")]
    [Min(0f)]
    [SerializeField] private float customPlayFadeDuration = 1f;

    [Header("Stop Settings")]

    [Tooltip("Si está activo, intentará detener la música cuando este objeto se destruya.")]
    [SerializeField] private bool stopOnDestroy = false;

    [Tooltip("Modo de detención de la música.")]
    [SerializeField] private StopMode stopMode = StopMode.TransitionDefaultDuration;

    [Tooltip("Duración personalizada del fade de salida cuando el modo seleccionado lo requiera.")]
    [Min(0f)]
    [SerializeField] private float customStopFadeDuration = 1f;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Ejecuta la reproducción automática si fue habilitada.
    /// </summary>
    private void Start()
    {
        if (!playOnStart)
            return;

        PlayConfiguredMusic();
    }

    /// <summary>
    /// Intenta detener la música configurada al destruirse el objeto,
    /// si la opción correspondiente fue habilitada.
    /// </summary>
    private void OnDestroy()
    {
        if (!stopOnDestroy)
            return;

        StopConfiguredMusic();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Reproduce la música configurada usando el modo definido en Inspector.
    /// </summary>
    public void PlayConfiguredMusic()
    {
        switch (playMode)
        {
            case PlayMode.NoTransition:
                AudioManager.PlayMusic(musicId, restartIfAlreadyPlaying, volumeMultiplier);
                break;

            case PlayMode.TransitionDefaultDuration:
                AudioManager.PlayMusicWithFade(musicId, restartIfAlreadyPlaying, -1f, volumeMultiplier);
                break;

            case PlayMode.TransitionCustomDuration:
                AudioManager.PlayMusicWithFade(
                    musicId,
                    restartIfAlreadyPlaying,
                    GetSafeDuration(customPlayFadeDuration),
                    volumeMultiplier);
                break;
        }
    }

    /// <summary>
    /// Detiene la música configurada usando el modo definido en Inspector.
    /// </summary>
    public void StopConfiguredMusic()
    {
        switch (stopMode)
        {
            case StopMode.NoTransition:
                AudioManager.StopMusic();
                break;

            case StopMode.TransitionDefaultDuration:
                AudioManager.StopMusicWithFade();
                break;

            case StopMode.TransitionCustomDuration:
                AudioManager.StopMusicWithFade(GetSafeDuration(customStopFadeDuration));
                break;
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Garantiza una duración no negativa para operaciones de fade.
    /// </summary>
    /// <param name="duration">Duración solicitada.</param>
    /// <returns>Duración validada.</returns>
    private float GetSafeDuration(float duration)
    {
        return Mathf.Max(0f, duration);
    }

    #endregion
}