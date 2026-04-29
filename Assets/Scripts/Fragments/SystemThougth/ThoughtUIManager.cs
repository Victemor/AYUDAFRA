using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// Controlador de interfaz encargado de representar visualmente los pensamientos
/// emitidos por el sistema de consciencia.
///
/// Soporta dos rutas según el tipo de <see cref="ConsciousnessSystem.ThoughtData"/>:
/// - <b>Localizada</b>: resuelve la <see cref="LocalizedString"/> async al idioma activo.
/// - <b>Raw</b>: usa <c>RawText</c> directamente (contenido aún no migrado).
///
/// Al cambiar de idioma, solo los pensamientos localizados se re-resuelven;
/// los raw se muestran tal cual fueron guardados.
/// </summary>
public sealed class ThoughtUIManager : MonoBehaviour
{
    #region Serialized Fields

    [Header("Referencias UI")]

    [SerializeField]
    [Tooltip("Componente de texto donde se mostrará el historial de pensamientos.")]
    private TMP_Text thoughtText;

    [Header("Configuración de escritura")]

    [SerializeField]
    [Tooltip("Velocidad de escritura expresada en caracteres por segundo.")]
    [Min(0.01f)]
    private float charactersPerSecond = 30f;

    [SerializeField]
    [Tooltip("Tiempo de espera entre la finalización de un pensamiento y el inicio del siguiente.")]
    [Min(0f)]
    private float delayBetweenThoughts = 1f;

    [SerializeField]
    [Tooltip("Indica si el texto debe limpiarse antes de reconstruir o comenzar una nueva escritura visual.")]
    private bool clearBeforeTyping = true;

    [SerializeField]
    [Tooltip("Indica si al re-activarse el componente debe reconstruir el historial.")]
    private bool rebuildHistoryOnEnable = true;

    [Header("Audio de escritura")]

    [SerializeField]
    [Tooltip("Indica si debe reproducirse sonido durante el efecto de tipeo.")]
    private bool playTypingSound = true;

    [SerializeField]
    [Tooltip("Identificador de sonido usado para el tipeo.")]
    private SoundId typingSoundId = SoundId.Teclado;

    [SerializeField]
    [Tooltip("Multiplicador de volumen aplicado al sonido de tipeo.")]
    [Range(0f, 1f)]
    private float typingSoundVolumeMultiplier = 0.252f;

    [SerializeField]
    [Tooltip("Tiempo mínimo entre sonidos consecutivos de tipeo.")]
    [Min(0f)]
    private float typingSoundCooldown = 0.1f;

    [SerializeField]
    [Tooltip("Cantidad mínima de caracteres visibles entre sonidos de tipeo.")]
    [Min(1)]
    private int charactersPerSound = 1;

    #endregion

    #region Private Fields

    private readonly Queue<string> thoughtQueue = new();
    private Coroutine processingCoroutine;
    private bool isProcessing;
    private float lastTypingSoundTime = float.NegativeInfinity;
    private bool isInitialized;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        ConsciousnessSystem.OnThoughtAdded += HandleThoughtAdded;
        LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;

        if (isInitialized && rebuildHistoryOnEnable)
        {
            StartRebuild();
        }
    }

    private void Start()
    {
        isInitialized = true;
        StartRebuild();
    }

    private void OnDisable()
    {
        ConsciousnessSystem.OnThoughtAdded -= HandleThoughtAdded;
        LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;

        if (processingCoroutine != null)
        {
            StopCoroutine(processingCoroutine);
            processingCoroutine = null;
        }

        isProcessing = false;
    }

    #endregion

    #region Event Handlers

    private void HandleThoughtAdded(ConsciousnessSystem.ThoughtData thought)
    {
        StartCoroutine(EnqueueResolvedThought(thought));
    }

    /// <summary>
    /// Al cambiar de idioma, reconstruye solo los pensamientos localizados
    /// con el nuevo idioma activo. Los pensamientos raw se mantienen igual.
    /// </summary>
    private void HandleLocaleChanged(Locale locale)
    {
        StartRebuild();
    }

    #endregion

    #region Private Methods

    private void StartRebuild()
    {
        if (processingCoroutine != null)
        {
            StopCoroutine(processingCoroutine);
            processingCoroutine = null;
        }

        isProcessing = false;
        thoughtQueue.Clear();

        StartCoroutine(RebuildFromConsciousnessAsync());
    }

    /// <summary>
    /// Reconstruye el historial completo. Para cada pensamiento:
    /// - Si es localizado → resuelve async al idioma activo.
    /// - Si es raw → usa el texto plano directamente.
    /// </summary>
    private IEnumerator RebuildFromConsciousnessAsync()
    {
        if (thoughtText == null || ConsciousnessSystem.Instance == null)
        {
            yield break;
        }

        if (clearBeforeTyping)
        {
            thoughtText.text = string.Empty;
        }

        IReadOnlyList<ConsciousnessSystem.ThoughtData> thoughts =
            ConsciousnessSystem.Instance.GetAllThoughts();

        for (int i = 0; i < thoughts.Count; i++)
        {
            ConsciousnessSystem.ThoughtData thought = thoughts[i];
            string resolvedText;

            if (thought.IsLocalized)
            {
                var handle = thought.ToLocalizedString().GetLocalizedStringAsync();
                yield return handle;

                if (!handle.IsDone || string.IsNullOrWhiteSpace(handle.Result))
                {
                    continue;
                }

                resolvedText = handle.Result;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(thought.RawText))
                {
                    continue;
                }

                resolvedText = thought.RawText;
            }

            thoughtText.text += $"\n\n{resolvedText}";
        }
    }

    /// <summary>
    /// Resuelve un pensamiento (localizado o raw) y lo encola para el efecto de tipeo.
    /// </summary>
    private IEnumerator EnqueueResolvedThought(ConsciousnessSystem.ThoughtData thought)
    {
        string resolvedText;

        if (thought.IsLocalized)
        {
            var handle = thought.ToLocalizedString().GetLocalizedStringAsync();
            yield return handle;

            if (!handle.IsDone || string.IsNullOrWhiteSpace(handle.Result))
            {
                yield break;
            }

            resolvedText = handle.Result;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(thought.RawText))
            {
                yield break;
            }

            resolvedText = thought.RawText;
        }

        thoughtQueue.Enqueue(resolvedText);

        if (!isProcessing)
        {
            processingCoroutine = StartCoroutine(ProcessQueue());
        }
    }

    private bool ShouldPlayTypingSound(char character, int visibleCharacterCount)
    {
        if (!playTypingSound) return false;
        if (char.IsWhiteSpace(character)) return false;
        if (visibleCharacterCount <= 0) return false;
        if (visibleCharacterCount % charactersPerSound != 0) return false;
        if (Time.unscaledTime - lastTypingSoundTime < typingSoundCooldown) return false;
        return true;
    }

    private void PlayTypingSound()
    {
        AudioManager.PlaySfx(typingSoundId, typingSoundVolumeMultiplier);
        lastTypingSoundTime = Time.unscaledTime;
    }

    #endregion

    #region Coroutines

    private IEnumerator ProcessQueue()
    {
        isProcessing = true;

        while (thoughtQueue.Count > 0)
        {
            string nextThought = thoughtQueue.Dequeue();

            yield return StartCoroutine(TypeThought($"\n\n{nextThought}"));

            if (delayBetweenThoughts > 0f)
            {
                yield return new WaitForSecondsRealtime(delayBetweenThoughts);
            }
        }

        isProcessing = false;
        processingCoroutine = null;
    }

    private IEnumerator TypeThought(string thought)
    {
        if (thoughtText == null) yield break;

        float secondsPerChar = 1f / charactersPerSecond;
        int visibleCount = 0;

        for (int i = 0; i < thought.Length; i++)
        {
            thoughtText.text += thought[i];
            visibleCount++;

            if (ShouldPlayTypingSound(thought[i], visibleCount))
            {
                PlayTypingSound();
            }

            yield return new WaitForSecondsRealtime(secondsPerChar);
        }
    }

    #endregion
}