using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    public List<MemorySaveData> memories = new();
    public List<string> connections = new();
}