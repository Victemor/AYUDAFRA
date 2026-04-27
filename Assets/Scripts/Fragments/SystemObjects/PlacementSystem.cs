using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Maneja selección de slots y colocación.
/// </summary>
public class PlacementSystem : MonoBehaviour
{
    public static PlacementSystem Instance { get; private set; }

    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask containerLayer;

    private InventorySlotUI selectedSlot;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (selectedSlot == null)
            return;

        if (Input.GetMouseButtonDown(0))
            TryPlace();
    }

    public void SelectSlot(InventorySlotUI slot)
    {
        if (selectedSlot != null)
            selectedSlot.Deselect();

        selectedSlot = slot;
        selectedSlot.Select();
    }

    private void TryPlace()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, 200f, containerLayer))
        {
            CancelPlacement();
            return;
        }

        if (!hit.collider.TryGetComponent(out PlacementContainer container))
        {
            CancelPlacement();
            return;
        }

        if (container.IsOccupied)
        {
            CancelPlacement();
            return;
        }

        bool success = container.TryPlace(
            selectedSlot.StoredInstance,
            selectedSlot.StoredPrefab,
            out GameObject instance
        );

        if (success)
        {
            selectedSlot.Clear();
            selectedSlot = null;
        }
        else
        {
            CancelPlacement();
        }
    }

    private void CancelPlacement()
    {
        if (selectedSlot != null)
        {
            selectedSlot.Deselect();
            selectedSlot = null;
        }
    }
}