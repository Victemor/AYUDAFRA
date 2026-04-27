using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sistema global que gestiona subsistemas persistentes de gameplay.
/// Se encarga de reconfigurar referencias dependientes de escena (como cámaras).
/// </summary>
public class SystemsGameplay : MonoBehaviour
{
    public static SystemsGameplay Instance { get; private set; }

    [Header("Systems")]

    [SerializeField] private ConsciousnessSystem consciousnessSystem;
    [SerializeField] private TutorialController tutorialController;

    [Header("Canvas References")]

    [Tooltip("Canvas del sistema de conciencia.")]
    [SerializeField] private Canvas consciousnessCanvas;

    [Tooltip("Canvas del sistema de tutorial.")]
    [SerializeField] private Canvas tutorialCanvas;

    private void Awake()
    {
        InitializeSingleton();
    }


    /// <summary>
    /// Inicializa el singleton persistente.
    /// </summary>
    private void InitializeSingleton()
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
    /// Busca la cámara principal de la escena y la asigna a los canvas.
    /// </summary>
    public void AssignCameraToCanvases(Camera sceneCamera)
    {

        ConfigureCanvas(consciousnessCanvas, sceneCamera);
        //ConfigureCanvas(tutorialCanvas, sceneCamera);
    }

    public void EnableConsciousnessSystem(bool enable)
    {
        consciousnessCanvas.gameObject.SetActive(enable);
    }

    /// <summary>
    /// Configura un canvas para usar una cámara específica.
    /// </summary>
    private void ConfigureCanvas(Canvas canvas, Camera cam)
    {
          Debug.LogWarning(
                "Intentando asignar canvas en el sistema"
            );
        if (canvas == null)
            return;
Debug.LogWarning(
                "Intentando asignar canvas en el sistema 2"
            );

            canvas.worldCamera = cam;
        
    }

    /// <summary>
    /// Obtiene el sistema de conciencia.
    /// </summary>
    public ConsciousnessSystem GetConsciousnessSystem()
    {
        return consciousnessSystem;
    }

    /// <summary>
    /// Obtiene el sistema de tutorial.
    /// </summary>
    public TutorialController GetTutorialController()
    {
        return tutorialController;
    }
}