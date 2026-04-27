using DG.Tweening;
using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manager de navegación en gameplay.
/// Responsable de volver al menú y cargar escenas con transición.
/// </summary>
public class UIGameplayManager : MonoBehaviour
{
    public static UIGameplayManager Instance { get; private set; }

    [Header("Transition")]

    [SerializeField]
    [Tooltip("Componente encargado del fade de pantalla.")]
    private ScreenFade screenFade;

    [SerializeField]
    [Tooltip("Tiempo de espera antes de cargar la escena.")]
    private float loadDelay = 1f;

    [Header("Scenes")]

    [SerializeField]
    [Tooltip("Escena del menú principal.")]
    private SceneReference mainMenuScene;

    [SerializeField]
    [Tooltip("Nombre exacto de la escena de intro. Esta escena no fuerza estado de exploración.")]
    private string introSceneName = "Intro";

    /// <summary>
    /// Indica si el sistema está en transición de escena.
    /// </summary>
    public bool IsTransitioning { get; private set; }

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
        if (IsTransitioning)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            BackToMenu();
        }
    }

    /// <summary>
    /// Regresa al menú principal usando transición.
    /// </summary>
    public void BackToMenu()
    {
        if (mainMenuScene == null || string.IsNullOrEmpty(mainMenuScene.SceneName))
        {
            Debug.LogError("[UIGameplayManager] MainMenuScene no asignado.", this);
            return;
        }

        LoadScene(mainMenuScene.SceneName, name);
    }

    /// <summary>
    /// Carga una escena con transición y sincroniza el estado de gameplay después de cargar.
    /// </summary>
    public void LoadScene(string sceneName, string fromObject)
    {
        Debug.Log($"[UIGameplayManager] LoadScene llamado desde {fromObject} para cargar {sceneName}", this);

        if (IsTransitioning)
        {
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[UIGameplayManager] SceneName inválido.", this);
            return;
        }

        IsTransitioning = true;
        GamePlayStateController.Instance?.EnterTransition();

        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (screenFade == null)
        {
            Debug.LogWarning("[UIGameplayManager] ScreenFade no asignado.", this);
            SceneManager.LoadScene(sceneName);
            return;
        }

        screenFade.FadeIn(name);

        DOVirtual.DelayedCall(loadDelay, () =>
        {
            SceneManager.LoadScene(sceneName);
        });
    }

    /// <summary>
    /// Aplica el estado correcto después de cargar la nueva escena.
    /// </summary>
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        IsTransitioning = false;

        if (GamePlayStateController.Instance == null)
        {
            Debug.LogWarning("[UIGameplayManager] GamePlayStateController no encontrado.", this);
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