using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestiona la selección global de slots de inventario.
/// Controla el estado visual (alpha) de todos los slots registrados.
/// </summary>
public class InventorySelectionManager : MonoBehaviour
{
    public static InventorySelectionManager Instance { get; private set; }

    [Header("Visual Settings")]
    
    [Tooltip("Alpha aplicado a los slots NO seleccionados.")]
    [Range(0f, 1f)]
    [SerializeField] private float unselectedAlpha = 0.3f;

    private readonly List<InventorySlotUI> registeredSlots = new();

    /// <summary>
    /// Slot actualmente seleccionado.
    /// </summary>
    public InventorySlotUI CurrentSelected { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// Registra un slot en el sistema.
    /// </summary>
    public void RegisterSlot(InventorySlotUI slot)
    {
        if (slot == null || registeredSlots.Contains(slot))
            return;

        registeredSlots.Add(slot);
    }

    /// <summary>
    /// Desregistra un slot.
    /// </summary>
    public void UnregisterSlot(InventorySlotUI slot)
    {
        if (slot == null)
            return;

        registeredSlots.Remove(slot);
    }

    /// <summary>
    /// Define un slot como seleccionado y actualiza el alpha del resto.
    /// </summary>
    public void SelectSlot(InventorySlotUI selectedSlot)
    {
        CurrentSelected = selectedSlot;

        foreach (var slot in registeredSlots)
        {
            if (slot == null)
                continue;

            if (!slot.IsOccupied)
                continue;

            float targetAlpha = slot == selectedSlot ? 1f : unselectedAlpha;
            slot.SetVisualAlpha(targetAlpha);
        }
    }

    /// <summary>
    /// Resetea todos los slots a alpha normal.
    /// </summary>
    public void ResetVisuals()
    {
        foreach (var slot in registeredSlots)
        {
            if (slot == null || !slot.IsOccupied)
                continue;

            slot.SetVisualAlpha(1f);
        }

        CurrentSelected = null;
    }
}