using System;
using System.Collections.Generic;

/// <summary>
/// Datos serializables del sistema de consciencia para guardado en disco.
/// </summary>
[Serializable]
public class ConsciousnessSaveData
{
    public List<ThoughtSaveData> thoughts = new();
}

/// <summary>
/// DTO serializable de un pensamiento individual.
/// Admite dos rutas:
/// - <b>Localizada</b>: <c>tableName</c> + <c>key</c> no vacíos → el texto se re-resuelve
///   al idioma activo al cargar, lo que permite cambiar de idioma entre sesiones.
/// - <b>Raw (fallback)</b>: <c>rawText</c> no vacío → texto plano para contenido
///   aún no migrado a la tabla de localización.
/// </summary>
[Serializable]
public class ThoughtSaveData
{
    /// <summary>Nombre de la tabla de localización. Vacío si es raw.</summary>
    public string tableName;

    /// <summary>Clave de entrada en la tabla. Vacía si es raw.</summary>
    public string key;

    /// <summary>
    /// Texto plano para pensamientos no localizados.
    /// Solo se usa cuando <c>tableName</c> y <c>key</c> están vacíos.
    /// </summary>
    public string rawText;

    public float timestamp;

    /// <summary>
    /// True si este save data tiene una referencia de localización válida.
    /// False implica que debe usarse <c>rawText</c> directamente.
    /// </summary>
    public bool IsLocalized =>
        !string.IsNullOrWhiteSpace(tableName) &&
        !string.IsNullOrWhiteSpace(key);
}