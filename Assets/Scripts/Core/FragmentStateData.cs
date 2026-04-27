
using System;
using UnityEngine;

[Serializable]
public class FragmentStateData
{
    [SerializeField] private string fragmentName;

    [SerializeField] private bool wasUnlocked;
    [SerializeField] private bool wasVisited;
    [SerializeField] private bool wasCompleted;

    public string FragmentName => fragmentName;
    public bool WasUnlocked { get => wasUnlocked; set => wasUnlocked = value; }
    public bool WasVisited { get => wasVisited; set => wasVisited = value; }
    public bool WasCompleted { get => wasCompleted; set => wasCompleted = value; }

    public FragmentStateData(string fragmentName)
    {
        this.fragmentName = fragmentName;
    }
}