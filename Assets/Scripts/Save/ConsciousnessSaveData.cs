using System;
using System.Collections.Generic;

[Serializable]
public class ConsciousnessSaveData
{
    public List<ThoughtSaveData> thoughts = new();
}

[Serializable]
public class ThoughtSaveData
{
    public string text;
    public float timestamp;
}