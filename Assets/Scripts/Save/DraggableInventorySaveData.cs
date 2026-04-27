using System;
using System.Collections.Generic;

namespace Game.Save
{
    /// <summary>
    /// Save DTO del inventario draggable.
    /// </summary>
    [Serializable]
    public class DraggableInventorySaveData
    {
        public List<DraggableInventorySlotSaveData> slots = new();
        public string heldItemId;
    }
}