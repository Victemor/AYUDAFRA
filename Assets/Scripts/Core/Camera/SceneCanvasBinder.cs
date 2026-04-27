using UnityEngine;

/// <summary>
/// Componente responsable de notificar al sistema global de gameplay
/// que la escena ya está inicializada y lista para asignar referencias dependientes,
/// como la cámara en los canvas.
///
/// Este script debe existir en cada escena jugable.
/// </summary>
public sealed class SceneCanvasBinder : MonoBehaviour
{
     [SerializeField] private  Camera sceneCamera ;
     [SerializeField] private  bool enableConsciousnessSystem = true ;
    #region Unity Lifecycle

    /// <summary>
    /// Ejecutado al inicio de la escena.
    /// Se asegura de que la cámara principal ya esté disponible antes de asignarla.
    /// </summary>
    private void Start()
    {
        BindCanvasToSceneCamera();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Invoca la asignación de la cámara en los canvas del sistema global.
    /// Incluye validación defensiva del singleton.
    /// </summary>
    private void BindCanvasToSceneCamera()
    {
        Debug.LogWarning(
                "Intentando asignar canvas"
            );
        if (SystemsGameplay.Instance == null)
        {
            Debug.LogWarning(
                "SystemsGameplay no está inicializado en la escena. " +
                "No se pueden asignar las cámaras a los canvas."
            );
            return;
        }

        SystemsGameplay.Instance.AssignCameraToCanvases(sceneCamera);
        SystemsGameplay.Instance.EnableConsciousnessSystem(enableConsciousnessSystem);
    }

    #endregion
}