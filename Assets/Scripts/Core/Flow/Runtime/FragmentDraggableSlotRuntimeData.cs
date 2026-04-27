using System.Collections.Generic;
using Game.Data;

namespace Game.Runtime
{
    /// <summary>
    /// Estado runtime de un slot físico de fragmento para items draggable.
    /// </summary>
    public sealed class FragmentDraggableSlotRuntimeData
    {
        private readonly HashSet<string> allowedItemIds = new();

        /// <summary>
        /// ID único del slot.
        /// </summary>
        public string SlotId { get; }

        /// <summary>
        /// ID del fragmento dueño del slot.
        /// </summary>
        public string FragmentId { get; }

        /// <summary>
        /// Estado actual del slot.
        /// </summary>
        public FragmentDraggableSlotState CurrentState { get; private set; }

        /// <summary>
        /// ID del item actualmente contenido.
        /// </summary>
        public string CurrentItemId { get; private set; } = string.Empty;

        /// <summary>
        /// Devuelve true si el slot está ocupado.
        /// </summary>
        public bool IsOccupied => CurrentState != FragmentDraggableSlotState.Empty;

        /// <summary>
        /// Devuelve true si el slot quedó resuelto con un item correcto.
        /// </summary>
        public bool IsResolved => CurrentState == FragmentDraggableSlotState.OccupiedCorrectLocked;

        public FragmentDraggableSlotRuntimeData(
            string slotId,
            string fragmentId,
            IReadOnlyList<DraggableItemDefinition> allowedItems)
        {
            SlotId = slotId;
            FragmentId = fragmentId;
            CurrentState = FragmentDraggableSlotState.Empty;

            UpdateAllowedItems(allowedItems);
        }

        /// <summary>
        /// Actualiza la lista de items válidos del slot.
        /// </summary>
        public void UpdateAllowedItems(IReadOnlyList<DraggableItemDefinition> allowedItems)
        {
            allowedItemIds.Clear();

            if (allowedItems == null)
            {
                return;
            }

            for (int i = 0; i < allowedItems.Count; i++)
            {
                DraggableItemDefinition item = allowedItems[i];
                if (item == null || string.IsNullOrWhiteSpace(item.Id))
                {
                    continue;
                }

                allowedItemIds.Add(item.Id);
            }
        }

        /// <summary>
        /// Devuelve true si el slot acepta esta definición como correcta.
        /// </summary>
        public bool Accepts(DraggableItemDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                return false;
            }

            return allowedItemIds.Contains(definition.Id);
        }

        /// <summary>
        /// Marca el slot como ocupado por un item incorrecto.
        /// </summary>
        public void SetOccupiedWrong(string itemId)
        {
            CurrentState = FragmentDraggableSlotState.OccupiedWrong;
            CurrentItemId = itemId ?? string.Empty;
        }

        /// <summary>
        /// Marca el slot como resuelto por un item correcto.
        /// </summary>
        public void SetResolved(string itemId)
        {
            CurrentState = FragmentDraggableSlotState.OccupiedCorrectLocked;
            CurrentItemId = itemId ?? string.Empty;
        }

        /// <summary>
        /// Limpia completamente el slot.
        /// </summary>
        public void Clear()
        {
            CurrentState = FragmentDraggableSlotState.Empty;
            CurrentItemId = string.Empty;
        }

        /// <summary>
        /// Restaura el runtime desde save.
        /// </summary>
        public void Restore(FragmentDraggableSlotState state, string currentItemId)
        {
            CurrentState = state;
            CurrentItemId = currentItemId ?? string.Empty;
        }
    }
}