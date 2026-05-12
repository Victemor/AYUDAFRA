using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector personalizado del TrackGenerationProfile.
/// </summary>
[CustomEditor(typeof(TrackGenerationProfile))]
public sealed class TrackGenerationProfileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

        TrackGenerationProfile profile = (TrackGenerationProfile)target;
        List<string> warnings = TrackGenerationValidationUtility.CollectProfileWarnings(profile);

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