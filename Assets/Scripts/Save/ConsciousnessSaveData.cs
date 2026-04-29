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
///
/// Un pensamiento puede estar referenciado de dos formas en Unity Localization:
/// - Por nombre de clave (string): <c>key</c> no vacío → se usa directamente.
/// - Por ID numérico (long): <c>key</c> vacío pero <c>keyId</c> > 0 → fallback.
///
/// Unity puede guardar internamente la referencia por ID cuando se selecciona
/// desde el picker del Inspector, haciendo que <c>TableEntryReference.Key</c>
/// devuelva string vacío. Guardamos ambos para cubrir los dos casos.
/// </summary>
[Serializable]
public class ThoughtSaveData
{
    /// <summary>Nombre de la tabla de localización.</summary>
    public string tableName;

    /// <summary>Clave string de la entrada. Puede estar vacío si la referencia es por ID.</summary>
    public string key;

    /// <summary>ID numérico de la entrada. Respaldo cuando key está vacío.</summary>
    public long keyId;

    /// <summary>Texto plano para pensamientos no localizados.</summary>
    public string rawText;

    public float timestamp;

    /// <summary>
    /// True si tiene referencia de localización válida (por key o por keyId).
    /// </summary>
    public bool IsLocalized =>
        !string.IsNullOrWhiteSpace(tableName) &&
        (!string.IsNullOrWhiteSpace(key) || keyId > 0);
}