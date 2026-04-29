using Game.Core;
using UnityEngine;

/// <summary>
/// Manager de navegación del menú principal.
/// Módulo 4: delega las transiciones de escena a <see cref="SceneTransitionController"/>
/// para obtener la pantalla de carga asíncrona con preload.
/// </summary>
public class UIMainMenuManager : MonoBehaviour
{
    [Header("Sprites de carga")]

    [SerializeField]
    [Tooltip("Sprite mostrado al entrar a un fragmento. Null = solo negro.")]
    private Sprite fragmentLoadingSprite;

    [Header("Fallback")]

    [SerializeField]
    [Tooltip("Componente de fade usado si SceneTransitionController no está disponible.")]
    private ScreenFade screenFade;

    [SerializeField]
    private float fallbackLoadDelay = 1f;

    private void Start()
    {
        GamePlayStateController.Instance?.EnterMenu();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Abre una escena con pantalla de carga asíncrona.
    /// Úsalo para cargar fragmentos desde el menú.
    /// </summary>
    public void OpenScene(string sceneName)
    {
        OpenScene(sceneName, fragmentLoadingSprite);
    }

    /// <summary>
    /// Abre una escena con sprite de carga específico.
    /// </summary>
    public void OpenScene(string sceneName, Sprite sprite)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[UIMainMenuManager] SceneName inválido.", this);
            return;
        }

        if (SceneTransitionController.Instance != null)
        {
            SceneTransitionController.Instance.LoadScene(sceneName, sprite);
        }
        else
        {
            Debug.LogWarning("[UIMainMenuManager] SceneTransitionController no disponible. Usando fallback.", this);
            FallbackOpenScene(sceneName);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fallback
    // ─────────────────────────────────────────────────────────────────────────

    private void FallbackOpenScene(string sceneName)
    {
        if (screenFade == null)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
            return;
        }

        screenFade.FadeIn(name);

        DG.Tweening.DOVirtual.DelayedCall(fallbackLoadDelay, () =>
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        });
    }
}