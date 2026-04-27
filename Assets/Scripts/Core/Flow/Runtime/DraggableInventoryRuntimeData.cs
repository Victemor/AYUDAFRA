using System;
using Game.Data;

namespace Game.Runtime
{
    /// <summary>
    /// Runtime del inventario draggable.
    /// Gestiona slots fijos sin stack y un único item seleccionado.
    /// </summary>
    public sealed class DraggableInventoryRuntimeData
    {
        private const int CapacityValue = 4;
        private const int InvalidSlotIndex = -1;

        private readonly DraggableItemRuntimeData[] slots = new DraggableItemRuntimeData[CapacityValue];

        private DraggableItemRuntimeData heldItem;
        private int heldSourceSlotIndex = InvalidSlotIndex;

        /// <summary>
        /// Capacidad fija del inventario.
        /// </summary>
        public int Capacity => CapacityValue;

        /// <summary>
        /// Item actualmente seleccionado.
        /// </summary>
        public DraggableItemRuntimeData HeldItem => heldItem;

        /// <summary>
        /// Índice del slot desde el cual salió el item seleccionado.
        /// </summary>
        public int HeldSourceSlotIndex => heldSourceSlotIndex;

        /// <summary>
        /// Indica si existe un item actualmente en estado Held.
        /// </summary>
        public bool HasHeldItem => heldItem != null;

        /// <summary>
        /// Devuelve el item contenido en un slot específico.
        /// </summary>
        public DraggableItemRuntimeData GetSlot(int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                return null;
            }

            return slots[slotIndex];
        }

        /// <summary>
        /// Intenta agregar un item al primer slot libre disponible.
        /// </summary>
        public bool TryAddItem(DraggableItemRuntimeData item)
        {
            if (item == null)
            {
                return false;
            }

            if (HasHeldItem && heldItem == item)
            {
                heldItem = null;
                heldSourceSlotIndex = InvalidSlotIndex;
            }

            int freeSlotIndex = GetFirstFreeSlotIndex();
            if (freeSlotIndex < 0)
            {
                return false;
            }

            slots[freeSlotIndex] = item;
            item.SetInInventory(freeSlotIndex);
            return true;
        }

        /// <summary>
        /// Intenta comenzar selección lógica desde un slot.
        /// Nunca devuelve true con item null.
        /// </summary>
        public bool TryBeginCarryFromSlot(int slotIndex, out DraggableItemRuntimeData item)
        {
            item = null;

            if (HasHeldItem)
            {
                return false;
            }

            if (!IsValidSlotIndex(slotIndex))
            {
                return false;
            }

            DraggableItemRuntimeData slotItem = slots[slotIndex];
            if (slotItem == null)
            {
                return false;
            }

            slots[slotIndex] = null;

            heldItem = slotItem;
            heldSourceSlotIndex = slotIndex;

            heldItem.SetHeld();
            item = heldItem;

            return item != null;
        }

        /// <summary>
        /// Consume definitivamente el item seleccionado.
        /// </summary>
        public void ConsumeHeldItem()
        {
            heldItem = null;
            heldSourceSlotIndex = InvalidSlotIndex;
        }

        /// <summary>
        /// Devuelve el item seleccionado al inventario.
        /// </summary>
        public bool ReturnHeldItemToInventory()
        {
            if (heldItem == null)
            {
                return false;
            }

            int preferredSlot = heldSourceSlotIndex;

            if (!IsValidSlotIndex(preferredSlot) || slots[preferredSlot] != null)
            {
                preferredSlot = GetFirstFreeSlotIndex();
            }

            if (preferredSlot < 0)
            {
                return false;
            }

            slots[preferredSlot] = heldItem;
            heldItem.SetInInventory(preferredSlot);

            heldItem = null;
            heldSourceSlotIndex = InvalidSlotIndex;

            CompactSlots();
            return true;
        }

        /// <summary>
        /// Restaura slots desde save.
        /// </summary>
        public void RestoreSlots(DraggableItemRuntimeData[] restoredSlots)
        {
            for (int i = 0; i < CapacityValue; i++)
            {
                slots[i] = restoredSlots != null && i < restoredSlots.Length
                    ? restoredSlots[i]
                    : null;

                if (slots[i] != null)
                {
                    slots[i].SetInInventory(i);
                }
            }

            heldItem = null;
            heldSourceSlotIndex = InvalidSlotIndex;

            CompactSlots();
        }

        /// <summary>
        /// Compacta slots hacia la izquierda manteniendo orden relativo.
        /// </summary>
        public void CompactSlots()
        {
            int writeIndex = 0;

            for (int readIndex = 0; readIndex < CapacityValue; readIndex++)
            {
                DraggableItemRuntimeData current = slots[readIndex];
                if (current == null)
                {
                    continue;
                }

                if (writeIndex != readIndex)
                {
                    slots[writeIndex] = current;
                    slots[readIndex] = null;
                }

                current.SetInInventory(writeIndex);
                writeIndex++;
            }

            for (int i = writeIndex; i < CapacityValue; i++)
            {
                slots[i] = null;
            }
        }

        private int GetFirstFreeSlotIndex()
        {
            for (int i = 0; i < CapacityValue; i++)
            {
                if (slots[i] == null)
                {
                    return i;
                }
            }

            return InvalidSlotIndex;
        }

        private bool IsValidSlotIndex(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < CapacityValue;
        }
    }
}