using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor personalizado para <see cref="WindController"/>.
/// Permite probar intensidades y transiciones del viento en tiempo real.
/// </summary>
[CustomEditor(typeof(WindController))]
public sealed class WindControllerEditor : Editor
{
    private float testIntensity = 50f;
    private float transitionTime = 0.5f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        WindController windController = (WindController)target;

        RuntimeEditorGui.DrawSectionHeader("Testing de Viento");
        RuntimeEditorGui.DrawInfo("Permite probar la intensidad del viento en Play Mode.");

        RuntimeEditorGui.SetRuntimeEnabled(true);

        testIntensity = EditorGUILayout.FloatField("Intensidad (rate)", testIntensity);
        transitionTime = EditorGUILayout.FloatField("Duración transición", transitionTime);

        EditorGUILayout.Space(5f);

        if (GUILayout.Button("Aplicar Intensidad"))
        {
            windController.SetWindEmissionWithTransition(testIntensity, transitionTime);
        }

        if (GUILayout.Button("Aplicar Instantáneo"))
        {
            windController.SetWindEmissionAtInstant(testIntensity);
        }

        if (GUILayout.Button("Detener Viento"))
        {
            windController.StopWindWithTransitionTime(transitionTime);
        }

        RuntimeEditorGui.ResetGuiEnabled();
    }
}