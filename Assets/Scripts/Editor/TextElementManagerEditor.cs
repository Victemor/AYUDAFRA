using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor personalizado para <see cref="DialogueController"/>.
/// Permite probar texto, posición y animaciones en runtime.
/// </summary>
[CustomEditor(typeof(DialogueController))]
public sealed class DialogueControllerEditor : Editor
{
    private string testText = "No debí hacer eso...";
    private Vector3 testPosition = Vector3.zero;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10f);
        EditorGUILayout.LabelField("Testing Texto (Runtime)", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Permite probar texto, posición y efecto máquina de escribir en tiempo real.",
            MessageType.Info);

        GUI.enabled = Application.isPlaying;

        testText = EditorGUILayout.TextField("Texto", testText);
        testPosition = EditorGUILayout.Vector3Field("Posición", testPosition);

        GUILayout.Space(5f);

        DialogueController controller = (DialogueController)target;

        if (GUILayout.Button("Mostrar"))
        {
            controller.ShowText(testText, testPosition);
        }

        if (GUILayout.Button("Actualizar Texto"))
        {
            controller.UpdateCurrentText(testText);
        }

        if (GUILayout.Button("Ocultar (Fade Out)"))
        {
            controller.HideCurrent();
        }

        if (GUILayout.Button("Ocultar Inmediato"))
        {
            controller.HideCurrentImmediate();
        }

        GUI.enabled = true;
    }
}