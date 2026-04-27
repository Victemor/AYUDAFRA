using System;
using System.Collections.Generic;
using Game.Save;

[Serializable]
public class UnifiedSaveData
{
    public List<MemorySaveData> memories = new();
    public List<string> connections = new();
    public List<DropData> drops = new();

    public TutorialProgressData tutorialProgress = new();
    public ConsciousnessSaveData consciousness = new();

    public List<DraggableItemSaveData> draggableItems = new();
    public DraggableInventorySaveData draggableInventory = new();
    public List<FragmentDraggableSlotSaveData> draggableFragmentSlots = new();

    public List<SceneWorldObjectSaveData> sceneWorldObjects = new();
}