using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton global que gestiona el idioma activo del juego.
/// Persiste entre escenas (DontDestroyOnLoad) y notifica a todos
/// los sistemas suscritos cuando el idioma cambia.
/// 
/// Colocar este componente en un GameObject en la escena de menú principal.
/// No moverlo a la escena de intro; el sistema de persistencia maneja
/// la sincronización correcta al llegar a la escena que lo contiene.
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────

    /// <summary>Instancia global del LocalizationManager.</summary>
    public static LocalizationManager Instance { get; private set; }

    // ─── Eventos ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Se dispara cada vez que el idioma cambia.
    /// Los sistemas que muestran texto deben suscribirse aquí
    /// en lugar de consultar el idioma en cada Update.
    /// </summary>
    public static event Action<Language> OnLanguageChanged;

    // ─── Inspector ────────────────────────────────────────────────────────────

    [Tooltip("Idioma que se usa si el usuario nunca ha seleccionado uno.")]
    [SerializeField] private Language defaultLanguage = Language.Spanish;

    [Tooltip("Lista de todos los LanguageData disponibles. Debe incluir uno por cada valor del enum Language.")]
    [SerializeField] private List<LanguageData> availableLanguages = new();

    // ─── Estado ───────────────────────────────────────────────────────────────

    private const string PrefKey = "SelectedLanguage";

    /// <summary>Idioma actualmente activo.</summary>
    public Language CurrentLanguage { get; private set; }

    private LanguageData _activeData;
    private readonly Dictionary<Language, LanguageData> _dataMap = new();

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (!InitializeSingleton()) return;
        BuildDataMap();
        ApplyLanguage(LoadSavedLanguage(), silent: true);
    }

    // ─── Pública API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Cambia el idioma del juego, guarda la preferencia y notifica
    /// a todos los sistemas suscritos.
    /// </summary>
    /// <param name="language">Idioma destino.</param>
    public void SetLanguage(Language language)
    {
        if (language == CurrentLanguage) return;
        ApplyLanguage(language, silent: false);
    }

    /// <summary>
    /// Devuelve el texto localizado para la clave dada en el idioma activo.
    /// </summary>
    /// <param name="key">Clave de localización (ej: "ui.menu.play").</param>
    public string GetText(string key)
    {
        if (_activeData == null)
        {
            Debug.LogError("[LocalizationManager] No hay datos de idioma cargados.");
            return $"[{key}]";
        }
        return _activeData.GetText(key);
    }

    /// <summary>
    /// Devuelve el texto localizado con formateo de string (string.Format).
    /// Útil para textos dinámicos: GetTextFormatted("ui.score", 42) → "Puntos: 42".
    /// </summary>
    /// <param name="key">Clave de localización.</param>
    /// <param name="args">Argumentos para string.Format.</param>
    public string GetTextFormatted(string key, params object[] args)
    {
        string template = GetText(key);
        try
        {
            return string.Format(template, args);
        }
        catch (FormatException e)
        {
            Debug.LogWarning($"[LocalizationManager] Error de formato en clave '{key}': {e.Message}");
            return template;
        }
    }

    // ─── Privado ──────────────────────────────────────────────────────────────

    private bool InitializeSingleton()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return false;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        return true;
    }

    private void BuildDataMap()
    {
        _dataMap.Clear();
        foreach (var data in availableLanguages)
        {
            if (data == null) continue;
            if (!_dataMap.TryAdd(data.Language, data))
            {
                Debug.LogWarning($"[LocalizationManager] LanguageData duplicado para '{data.Language}'. Se ignora.", data);
            }
        }
    }

    private Language LoadSavedLanguage()
    {
        int savedIndex = PlayerPrefs.GetInt(PrefKey, (int)defaultLanguage);

        // Valida que el valor guardado sea un Language válido,
        // por si acaso se eliminaron idiomas del enum entre versiones.
        if (Enum.IsDefined(typeof(Language), savedIndex))
            return (Language)savedIndex;

        Debug.LogWarning($"[LocalizationManager] Idioma guardado inválido ({savedIndex}). Usando idioma por defecto.");
        return defaultLanguage;
    }

    private void ApplyLanguage(Language language, bool silent)
    {
        if (!_dataMap.TryGetValue(language, out LanguageData data))
        {
            Debug.LogError($"[LocalizationManager] No se encontró LanguageData para '{language}'. Verifica la lista en el Inspector.");
            return;
        }

        CurrentLanguage = language;
        _activeData     = data;

        PlayerPrefs.SetInt(PrefKey, (int)language);
        PlayerPrefs.Save();

        if (!silent)
            OnLanguageChanged?.Invoke(CurrentLanguage);
    }
}