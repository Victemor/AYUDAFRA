using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

// ═══════════════════════════════════════════════════════════════════════════════
// STRUCT: LocalizedConsciousnessEntry
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Representa una entrada del sistema de conciencia que puede re-traducirse.
/// Guarda la referencia de localización (tableName + key) o texto raw,
/// permitiendo reconstruir el texto en cualquier idioma en cualquier momento.
///
/// Migración desde LocalizationManager:
/// La resolución ya no puede ser síncrona porque Unity Localization es async.
/// El texto se resuelve via <see cref="LocalizedString.GetLocalizedStringAsync"/>
/// dentro de <see cref="ConsciousnessDisplay.RenderEntriesAsync"/>.
/// </summary>
[Serializable]
public struct LocalizedConsciousnessEntry
{
    /// <summary>
    /// Nombre de la tabla de localización (ej. "Tabla 1").
    /// Vacío si la entrada usa RawText.
    /// </summary>
    public string TableName;

    /// <summary>
    /// Clave de localización dentro de la tabla (ej. "F0008").
    /// Vacío si la entrada usa RawText.
    /// </summary>
    public string LocalizationKey;

    /// <summary>
    /// Argumentos de formato opcionales. Se aplican con string.Format sobre
    /// el texto ya resuelto. Útil para textos como "Fragmento {0} encontrado".
    /// </summary>
    public object[] FormatArgs;

    /// <summary>
    /// Texto fijo no localizable. Se usa cuando LocalizationKey está vacío.
    /// Útil para contenido generado proceduralmente.
    /// </summary>
    public string RawText;

    /// <summary>True si esta entrada tiene referencia de localización válida.</summary>
    public bool IsLocalized =>
        !string.IsNullOrWhiteSpace(TableName) &&
        !string.IsNullOrWhiteSpace(LocalizationKey);

    /// <summary>
    /// Crea una entrada localizable. TableName por defecto es "Tabla 1".
    /// </summary>
    public static LocalizedConsciousnessEntry FromKey(
        string key,
        string tableName = "Tabla 1",
        params object[] args)
        => new() { TableName = tableName, LocalizationKey = key, FormatArgs = args };

    /// <summary>
    /// Crea una entrada con texto fijo, sin localización.
    /// </summary>
    public static LocalizedConsciousnessEntry FromRaw(string rawText)
        => new() { RawText = rawText };

    /// <summary>
    /// Construye el <see cref="LocalizedString"/> para resolución async.
    /// Solo llamar cuando <see cref="IsLocalized"/> es true.
    /// </summary>
    public LocalizedString ToLocalizedString() => new LocalizedString(TableName, LocalizationKey);
}

// ═══════════════════════════════════════════════════════════════════════════════
// MONOBEHAVIOUR: ConsciousnessDisplay
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Gestiona el panel del sistema de conciencia.
/// Mantiene el historial como <see cref="LocalizedConsciousnessEntry"/> en lugar
/// de strings ya traducidos, permitiendo re-renderizar todo en cualquier idioma
/// cuando el jugador cambia de idioma mid-session.
///
/// Cambio clave vs versión anterior:
/// - Elimina dependencia de <c>LocalizationManager</c> (sistema custom eliminado).
/// - Usa <see cref="LocalizationSettings.SelectedLocaleChanged"/> para detectar
///   cambios de idioma y re-renderizar inmediatamente, sin necesidad de recargar escena.
/// - <c>RenderEntries()</c> ahora es una coroutine asíncrona porque Unity Localization
///   no puede resolver textos de forma síncrona.
/// </summary>
public class ConsciousnessDisplay : MonoBehaviour
{
    #region Inspector

    [Tooltip("Componente TMP_Text donde se renderiza el texto de conciencia acumulado.")]
    [SerializeField] private TMP_Text consciousnessText;

    [Tooltip("Separador entre entradas. Puede ser '\\n', '\\n\\n', ' • ', etc.")]
    [SerializeField] private string entrySeparator = "\n";

    #endregion

    #region Private Fields

    private readonly List<LocalizedConsciousnessEntry> entries = new();
    private Coroutine renderCoroutine;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;

        if (renderCoroutine != null)
        {
            StopCoroutine(renderCoroutine);
            renderCoroutine = null;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Añade una entrada localizada usando tabla + clave.
    /// Por defecto busca en "Tabla 1".
    /// </summary>
    /// <param name="key">Clave en la tabla de localización (ej. "F0008").</param>
    /// <param name="tableName">Nombre de la tabla. Por defecto "Tabla 1".</param>
    /// <param name="args">Argumentos opcionales para string.Format.</param>
    public void AddEntry(string key, string tableName = "Tabla 1", params object[] args)
    {
        entries.Add(LocalizedConsciousnessEntry.FromKey(key, tableName, args));
        StartRender();
    }

    /// <summary>
    /// Añade texto fijo no localizado (nombres propios, valores numéricos, etc.).
    /// </summary>
    public void AddRawEntry(string rawText)
    {
        entries.Add(LocalizedConsciousnessEntry.FromRaw(rawText));
        StartRender();
    }

    /// <summary>
    /// Limpia todo el historial del panel.
    /// </summary>
    public void ClearEntries()
    {
        entries.Clear();

        if (renderCoroutine != null)
        {
            StopCoroutine(renderCoroutine);
            renderCoroutine = null;
        }

        if (consciousnessText != null)
        {
            consciousnessText.text = string.Empty;
        }
    }

    /// <summary>
    /// Devuelve el historial actual (solo lectura).
    /// </summary>
    public IReadOnlyList<LocalizedConsciousnessEntry> GetEntries() => entries.AsReadOnly();

    /// <summary>
    /// Restaura el historial desde datos guardados.
    /// </summary>
    public void RestoreEntries(IEnumerable<LocalizedConsciousnessEntry> savedEntries)
    {
        entries.Clear();
        entries.AddRange(savedEntries);
        StartRender();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Detecta cambio de idioma y re-renderiza todo el historial
    /// inmediatamente en el nuevo idioma, sin recargar la escena.
    /// </summary>
    private void HandleLocaleChanged(Locale locale) => StartRender();

    /// <summary>
    /// Cancela cualquier render en progreso y lanza uno nuevo.
    /// </summary>
    private void StartRender()
    {
        if (renderCoroutine != null)
        {
            StopCoroutine(renderCoroutine);
        }

        renderCoroutine = StartCoroutine(RenderEntriesAsync());
    }

    /// <summary>
    /// Reconstruye el texto completo resolviendo cada entrada de forma asíncrona.
    /// La resolución async es obligatoria porque Unity Localization carga las
    /// tablas de forma diferida y no garantiza disponibilidad síncrona.
    /// </summary>
    private IEnumerator RenderEntriesAsync()
    {
        if (consciousnessText == null)
        {
            Debug.LogWarning("[ConsciousnessDisplay] consciousnessText no asignado.", this);
            yield break;
        }

        if (entries.Count == 0)
        {
            consciousnessText.text = string.Empty;
            renderCoroutine = null;
            yield break;
        }

        var resolved = new string[entries.Count];

        for (int i = 0; i < entries.Count; i++)
        {
            LocalizedConsciousnessEntry entry = entries[i];

            if (entry.IsLocalized)
            {
                var handle = entry.ToLocalizedString().GetLocalizedStringAsync();
                yield return handle;

                if (!handle.IsDone || string.IsNullOrWhiteSpace(handle.Result))
                {
                    resolved[i] = $"[{entry.LocalizationKey}]";
                    continue;
                }

                // Aplica argumentos de formato si los hay.
                if (entry.FormatArgs is { Length: > 0 })
                {
                    try
                    {
                        resolved[i] = string.Format(handle.Result, entry.FormatArgs);
                    }
                    catch (FormatException e)
                    {
                        Debug.LogWarning(
                            $"[ConsciousnessDisplay] Error de formato en clave '{entry.LocalizationKey}': {e.Message}",
                            this);

                        resolved[i] = handle.Result;
                    }
                }
                else
                {
                    resolved[i] = handle.Result;
                }
            }
            else
            {
                resolved[i] = entry.RawText ?? string.Empty;
            }
        }

        var sb = new System.Text.StringBuilder();

        for (int i = 0; i < resolved.Length; i++)
        {
            sb.Append(resolved[i]);

            if (i < resolved.Length - 1)
            {
                sb.Append(entrySeparator);
            }
        }

        consciousnessText.text = sb.ToString();
        renderCoroutine = null;
    }

    #endregion
}