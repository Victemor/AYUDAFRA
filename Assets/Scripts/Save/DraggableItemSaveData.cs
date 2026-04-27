using System;
using UnityEngine;

namespace Game.Save
{
    /// <summary>
    /// Save DTO de un objeto draggable.
    /// </summary>
    [Serializable]
    public class DraggableItemSaveData
    {
        public string id;
        public int state;
        public int inventorySlotIndex;
        public string currentFragmentSlotId;
        public string currentSceneName;
        public Vector3 worldPosition;
    }
}