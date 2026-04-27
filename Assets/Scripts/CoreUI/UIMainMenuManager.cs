using DG.Tweening;
using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manager global de navegación y UI.
/// Persiste entre escenas.
/// </summary>
public class UIMainMenuManager : MonoBehaviour
{

    [Header("Transition")]
    [SerializeField] private ScreenFade screenFade;

    [SerializeField] private float loadDelay = 1f;

    /// <summary>
    /// Indica si el sistema está en transición de escena.
    /// Bloquea cualquier input durante este estado.
    /// </summary>
    public bool IsTransitioning { get; private set; }

    void Start()
    {
        GamePlayStateController.Instance?.EnterMenu();
    }

    /// <summary>
    /// Carga escena con transición.
    /// </summary>
    public void OpenScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("SceneName inválido.");
            return;
        }

        if (screenFade == null)
        {
            Debug.LogWarning("ScreenFade no asignado.");
            SceneManager.LoadScene(sceneName);
            return;
        }

        screenFade.FadeIn(this.name);

        DOVirtual.DelayedCall(loadDelay, () =>
        {

            SceneManager.LoadScene(sceneName);
        });
    }
}