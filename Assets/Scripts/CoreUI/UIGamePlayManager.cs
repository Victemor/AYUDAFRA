using DG.Tweening;
using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manager de navegación en gameplay.
/// Módulo 4: delega las transiciones de escena a <see cref="SceneTransitionController"/>
/// para obtener la pantalla de carga asíncrona con preload.
/// </summary>
public class UIGameplayManager : MonoBehaviour
{
    public static UIGameplayManager Instance { get; private set; }

    [Header("Transition")]

    [SerializeField]
    [Tooltip("Sprite mostrado durante la carga al volver al menú. Null = solo negro.")]
    private Sprite menuLoadingSprite;

    [SerializeField]
    [Tooltip("Sprite mostrado al entrar a un fragmento. Null = solo negro.")]
    private Sprite fragmentLoadingSprite;

    [Header("Scenes")]

    [SerializeField]
    [Tooltip("Nombre exacto de la escena del menú principal.")]
    private SceneReference mainMenuScene;

    [SerializeField]
    [Tooltip("Nombre exacto de la escena de intro (no fuerza estado de exploración).")]
    private string introSceneName = "Intro";

    [Header("Fade (fallback sin SceneTransitionController)")]

    [SerializeField]
    [Tooltip("Componente de fade usado si SceneTransitionController no está disponible.")]
    private ScreenFade screenFade;

    /// <summary>
    /// True mientras hay una transición en progreso.
    /// Delega a SceneTransitionController para mantener compatibilidad
    /// con DropController y cualquier otro sistema que lo consulte.
    /// </summary>
    public bool IsTransitioning =>
        SceneTransitionController.Instance != null &&
        SceneTransitionController.Instance.IsTransitioning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (screenFade != null)
        {
            screenFade.FadeOut();
        }
    }

    private void Update()
    {
        if (SceneTransitionController.Instance != null && SceneTransitionController.Instance.IsTransitioning)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            BackToMenu();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Regresa al menú principal con pantalla de carga.
    /// </summary>
    public void BackToMenu()
    {
        if (mainMenuScene == null || string.IsNullOrEmpty(mainMenuScene.SceneName))
        {
            Debug.LogError("[UIGameplayManager] MainMenuScene no asignado.", this);
            return;
        }

        LoadScene(mainMenuScene.SceneName, menuLoadingSprite);
    }

    /// <summary>
    /// Carga una escena con pantalla de carga asíncrona.
    /// </summary>
    public void LoadScene(string sceneName, Sprite sprite = null)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[UIGameplayManager] SceneName inválido.", this);
            return;
        }

        if (SceneTransitionController.Instance != null)
        {
            SceneTransitionController.Instance.LoadScene(sceneName, sprite);
        }
        else
        {
            // Fallback al sistema anterior si el controller no está disponible.
            Debug.LogWarning("[UIGameplayManager] SceneTransitionController no disponible. Usando fallback.", this);
            FallbackLoadScene(sceneName);
        }
    }

    /// <summary>
    /// Carga un fragmento (escena de gameplay) con el sprite de fragmento.
    /// </summary>
    public void LoadFragment(string sceneName)
    {
        LoadScene(sceneName, fragmentLoadingSprite);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fallback
    // ─────────────────────────────────────────────────────────────────────────

    private void FallbackLoadScene(string sceneName)
    {
        if (screenFade != null)
        {
            screenFade.FadeIn(name);
        }

        SceneManager.sceneLoaded += HandleFallbackSceneLoaded;
        SceneManager.LoadScene(sceneName);
    }

    private void HandleFallbackSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= HandleFallbackSceneLoaded;

        if (GamePlayStateController.Instance == null)
        {
            return;
        }

        if (mainMenuScene != null && scene.name == mainMenuScene.SceneName)
        {
            GamePlayStateController.Instance.EnterMenu();
            return;
        }

        if (scene.name == introSceneName)
        {
            return;
        }

        GamePlayStateController.Instance.EnterFreeExploration();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (mainMenuScene != null)
        {
            mainMenuScene.Validate();
        }
    }
#endif
}