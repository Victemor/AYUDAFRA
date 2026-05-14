using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor personalizado para <see cref="CollectiblePowerUpDebugger"/>.
///
/// En Play Mode dibuja un panel de depuración con un botón "Aplicar" por cada
/// power-up configurado y muestra el tiempo restante del efecto activo.
/// El panel se repinta automáticamente cada frame de editor para mantener los
/// timers actualizados sin necesidad de mover el mouse.
/// </summary>
[CustomEditor(typeof(CollectiblePowerUpDebugger))]
public sealed class CollectiblePowerUpDebuggerEditor : Editor
{
    #region Editor Lifecycle

    private void OnEnable()
    {
        // Fuerza repintado constante en Play Mode para actualizar timers.
        EditorApplication.update += Repaint;
    }

    private void OnDisable()
    {
        EditorApplication.update -= Repaint;
    }

    #endregion

    #region Inspector GUI

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CollectiblePowerUpDebugger debugger = (CollectiblePowerUpDebugger)target;

        EditorGUILayout.Space(6);
        DrawDivider();
        EditorGUILayout.LabelField("Debug Power-Ups", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Los botones de prueba están disponibles únicamente en Play Mode.",
                MessageType.Info);
            return;
        }

        if (debugger.Controller == null)
        {
            EditorGUILayout.HelpBox(
                "Asigna un BallPowerUpController en el campo Controller.",
                MessageType.Warning);
            return;
        }

        CollectiblePowerUpData[] powerUps = debugger.TestPowerUps;

        if (powerUps == null || powerUps.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "Añade power-ups al array Test Power Ups para probarlos.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.Space(4);

        for (int i = 0; i < powerUps.Length; i++)
        {
            CollectiblePowerUpData data = powerUps[i];

            if (data == null)
            {
                EditorGUILayout.LabelField($"[{i}]", "— sin asignar —");
                continue;
            }

            DrawPowerUpRow(debugger.Controller, data);
        }
    }

    #endregion

    #region Row Drawing

    private static void DrawPowerUpRow(BallPowerUpController controller, CollectiblePowerUpData data)
    {
        bool    isActive   = controller.IsActive(data.Type);
        float   remaining  = controller.GetRemainingTime(data.Type);

        EditorGUILayout.BeginHorizontal();

        // Indicador de estado
        Color statusColor = isActive ? new Color(0.4f, 0.9f, 0.5f) : new Color(0.6f, 0.6f, 0.6f);
        GUIStyle dot      = new GUIStyle(EditorStyles.label) { normal = { textColor = statusColor } };
        EditorGUILayout.LabelField("●", dot, GUILayout.Width(16));

        // Nombre + timer
        string label = isActive
            ? $"{data.name}  [{remaining:F1}s restantes]"
            : data.name;

        EditorGUILayout.LabelField(label, GUILayout.ExpandWidth(true));

        // Botón Aplicar
        if (GUILayout.Button("Aplicar", GUILayout.Width(70)))
        {
            controller.Collect(data);
        }

        EditorGUILayout.EndHorizontal();
    }

    private static void DrawDivider()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
        EditorGUILayout.Space(2);
    }

    #endregion
}