using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema central de audio del juego.
///
/// Responsabilidades:
/// - Reproducir música usando una fuente dedicada.
/// - Reproducir efectos cortos usando un pool reutilizable de AudioSources.
/// - Reproducir loops ambientales persistentes con una fuente dedicada por SoundId.
/// - Resolver configuraciones desde un catálogo central tipado por categoría.
/// - Permitir control de volumen, reproducción y detención con o sin fade.
/// - Evitar reinicios innecesarios en loops y música cuando ya están activos.
///
/// Decisiones de diseño:
/// - Music, Sfx y LoopSfx se manejan por canales lógicos distintos porque
///   tienen necesidades operativas diferentes.
/// - Los loops persistentes no usan el pool de SFX para evitar reutilización accidental,
///   facilitar fades dedicados y permitir coexistencia simultánea.
/// - Se usa un singleton pragmático porque el audio es un sistema global del juego.
/// </summary>
public sealed class AudioManager : MonoBehaviour
{
    #region Singleton

    /// <summary>
    /// Instancia global del sistema de audio.
    /// </summary>
    public static AudioManager Instance { get; private set; }

    #endregion

    #region Inspector

    [Header("Catalog")]

    [Tooltip("Catálogo central que separa música, SFX cortos y loops persistentes.")]
    [SerializeField] private AudioCatalog catalog;

    [Header("SFX Pool")]

    [Tooltip("Cantidad de AudioSources reutilizables para efectos cortos.")]
    [Min(1)]
    [SerializeField] private int sfxPoolSize = 10;

    [Header("Music")]

    [Tooltip("Duración de fade por defecto para la música.")]
    [Min(0f)]
    [SerializeField] private float defaultMusicFadeDuration = 1f;

    [Header("Loop SFX")]

    [Tooltip("Duración de fade por defecto para loops ambientales.")]
    [Min(0f)]
    [SerializeField] private float defaultLoopFadeDuration = 1f;

    #endregion

    #region Private Fields

    /// <summary>
    /// Pool circular de fuentes para efectos cortos.
    /// </summary>
    private readonly List<AudioSource> sfxPool = new List<AudioSource>();

    /// <summary>
    /// Asociación entre identificadores de SFX y sus fuentes activas.
    /// </summary>
    private readonly Dictionary<SoundId, List<AudioSource>> activeSfxById = new Dictionary<SoundId, List<AudioSource>>();

    /// <summary>
    /// Índice actual del pool circular de SFX.
    /// </summary>
    private int currentSfxIndex;

    /// <summary>
    /// Fuente dedicada exclusivamente a música.
    /// </summary>
    private AudioSource musicSource;

    /// <summary>
    /// Identificador de la música actualmente activa.
    /// </summary>
    private SoundId? currentMusicId;

    /// <summary>
    /// Rutina de fade activa para música.
    /// </summary>
    private Coroutine musicFadeCoroutine;

    /// <summary>
    /// Fuentes dedicadas a loops persistentes por SoundId.
    /// </summary>
    private readonly Dictionary<SoundId, AudioSource> loopSourcesById = new Dictionary<SoundId, AudioSource>();

    /// <summary>
    /// Rutinas de fade activas para cada loop persistente.
    /// </summary>
    private readonly Dictionary<SoundId, Coroutine> loopFadeCoroutines = new Dictionary<SoundId, Coroutine>();

    #endregion

    #region Properties

    /// <summary>
    /// Indica si hay música reproduciéndose actualmente.
    /// </summary>
    public bool IsMusicPlaying => musicSource != null && musicSource.isPlaying;

    /// <summary>
    /// Id de la música activa, si existe.
    /// </summary>
    public SoundId? CurrentMusicId => currentMusicId;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Inicializa el singleton y prepara las fuentes base del sistema.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeSfxPool();
        InitializeMusicSource();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Crea el pool reutilizable para efectos cortos.
    /// </summary>
    private void InitializeSfxPool()
    {
        sfxPool.Clear();

        for (int i = 0; i < sfxPoolSize; i++)
        {
            GameObject sourceObject = new GameObject($"SfxSource_{i}");
            sourceObject.transform.SetParent(transform);

            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;

            sfxPool.Add(source);
        }
    }

    /// <summary>
    /// Crea la fuente dedicada para música.
    /// </summary>
    private void InitializeMusicSource()
    {
        GameObject sourceObject = new GameObject("MusicSource");
        sourceObject.transform.SetParent(transform);

        musicSource = sourceObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
    }

    #endregion

    #region Static API

    /// <summary>
    /// Reproduce un efecto corto de forma segura.
    /// </summary>
    public static void PlaySfx(SoundId id, float volumeMultiplier = 1f)
    {
        if (Instance == null)
            return;

        Instance.PlaySfxInternal(id, volumeMultiplier);
    }

    /// <summary>
    /// Detiene todos los efectos cortos asociados a un identificador.
    /// </summary>
    public static void StopSfx(SoundId id)
    {
        if (Instance == null)
            return;

        Instance.StopSfxInternal(id);
    }

    /// <summary>
    /// Detiene todos los SFX cortos.
    /// </summary>
    public static void StopAllSfx()
    {
        if (Instance == null)
            return;

        Instance.StopAllSfxInternal();
    }

    /// <summary>
    /// Reproduce música sin fade.
    /// </summary>
    public static void PlayMusic(SoundId id, bool restartIfAlreadyPlaying = false, float volumeMultiplier = 1f)
    {
        if (Instance == null)
            return;

        Instance.PlayMusicInternal(id, restartIfAlreadyPlaying, volumeMultiplier);
    }

    /// <summary>
    /// Reproduce música con fade.
    /// </summary>
    public static void PlayMusicWithFade(SoundId id, bool restartIfAlreadyPlaying = false, float duration = -1f, float volumeMultiplier = 1f)
    {
        if (Instance == null)
            return;

        Instance.PlayMusicWithFadeInternal(id, restartIfAlreadyPlaying, duration, volumeMultiplier);
    }

    /// <summary>
    /// Detiene la música sin fade.
    /// </summary>
    public static void StopMusic()
    {
        if (Instance == null)
            return;

        Instance.StopMusicInternal();
    }

    /// <summary>
    /// Detiene la música con fade.
    /// </summary>
    public static void StopMusicWithFade(float duration = -1f)
    {
        if (Instance == null)
            return;

        Instance.StopMusicWithFadeInternal(duration);
    }

    /// <summary>
    /// Reproduce un loop persistente sin fade.
    /// </summary>
    public static void PlayLoop(SoundId id, float volumeMultiplier = 1f, bool restartIfAlreadyPlaying = false)
    {
        if (Instance == null)
            return;

        Instance.PlayLoopInternal(id, volumeMultiplier, restartIfAlreadyPlaying);
    }

    /// <summary>
    /// Reproduce un loop persistente con fade.
    /// </summary>
    public static void PlayLoopWithFade(SoundId id, float duration = -1f, float volumeMultiplier = 1f, bool restartIfAlreadyPlaying = false)
    {
        if (Instance == null)
            return;

        Instance.PlayLoopWithFadeInternal(id, duration, volumeMultiplier, restartIfAlreadyPlaying);
    }

    /// <summary>
    /// Detiene un loop persistente sin fade.
    /// </summary>
    public static void StopLoop(SoundId id)
    {
        if (Instance == null)
            return;

        Instance.StopLoopInternal(id);
    }

    /// <summary>
    /// Detiene un loop persistente con fade.
    /// </summary>
    public static void StopLoopWithFade(SoundId id, float duration = -1f)
    {
        if (Instance == null)
            return;

        Instance.StopLoopWithFadeInternal(id, duration);
    }

    /// <summary>
    /// Ajusta el volumen de un loop persistente activo sin reiniciarlo.
    /// </summary>
    public static void SetLoopVolume(SoundId id, float volumeMultiplier)
    {
        if (Instance == null)
            return;

        Instance.SetLoopVolumeInternal(id, volumeMultiplier);
    }

    /// <summary>
    /// Detiene todos los loops persistentes.
    /// </summary>
    public static void StopAllLoops()
    {
        if (Instance == null)
            return;

        Instance.StopAllLoopsInternal();
    }

    /// <summary>
    /// Detiene todo el audio del sistema.
    /// </summary>
    public static void StopAll()
    {
        if (Instance == null)
            return;

        Instance.StopAllInternal();
    }

    #endregion

    #region SFX

    /// <summary>
    /// Reproduce un efecto corto usando una fuente del pool.
    /// </summary>
    private void PlaySfxInternal(SoundId id, float volumeMultiplier)
    {
        AudioClipConfig config = catalog.GetSfx(id);
        if (config == null)
            return;

        AudioClip clip = config.GetRandomClip();
        if (clip == null)
            return;

        AudioSource source = GetNextSfxSource();

        UnregisterSfxSourceFromAllIds(source);

        source.Stop();
        source.clip = clip;
        source.volume = config.Volume * Mathf.Clamp01(volumeMultiplier);
        source.pitch = Random.Range(config.PitchRange.x, config.PitchRange.y);
        source.loop = false;
        source.Play();

        RegisterActiveSfxSource(id, source);
    }

    /// <summary>
    /// Detiene todos los SFX de un SoundId específico.
    /// </summary>
    private void StopSfxInternal(SoundId id)
    {
        if (!activeSfxById.TryGetValue(id, out List<AudioSource> sources))
            return;

        for (int i = sources.Count - 1; i >= 0; i--)
        {
            AudioSource source = sources[i];
            if (source == null)
            {
                sources.RemoveAt(i);
                continue;
            }

            source.Stop();
            source.clip = null;
            sources.RemoveAt(i);
        }

        if (sources.Count == 0)
            activeSfxById.Remove(id);
    }

    /// <summary>
    /// Detiene todos los SFX cortos activos.
    /// </summary>
    private void StopAllSfxInternal()
    {
        for (int i = 0; i < sfxPool.Count; i++)
        {
            AudioSource source = sfxPool[i];
            if (source == null)
                continue;

            source.Stop();
            source.clip = null;
        }

        activeSfxById.Clear();
    }

    #endregion

    #region Music

    /// <summary>
    /// Reproduce una pista musical sin fade.
    /// </summary>
    private void PlayMusicInternal(SoundId id, bool restartIfAlreadyPlaying, float volumeMultiplier)
    {
        AudioClipConfig config = catalog.GetMusic(id);
        if (config == null)
            return;

        AudioClip clip = config.GetRandomClip();
        if (clip == null)
            return;

        float targetVolume = config.Volume * Mathf.Clamp01(volumeMultiplier);

        bool isSameMusic = currentMusicId.HasValue &&
                           currentMusicId.Value.Equals(id) &&
                           musicSource.isPlaying &&
                           musicSource.clip == clip;

        StopMusicFadeRoutine();

        if (isSameMusic && !restartIfAlreadyPlaying)
        {
            musicSource.volume = targetVolume;
            return;
        }

        currentMusicId = id;
        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.pitch = 1f;
        musicSource.volume = targetVolume;
        musicSource.Play();
    }

    /// <summary>
    /// Reproduce una pista musical con fade.
    /// </summary>
    private void PlayMusicWithFadeInternal(SoundId id, bool restartIfAlreadyPlaying, float duration, float volumeMultiplier)
    {
        AudioClipConfig config = catalog.GetMusic(id);
        if (config == null)
            return;

        AudioClip clip = config.GetRandomClip();
        if (clip == null)
            return;

        float resolvedDuration = duration < 0f ? defaultMusicFadeDuration : duration;
        float targetVolume = config.Volume * Mathf.Clamp01(volumeMultiplier);

        bool isSameMusic = currentMusicId.HasValue &&
                           currentMusicId.Value.Equals(id) &&
                           musicSource.isPlaying &&
                           musicSource.clip == clip;

        StopMusicFadeRoutine();

        if (isSameMusic && !restartIfAlreadyPlaying)
        {
            if (resolvedDuration <= 0f)
            {
                musicSource.volume = targetVolume;
                return;
            }

            musicFadeCoroutine = StartCoroutine(FadeAudioSourceVolumeCoroutine(musicSource, targetVolume, resolvedDuration, null));
            return;
        }

        musicFadeCoroutine = StartCoroutine(FadeInMusicCoroutine(id, clip, targetVolume, resolvedDuration));
    }

    /// <summary>
    /// Detiene la música activa inmediatamente.
    /// </summary>
    private void StopMusicInternal()
    {
        StopMusicFadeRoutine();

        if (musicSource == null)
            return;

        musicSource.Stop();
        musicSource.clip = null;
        musicSource.volume = 0f;
        currentMusicId = null;
    }

    /// <summary>
    /// Detiene la música activa con fade.
    /// </summary>
    private void StopMusicWithFadeInternal(float duration)
    {
        if (musicSource == null || !musicSource.isPlaying)
            return;

        float resolvedDuration = duration < 0f ? defaultMusicFadeDuration : duration;

        StopMusicFadeRoutine();

        if (resolvedDuration <= 0f)
        {
            StopMusicInternal();
            return;
        }

        musicFadeCoroutine = StartCoroutine(FadeOutMusicCoroutine(resolvedDuration));
    }

    #endregion

    #region Loop SFX

    /// <summary>
    /// Reproduce un loop persistente sin fade.
    /// </summary>
    private void PlayLoopInternal(SoundId id, float volumeMultiplier, bool restartIfAlreadyPlaying)
    {
        AudioClipConfig config = catalog.GetLoop(id);
        if (config == null)
            return;

        AudioClip clip = config.GetRandomClip();
        if (clip == null)
            return;

        AudioSource source = GetOrCreateLoopSource(id);
        float targetVolume = config.Volume * Mathf.Clamp01(volumeMultiplier);

        StopLoopFadeRoutine(id);

        bool isSameLoop = source.isPlaying && source.clip == clip;

        if (isSameLoop && !restartIfAlreadyPlaying)
        {
            source.volume = targetVolume;
            return;
        }

        source.Stop();
        source.clip = clip;
        source.loop = true;
        source.pitch = 1f;
        source.volume = targetVolume;
        source.Play();
    }

    /// <summary>
    /// Reproduce un loop persistente con fade.
    /// </summary>
    private void PlayLoopWithFadeInternal(SoundId id, float duration, float volumeMultiplier, bool restartIfAlreadyPlaying)
    {
        AudioClipConfig config = catalog.GetLoop(id);
        if (config == null)
            return;

        AudioClip clip = config.GetRandomClip();
        if (clip == null)
            return;

        AudioSource source = GetOrCreateLoopSource(id);
        float resolvedDuration = duration < 0f ? defaultLoopFadeDuration : duration;
        float targetVolume = config.Volume * Mathf.Clamp01(volumeMultiplier);

        StopLoopFadeRoutine(id);

        bool isSameLoop = source.isPlaying && source.clip == clip;

        if (isSameLoop && !restartIfAlreadyPlaying)
        {
            if (resolvedDuration <= 0f)
            {
                source.volume = targetVolume;
                return;
            }

            Coroutine routine = StartCoroutine(FadeLoopVolumeCoroutine(id, targetVolume, resolvedDuration));
            loopFadeCoroutines[id] = routine;
            return;
        }

        source.Stop();
        source.clip = clip;
        source.loop = true;
        source.pitch = 1f;
        source.volume = 0f;
        source.Play();

        if (resolvedDuration <= 0f)
        {
            source.volume = targetVolume;
            return;
        }

        Coroutine fadeRoutine = StartCoroutine(FadeLoopVolumeCoroutine(id, targetVolume, resolvedDuration));
        loopFadeCoroutines[id] = fadeRoutine;
    }

    /// <summary>
    /// Detiene un loop persistente inmediatamente.
    /// </summary>
    private void StopLoopInternal(SoundId id)
    {
        if (!loopSourcesById.TryGetValue(id, out AudioSource source) || source == null)
            return;

        StopLoopFadeRoutine(id);

        source.Stop();
        source.clip = null;
        source.volume = 0f;
    }

    /// <summary>
    /// Detiene un loop persistente con fade.
    /// </summary>
    private void StopLoopWithFadeInternal(SoundId id, float duration)
    {
        if (!loopSourcesById.TryGetValue(id, out AudioSource source) || source == null || !source.isPlaying)
            return;

        float resolvedDuration = duration < 0f ? defaultLoopFadeDuration : duration;

        StopLoopFadeRoutine(id);

        if (resolvedDuration <= 0f)
        {
            StopLoopInternal(id);
            return;
        }

        Coroutine routine = StartCoroutine(FadeOutLoopCoroutine(id, resolvedDuration));
        loopFadeCoroutines[id] = routine;
    }

    /// <summary>
    /// Ajusta el volumen de un loop persistente activo.
    /// </summary>
    private void SetLoopVolumeInternal(SoundId id, float volumeMultiplier)
    {
        if (!loopSourcesById.TryGetValue(id, out AudioSource source) || source == null)
            return;

        AudioClipConfig config = catalog.GetLoop(id);
        if (config == null)
            return;

        source.volume = config.Volume * Mathf.Clamp01(volumeMultiplier);
    }

    /// <summary>
    /// Detiene todos los loops persistentes.
    /// </summary>
    private void StopAllLoopsInternal()
    {
        foreach (KeyValuePair<SoundId, AudioSource> pair in loopSourcesById)
        {
            StopLoopFadeRoutine(pair.Key);

            if (pair.Value == null)
                continue;

            pair.Value.Stop();
            pair.Value.clip = null;
            pair.Value.volume = 0f;
        }
    }

    #endregion

    #region Global Control

    /// <summary>
    /// Detiene todo el audio administrado por el sistema.
    /// </summary>
    private void StopAllInternal()
    {
        StopAllSfxInternal();
        StopMusicInternal();
        StopAllLoopsInternal();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Obtiene la siguiente fuente del pool de SFX usando recorrido circular.
    /// </summary>
    private AudioSource GetNextSfxSource()
    {
        AudioSource source = sfxPool[currentSfxIndex];
        currentSfxIndex = (currentSfxIndex + 1) % sfxPool.Count;
        return source;
    }

    /// <summary>
    /// Obtiene o crea la fuente dedicada para un loop persistente.
    /// </summary>
    private AudioSource GetOrCreateLoopSource(SoundId id)
    {
        if (loopSourcesById.TryGetValue(id, out AudioSource existingSource) && existingSource != null)
            return existingSource;

        GameObject sourceObject = new GameObject($"LoopSource_{id}");
        sourceObject.transform.SetParent(transform);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;

        loopSourcesById[id] = source;
        return source;
    }

    /// <summary>
    /// Registra una fuente activa para un SFX corto.
    /// </summary>
    private void RegisterActiveSfxSource(SoundId id, AudioSource source)
    {
        if (!activeSfxById.TryGetValue(id, out List<AudioSource> sources))
        {
            sources = new List<AudioSource>();
            activeSfxById.Add(id, sources);
        }

        if (!sources.Contains(source))
            sources.Add(source);
    }

    /// <summary>
    /// Elimina una fuente del registro de todos los SFX activos.
    /// </summary>
    private void UnregisterSfxSourceFromAllIds(AudioSource source)
    {
        foreach (KeyValuePair<SoundId, List<AudioSource>> pair in activeSfxById)
        {
            pair.Value.Remove(source);
        }

        CleanupEmptySfxLists();
    }

    /// <summary>
    /// Limpia entradas vacías del diccionario de SFX activos.
    /// </summary>
    private void CleanupEmptySfxLists()
    {
        List<SoundId> keysToRemove = null;

        foreach (KeyValuePair<SoundId, List<AudioSource>> pair in activeSfxById)
        {
            if (pair.Value.Count > 0)
                continue;

            if (keysToRemove == null)
                keysToRemove = new List<SoundId>();

            keysToRemove.Add(pair.Key);
        }

        if (keysToRemove == null)
            return;

        for (int i = 0; i < keysToRemove.Count; i++)
        {
            activeSfxById.Remove(keysToRemove[i]);
        }
    }

    /// <summary>
    /// Detiene la rutina activa de fade de música.
    /// </summary>
    private void StopMusicFadeRoutine()
    {
        if (musicFadeCoroutine == null)
            return;

        StopCoroutine(musicFadeCoroutine);
        musicFadeCoroutine = null;
    }

    /// <summary>
    /// Detiene la rutina activa de fade de un loop persistente.
    /// </summary>
    private void StopLoopFadeRoutine(SoundId id)
    {
        if (!loopFadeCoroutines.TryGetValue(id, out Coroutine routine) || routine == null)
            return;

        StopCoroutine(routine);
        loopFadeCoroutines.Remove(id);
    }

    #endregion

    #region Coroutines

    /// <summary>
    /// Realiza fade in de una nueva pista musical.
    /// </summary>
    private IEnumerator FadeInMusicCoroutine(SoundId id, AudioClip clip, float targetVolume, float duration)
    {
        currentMusicId = id;

        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.pitch = 1f;
        musicSource.volume = 0f;
        musicSource.Play();

        if (duration <= 0f)
        {
            musicSource.volume = targetVolume;
            musicFadeCoroutine = null;
            yield break;
        }

        yield return FadeAudioSourceVolumeCoroutine(musicSource, targetVolume, duration, null);
        musicFadeCoroutine = null;
    }

    /// <summary>
    /// Realiza fade out de la música actual y la detiene.
    /// </summary>
    private IEnumerator FadeOutMusicCoroutine(float duration)
    {
        yield return FadeAudioSourceVolumeCoroutine(musicSource, 0f, duration, () =>
        {
            musicSource.Stop();
            musicSource.clip = null;
            currentMusicId = null;
        });

        musicFadeCoroutine = null;
    }

    /// <summary>
    /// Realiza un cambio suave de volumen en un loop persistente.
    /// </summary>
    private IEnumerator FadeLoopVolumeCoroutine(SoundId id, float targetVolume, float duration)
    {
        if (!loopSourcesById.TryGetValue(id, out AudioSource source) || source == null)
        {
            loopFadeCoroutines.Remove(id);
            yield break;
        }

        yield return FadeAudioSourceVolumeCoroutine(source, targetVolume, duration, null);
        loopFadeCoroutines.Remove(id);
    }

    /// <summary>
    /// Realiza fade out de un loop persistente y lo detiene.
    /// </summary>
    private IEnumerator FadeOutLoopCoroutine(SoundId id, float duration)
    {
        if (!loopSourcesById.TryGetValue(id, out AudioSource source) || source == null)
        {
            loopFadeCoroutines.Remove(id);
            yield break;
        }

        yield return FadeAudioSourceVolumeCoroutine(source, 0f, duration, () =>
        {
            source.Stop();
            source.clip = null;
        });

        loopFadeCoroutines.Remove(id);
    }

    /// <summary>
    /// Interpola suavemente el volumen de una fuente hasta un valor objetivo.
    /// </summary>
    private IEnumerator FadeAudioSourceVolumeCoroutine(AudioSource source, float targetVolume, float duration, System.Action onCompleted)
    {
        if (source == null)
            yield break;

        float startVolume = source.volume;

        if (duration <= 0f)
        {
            source.volume = targetVolume;
            onCompleted?.Invoke();
            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / duration);
            source.volume = Mathf.Lerp(startVolume, targetVolume, normalizedTime);
            yield return null;
        }

        source.volume = targetVolume;
        onCompleted?.Invoke();
    }

    #endregion
}