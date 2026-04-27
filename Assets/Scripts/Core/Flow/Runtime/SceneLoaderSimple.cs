using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// Servicio global para carga de escenas.
/// No depende de UI.
/// </summary>
public class SceneLoaderSimple : MonoBehaviour
{
    public static SceneLoaderSimple Instance { get; private set; }

    [SerializeField] private float loadDelay = 0.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Carga una escena de forma segura.
    /// </summary>
    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("SceneName inválido");
            return;
        }

        DOVirtual.DelayedCall(loadDelay, () =>
        {
            SceneManager.LoadScene(sceneName);
        });
    }
}