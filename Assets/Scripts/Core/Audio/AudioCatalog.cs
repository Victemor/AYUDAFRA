using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Catálogo central de audio del juego.
///
/// Responsabilidades:
/// - Mapear SoundId a configuraciones de audio según su categoría lógica.
/// - Separar explícitamente música, efectos cortos y loops persistentes.
/// - Proveer búsquedas rápidas mediante diccionarios construidos en runtime.
///
/// Decisiones de diseño:
/// - La separación por categorías evita ambigüedad operacional en el AudioManager.
/// - Un mismo SoundId debe existir solo dentro de la categoría correcta.
/// - Se ignoran duplicados posteriores para mantener comportamiento determinista.
/// </summary>
[CreateAssetMenu(menuName = "Audio/Audio Catalog")]
public sealed class AudioCatalog : ScriptableObject
{
    #region Types

    /// <summary>
    /// Entrada serializable que asocia un SoundId con una configuración de audio.
    /// </summary>
    [System.Serializable]
    private struct Entry
    {
        [Tooltip("Identificador lógico del sonido.")]
        [SerializeField] private SoundId id;

        [Tooltip("Configuración de audio asociada a este identificador.")]
        [SerializeField] private AudioClipConfig config;

        /// <summary>
        /// Identificador de sonido.
        /// </summary>
        public SoundId Id => id;

        /// <summary>
        /// Configuración asociada.
        /// </summary>
        public AudioClipConfig Config => config;
    }

    #endregion

    #region Inspector

    [Header("Music")]

    [Tooltip("Entradas de música general del juego.")]
    [SerializeField] private Entry[] musicEntries;

    [Header("SFX")]

    [Tooltip("Entradas de efectos cortos reproducidos mediante pool.")]
    [SerializeField] private Entry[] sfxEntries;

    [Header("Loop SFX")]

    [Tooltip("Entradas de loops persistentes o ambientes como lluvia, viento y fuego.")]
    [SerializeField] private Entry[] loopEntries;

    #endregion

    #region Private Fields

    /// <summary>
    /// Lookup interno para música.
    /// </summary>
    private Dictionary<SoundId, AudioClipConfig> musicLookup;

    /// <summary>
    /// Lookup interno para efectos cortos.
    /// </summary>
    private Dictionary<SoundId, AudioClipConfig> sfxLookup;

    /// <summary>
    /// Lookup interno para loops persistentes.
    /// </summary>
    private Dictionary<SoundId, AudioClipConfig> loopLookup;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Resetea el estado runtime del catálogo para forzar una reconstrucción segura.
    /// </summary>
    private void OnEnable()
    {
        musicLookup = null;
        sfxLookup = null;
        loopLookup = null;
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Construye los diccionarios internos a partir de las listas serializadas.
    /// </summary>
    public void Initialize()
    {
        musicLookup = BuildLookup(musicEntries);
        sfxLookup = BuildLookup(sfxEntries);
        loopLookup = BuildLookup(loopEntries);
    }

    /// <summary>
    /// Garantiza que los diccionarios internos estén disponibles antes de una consulta.
    /// </summary>
    private void EnsureInitialized()
    {
        if (musicLookup != null && sfxLookup != null && loopLookup != null)
            return;

        Initialize();
    }

    /// <summary>
    /// Construye un diccionario a partir de un arreglo de entradas.
    /// Si hay ids repetidos, conserva la primera aparición.
    /// </summary>
    /// <param name="entries">Entradas origen.</param>
    /// <returns>Diccionario construido.</returns>
    private Dictionary<SoundId, AudioClipConfig> BuildLookup(Entry[] entries)
    {
        Dictionary<SoundId, AudioClipConfig> lookup = new Dictionary<SoundId, AudioClipConfig>();

        if (entries == null)
            return lookup;

        for (int i = 0; i < entries.Length; i++)
        {
            Entry entry = entries[i];

            if (entry.Config == null)
                continue;

            if (lookup.ContainsKey(entry.Id))
                continue;

            lookup.Add(entry.Id, entry.Config);
        }

        return lookup;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Obtiene la configuración asociada a un id de música.
    /// </summary>
    public AudioClipConfig GetMusic(SoundId id)
    {
        EnsureInitialized();
        musicLookup.TryGetValue(id, out AudioClipConfig config);
        return config;
    }

    /// <summary>
    /// Obtiene la configuración asociada a un id de SFX corto.
    /// </summary>
    public AudioClipConfig GetSfx(SoundId id)
    {
        EnsureInitialized();
        sfxLookup.TryGetValue(id, out AudioClipConfig config);
        return config;
    }

    /// <summary>
    /// Obtiene la configuración asociada a un id de loop persistente.
    /// </summary>
    public AudioClipConfig GetLoop(SoundId id)
    {
        EnsureInitialized();
        loopLookup.TryGetValue(id, out AudioClipConfig config);
        return config;
    }

    #endregion
}