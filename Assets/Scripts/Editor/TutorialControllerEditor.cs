using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom editor para facilitar pruebas del TutorialController desde el inspector.
/// </summary>
[CustomEditor(typeof(TutorialController))]
public sealed class TutorialControllerEditor : Editor
{
    private SerializedProperty instructionsProp;
    private SerializedProperty viewProp;
    private SerializedProperty mouseMoveThresholdProp;
    private SerializedProperty debugIdProp;
    private SerializedProperty debugOffsetProp;
    private SerializedProperty debugLogsProp;

    private void OnEnable()
    {
        instructionsProp = serializedObject.FindProperty("instructions");
        viewProp = serializedObject.FindProperty("view");
        mouseMoveThresholdProp = serializedObject.FindProperty("mouseMoveThreshold");
        debugIdProp = serializedObject.FindProperty("debugId");
        debugOffsetProp = serializedObject.FindProperty("debugOffset");
        debugLogsProp = serializedObject.FindProperty("debugLogs");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawConfigurationSection();
        DrawInputSection();
        DrawDebugSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawConfigurationSection()
    {
        EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(instructionsProp, true);
        EditorGUILayout.PropertyField(viewProp);
    }

    private void DrawInputSection()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Input Detection", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(mouseMoveThresholdProp);
    }

    private void DrawDebugSection()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Debug Controls", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(debugIdProp);
        EditorGUILayout.PropertyField(debugOffsetProp);
        EditorGUILayout.PropertyField(debugLogsProp);

        EditorGUILayout.Space(5);

        GUI.enabled = Application.isPlaying;

        TutorialController controller = (TutorialController)target;

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Show"))
        {
            controller.ShowInstruction(debugIdProp.stringValue, debugOffsetProp.floatValue);
        }

        if (GUILayout.Button("Force"))
        {
            controller.ForceShowInstruction(debugIdProp.stringValue, debugOffsetProp.floatValue);
        }

        if (GUILayout.Button("Hide"))
        {
            controller.HideTutorial();
        }

        EditorGUILayout.EndHorizontal();

        GUI.enabled = true;

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Los botones solo funcionan en Play Mode.", MessageType.Info);
        }
    }
}