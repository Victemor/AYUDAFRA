using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor personalizado para TimerSliderUI.
/// Permite probar el temporizador directamente desde el Inspector.
/// </summary>
[CustomEditor(typeof(TimerSliderUI))]
public class TimerSliderUIEditor : Editor
{
    private float testDuration = 5f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Runtime Testing", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to test the timer.", MessageType.Info);
            return;
        }

        TimerSliderUI timer = (TimerSliderUI)target;

        EditorGUILayout.Space(5);

        testDuration = EditorGUILayout.FloatField("Test Duration", testDuration);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Start (Custom)"))
        {
            timer.StartTimer(testDuration);
        }

        if (GUILayout.Button("Start (Default)"))
        {
            timer.StartTimer();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Stop Timer"))
        {
            timer.StopTimer();
        }
    }
}