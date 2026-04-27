using UnityEngine;

/// <summary>
/// Orquestador central de emociones.
/// Mezcla dos emociones en función del slider y aplica efectos visuales.
/// </summary>
public sealed class EmotionFeedbackManager : MonoBehaviour
{
    #region Serialized Fields

    [Header("Referencias")]

    [SerializeField]
    [Tooltip("UI que controla el valor del slider.")]
    private EmotionSliderUI sliderUI;

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
    private FireGroupController fireManager;

    [SerializeField]
    [Tooltip("Controlador de cámara (sway).")]
    private CinemachineController cameraController;

    [Header("Emociones Activas")]

    [SerializeField]
    [Tooltip("Emoción del lado izquierdo (0 → 0.5).")]
    private EmotionData emotionA;

    [SerializeField]
    [Tooltip("Emoción del lado derecho (0.5 → 1).")]
    private EmotionData emotionB;

    #endregion
    private const float NeutralThreshold = 0.01f;

    #region Unity Methods

    private void OnEnable()
    {
        if (sliderUI != null)
        {
            sliderUI.OnValueChanged += HandleSliderChanged;
        }
    }

    private void OnDisable()
    {
        if (sliderUI != null)
        {
            sliderUI.OnValueChanged -= HandleSliderChanged;
        }
    }

    private void Start()
    {
        if (sliderUI != null)
        {
            sliderUI.SetLabels(emotionA, emotionB);
        }

        // Estado inicial neutro
        HandleSliderChanged(0.5f);
    }

    #endregion

    #region Core Logic

    /// <summary>
    /// Convierte el valor del slider en intensidades A/B.
    /// Maneja el cambio del slider con zona neutra en el centro.
    /// </summary>
    private void HandleSliderChanged(float value)
    {
        // Detectar zona neutra (alrededor de 0.5)
        if (Mathf.Abs(value - 0.5f) <= NeutralThreshold)
        {
            ApplyNeutralState();
            return;
        }

        float a = Mathf.Clamp01(1f - (value / 0.5f));
        float b = Mathf.Clamp01((value - 0.5f) / 0.5f);

        ApplyFeedback(a, b);
    }
    /// <summary>
    /// Aplica el estado neutro (sin emociones activas).
    /// </summary>
    private void ApplyNeutralState()
    {
        // BLOOM
        if (bloomManager != null)
        {
            bloomManager.SetIntensity(0f);
            bloomManager.SetTint(Color.white);
        }

       
    }
    /// <summary>
    /// Evalúa una posición del slider y devuelve la emoción dominante.
    /// </summary>
    private EmotionResult EvaluateEmotion(float value)
    {
        // Zona neutra
        if (Mathf.Abs(value - 0.5f) <= NeutralThreshold)
        {
            // 🔥 elegir aleatoriamente A o B
            bool chooseA = Random.value > 0.5f;

            var chosenEmotion = chooseA ? emotionA : emotionB;

            // 🔥 intensidad media (decisión de diseño)
            float intensity = 0.5f;

            return new EmotionResult(chosenEmotion, intensity, false);
        }

        float a = Mathf.Clamp01(1f - (value / 0.5f));
        float b = Mathf.Clamp01((value - 0.5f) / 0.5f);

        if (a > b)
        {
            return new EmotionResult(emotionA, a, false);
        }
        else
        {
            return new EmotionResult(emotionB, b, false);
        }
    }
    /// <summary>
    /// Aplica todos los efectos visuales en función de las emociones.
    /// </summary>
    private void ApplyFeedback(float a, float b)
    {
        if (emotionA == null || emotionB == null)
            return;

        // -------------------------
        // BLOOM
        // -------------------------

        float bloom =
        emotionA.GetBloomIntensity(a) * a +
        emotionB.GetBloomIntensity(b) * b;

        if (bloomManager != null)
        {
            bloomManager.SetIntensity(bloom);

            Color blendedColor = Color.Lerp(emotionA.Color, emotionB.Color, b);
            bloomManager.SetTint(blendedColor);
        }

        // -------------------------
        // CÁMARA (SWAY)
        // -------------------------

        if (cameraController != null)
        {
            var swayA = emotionA.GetSway(a);
            var swayB = emotionB.GetSway(b);

            float swayX = swayA.x * a + swayB.x * b;
            float swayZ = swayA.z * a + swayB.z * b;
            float swaySpeed = swayA.speed * a + swayB.speed * b;

            cameraController.SetSway(swayX, swayZ, swaySpeed);
        }
        // -------------------------
        // LLUVIA
        // -------------------------

        if (rainSystem != null)
        {
            float rain =
                emotionA.GetRain(a) * a +
                emotionB.GetRain(b) * b;

            rainSystem.SetRainEmissionWithTimeTransition(rain, 0.5f);
        }
        // -------------------------
        // VIENTO
        // -------------------------

        if (windSystem != null)
        {
            float wind =
                emotionA.GetWind(a) * a +
                emotionB.GetWind(b) * b;

            windSystem.SetWindEmissionWithTransition(wind, 0.5f);
        }
        // -------------------------
        // FUEGO
        // -------------------------

        if (fireManager != null)
        {
            float fire =
                emotionA.GetFire(a) * a +
                emotionB.GetFire(b) * b;

            fireManager.SetFireIntensity(fire, 0.5f);
        }
    }
        
    #endregion

    #region Public API

    /// <summary>
    /// Permite cambiar dinámicamente el par de emociones.
    /// </summary>
    public void SetEmotions(EmotionData left, EmotionData right)
    {
        emotionA = left;
        emotionB = right;

        if (sliderUI != null)
        {
            sliderUI.SetLabels(emotionA, emotionB);
            sliderUI.SetValue(0.5f, false);
        }

        // Reaplicar estado actual
        HandleSliderChanged(0.5f);
    }
    /// <summary>
    /// Obtiene la emoción dominante y su intensidad según la posición actual del slider.
    /// No aplica efectos visuales.
    /// </summary>
    public EmotionResult GetCurrentEmotion()
    {
        if (sliderUI == null)
            return new EmotionResult(null, 0f, true);

        float value = sliderUI.GetValue();

        return EvaluateEmotion(value);
    }
    #endregion
    
}