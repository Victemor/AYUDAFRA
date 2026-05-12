using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector personalizado de LevelGenerationSettings.
/// </summary>
[CustomEditor(typeof(LevelGenerationSettings))]
public sealed class LevelGenerationSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

        LevelGenerationSettings settings = (LevelGenerationSettings)target;
        List<string> warnings = TrackGenerationValidationUtility.CollectLevelSettingsWarnings(settings);

        if (warnings.Count == 0)
        {
            EditorGUILayout.HelpBox("No validation warnings.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < warnings.Count; i++)
            {
                EditorGUILayout.HelpBox(warnings[i], MessageType.Warning);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}