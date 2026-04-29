using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using Game.Save;

/// <summary>
/// Controlador de interfaz que muestra los pensamientos del sistema de consciencia.
///
/// Fix 1 — Re-escritura en cada cambio de escena:
/// <see cref="SaveSystem.OnSaveLoaded"/> se dispara en cada escena (porque
/// <see cref="ScenePersistenceController"/> llama <c>LoadGame()</c> en su Start).
/// Ahora <see cref="HandleSaveLoaded"/> solo hace el rebuild la PRIMERA VEZ.
/// Los cambios de escena posteriores no re-escriben el historial.
///
/// Fix 2 — Al cambiar idioma, reemplazar de golpe:
/// <see cref="RebuildFromConsciousnessAsync"/> resuelve TODOS los pensamientos
/// primero y luego asigna el texto completo en una sola operación, sin el efecto
/// de "aparición progresiva" que generaba el resolve pensamiento a pensamiento.
/// El efecto de tipeo solo aplica a pensamientos NUEVOS que llegan en tiempo real.
/// </summary>
public sealed class ThoughtUIManager : MonoBehaviour
{
    #region Serialized Fields

    [Header("Referencias UI")]

    [SerializeField]
    [Tooltip("Componente TMP_Text donde se mostrará el historial de pensamientos.")]
    private TMP_Text thoughtText;

    [Header("Configuración de escritura")]

    [SerializeField]
    [Tooltip("Velocidad de escritura en caracteres por segundo (solo para pensamientos nuevos).")]
    [Min(0.01f)]
    private float charactersPerSecond = 30f;

    [SerializeField]
    [Tooltip("Tiempo de espera entre pensamientos nuevos consecutivos.")]
    [Min(0f)]
    private float delayBetweenThoughts = 1f;

    [SerializeField]
    [Tooltip("Reconstruye el historial al re-activarse el componente.")]
    private bool rebuildHistoryOnEnable = true;

    [Header("Audio de escritura")]

    [SerializeField]
    private bool playTypingSound = true;

    [SerializeField]
    private SoundId typingSoundId = SoundId.Teclado;

    [SerializeField]
    [Range(0f, 1f)]
    private float typingSoundVolumeMultiplier = 0.252f;

    [SerializeField]
    [Min(0f)]
    private float typingSoundCooldown = 0.1f;

    [SerializeField]
    [Min(1)]
    private int charactersPerSound = 1;

    #endregion

    #region Private Fields

    private readonly Queue<string> thoughtQueue = new();
    private Coroutine processingCoroutine;
    private bool isProcessing;
    private float lastTypingSoundTime = float.NegativeInfinity;
    private bool isInitialized;
    private bool saveHasBeenLoaded;

    /// <summary>
    /// True una vez que el primer rebuild del historial se completó.
    /// Evita que <see cref="HandleSaveLoaded"/> haga rebuild en cada cambio de escena.
    /// </summary>
    private bool hasInitialRebuildDone;

    /// <summary>Token de cancelación para el tipeo por generación.</summary>
    private int typingGeneration;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        ConsciousnessSystem.OnThoughtAdded         += HandleThoughtAdded;
        LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
        SaveSystem.OnSaveLoaded                    += HandleSaveLoaded;

        if (isInitialized && saveHasBeenLoaded && rebuildHistoryOnEnable && hasInitialRebuildDone)
        {
            StartRebuild(instantaneous: false);
        }
    }

    private void Start()
    {
        isInitialized = true;

        if (saveHasBeenLoaded && !hasInitialRebuildDone)
        {
            hasInitialRebuildDone = true;
            StartRebuild(instantaneous: false);
        }
    }

    private void OnDisable()
    {
        ConsciousnessSystem.OnThoughtAdded         -= HandleThoughtAdded;
        LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        SaveSystem.OnSaveLoaded                    -= HandleSaveLoaded;

        CancelCurrentTyping();
        isProcessing = false;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Solo dispara el rebuild la primera vez que el save carga.
    /// Los reloads posteriores (por cambios de escena) se ignoran.
    /// </summary>
    private void HandleSaveLoaded()
    {
        saveHasBeenLoaded = true;

        if (!isInitialized || hasInitialRebuildDone)
        {
            return;
        }

        hasInitialRebuildDone = true;
        StartRebuild(instantaneous: false);
    }

    private void HandleThoughtAdded(ConsciousnessSystem.ThoughtData thought)
    {
        StartCoroutine(EnqueueResolvedThought(thought));
    }

    /// <summary>
    /// Al cambiar de idioma, reemplaza todo el texto de golpe sin efecto de tipeo.
    /// </summary>
    private void HandleLocaleChanged(Locale locale)
    {
        StartRebuild(instantaneous: true);
    }

    #endregion

    #region Private — Rebuild

    /// <summary>
    /// Cancela el tipeo activo y lanza el rebuild del historial.
    /// </summary>
    /// <param name="instantaneous">
    /// True → reemplaza todo el texto de una sola vez (para cambio de idioma).
    /// False → el historial aparece directamente pero sin efecto de tipeo por ser historial,
    /// reservando el tipeo solo para pensamientos nuevos en tiempo real.
    /// </param>
    private void StartRebuild(bool instantaneous)
    {
        CancelCurrentTyping();
        isProcessing = false;
        thoughtQueue.Clear();

        StartCoroutine(RebuildFromConsciousnessAsync(instantaneous));
    }

    private void CancelCurrentTyping()
    {
        typingGeneration++;

        if (processingCoroutine != null)
        {
            StopCoroutine(processingCoroutine);
            processingCoroutine = null;
        }
    }

    /// <summary>
    /// Reconstruye el historial completo.
    ///
    /// Cuando <paramref name="instantaneous"/> es true (cambio de idioma):
    /// resuelve TODOS los pensamientos primero, luego asigna el texto completo
    /// en UNA SOLA operación. Esto produce un reemplazo inmediato y limpio
    /// sin sensación de "escritura progresiva".
    ///
    /// Cuando es false (carga inicial): igual asignación directa pero
    /// puede mostrarse pensamiento a pensamiento sin typing.
    /// </summary>
    private IEnumerator RebuildFromConsciousnessAsync(bool instantaneous)
    {
        if (thoughtText == null || ConsciousnessSystem.Instance == null)
        {
            yield break;
        }

        IReadOnlyList<ConsciousnessSystem.ThoughtData> thoughts =
            ConsciousnessSystem.Instance.GetAllThoughts();

        if (thoughts.Count == 0)
        {
            thoughtText.text = string.Empty;
            yield break;
        }

        // Resuelve todos los textos primero.
        var resolved = new List<string>(thoughts.Count);

        for (int i = 0; i < thoughts.Count; i++)
        {
            ConsciousnessSystem.ThoughtData thought = thoughts[i];
            string text;

            if (thought.IsLocalized)
            {
                var handle = thought.ToLocalizedString().GetLocalizedStringAsync();
                yield return handle;

                if (!handle.IsDone || string.IsNullOrWhiteSpace(handle.Result))
                {
                    continue;
                }

                text = handle.Result;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(thought.RawText))
                {
                    continue;
                }

                text = thought.RawText;
            }

            resolved.Add(text);
        }

        if (resolved.Count == 0)
        {
            thoughtText.text = string.Empty;
            yield break;
        }

        // Construye el texto completo y lo asigna de una sola vez.
        // Esto es crítico para el cambio de idioma: evita que el texto
        // aparezca pensamiento a pensamiento dando sensación de re-escritura.
        var sb = new StringBuilder();
        for (int i = 0; i < resolved.Count; i++)
        {
            if (i > 0) sb.Append("\n\n");
            sb.Append(resolved[i]);
        }

        thoughtText.text = sb.ToString();
    }

    #endregion

    #region Private — Queue & Typing (pensamientos nuevos en tiempo real)

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

    private IEnumerator ProcessQueue()
    {
        isProcessing = true;

        while (thoughtQueue.Count > 0)
        {
            string nextThought = thoughtQueue.Dequeue();
            int generation = typingGeneration;

            yield return StartCoroutine(TypeThought($"\n\n{nextThought}", generation));

            if (delayBetweenThoughts > 0f)
            {
                yield return new WaitForSecondsRealtime(delayBetweenThoughts);
            }
        }

        isProcessing = false;
        processingCoroutine = null;
    }

    private IEnumerator TypeThought(string thought, int generation)
    {
        if (thoughtText == null) yield break;

        float secondsPerChar = 1f / charactersPerSecond;
        int visibleCount = 0;

        for (int i = 0; i < thought.Length; i++)
        {
            if (typingGeneration != generation) yield break;

            thoughtText.text += thought[i];
            visibleCount++;

            if (ShouldPlayTypingSound(thought[i], visibleCount))
            {
                PlayTypingSound();
            }

            yield return new WaitForSecondsRealtime(secondsPerChar);
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
}