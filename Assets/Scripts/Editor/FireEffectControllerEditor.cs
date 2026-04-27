using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor personalizado para <see cref="FireController"/>.
/// Permite probar encendido, apagado y secuencias temporales del fuego.
/// </summary>
[CustomEditor(typeof(FireController))]
public sealed class FireControllerEditor : Editor
{
    private float fadeInDuration = 1f;
    private float fadeOutDuration = 1f;
    private float intensity = 1f;
    private float tempDuration = 5f;
    private float returnIntensity = 1f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        FireController fireController = (FireController)target;

        RuntimeEditorGui.DrawSectionHeader("Controles del Fuego");
        RuntimeEditorGui.DrawInfo("Permite probar el sistema de fuego en Play Mode.");

        RuntimeEditorGui.SetRuntimeEnabled(true);

        fadeInDuration = EditorGUILayout.FloatField("Fade In Duration", fadeInDuration);
        fadeOutDuration = EditorGUILayout.FloatField("Fade Out Duration", fadeOutDuration);
        intensity = EditorGUILayout.Slider("Intensidad Encendido", intensity, 0f, 1f);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Encender Fuego"))
        {
            fireController.StartFire(intensity, fadeInDuration);
        }

        if (GUILayout.Button("Apagar Fuego"))
        {
            fireController.StopFire(fadeOutDuration);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Controles Temporales", EditorStyles.boldLabel);

        tempDuration = EditorGUILayout.FloatField("Duración Temporal", tempDuration);
        returnIntensity = EditorGUILayout.Slider("Intensidad al Volver", returnIntensity, 0f, 1f);

        if (GUILayout.Button("Encender por Segundos"))
        {
            fireController.StartFireForSeconds(intensity, tempDuration, fadeInDuration, fadeOutDuration);
        }

        if (GUILayout.Button("Apagar por Segundos"))
        {
            fireController.StopFireForSeconds(tempDuration, returnIntensity, fadeOutDuration, fadeInDuration);
        }

        RuntimeEditorGui.ResetGuiEnabled();
    }
}