using UnityEngine;

/// <summary>
/// Configuración reusable de un sonido.
///
/// Responsabilidades:
/// - Contener uno o varios clips candidatos para reproducción.
/// - Definir volumen base y variación de pitch.
/// - Indicar si el sonido fue concebido como loop.
///
/// Decisiones de diseño:
/// - El comportamiento de canal sigue estando controlado por AudioManager.
/// - La propiedad Loop describe la naturaleza del asset, pero el manager
///   sigue teniendo la última palabra según la categoría operativa.
/// - Se encapsula la selección aleatoria y la obtención del pitch para
///   centralizar la lógica y mantener consistencia entre llamadas.
/// </summary>
[CreateAssetMenu(menuName = "Audio/Audio Clip Config")]
public sealed class AudioClipConfig : ScriptableObject
{
    #region Inspector

    [Header("Clips")]

    [Tooltip("Lista de clips posibles. Si hay más de uno, se elige uno aleatoriamente.")]
    [SerializeField] private AudioClip[] clips;

    [Header("Playback Settings")]

    [Tooltip("Volumen base del sonido.")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;

    [Tooltip("Rango de pitch aleatorio aplicado al reproducir.")]
    [SerializeField] private Vector2 pitchRange = new Vector2(1f, 1f);

    [Tooltip("Indica si este asset fue pensado como loop continuo.")]
    [SerializeField] private bool loop;

    #endregion

    #region Properties

    /// <summary>
    /// Volumen base del sonido.
    /// </summary>
    public float Volume => Mathf.Clamp01(volume);

    /// <summary>
    /// Rango de pitch configurado.
    /// </summary>
    public Vector2 PitchRange => GetValidatedPitchRange();

    /// <summary>
    /// Indica si este sonido fue concebido como loop.
    /// </summary>
    public bool Loop => loop;

    /// <summary>
    /// Indica si hay al menos un clip válido disponible.
    /// </summary>
    public bool HasClips => clips != null && clips.Length > 0;

    #endregion

    #region Public API

    /// <summary>
    /// Obtiene un clip válido para reproducción.
    /// Si hay varios clips, selecciona uno aleatoriamente.
    /// </summary>
    /// <returns>Clip seleccionado o null si no existe ninguno.</returns>
    public AudioClip GetRandomClip()
    {
        if (!HasClips)
            return null;

        if (clips.Length == 1)
            return clips[0];

        return clips[Random.Range(0, clips.Length)];
    }

    /// <summary>
    /// Obtiene un valor de pitch aleatorio dentro del rango configurado.
    /// </summary>
    /// <returns>Pitch resultante validado.</returns>
    public float GetRandomPitch()
    {
        Vector2 validatedRange = GetValidatedPitchRange();

        if (Mathf.Approximately(validatedRange.x, validatedRange.y))
            return validatedRange.x;

        return Random.Range(validatedRange.x, validatedRange.y);
    }

    #endregion

    #region Validation

    /// <summary>
    /// Devuelve un rango de pitch válido garantizando que el mínimo
    /// no sea mayor que el máximo.
    /// </summary>
    /// <returns>Rango validado.</returns>
    private Vector2 GetValidatedPitchRange()
    {
        float min = Mathf.Min(pitchRange.x, pitchRange.y);
        float max = Mathf.Max(pitchRange.x, pitchRange.y);
        return new Vector2(min, max);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Normaliza valores serializados para evitar configuraciones inválidas.
    /// </summary>
    private void OnValidate()
    {
        volume = Mathf.Clamp01(volume);

        float min = Mathf.Min(pitchRange.x, pitchRange.y);
        float max = Mathf.Max(pitchRange.x, pitchRange.y);
        pitchRange = new Vector2(min, max);
    }
#endif

    #endregion
}