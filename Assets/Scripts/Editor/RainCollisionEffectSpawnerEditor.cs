using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor personalizado para <see cref="RainController"/>.
/// Permite probar arranque, detención y cambios de intensidad en runtime.
/// </summary>
[CustomEditor(typeof(RainController))]
public sealed class RainControllerEditor : Editor
{
    private float intensityToTest = 200f;
    private float transitionDuration = 2f;
    private float temporalDuration = 5f;
    private float temporalIntensity = 300f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        RainController rainController = (RainController)target;

        RuntimeEditorGui.DrawSectionHeader("Controles de Lluvia");
        RuntimeEditorGui.DrawInfo("Permite probar la lluvia en Play Mode.");

        RuntimeEditorGui.SetRuntimeEnabled(true);

        if (GUILayout.Button("Iniciar Lluvia"))
        {
            rainController.SetRainEmissionWithTimeTransition(intensityToTest, transitionDuration);
        }

        if (GUILayout.Button("Detener Lluvia"))
        {
            rainController.StopRain();
        }

        EditorGUILayout.Space(5f);
        EditorGUILayout.LabelField("Cambio de Intensidad", EditorStyles.miniBoldLabel);

        intensityToTest = EditorGUILayout.FloatField("Partículas por segundo", intensityToTest);
        transitionDuration = EditorGUILayout.FloatField("Duración de transición", transitionDuration);

        if (GUILayout.Button("Cambiar Intensidad"))
        {
            rainController.SetRainEmissionWithTimeTransition(intensityToTest, transitionDuration);
        }

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Pruebas Temporales", EditorStyles.boldLabel);

        temporalDuration = EditorGUILayout.FloatField("Duración (segundos)", temporalDuration);
        temporalIntensity = EditorGUILayout.FloatField("Intensidad temporal", temporalIntensity);

        RuntimeEditorGui.DrawInfo("Las pruebas temporales quedan reservadas hasta reconectar esas APIs en el controlador.");

        RuntimeEditorGui.ResetGuiEnabled();
    }
}