using System;
using UnityEngine;

/// <summary>
/// Configuración por nivel para permitir, bloquear o cambiar el peso de un prefab del catálogo global.
/// </summary>
[Serializable]
public sealed class LevelSpawnPrefabOverride
{
    [Tooltip("ID del prefab dentro del catálogo global. Normalmente se sincroniza desde el editor.")]
    [SerializeField] private string prefabId;

    [Tooltip("Si está activo, este prefab puede aparecer en este nivel.")]
    [SerializeField] private bool enabledInLevel = true;

    [Tooltip("Si está activo, este nivel usa un peso propio en vez del peso base del catálogo.")]
    [SerializeField] private bool overrideSelectionWeight;

    [Tooltip("Peso usado en este nivel cuando Override Selection Weight está activo. Si es 0, no aparece.")]
    [SerializeField] private float levelSelectionWeight = 1f;

    public string PrefabId => prefabId;
    public bool EnabledInLevel => enabledInLevel;
    public bool OverrideSelectionWeight => overrideSelectionWeight;
    public float LevelSelectionWeight => Mathf.Max(0f, levelSelectionWeight);

    /// <summary>
    /// Configura el ID desde herramientas de editor.
    /// </summary>
    public void SetPrefabId(string id)
    {
        prefabId = id;
    }

    /// <summary>
    /// Normaliza valores inválidos configurados desde el Inspector.
    /// </summary>
    public void Normalize()
    {
        if (levelSelectionWeight < 0f)
        {
            levelSelectionWeight = 0f;
        }
    }
}