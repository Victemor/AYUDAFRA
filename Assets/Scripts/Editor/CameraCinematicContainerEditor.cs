using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor personalizado para <see cref="CameraSystemController"/>.
/// Permite configurar estados y probar comportamiento en tiempo real.
/// </summary>
[CustomEditor(typeof(CameraSystemController))]
public sealed class CameraSystemControllerEditor : Editor
{
    #region Serialized Properties

    private SerializedProperty cameraControllerProp;
    private SerializedProperty followTargetProp;
    private SerializedProperty cinematicTargetProp;

    private SerializedProperty explorationMinZoomProp;
    private SerializedProperty explorationMaxZoomProp;
    private SerializedProperty explorationInitialZoomProp;

    private SerializedProperty cinematicMinZoomProp;
    private SerializedProperty cinematicMaxZoomProp;
    private SerializedProperty cinematicInitialZoomProp;
    private SerializedProperty cinematicHorizontalCenterOffsetProp;

    private SerializedProperty transitionDurationProp;
    private SerializedProperty transitionCurveProp;

    #endregion

    #region Private Fields

    private Vector3 debugOffset = Vector3.zero;

    #endregion

    #region Unity Methods

    /// <summary>
    /// Cachea las propiedades serializadas usadas por el editor.
    /// </summary>
    private void OnEnable()
    {
        cameraControllerProp = serializedObject.FindProperty("cameraController");
        followTargetProp = serializedObject.FindProperty("followTarget");
        cinematicTargetProp = serializedObject.FindProperty("cinematicTarget");

        explorationMinZoomProp = serializedObject.FindProperty("explorationMinZoom");
        explorationMaxZoomProp = serializedObject.FindProperty("explorationMaxZoom");
        explorationInitialZoomProp = serializedObject.FindProperty("explorationInitialZoom");

        cinematicMinZoomProp = serializedObject.FindProperty("cinematicMinZoom");
        cinematicMaxZoomProp = serializedObject.FindProperty("cinematicMaxZoom");
        cinematicInitialZoomProp = serializedObject.FindProperty("cinematicInitialZoom");
        cinematicHorizontalCenterOffsetProp = serializedObject.FindProperty("cinematicHorizontalCenterOffset");

        transitionDurationProp = serializedObject.FindProperty("transitionDuration");
        transitionCurveProp = serializedObject.FindProperty("transitionCurve");
    }

    /// <summary>
    /// Dibuja el inspector personalizado.
    /// </summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawReferences();
        EditorGUILayout.Space(10f);

        DrawExplorationSettings();
        EditorGUILayout.Space(10f);

        DrawCinematicSettings();
        EditorGUILayout.Space(10f);

        DrawTransitionSettings();
        EditorGUILayout.Space(15f);

        DrawDebugControls();

        serializedObject.ApplyModifiedProperties();
    }

    #endregion

    #region Inspector Draw

    /// <summary>
    /// Dibuja el bloque de referencias principales.
    /// </summary>
    private void DrawReferences()
    {
        EditorGUILayout.LabelField("Referencias", EditorStyles.boldLabel);
        DrawPropertyIfExists(cameraControllerProp);
        DrawPropertyIfExists(followTargetProp);
        DrawPropertyIfExists(cinematicTargetProp);
    }

    /// <summary>
    /// Dibuja la configuración del modo exploración.
    /// </summary>
    private void DrawExplorationSettings()
    {
        EditorGUILayout.LabelField("Exploration Settings", EditorStyles.boldLabel);
        DrawPropertyIfExists(explorationMinZoomProp);
        DrawPropertyIfExists(explorationMaxZoomProp);
        DrawPropertyIfExists(explorationInitialZoomProp);

        ValidateZoom(explorationMinZoomProp, explorationMaxZoomProp);
    }

    /// <summary>
    /// Dibuja la configuración del modo cinemático.
    /// </summary>
    private void DrawCinematicSettings()
    {
        EditorGUILayout.LabelField("Cinematic Settings", EditorStyles.boldLabel);
        DrawPropertyIfExists(cinematicMinZoomProp);
        DrawPropertyIfExists(cinematicMaxZoomProp);
        DrawPropertyIfExists(cinematicInitialZoomProp);

        ValidateZoom(cinematicMinZoomProp, cinematicMaxZoomProp);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Cinematic Composition", EditorStyles.boldLabel);
        DrawPropertyIfExists(cinematicHorizontalCenterOffsetProp);
    }

    /// <summary>
    /// Dibuja la configuración de transición.
    /// </summary>
    private void DrawTransitionSettings()
    {
        EditorGUILayout.LabelField("Transition Settings", EditorStyles.boldLabel);
        DrawPropertyIfExists(transitionDurationProp);
        DrawPropertyIfExists(transitionCurveProp);
    }

    /// <summary>
    /// Dibuja controles de depuración disponibles en Play Mode.
    /// </summary>
    private void DrawDebugControls()
    {
        EditorGUILayout.LabelField("Debug Controls", EditorStyles.boldLabel);

        CameraSystemController controller = (CameraSystemController)target;

        if (!Application.isPlaying)
        {
            RuntimeEditorGui.DrawInfo("Los controles de debug solo funcionan en Play Mode.");
            return;
        }

        RuntimeEditorGui.SetRuntimeEnabled(true);

        if (GUILayout.Button("Enter Exploration Mode"))
        {
            controller.EnterExplorationMode();
        }

        if (GUILayout.Button("Enter Cinematic Mode (Serialized Target)"))
        {
            controller.EnterCinematicMode();
        }

        EditorGUILayout.Space(5f);
        EditorGUILayout.LabelField("Custom Cinematic Test", EditorStyles.miniBoldLabel);
        debugOffset = EditorGUILayout.Vector3Field("Offset", debugOffset);

        if (GUILayout.Button("Enter Cinematic Mode (With Offset)"))
        {
            if (cinematicTargetProp != null && cinematicTargetProp.objectReferenceValue != null)
            {
                controller.EnterCinematicMode(
                    (Transform)cinematicTargetProp.objectReferenceValue,
                    debugOffset);
            }
            else
            {
                Debug.LogWarning("No hay cinematicTarget asignado.");
            }
        }

        if (GUILayout.Button("Exit Cinematic Mode"))
        {
            controller.ExitCinematicMode();
        }

        EditorGUILayout.Space(10f);
        DrawRuntimeState(controller);

        RuntimeEditorGui.ResetGuiEnabled();
    }

    /// <summary>
    /// Dibuja información de runtime del estado actual del sistema.
    /// </summary>
    private void DrawRuntimeState(CameraSystemController controller)
    {
        EditorGUILayout.LabelField("Runtime Info", EditorStyles.boldLabel);

        string currentState = controller.IsCinematicMode()
            ? "CINEMATIC"
            : "EXPLORATION";

        EditorGUILayout.HelpBox($"Estado actual: {currentState}", MessageType.None);
    }

    #endregion

    #region Validation

    /// <summary>
    /// Valida que el zoom mínimo no supere al zoom máximo.
    /// </summary>
    private void ValidateZoom(SerializedProperty minProp, SerializedProperty maxProp)
    {
        if (minProp == null || maxProp == null)
        {
            return;
        }

        if (minProp.floatValue > maxProp.floatValue)
        {
            RuntimeEditorGui.DrawWarning("El Zoom Min no puede ser mayor que Zoom Max.");
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Dibuja una propiedad serializada solo si existe.
    /// </summary>
    private void DrawPropertyIfExists(SerializedProperty property)
    {
        if (property == null)
        {
            return;
        }

        EditorGUILayout.PropertyField(property);
    }

    #endregion
}