using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
// STRUCT: LocalizedConsciousnessEntry
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Representa una entrada del sistema de conciencia que puede re-traducirse.
/// Guarda la CLAVE de localización y los argumentos de formato en lugar del
/// texto ya traducido, permitiendo reconstruir el texto en cualquier idioma
/// incluso después de haberse "escrito" en pantalla.
/// </summary>
[Serializable]
public struct LocalizedConsciousnessEntry
{
    /// <summary>
    /// Clave de localización. Si es null o vacío, se usa <see cref="RawText"/> directamente
    /// (para entradas que no son localizadas, como nombres propios generados dinámicamente).
    /// </summary>
    public string LocalizationKey;

    /// <summary>
    /// Argumentos para string.Format aplicados sobre el texto localizado.
    /// Ejemplo: si el texto es "Fragmento {0} encontrado", args[0] sería el nombre del fragmento.
    /// </summary>
    public object[] FormatArgs;

    /// <summary>
    /// Texto fijo no localizable. Se usa cuando <see cref="LocalizationKey"/> está vacío.
    /// Útil para contenido generado proceduralmente que no existe en las tablas de traducción.
    /// </summary>
    public string RawText;

    /// <summary>
    /// Construye una entrada localizable con argumentos de formato opcionales.
    /// </summary>
    public static LocalizedConsciousnessEntry FromKey(string key, params object[] args)
        => new() { LocalizationKey = key, FormatArgs = args };

    /// <summary>
    /// Construye una entrada con texto fijo, no sujeta a localización.
    /// </summary>
    public static LocalizedConsciousnessEntry FromRaw(string rawText)
        => new() { RawText = rawText };

    /// <summary>
    /// Resuelve el texto final de esta entrada según el idioma activo.
    /// </summary>
    public string Resolve()
    {
        if (!string.IsNullOrEmpty(LocalizationKey))
        {
            return (FormatArgs is { Length: > 0 })
                ? LocalizationManager.Instance.GetTextFormatted(LocalizationKey, FormatArgs)
                : LocalizationManager.Instance.GetText(LocalizationKey);
        }
        return RawText ?? string.Empty;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// MONOBEHAVIOUR: ConsciousnessDisplay
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Gestiona la pantalla/panel del sistema de conciencia.
/// Mantiene un historial de entradas como <see cref="LocalizedConsciousnessEntry"/>
/// en lugar de strings ya traducidos, lo que permite re-renderizar todo el
/// historial correctamente cuando el idioma cambia mid-session.
/// 
/// El panel comienza vacío; es el sistema de acciones quien llama a
/// <see cref="AddEntry"/> para escribir contenido aquí.
/// </summary>
public class ConsciousnessDisplay : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Tooltip("Componente TMP_Text donde se renderiza el texto de conciencia acumulado.")]
    [SerializeField] private TMP_Text consciousnessText;

    [Tooltip("Separador entre entradas. Puede ser '\\n', '\\n\\n', ' • ', etc.")]
    [SerializeField] private string entrySeparator = "\n";

    // ─── Estado ───────────────────────────────────────────────────────────────

    private readonly List<LocalizedConsciousnessEntry> _entries = new();

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    // ─── API Pública ──────────────────────────────────────────────────────────

    /// <summary>
    /// Añade una nueva entrada localizable al panel de conciencia.
    /// Llamar desde el sistema de acciones cuando una condición se cumple.
    /// </summary>
    /// <param name="key">Clave de localización del texto a mostrar.</param>
    /// <param name="args">Argumentos opcionales para string.Format.</param>
    public void AddEntry(string key, params object[] args)
    {
        _entries.Add(LocalizedConsciousnessEntry.FromKey(key, args));
        RenderEntries();
    }

    /// <summary>
    /// Añade texto fijo no localizado (nombres propios, valores numéricos, etc.).
    /// </summary>
    /// <param name="rawText">Texto literal a mostrar sin traducción.</param>
    public void AddRawEntry(string rawText)
    {
        _entries.Add(LocalizedConsciousnessEntry.FromRaw(rawText));
        RenderEntries();
    }

    /// <summary>
    /// Limpia todo el historial del panel de conciencia.
    /// </summary>
    public void ClearEntries()
    {
        _entries.Clear();
        if (consciousnessText != null)
            consciousnessText.text = string.Empty;
    }

    /// <summary>
    /// Devuelve una copia de solo lectura del historial actual.
    /// Útil para guardar el estado en el sistema de save.
    /// </summary>
    public IReadOnlyList<LocalizedConsciousnessEntry> GetEntries() => _entries.AsReadOnly();

    /// <summary>
    /// Restaura el historial desde datos guardados (ej: al cargar una partida).
    /// </summary>
    /// <param name="savedEntries">Lista de entradas a restaurar.</param>
    public void RestoreEntries(IEnumerable<LocalizedConsciousnessEntry> savedEntries)
    {
        _entries.Clear();
        _entries.AddRange(savedEntries);
        RenderEntries();
    }

    // ─── Privado ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Re-renderiza todas las entradas cuando cambia el idioma.
    /// Esto garantiza que incluso texto "ya escrito" aparezca en el nuevo idioma.
    /// </summary>
    private void OnLanguageChanged(Language _) => RenderEntries();

    private void RenderEntries()
    {
        if (consciousnessText == null)
        {
            Debug.LogWarning("[ConsciousnessDisplay] consciousnessText no asignado en el Inspector.");
            return;
        }

        if (_entries.Count == 0)
        {
            consciousnessText.text = string.Empty;
            return;
        }

        // Construye el texto resolviendo cada entrada en el idioma actual.
        // System.Text.StringBuilder evita allocations excesivas en listas largas.
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < _entries.Count; i++)
        {
            sb.Append(_entries[i].Resolve());
            if (i < _entries.Count - 1)
                sb.Append(entrySeparator);
        }

        consciousnessText.text = sb.ToString();
    }
}