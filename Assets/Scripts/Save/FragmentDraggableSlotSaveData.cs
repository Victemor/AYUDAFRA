using System;

namespace Game.Save
{
    /// <summary>
    /// Save DTO de un slot físico draggable.
    /// </summary>
    [Serializable]
    public class FragmentDraggableSlotSaveData
    {
        public string slotId;
        public int state;
        public string currentItemId;
    }
}