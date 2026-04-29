using System;
using UnityEngine;

/// <summary>
/// Contenedor inmutable para un par clave-valor de traducción.
/// Se usa como struct (tipo valor) porque es pequeño, inmutable
/// y se serializa en listas dentro de <see cref="LanguageData"/>.
/// </summary>
[Serializable]
public struct LocalizationEntry
{
    [Tooltip("Identificador único de este texto. Ej: 'ui.menu.play' o 'consciousness.memory_fragment'")]
    [SerializeField] private string key;

    [Tooltip("Texto traducido para este idioma. Soporta rich text de TMP.")]
    [TextArea(1, 4)]
    [SerializeField] private string value;

    /// <summary>Clave única que identifica este texto en todos los idiomas.</summary>
    public string Key   => key;

    /// <summary>Texto traducido listo para mostrar en pantalla.</summary>
    public string Value => value;
}