using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor personalizado para <see cref="EmotionalClimateController"/>.
/// Permite cambiar el perfil climático activo y probar su aplicación
/// directamente desde el Inspector durante ejecución.
/// </summary>
[CustomEditor(typeof(EmotionalClimateController))]
public sealed class EmotionalClimateControllerEditor : Editor
{
    private SerializedProperty bloomManagerProp;
    private SerializedProperty rainSystemProp;
    private SerializedProperty windSystemProp;
    private SerializedProperty fireManagerProp;
    private SerializedProperty cameraControllerProp;
    private SerializedProperty currentWeatherProp;
    private SerializedProperty transitionTimeProp;

    /// <summary>
    /// Cachea las propiedades serializadas para dibujo eficiente y seguro.
    /// </summary>
    private void OnEnable()
    {
        bloomManagerProp = serializedObject.FindProperty("bloomManager");
        rainSystemProp = serializedObject.FindProperty("rainSystem");
        windSystemProp = serializedObject.FindProperty("windSystem");
        fireManagerProp = serializedObject.FindProperty("fireManager");
        cameraControllerProp = serializedObject.FindProperty("cameraController");
        currentWeatherProp = serializedObject.FindProperty("currentWeather");
        transitionTimeProp = serializedObject.FindProperty("transitionTime");
    }

    /// <summary>
    /// Dibuja el inspector personalizado con controles de prueba en runtime.
    /// </summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawReferencesSection();
        EditorGUILayout.Space(6f);
        DrawWeatherSection();
        EditorGUILayout.Space(6f);
        DrawTransitionSection();
        EditorGUILayout.Space(10f);
        DrawRuntimeControls();

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Dibuja las referencias de sistemas del clima.
    /// </summary>
    private void DrawReferencesSection()
    {
        EditorGUILayout.LabelField("Referencias", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(bloomManagerProp);
        EditorGUILayout.PropertyField(rainSystemProp);
        EditorGUILayout.PropertyField(windSystemProp);
        EditorGUILayout.PropertyField(fireManagerProp);
        EditorGUILayout.PropertyField(cameraControllerProp);
    }

    /// <summary>
    /// Dibuja la sección del perfil climático activo.
    /// </summary>
    private void DrawWeatherSection()
    {
        EditorGUILayout.LabelField("Clima Activo", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(currentWeatherProp);

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();

            EmotionalClimateController controller = (EmotionalClimateController)target;

            if (Application.isPlaying)
            {
                WeatherProfile selectedWeather = currentWeatherProp.objectReferenceValue as WeatherProfile;

                if (selectedWeather != null)
                {
                    controller.SetWeather(selectedWeather);
                }
            }
        }
    }

    /// <summary>
    /// Dibuja la configuración de transición.
    /// </summary>
    private void DrawTransitionSection()
    {
        EditorGUILayout.LabelField("Transiciones", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(transitionTimeProp);
    }

    /// <summary>
    /// Dibuja botones utilitarios para pruebas durante ejecución.
    /// </summary>
    private void DrawRuntimeControls()
    {
        EmotionalClimateController controller = (EmotionalClimateController)target;

        EditorGUILayout.LabelField("Controles de Prueba", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Aplicar Current Weather"))
            {
                WeatherProfile selectedWeather = currentWeatherProp.objectReferenceValue as WeatherProfile;

                if (selectedWeather != null)
                {
                    controller.SetWeather(selectedWeather);
                }
                else
                {
                    Debug.LogWarning("[WeatherEditor] No hay un WeatherProfile asignado en Current Weather.");
                }
            }

            if (GUILayout.Button("Limpiar Clima"))
            {
                controller.ClearWeather();
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Los botones de prueba se habilitan únicamente en Play Mode. " +
                "Durante ejecución, cambiar Current Weather desde el inspector lo aplicará automáticamente.",
                MessageType.Info
            );
        }
    }
}