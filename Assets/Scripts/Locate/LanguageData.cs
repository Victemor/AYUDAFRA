using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject que actúa como tabla de traducciones para un idioma específico.
/// Crear un asset por idioma: LanguageData_Spanish, LanguageData_English, etc.
/// El build-up del diccionario se hace lazy para evitar costos en frames de juego.
/// </summary>
[CreateAssetMenu(fileName = "LanguageData_", menuName = "Localization/Language Data")]
public class LanguageData : ScriptableObject
{
    [Tooltip("Idioma al que pertenece este asset.")]
    [SerializeField] private Language language;

    [Tooltip("Lista de entradas clave-valor para este idioma.")]
    [SerializeField] private List<LocalizationEntry> entries = new();

    /// <summary>Idioma que representa este asset.</summary>
    public Language Language => language;

    // Diccionario construido en la primera consulta (lazy) para evitar
    // trabajo innecesario si el idioma nunca se activa durante la sesión.
    private Dictionary<string, string> _lookup;

    /// <summary>
    /// Devuelve el texto traducido para la clave dada.
    /// Si la clave no existe, retorna la clave misma entre corchetes para
    /// que el equipo de diseño/QA detecte entradas faltantes fácilmente.
    /// </summary>
    /// <param name="key">Clave de localización (ej: "ui.menu.play").</param>
    public string GetText(string key)
    {
        BuildLookupIfNeeded();
        return _lookup.TryGetValue(key, out string value) ? value : $"[{key}]";
    }

    /// <summary>
    /// Verifica si una clave existe en este idioma.
    /// Útil para validaciones en el Editor.
    /// </summary>
    public bool ContainsKey(string key)
    {
        BuildLookupIfNeeded();
        return _lookup.ContainsKey(key);
    }

    /// <summary>
    /// Fuerza la reconstrucción del diccionario interno.
    /// Llamar en el Editor si se editan las entradas en caliente.
    /// </summary>
    public void InvalidateLookup()
    {
        _lookup = null;
    }

    private void BuildLookupIfNeeded()
    {
        if (_lookup != null) return;

        _lookup = new Dictionary<string, string>(entries.Count);
        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.Key)) continue;

            if (!_lookup.TryAdd(entry.Key, entry.Value))
            {
                Debug.LogWarning($"[LanguageData] Clave duplicada '{entry.Key}' en {name}. Se usará la primera entrada.", this);
            }
        }
    }

    // Invalida el lookup cuando el asset cambia en el Editor,
    // garantizando que los cambios en caliente se reflejen inmediatamente.
    private void OnValidate() => InvalidateLookup();
}