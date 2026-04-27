using System;
using System.Collections.Generic;

[Serializable]
public class FragmentProgressData
{
    public List<DropData> drops = new();
    public List<FragmentStateData> fragmentStates = new();
    public TutorialProgressData tutorialProgress = new();

    public FragmentStateData GetFragmentState(string fragmentName)
    {
        return fragmentStates.Find(f => f.FragmentName == fragmentName);
    }

    public DropData GetDropData(string fragmentName)
    {
        return drops.Find(d => d.FragmentName == fragmentName);
    }
}