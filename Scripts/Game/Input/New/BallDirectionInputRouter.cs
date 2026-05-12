using UnityEngine;

/// <summary>
/// Conecta el eje horizontal del UnifiedBallInput con el SphereRotationController.
/// </summary>
public sealed class BallDirectionInputRouter : MonoBehaviour
{
    #region Inspector

    [SerializeField]
    [Tooltip("Input unificado de la escena.")]
    private UnifiedBallInput unifiedInput;

    [SerializeField]
    [Tooltip("Controlador de rotación de la esfera.")]
    private SphereRotationController rotationController;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        unifiedInput       = FindFirstObjectByType<UnifiedBallInput>();
        rotationController = GetComponent<SphereRotationController>();
    }

    private void Awake()
    {
        if (unifiedInput == null)
            unifiedInput = FindFirstObjectByType<UnifiedBallInput>();

        if (rotationController == null)
            rotationController = GetComponent<SphereRotationController>();
    }

    private void OnEnable()
    {
        if (unifiedInput != null)
            unifiedInput.OnDirectionInput += HandleDirectionInput;
    }

    private void OnDisable()
    {
        if (unifiedInput != null)
            unifiedInput.OnDirectionInput -= HandleDirectionInput;

        rotationController?.SetRotationInput(0f);
    }

    #endregion

    #region Private

    private void HandleDirectionInput(float horizontal)
    {
        rotationController?.SetRotationInput(horizontal);
    }

    #endregion
}