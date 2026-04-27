using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Guarda objetos de la escena en slots mediante click derecho.
/// </summary>
public class SystemSaveSlot : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask savableLayers;
    [SerializeField] private List<InventorySlotUI> slots = new(4);

    private void Update()
    {
        if (Input.GetMouseButtonDown(1))
            TrySave();
    }

    private void TrySave()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        RaycastHit[] hits = Physics.RaycastAll(ray, 200f, savableLayers);

        if (hits.Length == 0)
            return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.collider.TryGetComponent(out SavableItem item))
            {
                SaveItem(item);
                return;
            }
        }
    }

    private void SaveItem(SavableItem item)
    {
        InventorySlotUI slot = GetFreeSlot();
        if (slot == null)
            return;

        slot.SetItem(item.gameObject, item.PrefabReference, item.InventorySprite);
        item.gameObject.SetActive(false);
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