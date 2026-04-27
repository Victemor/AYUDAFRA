using UnityEditor;
using UnityEngine;

/// <summary>
/// Utilidad compartida para construir secciones de testing runtime
/// en custom editors de manera consistente.
/// </summary>
public static class RuntimeEditorGui
{
    /// <summary>
    /// Dibuja un título de sección con un pequeño espaciado superior.
    /// </summary>
    public static void DrawSectionHeader(string title)
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    /// <summary>
    /// Dibuja un HelpBox informativo.
    /// </summary>
    public static void DrawInfo(string message)
    {
        EditorGUILayout.HelpBox(message, MessageType.Info);
    }

    /// <summary>
    /// Dibuja un HelpBox de advertencia.
    /// </summary>
    public static void DrawWarning(string message)
    {
        EditorGUILayout.HelpBox(message, MessageType.Warning);
    }

    /// <summary>
    /// Dibuja un HelpBox de error.
    /// </summary>
    public static void DrawError(string message)
    {
        EditorGUILayout.HelpBox(message, MessageType.Error);
    }

    /// <summary>
    /// Habilita o deshabilita controles de GUI según el Play Mode actual.
    /// </summary>
    public static void SetRuntimeEnabled(bool requiredPlayMode)
    {
        GUI.enabled = !requiredPlayMode || Application.isPlaying;
    }

    /// <summary>
    /// Restablece el estado de GUI a habilitado.
    /// </summary>
    public static void ResetGuiEnabled()
    {
        GUI.enabled = true;
    }
}