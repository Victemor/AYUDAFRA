using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DropData
{
    [SerializeField] private string fragmentName;
    [SerializeField] private Vector2 position;
    [SerializeField] private List<string> connectedFragments;
    [SerializeField] private bool wasVisited;
    [SerializeField] private string customLabel;

    public string CustomLabel => customLabel;

    public string FragmentName => fragmentName;

    public Vector2 Position
    {
        get => position;
        set => position = value;
    }

    public IReadOnlyList<string> ConnectedFragments => connectedFragments;

    public bool WasVisited
    {
        get => wasVisited;
        set => wasVisited = value;
    }

    public DropData(string fragmentName, Vector2 position)
    {
        this.fragmentName = fragmentName;
        this.position = position;
        connectedFragments = new List<string>();
        wasVisited = false;
    }

    public void SetCustomLabel(string value)
    {
        customLabel = value;
    }

    public void ConnectTo(string otherFragmentName)
    {
        if (string.IsNullOrEmpty(otherFragmentName))
            return;

        if (!connectedFragments.Contains(otherFragmentName))
        {
            connectedFragments.Add(otherFragmentName);
        }
    }

    public bool IsConnectedTo(string fragmentName)
    {
        return connectedFragments.Contains(fragmentName);
    }

    public void RemoveConnection(string otherFragment)
    {
        connectedFragments.Remove(otherFragment);
    }

    public void ClearConnections()
    {
        connectedFragments.Clear();
    }

    public void SyncConnections(IEnumerable<string> connections)
    {
        connectedFragments.Clear();

        if (connections == null)
            return;

        foreach (string connection in connections)
        {
            if (!string.IsNullOrEmpty(connection) && !connectedFragments.Contains(connection))
            {
                connectedFragments.Add(connection);
            }
        }
    }
}