using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Permite sacar objetos del contenedor y devolverlos al inventario.
/// </summary>
public class ContainerInteraction : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask containerLayer;
    [SerializeField] private List<InventorySlotUI> slots;

    private void Update()
    {
        if (Input.GetMouseButtonDown(1))
            TryRetrieve();
    }

    private void TryRetrieve()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, 200f, containerLayer))
            return;

        if (!hit.collider.TryGetComponent(out PlacementContainer container))
            return;

        if (!container.IsOccupied)
            return;

        GameObject obj = container.CurrentObject;

        if (!obj.TryGetComponent(out SavableItem item))
            return;

        InventorySlotUI freeSlot = GetFreeSlot();
        if (freeSlot == null)
            return;

        freeSlot.SetItem(obj, item.PrefabReference, item.InventorySprite);

        container.Clear();        // 🔑 liberar primero
        obj.SetActive(false);     // luego desactivar
    }

    private InventorySlotUI GetFreeSlot()
    {
        foreach (var slot in slots)
        {
            if (!slot.IsOccupied)
                return slot;
        }

        return null;
    }
}