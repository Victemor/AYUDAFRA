using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;
using Game.Core;

/// <summary>
/// Controlador global de transiciones de escena con pantalla de carga asíncrona.
///
/// ⚠️ SETUP OBLIGATORIO EN ESCENA:
/// El Canvas y el panel negro DEBEN ser hijos de este mismo GameObject.
/// Si están en otro objeto de escena, se destruyen al cargar la nueva escena
/// (LoadSceneMode.Single destruye toda la escena anterior).
/// Este GameObject es DontDestroyOnLoad, así que sus hijos sobreviven.
///
/// Jerarquía recomendada:
///   SceneTransitionController (este script)
///   └── LoadingCanvas (Canvas, CanvasScaler, GraphicRaycaster)
///       ├── BackgroundPanel (Image negro, CanvasGroup) ← asignar en loadingPanel
///       └── LoadingSprite (Image opcional)             ← asignar en loadingSprite
///
/// Flujo de transición:
/// 1. FadeIn  → pantalla negra visible (fadeInDuration)
/// 2. Carga asíncrona en background (allowSceneActivation = false)
/// 3. Espera hasta que AMBAS condiciones se cumplan:
///    - La escena alcanzó el 90% de carga (lista para activar)
///    - Transcurrió el minimumDisplayTime desde que comenzó la carga
/// 4. Activa la escena
/// 5. Espera postActivationDelay (sistemas de la nueva escena inicializan)
/// 6. FadeOut → nueva escena visible (fadeOutDuration)
/// 7. Oculta el panel
/// </summary>
public sealed class SceneTransitionController : MonoBehaviour
{
    public static SceneTransitionController Instance { get; private set; }

    [Header("Referencias UI")]

    [SerializeField]
    [Tooltip("CanvasGroup del panel negro. DEBE ser hijo de este GameObject.")]
    private CanvasGroup loadingPanel;

    [SerializeField]
    [Tooltip("Image donde se mostrará el sprite de carga opcional. DEBE ser hijo de este GameObject.")]
    private Image loadingSprite;

    [Header("Tiempos")]

    [SerializeField]
    [Tooltip("Tiempo mínimo que permanece la pantalla de carga visible en segundos. " +
             "Si la escena carga antes, espera hasta completar este tiempo. " +
             "Si tarda más, activa cuando esté lista.")]
    [Min(0f)]
    private float minimumDisplayTime = 10f;

    [SerializeField]
    [Tooltip("Duración del fade in (escena actual → negro).")]
    [Min(0f)]
    private float fadeInDuration = 0.4f;

    [SerializeField]
    [Tooltip("Duración del fade out (negro → nueva escena).")]
    [Min(0f)]
    private float fadeOutDuration = 0.4f;

    [SerializeField]
    [Tooltip("Segundos de espera DESPUÉS de activar la escena, antes del fade out. " +
             "Permite que los sistemas de la nueva escena (Awake/Start) terminen.")]
    [Min(0f)]
    private float postActivationDelay = 0.5f;

    [Header("Configuración")]

    [SerializeField]
    [Tooltip("Nombre exacto de la escena de menú principal.")]
    private string menuSceneName = "MainMenu";

    [Header("Debug")]

    [SerializeField]
    private bool debugLogs;

    /// <summary>True mientras hay una transición en progreso.</summary>
    public bool IsTransitioning { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Panel empieza invisible y sin bloquear input.
        ResetPanel();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inicia la transición con pantalla de carga asíncrona.
    /// </summary>
    /// <param name="sceneName">Nombre exacto de la escena a cargar.</param>
    /// <param name="sprite">Sprite visible durante la carga. Null = pantalla negra pura.</param>
    /// <param name="onComplete">Callback cuando la nueva escena ya está visible.</param>
    public void LoadScene(string sceneName, Sprite sprite = null, Action onComplete = null)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[SceneTransitionController] SceneName inválido.", this);
            return;
        }

        if (IsTransitioning)
        {
            Log("Transición ya en progreso — solicitud ignorada.");
            return;
        }

        StartCoroutine(TransitionRoutine(sceneName, sprite, onComplete));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Coroutine principal
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator TransitionRoutine(string sceneName, Sprite sprite, Action onComplete)
    {
        IsTransitioning = true;
        Log($"▶ Transición iniciada → '{sceneName}'");

        GamePlayStateController.Instance?.EnterTransition();

        // ── Fase 1: Fade In ───────────────────────────────────────────────────
        ShowPanel(sprite);
        yield return FadePanel(0f, 1f, fadeInDuration);
        yield return ShowSpriteAfterFade();
        Log("▶ Pantalla negra visible. Iniciando carga asíncrona.");

        // ── Fase 2: Carga asíncrona en background ─────────────────────────────
        float loadStartTime = Time.unscaledTime;

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        asyncLoad.allowSceneActivation = false;

        // Espera hasta que la escena esté lista (90%) Y haya pasado el tiempo mínimo.
        while (true)
        {
            float elapsed  = Time.unscaledTime - loadStartTime;
            bool  ready    = asyncLoad.progress >= 0.9f;
            bool  timeUp   = elapsed >= minimumDisplayTime;

            Log($"  Progreso: {asyncLoad.progress * 100f:F0}% | Tiempo: {elapsed:F1}s / {minimumDisplayTime}s");

            if (ready && timeUp)
            {
                break;
            }

            yield return null;
        }

        Log("▶ Escena lista Y tiempo mínimo cumplido. Activando escena.");

        // ── Fase 3: Activar la escena ─────────────────────────────────────────
        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        Log($"▶ Escena '{sceneName}' activa. Post-activation delay: {postActivationDelay}s");

        // ── Fase 4: Delay post-activación ─────────────────────────────────────
        if (postActivationDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(postActivationDelay);
        }

        ApplyGameStateForScene(sceneName);
        onComplete?.Invoke();

        // ── Fase 5: Fade Out del sprite y panel negro simultáneamente ─────────
        // El sprite y el negro desaparecen al mismo tiempo para evitar
        // el efecto de doble transición (negro → negro).
        yield return FadeOutSimultaneous(fadeOutDuration);

        ResetPanel();
        IsTransitioning = false;

        Log($"▶ Transición completada → '{sceneName}'");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Panel helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowPanel(Sprite sprite)
    {
        if (loadingPanel == null)
        {
            Debug.LogWarning("[SceneTransitionController] loadingPanel no asignado.", this);
            return;
        }

        loadingPanel.gameObject.SetActive(true);
        loadingPanel.alpha = 0f;
        loadingPanel.blocksRaycasts = true;

        // El sprite empieza invisible durante el fade in y aparece después.
        if (loadingSprite != null)
        {
            loadingSprite.sprite = sprite;
            loadingSprite.color  = Color.clear;
        }
    }

    /// <summary>
    /// Hace aparecer el sprite con fade tras el negro completo.
    /// </summary>
    private IEnumerator ShowSpriteAfterFade(float spriteFadeDuration = 0.3f)
    {
        if (loadingSprite == null || loadingSprite.sprite == null)
        {
            yield break;
        }

        if (spriteFadeDuration <= 0f)
        {
            loadingSprite.color = Color.white;
            yield break;
        }

        var tween = loadingSprite.DOFade(1f, spriteFadeDuration).SetUpdate(true);
        yield return tween.WaitForCompletion();
    }

    /// <summary>
    /// Desvanece el sprite antes de hacer el fade out del panel negro.
    /// Produce la secuencia: sprite desaparece → negro → fade out a la nueva escena.
    /// </summary>
    private IEnumerator FadeOutSimultaneous(float duration)
    {
        if (loadingPanel == null)
        {
            yield break;
        }

        // Lanza ambos tweens al mismo tiempo sin esperar uno antes que el otro.
        loadingPanel.DOFade(0f, duration).SetUpdate(true);

        if (loadingSprite != null && loadingSprite.sprite != null)
        {
            loadingSprite.DOFade(0f, duration).SetUpdate(true);
        }

        // Espera la duración una sola vez — ambos tweens corren en paralelo.
        yield return new WaitForSecondsRealtime(duration);
    }

    /// <summary>
    /// Oculta y resetea el panel. Seguro de llamar aunque el panel sea null.
    /// </summary>
    private void ResetPanel()
    {
        if (loadingPanel == null)
        {
            return;
        }

        loadingPanel.alpha = 0f;
        loadingPanel.blocksRaycasts = false;
        loadingPanel.gameObject.SetActive(false);

        if (loadingSprite != null)
        {
            loadingSprite.sprite = null;
            loadingSprite.color  = Color.clear;
        }
    }

    private IEnumerator FadePanel(float from, float to, float duration)
    {
        if (loadingPanel == null)
        {
            yield break;
        }

        loadingPanel.alpha = from;

        if (duration <= 0f)
        {
            loadingPanel.alpha = to;
            yield break;
        }

        // SetUpdate(true) usa tiempo no escalado → funciona con Time.timeScale = 0.
        var tween = loadingPanel.DOFade(to, duration).SetUpdate(true);
        yield return tween.WaitForCompletion();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Estado de juego
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyGameStateForScene(string sceneName)
    {
        if (GamePlayStateController.Instance == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(menuSceneName) && sceneName == menuSceneName)
        {
            GamePlayStateController.Instance.EnterMenu();
        }
        else
        {
            GamePlayStateController.Instance.EnterFreeExploration();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Debug
    // ─────────────────────────────────────────────────────────────────────────

    private void Log(string message)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log($"[SceneTransitionController] {message}", this);
    }
}