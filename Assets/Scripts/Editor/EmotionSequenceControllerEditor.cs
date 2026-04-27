using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor personalizado para <see cref="EmotionSequenceController"/>.
/// Permite probar secuencias emocionales en tiempo real y visualizar el último resultado.
/// </summary>
[CustomEditor(typeof(EmotionSequenceController))]
public sealed class EmotionSequenceControllerEditor : Editor
{
    /// <summary>
    /// Emoción izquierda de prueba.
    /// </summary>
    private EmotionData testEmotionA;

    /// <summary>
    /// Emoción derecha de prueba.
    /// </summary>
    private EmotionData testEmotionB;

    /// <summary>
    /// Duración de la prueba.
    /// </summary>
    private float testDuration = 5f;

    /// <summary>
    /// Último resultado recibido desde el controlador.
    /// </summary>
    private EmotionResult lastResult;

    /// <summary>
    /// Indica si ya existe un resultado disponible para mostrar.
    /// </summary>
    private bool hasResult;

    /// <summary>
    /// Caché local del target para suscripción de eventos.
    /// </summary>
    private EmotionSequenceController cachedController;

    /// <summary>
    /// Suscribe el editor al evento runtime cuando corresponde.
    /// </summary>
    private void OnEnable()
    {
        cachedController = (EmotionSequenceController)target;

        if (Application.isPlaying && cachedController != null)
        {
            cachedController.OnEmotionSelected += HandleEmotionSelected;
        }
    }

    /// <summary>
    /// Limpia la suscripción al deshabilitar el editor.
    /// </summary>
    private void OnDisable()
    {
        if (cachedController != null)
        {
            cachedController.OnEmotionSelected -= HandleEmotionSelected;
        }
    }

    /// <summary>
    /// Dibuja el inspector personalizado.
    /// </summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        RuntimeEditorGui.DrawSectionHeader("Runtime Testing");

        if (!Application.isPlaying)
        {
            RuntimeEditorGui.DrawInfo("Enter Play Mode to test sequences.");
            return;
        }

        EmotionSequenceController controller = (EmotionSequenceController)target;

        EditorGUILayout.Space(5f);

        testEmotionA = (EmotionData)EditorGUILayout.ObjectField(
            "Emotion A",
            testEmotionA,
            typeof(EmotionData),
            false);

        testEmotionB = (EmotionData)EditorGUILayout.ObjectField(
            "Emotion B",
            testEmotionB,
            typeof(EmotionData),
            false);

        testDuration = EditorGUILayout.FloatField("Duration", testDuration);

        EditorGUILayout.Space(5f);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Start Sequence"))
        {
            StartSequence(controller);
        }

        if (GUILayout.Button("Stop"))
        {
            StopSequence(controller);
        }

        EditorGUILayout.EndHorizontal();

        if (hasResult)
        {
            DrawLastResult();
        }
    }

    /// <summary>
    /// Inicia una secuencia de prueba si la configuración es válida.
    /// </summary>
    private void StartSequence(EmotionSequenceController controller)
    {
        if (testEmotionA == null || testEmotionB == null)
        {
            Debug.LogWarning("Asignar ambas emociones antes de iniciar.");
            return;
        }

        hasResult = false;
        controller.SetDuration(testDuration);
        controller.StartSequence(testEmotionA, testEmotionB);
    }

    /// <summary>
    /// Dibuja el bloque visual del último resultado recibido.
    /// </summary>
    private void DrawLastResult()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Last Result", EditorStyles.boldLabel);

        if (lastResult.IsNeutral)
        {
            EditorGUILayout.HelpBox("Estado Neutro", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Emotion", lastResult.Emotion.name);
        EditorGUILayout.LabelField("Intensity", lastResult.Intensity.ToString("F2"));
    }

    /// <summary>
    /// Maneja el resultado final de la secuencia.
    /// </summary>
    private void HandleEmotionSelected(EmotionResult result)
    {
        lastResult = result;
        hasResult = true;

        if (result.IsNeutral)
        {
            Debug.Log("[Emotion DEBUG] Estado neutro");
        }
        else
        {
            Debug.Log($"[Emotion DEBUG] {result.Emotion.name} | {result.Intensity:F2}");
        }

        Repaint();
    }

    /// <summary>
    /// Detiene la secuencia activa si existe.
    /// Mantiene el enfoque actual basado en reflexión para no tocar runtime todavía.
    /// </summary>
    private void StopSequence(EmotionSequenceController controller)
    {
        var controllerType = typeof(EmotionSequenceController);
        var activeSequenceField = controllerType.GetField(
            "activeSequence",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (activeSequenceField == null)
        {
            Debug.LogWarning("No se encontró el campo activeSequence.");
            return;
        }

        Coroutine activeCoroutine = activeSequenceField.GetValue(controller) as Coroutine;
        if (activeCoroutine == null)
        {
            Debug.LogWarning("No hay secuencia activa para detener.");
            return;
        }

        controller.StopCoroutine(activeCoroutine);
        Debug.Log("Secuencia detenida.");
    }
}