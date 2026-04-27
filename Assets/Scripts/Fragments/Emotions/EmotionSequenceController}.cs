using System.Collections;
using Game.Core;
using UnityEngine;

/// <summary>
/// Controlador de secuencias emocionales.
/// Orquesta la visualización del popup, el temporizador y la configuración del sistema emocional.
/// No contiene lógica emocional; delega en EmotionFeedbackManager.
/// </summary>
public sealed class EmotionSequenceController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Core")]

    [SerializeField]
    [Tooltip("Sistema principal de emociones.")]
    private EmotionFeedbackManager emotionManager;

    [SerializeField]
    [Tooltip("Sistema de temporizador visual.")]
    private TimerSliderUI timerUI;

    [Header("Popup")]

    [SerializeField]
    [Tooltip("CanvasGroup usado para controlar visibilidad del popup.")]
    private CanvasGroup popupCanvasGroup;

    [Header("Configuración")]

    [SerializeField]
    [Tooltip("Duración de la secuencia en segundos.")]
    private float duration = 5f;

    [SerializeField]
    [Tooltip("Duración del fade del popup.")]
    private float fadeDuration = 0.4f;

    #endregion

    #region Private Fields

    private Coroutine activeSequence;
    private bool isRunning;

    #endregion

    #region Public API

    /// <summary>
    /// Inicia una nueva secuencia emocional con dos emociones.
    /// </summary>
    public void StartSequence(EmotionData emotionA, EmotionData emotionB)
    {
        GamePlayStateController.Instance.EnterEmotionSelection();
        if (emotionManager == null || timerUI == null || popupCanvasGroup == null)
        {
            Debug.LogWarning("EmotionSequenceController: Referencias incompletas.");
            return;
        }

        if (activeSequence != null)
        {
            StopCoroutine(activeSequence);
        }

        activeSequence = StartCoroutine(SequenceRoutine(emotionA, emotionB));
    }
    /// <summary>
    /// Permite configurar la duración de la secuencia en runtime.
    /// Se asegura de mantener valores válidos.
    /// </summary>
    public void SetDuration(float newDuration)
    {
        duration = Mathf.Max(0.1f, newDuration);
    }
    /// <summary>
    /// Se invoca cuando termina la selección emocional.
    /// </summary>
    public event System.Action<EmotionResult> OnEmotionSelected;
    /// <summary>
    /// Obtiene la emoción actual desde el sistema emocional.
    /// </summary>
    public EmotionResult GetCurrentEmotion()
    {
        if (emotionManager == null)
            return new EmotionResult(null, 0f, true);

        return emotionManager.GetCurrentEmotion();
    }
    
    
    #endregion

    #region Core Logic

    /// <summary>
    /// Rutina principal de la secuencia emocional.
    /// </summary>
    private IEnumerator SequenceRoutine(EmotionData emotionA, EmotionData emotionB)
    {
        isRunning = true;

        // Configurar emociones
        emotionManager.SetEmotions(emotionA, emotionB);

        // Mostrar popup
        yield return FadeCanvas(0f, 1f);

        // Iniciar timer
        bool finished = false;

        void OnTimerFinished() => finished = true;

        timerUI.OnTimerCompleted += OnTimerFinished;
        timerUI.StartTimer(duration);

        // Esperar fin del timer
        while (!finished)
        {
            yield return null;
        }

        timerUI.OnTimerCompleted -= OnTimerFinished;

        EmotionResult result = GetCurrentEmotion();

        OnEmotionSelected?.Invoke(result);

        // Ocultar popup
        yield return FadeCanvas(1f, 0f);

        isRunning = false;
        activeSequence = null;
    }

    /// <summary>
    /// Realiza transición de alpha en el popup.
    /// </summary>
    private IEnumerator FadeCanvas(float from, float to)
    {
        float elapsed = 0f;

        popupCanvasGroup.alpha = from;
        popupCanvasGroup.interactable = false;
        popupCanvasGroup.blocksRaycasts = false;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;

            popupCanvasGroup.alpha = Mathf.Lerp(from, to, t);

            yield return null;
        }

        popupCanvasGroup.alpha = to;

        bool visible = to > 0.9f;
        popupCanvasGroup.interactable = visible;
        popupCanvasGroup.blocksRaycasts = visible;
    }

    #endregion
}