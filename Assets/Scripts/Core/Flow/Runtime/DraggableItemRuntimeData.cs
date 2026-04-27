using UnityEngine;
using Game.Data;

namespace Game.Runtime
{
    /// <summary>
    /// Estado runtime de un objeto draggable único.
    /// </summary>
    public sealed class DraggableItemRuntimeData
    {
        /// <summary>
        /// Definición estática del item.
        /// </summary>
        public DraggableItemDefinition Definition { get; }

        /// <summary>
        /// Estado actual del item.
        /// </summary>
        public DraggableItemState CurrentState { get; private set; }

        /// <summary>
        /// Índice de slot de inventario actual.
        /// </summary>
        public int InventorySlotIndex { get; private set; } = -1;

        /// <summary>
        /// ID del slot físico actual del fragmento.
        /// </summary>
        public string CurrentFragmentSlotId { get; private set; } = string.Empty;

        /// <summary>
        /// Nombre de la escena actual asociada al item.
        /// </summary>
        public string CurrentSceneName { get; private set; } = string.Empty;

        /// <summary>
        /// Posición en mundo persistida.
        /// </summary>
        public Vector3 WorldPosition { get; private set; }

        /// <summary>
        /// Devuelve true si el item quedó finalizado en un slot correcto.
        /// </summary>
        public bool IsFinalized => CurrentState == DraggableItemState.Finalized;

        /// <summary>
        /// Devuelve true si el item puede seguir moviéndose.
        /// </summary>
        public bool IsMovable => CurrentState != DraggableItemState.Finalized;

        public DraggableItemRuntimeData(DraggableItemDefinition definition)
        {
            Definition = definition;
            CurrentState = DraggableItemState.NotSpawned;
        }

        /// <summary>
        /// Marca el item como presente en mundo.
        /// </summary>
        public void SetInWorld(Vector3 worldPosition, string sceneName)
        {
            CurrentState = DraggableItemState.InWorld;
            WorldPosition = worldPosition;
            CurrentSceneName = sceneName ?? string.Empty;
            InventorySlotIndex = -1;
            CurrentFragmentSlotId = string.Empty;
        }

        /// <summary>
        /// Marca el item como almacenado en inventario.
        /// </summary>
        public void SetInInventory(int slotIndex)
        {
            CurrentState = DraggableItemState.InInventory;
            InventorySlotIndex = slotIndex;
            CurrentFragmentSlotId = string.Empty;
            CurrentSceneName = string.Empty;
        }

        /// <summary>
        /// Marca el item como seleccionado desde inventario.
        /// </summary>
        public void SetHeld()
        {
            CurrentState = DraggableItemState.Held;
            CurrentFragmentSlotId = string.Empty;
            CurrentSceneName = string.Empty;
        }

        /// <summary>
        /// Marca el item como colocado en un slot físico incorrecto o aún movible.
        /// </summary>
        public void SetInFragmentSlot(string slotId, string fragmentId)
        {
            CurrentState = DraggableItemState.InFragmentSlot;
            CurrentFragmentSlotId = slotId ?? string.Empty;
            CurrentSceneName = fragmentId ?? string.Empty;
            InventorySlotIndex = -1;
        }

        /// <summary>
        /// Marca el item como finalizado en un slot correcto.
        /// </summary>
        public void SetFinalized(string slotId, string fragmentId)
        {
            CurrentState = DraggableItemState.Finalized;
            CurrentFragmentSlotId = slotId ?? string.Empty;
            CurrentSceneName = fragmentId ?? string.Empty;
            InventorySlotIndex = -1;
        }

        /// <summary>
        /// Restaura el runtime desde save.
        /// </summary>
        public void Restore(
            DraggableItemState state,
            int inventorySlotIndex,
            string currentFragmentSlotId,
            string currentSceneName,
            Vector3 worldPosition)
        {
            CurrentState = state;
            InventorySlotIndex = inventorySlotIndex;
            CurrentFragmentSlotId = currentFragmentSlotId ?? string.Empty;
            CurrentSceneName = currentSceneName ?? string.Empty;
            WorldPosition = worldPosition;
        }
    }
}