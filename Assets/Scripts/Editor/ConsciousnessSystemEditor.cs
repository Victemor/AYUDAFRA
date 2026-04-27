using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor personalizado para <see cref="ConsciousnessSystem"/>.
/// Permite inyectar pensamientos manualmente desde el Inspector en Play Mode.
/// </summary>
[CustomEditor(typeof(ConsciousnessSystem))]
public sealed class ConsciousnessSystemEditor : Editor
{
    /// <summary>
    /// Texto de prueba enviado al sistema.
    /// </summary>
    private string testThought = string.Empty;

    /// <summary>
    /// Dibuja el inspector personalizado.
    /// </summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        RuntimeEditorGui.DrawSectionHeader("Testing de Conciencia");
        RuntimeEditorGui.DrawInfo("Permite agregar pensamientos manualmente durante Play Mode.");

        RuntimeEditorGui.SetRuntimeEnabled(true);

        testThought = EditorGUILayout.TextField("Pensamiento", testThought);

        if (GUILayout.Button("Agregar Pensamiento"))
        {
            AddTestThought();
        }

        RuntimeEditorGui.ResetGuiEnabled();
    }

    /// <summary>
    /// Envía el pensamiento configurado al sistema runtime.
    /// </summary>
    private void AddTestThought()
    {
        if (string.IsNullOrWhiteSpace(testThought))
        {
            Debug.LogWarning("El pensamiento está vacío.");
            return;
        }

        ConsciousnessSystem consciousnessSystem = (ConsciousnessSystem)target;
        consciousnessSystem.AddThought(testThought);
        testThought = string.Empty;
    }
}