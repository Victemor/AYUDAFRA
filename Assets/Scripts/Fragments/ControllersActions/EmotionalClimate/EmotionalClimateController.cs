using UnityEngine;

/// <summary>
/// Orquestador central del sistema de clima.
/// Aplica un perfil de clima de forma determinística a los sistemas visuales.
/// Diseñado para ser robusto ante estados inconsistentes (ej. bloom sin intensidad).
/// </summary>
public sealed class EmotionalClimateController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Referencias")]

    [SerializeField]
    [Tooltip("Sistema de control de Bloom.")]
    private PostProcessingController bloomManager;

    [SerializeField]
    [Tooltip("Sistema de lluvia.")]
    private RainController rainSystem;

    [SerializeField]
    [Tooltip("Sistema de viento.")]
    private WindController windSystem;

    [SerializeField]
    [Tooltip("Sistema de fuego.")]
    private FireGroupController fireManager;

    [SerializeField]
    [Tooltip("Controlador de cámara (sway).")]
    private CinemachineController cameraController;

    [Header("Clima Activo")]

    [SerializeField]
    [Tooltip("Perfil de clima inicial.")]
    private WeatherProfile currentWeather;

    [Header("Transiciones")]

    [SerializeField]
    [Tooltip("Duración de transición para efectos dinámicos.")]
    private float transitionTime = 0.5f;

    #endregion

    #region Unity Methods

    private void Start()
    {
        // Espera un frame para evitar problemas de orden de ejecución
        StartCoroutine(ApplyOnStart());
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Aplica el clima inicial asegurando que todos los sistemas estén inicializados.
    /// </summary>
    private System.Collections.IEnumerator ApplyOnStart()
    {
        yield return null;

        ApplyWeather(currentWeather);
    }

    #endregion

    #region Core Logic

    /// <summary>
    /// Aplica completamente un perfil de clima.
    /// </summary>
    private void ApplyWeather(WeatherProfile weather)
    {
        if (weather == null)
        {
            Debug.LogWarning("[Weather] WeatherProfile es null.");
            return;
        }

        ApplyBloom(weather);
        ApplyRain(weather);
        ApplyWind(weather);
        ApplyFire(weather);
        ApplyCamera(weather);

        Debug.Log($"[Weather] Applied profile: {weather.name}");
    }

    /// <summary>
/// Limpia el clima actual y deja todos los sistemas en estado neutro.
/// </summary>
    public void ClearWeather()
    {
        ApplyBloomNeutral();
        ApplyRainNeutral();
        ApplyWindNeutral();
        ApplyFireNeutral();
        ApplyCameraNeutral();

        currentWeather = null;

        Debug.Log("[Weather] Cleared");
    }

    private void ApplyBloomNeutral()
    {
        if (bloomManager == null) return;

        bloomManager.SetIntensity(0f);
        bloomManager.SetTint(Color.white);
    }

    private void ApplyRainNeutral()
    {
        if (rainSystem == null) return;

        rainSystem.SetRainEmissionWithTimeTransition(0f, transitionTime);
    }

    private void ApplyWindNeutral()
    {
        if (windSystem == null) return;

        windSystem.SetWindEmissionWithTransition(0f, transitionTime);
    }

    private void ApplyFireNeutral()
    {
        if (fireManager == null) return;

        fireManager.SetFireIntensity(0f, transitionTime);
    }

    private void ApplyCameraNeutral()
    {
        if (cameraController == null) return;

        cameraController.SetSway(0f, 0f, 0f);
    }

    /// <summary>
    /// Aplica configuración de Bloom asegurando visibilidad del color.
    /// </summary>
    private void ApplyBloom(WeatherProfile weather)
    {
        if (bloomManager == null)
            return;

        float intensity = weather.BloomIntensity;

        // Evita estado inconsistente: color activo sin intensidad
        if (intensity <= 0f)
        {
            bloomManager.SetIntensity(0f);
            bloomManager.SetTint(Color.white);
            return;
        }

        // Orden importante (igual que sistema emocional)
        bloomManager.SetIntensity(intensity);
        bloomManager.SetTint(weather.BloomColor);

        Debug.Log($"[Weather] Bloom → Intensity: {intensity}, Color: {weather.BloomColor}");
    }

    /// <summary>
    /// Aplica configuración de lluvia.
    /// </summary>
    private void ApplyRain(WeatherProfile weather)
    {
        if (rainSystem == null)
            return;

        rainSystem.SetRainEmissionWithTimeTransition(
            weather.RainIntensity,
            transitionTime
        );
    }

    /// <summary>
    /// Aplica configuración de viento.
    /// </summary>
    private void ApplyWind(WeatherProfile weather)
    {
        if (windSystem == null)
            return;

        windSystem.SetWindEmissionWithTransition(
            weather.WindIntensity,
            transitionTime
        );
    }

    /// <summary>
    /// Aplica configuración de fuego.
    /// </summary>
    private void ApplyFire(WeatherProfile weather)
    {
        if (fireManager == null)
            return;

        fireManager.SetFireIntensity(
            weather.FireIntensity,
            transitionTime
        );
    }

    /// <summary>
    /// Aplica configuración de sway de cámara.
    /// </summary>
    private void ApplyCamera(WeatherProfile weather)
    {
        if (cameraController == null)
            return;

        cameraController.SetSway(
            weather.SwayX,
            weather.SwayZ,
            weather.SwaySpeed
        );
    }

    #endregion

    #region Public API

    /// <summary>
    /// Cambia el clima actual y lo aplica inmediatamente.
    /// </summary>
    public void SetWeather(WeatherProfile newWeather)
    {
        if (newWeather == null)
        {
            Debug.LogWarning("[Weather] Intento de aplicar un WeatherProfile null.");
            return;
        }

        currentWeather = newWeather;
        ApplyWeather(currentWeather);
    }

    #endregion
}