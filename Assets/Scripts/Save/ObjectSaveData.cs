using System;
using System.Collections.Generic;
using Game.Save;
namespace Game.Save
{
    [Serializable]
    public class ObjectSaveData
    {
        public string id;
        public int stateIndex;
        public bool hasPendingTransition;

        public List<int> consumedStateIndexes = new();
        public List<RuntimeEmotionSaveData> runtimeEmotions = new();
        public List<ActSaveData> acts = new();
        public List<ObjectWorldStateSaveData> worldStates = new();
    }
}