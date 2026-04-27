using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor personalizado para <see cref="FootprintPathController"/>.
/// Permite disparar manualmente la animación de huellas en runtime.
/// </summary>
[CustomEditor(typeof(FootprintPathController))]
public sealed class FootprintPathControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        FootprintPathController footprintController = (FootprintPathController)target;

        RuntimeEditorGui.DrawSectionHeader("Testing de Huellas");
        RuntimeEditorGui.DrawInfo("Permite reiniciar la animación de huellas en Play Mode.");

        RuntimeEditorGui.SetRuntimeEnabled(true);

        if (GUILayout.Button("Reiniciar Animación de Huellas"))
        {
            footprintController.StartFootprintAnimation();
        }

        RuntimeEditorGui.ResetGuiEnabled();
    }
}