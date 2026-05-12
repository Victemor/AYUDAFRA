#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor de validación básica para el catálogo global de contenido.
/// </summary>
[CustomEditor(typeof(TrackContentGenerationProfile))]
public sealed class TrackContentGenerationProfileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TrackContentGenerationProfile profile = (TrackContentGenerationProfile)target;

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("Catalog Validation", EditorStyles.boldLabel);

        List<string> warnings = new List<string>();

        ValidateEntries("Boxes", profile.BoxPrefabs, warnings);
        ValidateEntries("Walls", profile.WallPrefabs, warnings);
        ValidateEntries("Balls", profile.BallPrefabs, warnings);
        ValidateEntries("Fans", profile.FanPrefabs, warnings);
        ValidateEntries("Coins", profile.CoinPrefabs, warnings);

        if (warnings.Count == 0)
        {
            EditorGUILayout.HelpBox("No catalog warnings.", MessageType.Info);
            return;
        }

        for (int i = 0; i < warnings.Count; i++)
        {
            EditorGUILayout.HelpBox(warnings[i], MessageType.Warning);
        }
    }

    private static void ValidateEntries(
        string label,
        IReadOnlyList<TrackSpawnPrefabEntry> entries,
        List<string> warnings)
    {
        HashSet<string> ids = new HashSet<string>();

        for (int i = 0; i < entries.Count; i++)
        {
            TrackSpawnPrefabEntry entry = entries[i];

            if (entry == null)
            {
                warnings.Add($"{label}: Element {i} is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                warnings.Add($"{label}: Element {i} has an empty ID.");
                continue;
            }

            if (!ids.Add(entry.Id))
            {
                warnings.Add($"{label}: Duplicate ID '{entry.Id}'. IDs must be unique inside each category.");
            }

            if (entry.Prefab == null)
            {
                warnings.Add($"{label}: '{entry.Id}' has no prefab assigned.");
            }

            if (entry.BaseSelectionWeight <= 0f)
            {
                warnings.Add($"{label}: '{entry.Id}' has weight 0, so it will not appear unless a level overrides it.");
            }
        }
    }
}
#endif