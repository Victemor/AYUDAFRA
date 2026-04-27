using System;

namespace Game.Save
{
    /// <summary>
    /// Save DTO de un slot del inventario draggable.
    /// </summary>
    [Serializable]
    public class DraggableInventorySlotSaveData
    {
        public int index;
        public string itemId;
    }
}