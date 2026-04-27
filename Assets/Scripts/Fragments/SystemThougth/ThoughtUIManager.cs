using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Controlador de interfaz encargado de representar visualmente los pensamientos
/// emitidos por el sistema de consciencia.
///
/// Orden de inicialización:
/// - OnEnable(): suscribe al evento de nuevos pensamientos. Solo reconstruye el
///   historial si ya pasó por Start() al menos una vez (re-activaciones posteriores).
/// - Start(): hace la reconstrucción inicial del historial desde el save.
///   Se hace aquí porque en este punto GameManager.Awake() ya corrió y
///   RestoreConsciousness() ya restauró los pensamientos al ConsciousnessSystem.
///
/// Responsabilidades:
/// - Escuchar nuevos pensamientos del sistema global.
/// - Encolar pensamientos para mostrarlos en orden.
/// - Escribir cada pensamiento con efecto de tipeo.
/// - Reproducir audio de tipeo de forma controlada.
/// - Reconstruir el historial visible al activarse.
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

    /// <summary>
    /// True a partir del primer Start(). Impide que OnEnable() reconstruya
    /// antes de que el save haya sido cargado.
    /// </summary>
    private bool isInitialized;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Suscribe al evento de nuevos pensamientos.
    /// Solo reconstruye el historial si ya pasamos por Start() (re-activaciones).
    /// El primer rebuild se hace en Start() para garantizar que el save ya fue cargado.
    /// </summary>
    private void OnEnable()
    {
        ConsciousnessSystem.OnThoughtAdded += HandleThoughtAdded;

        if (isInitialized && rebuildHistoryOnEnable)
        {
            RebuildFromConsciousness();
        }
    }

    /// <summary>
    /// Reconstruye el historial de pensamientos desde el save.
    /// En este punto todos los Awake() ya corrieron, incluyendo GameManager.Awake()
    /// que llama RestoreConsciousness(), por lo que ConsciousnessSystem ya tiene
    /// los pensamientos guardados.
    /// </summary>
    private void Start()
    {
        isInitialized = true;
        RebuildFromConsciousness();
    }

    private void OnDisable()
    {
        ConsciousnessSystem.OnThoughtAdded -= HandleThoughtAdded;

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
        if (string.IsNullOrWhiteSpace(thought.Text))
        {
            return;
        }

        thoughtQueue.Enqueue(thought.Text);

        if (!isProcessing)
        {
            processingCoroutine = StartCoroutine(ProcessQueue());
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Reconstruye en pantalla el historial actual de pensamientos almacenados
    /// por el sistema de consciencia.
    /// </summary>
    private void RebuildFromConsciousness()
    {
        if (thoughtText == null || ConsciousnessSystem.Instance == null)
        {
            return;
        }

        thoughtQueue.Clear();

        IReadOnlyList<ConsciousnessSystem.ThoughtData> thoughts = ConsciousnessSystem.Instance.GetAllThoughts();

        if (clearBeforeTyping)
        {
            thoughtText.text = string.Empty;
        }

        for (int i = 0; i < thoughts.Count; i++)
        {
            string currentText = thoughts[i].Text;

            if (string.IsNullOrWhiteSpace(currentText))
            {
                continue;
            }

            thoughtText.text += $"\n\n{currentText}";
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