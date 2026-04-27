using System.Collections.Generic;
using System;
using Game.Save;

[System.Serializable]
public class MemorySaveData
{
    public string id;
    public int state;
    public bool hasAlert;
    public List<ObjectSaveData> objects = new();
}
