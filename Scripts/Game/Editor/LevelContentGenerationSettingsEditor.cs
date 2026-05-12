#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor personalizado para facilitar la configuración por nivel del contenido procedural.
/// </summary>
[CustomEditor(typeof(LevelContentGenerationSettings))]
public sealed class LevelContentGenerationSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LevelContentGenerationSettings settings = (LevelContentGenerationSettings)target;

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("Catalog Sync", EditorStyles.boldLabel);

        if (settings.ContentCatalog == null)
        {
            EditorGUILayout.HelpBox(
                "Asigna un Content Catalog para sincronizar automáticamente los IDs de prefabs disponibles.",
                MessageType.Warning);
            return;
        }

        if (GUILayout.Button("Synchronize Prefab IDs From Catalog"))
        {
            Undo.RecordObject(settings, "Synchronize Prefab IDs");
            SynchronizeAll(settings);
            EditorUtility.SetDirty(settings);
        }

        EditorGUILayout.HelpBox(
            "Después de sincronizar, cada lista de overrides tendrá los IDs del catálogo. Puedes activar/desactivar prefabs o cambiar su peso por nivel.",
            MessageType.Info);
    }

    private static void SynchronizeAll(LevelContentGenerationSettings settings)
    {
        SerializedObject serializedSettings = new SerializedObject(settings);

        SyncList(
            serializedSettings,
            "boxOverrides",
            settings.ContentCatalog.BoxPrefabs);

        SyncList(
            serializedSettings,
            "wallOverrides",
            settings.ContentCatalog.WallPrefabs);

        SyncList(
            serializedSettings,
            "ballOverrides",
            settings.ContentCatalog.BallPrefabs);

        SyncList(
            serializedSettings,
            "fanOverrides",
            settings.ContentCatalog.FanPrefabs);

        SyncList(
            serializedSettings,
            "coinOverrides",
            settings.ContentCatalog.CoinPrefabs);

        serializedSettings.ApplyModifiedProperties();
    }

    private static void SyncList(
        SerializedObject serializedSettings,
        string propertyName,
        IReadOnlyList<TrackSpawnPrefabEntry> catalogEntries)
    {
        SerializedProperty list = serializedSettings.FindProperty(propertyName);
        if (list == null)
        {
            return;
        }

        HashSet<string> existingIds = new HashSet<string>();

        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            SerializedProperty idProperty = element.FindPropertyRelative("prefabId");

            if (idProperty != null && !string.IsNullOrWhiteSpace(idProperty.stringValue))
            {
                existingIds.Add(idProperty.stringValue);
            }
        }

        for (int i = 0; i < catalogEntries.Count; i++)
        {
            TrackSpawnPrefabEntry entry = catalogEntries[i];

            if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
            {
                continue;
            }

            if (existingIds.Contains(entry.Id))
            {
                continue;
            }

            int newIndex = list.arraySize;
            list.InsertArrayElementAtIndex(newIndex);

            SerializedProperty newElement = list.GetArrayElementAtIndex(newIndex);
            SerializedProperty idProperty = newElement.FindPropertyRelative("prefabId");
            SerializedProperty enabledProperty = newElement.FindPropertyRelative("enabledInLevel");
            SerializedProperty overrideProperty = newElement.FindPropertyRelative("overrideSelectionWeight");
            SerializedProperty weightProperty = newElement.FindPropertyRelative("levelSelectionWeight");

            idProperty.stringValue = entry.Id;
            enabledProperty.boolValue = true;
            overrideProperty.boolValue = false;
            weightProperty.floatValue = Mathf.Max(0f, entry.BaseSelectionWeight);
        }
    }
}
#endif