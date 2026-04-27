using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Editor para PlayerClickMovement.
/// Permite ejecutar acciones de control de movimiento directamente desde el Inspector
/// para facilitar pruebas durante desarrollo.
/// </summary>
[CustomEditor(typeof(PlayerClickMovement))]
public class PlayerClickMovementEditor : Editor
{
    /// <summary>
    /// Dibuja el inspector personalizado agregando botones de control.
    /// </summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Debug Controls", EditorStyles.boldLabel);

        PlayerClickMovement movement = (PlayerClickMovement)target;

        GUI.enabled = Application.isPlaying;

        if (GUILayout.Button("Enable Movement"))
        {
            movement.EnableMovement();
            MarkDirty(movement);
        }

        if (GUILayout.Button("Disable Movement"))
        {
            movement.DisableMovement();
            MarkDirty(movement);
        }

        GUI.enabled = true;

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Los botones solo funcionan en Play Mode.",
                MessageType.Info
            );
        }
    }

    /// <summary>
    /// Marca el objeto como modificado para asegurar persistencia en el editor.
    /// </summary>
    private void MarkDirty(Object obj)
    {
        EditorUtility.SetDirty(obj);
    }
}