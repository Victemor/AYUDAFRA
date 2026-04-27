using UnityEngine;
using Game.Runtime;

/// <summary>
/// Sistema centralizado de interacción con objetos del mundo mediante raycast.
/// Desacopla input de lógica de interacción.
/// </summary>
public class WorldInteractionSystem : MonoBehaviour
{
    [Header("Referencias")]

    [Tooltip("Cámara principal utilizada para raycasts.")]
    [SerializeField] private Camera mainCamera;

    [Header("Configuración")]

    [Tooltip("Capas válidas para interacción.")]
    [SerializeField] private LayerMask interactableLayer;

    [Tooltip("Distancia máxima del raycast.")]
    [SerializeField] private float rayDistance = 100f;

    private void Update()
    {
        HandleInput();
    }

    /// <summary>
    /// Detecta input de click y ejecuta interacción.
    /// </summary>
    private void HandleInput()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        TryInteract();
    }

    /// <summary>
    /// Lanza raycast e intenta interactuar con el objeto bajo el cursor.
    /// Si la interacción tiene éxito, notifica al sistema de tutoriales.
    /// </summary>
    private void TryInteract()
    {
        if (mainCamera == null)
        {
            Debug.LogError("[WorldInteractionSystem] Camera no asignada");
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, interactableLayer))
        {
            Debug.Log("[Interaction] No hit");
            return;
        }

        IInteractable interactable = hit.collider.GetComponent<IInteractable>();

        if (interactable == null)
        {
            Debug.Log("[Interaction] No IInteractable");
            return;
        }

        if (!interactable.CanInteract())
        {
            Debug.Log("[Interaction] Cannot interact");
            return;
        }

        Debug.Log($"[Interaction] CLICK → {hit.collider.name}");

        interactable.Interact();

        // Notifica al sistema de tutoriales que el jugador interactuó con un objeto.
        GameEvents.RaiseInteractableClicked();
    }
}