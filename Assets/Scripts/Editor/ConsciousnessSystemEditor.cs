#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor personalizado para <see cref="ConsciousnessSystem"/>.
/// Permite inyectar pensamientos manualmente en Play Mode,
/// tanto por texto plano (raw) como por tabla + clave de localización.
/// </summary>
[CustomEditor(typeof(ConsciousnessSystem))]
public sealed class ConsciousnessSystemEditor : Editor
{
    private string testRawText    = string.Empty;
    private string testTableName  = "Tabla 1";
    private string testKey        = string.Empty;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        RuntimeEditorGui.DrawSectionHeader("Testing de Conciencia");
        RuntimeEditorGui.DrawInfo("Inyecta pensamientos en Play Mode por texto plano o por clave de localización.");

        RuntimeEditorGui.SetRuntimeEnabled(true);

        // ── Raw ──────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Texto plano (raw)", EditorStyles.boldLabel);
        testRawText = EditorGUILayout.TextField("Texto", testRawText);

        if (GUILayout.Button("Agregar Raw"))
        {
            AddRawThought();
        }

        EditorGUILayout.Space(6f);

        // ── Localizado ───────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Localizado (tabla + clave)", EditorStyles.boldLabel);
        testTableName = EditorGUILayout.TextField("Tabla", testTableName);
        testKey       = EditorGUILayout.TextField("Clave", testKey);

        if (GUILayout.Button("Agregar Localizado"))
        {
            AddLocalizedThought();
        }

        RuntimeEditorGui.ResetGuiEnabled();
    }

    private void AddRawThought()
    {
        if (string.IsNullOrWhiteSpace(testRawText))
        {
            Debug.LogWarning("[ConsciousnessSystemEditor] El texto está vacío.");
            return;
        }

        ((ConsciousnessSystem)target).AddThoughtRaw(testRawText);
        testRawText = string.Empty;
    }

    private void AddLocalizedThought()
    {
        if (string.IsNullOrWhiteSpace(testTableName) || string.IsNullOrWhiteSpace(testKey))
        {
            Debug.LogWarning("[ConsciousnessSystemEditor] Tabla o clave vacía.");
            return;
        }

        ((ConsciousnessSystem)target).AddThought(testTableName, testKey);
        testKey = string.Empty;
    }
}
#endif