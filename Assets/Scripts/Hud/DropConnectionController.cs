using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla las relaciones entre gotas en el menú.
/// Primero registra relaciones lógicas y luego construye
/// las conexiones visuales cuando todas las gotas existen.
/// </summary>
public class DropConnectionsController : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject connectionPrefab;
    [SerializeField] private Transform connectionsParent;

    /// <summary>
    /// Relaciones lógicas únicas entre fragmentos (A|B).
    /// </summary>
    private readonly HashSet<string> logicalConnections = new();

    /// <summary>
    /// Conexiones visuales instanciadas.
    /// </summary>
    private readonly Dictionary<string, GameObject> visualConnections = new();

    private Dictionary<string, DropController> drops;

    #region Registration Phase

    /// <summary>
    /// Registra las conexiones lógicas de una gota
    /// sin crear aún elementos visuales.
    /// </summary>
    public void RegisterDropConnections(DropData dropData)
    {
        string from = dropData.FragmentName;

        foreach (string to in dropData.ConnectedFragments)
        {
            string key = GetConnectionKey(from, to);

            if (!logicalConnections.Contains(key))
            {
                logicalConnections.Add(key);
            }
        }
    }

    #endregion

    #region Build Phase

    /// <summary>
    /// Construye todas las conexiones visuales una vez
    /// que todas las gotas existen.
    /// </summary>
    public void BuildConnections(
        Dictionary<string, DropController> dropsByFragment)
    {
        drops = dropsByFragment;

        foreach (string key in logicalConnections)
        {
            if (visualConnections.ContainsKey(key))
                continue;

            string[] parts = key.Split('|');
            string a = parts[0];
            string b = parts[1];

            if (!drops.ContainsKey(a) || !drops.ContainsKey(b))
                continue;

            CreateVisualConnection(key, drops[a], drops[b]);
        }
    }

    private void CreateVisualConnection(
        string key,
        DropController dropA,
        DropController dropB)
    {
        GameObject instance = Instantiate(
            connectionPrefab,
            connectionsParent
        );

        BounceBetweenDrops bounce =
            instance.GetComponent<BounceBetweenDrops>();

        bounce.Initialize(a: dropA.transform, b: dropB.transform,
            fragmentA: dropA.dropData.FragmentName,
            fragmentB: dropB.dropData.FragmentName);

        visualConnections.Add(key, instance);
    }

    public void AddConnection(
    DropController dropA,
    DropController dropB)
    {
        string key = GetConnectionKey(
            dropA.dropData.FragmentName,
            dropB.dropData.FragmentName
        );

        if (logicalConnections.Contains(key))
            return;

        logicalConnections.Add(key);

        CreateVisualConnection(key, dropA, dropB);
    }
    public void RemoveConnection(string fragmentA, string fragmentB)
    {
        string key = GetConnectionKey(fragmentA, fragmentB);
    
        logicalConnections.Remove(key);
    
        if (visualConnections.TryGetValue(key, out GameObject go))
        {
            Destroy(go);
            visualConnections.Remove(key);
        }
    }

    #endregion

    #region Visual Control
    public void DimAllExcept(
    string ownerFragment,
    IReadOnlyList<string> connectedFragments)
    {
        foreach (var kvp in visualConnections)
        {
            BounceBetweenDrops connection =
                kvp.Value.GetComponent<BounceBetweenDrops>();

            if (connection == null)
                continue;

            bool isOwnerConnection =
                connection.fragmentA == ownerFragment ||
                connection.fragmentB == ownerFragment;

            bool isConnected = false;

            foreach (string fragment in connectedFragments)
            {
                if (fragment == connection.fragmentA ||
                    fragment == connection.fragmentB)
                {
                    isConnected = true;
                    break;
                }
            }

            if (isOwnerConnection && isConnected)
            {
                connection.SetNormal();
            }
            else
            {
                connection.SetDimmed();
            }
        }
    }

    public void DimAllConnections()
    {
        foreach (var kvp in visualConnections)
        {
            BounceBetweenDrops connection =
                kvp.Value.GetComponent<BounceBetweenDrops>();

            if (connection != null)
                connection.SetDimmed();
        }
    }
    public void RestoreAll()
    {
        foreach (var kvp in visualConnections)
        {
            BounceBetweenDrops connection =
                kvp.Value.GetComponent<BounceBetweenDrops>();

            if (connection != null)
                connection.SetNormal();
        }
    }

    public void SetConnectionDimmed(
    string fragmentA,
    string fragmentB,
    bool dimmed)
    {
        string key = GetConnectionKey(fragmentA, fragmentB);

        if (!visualConnections.TryGetValue(key, out GameObject go))
            return;

        BounceBetweenDrops connection =
            go.GetComponent<BounceBetweenDrops>();

        if (connection == null)
            return;

        if (dimmed)
            connection.SetDimmed();
        else
            connection.SetNormal();
    }


    #endregion

    #region Utilities

    private string GetConnectionKey(string a, string b)
    {
        return string.CompareOrdinal(a, b) < 0
            ? $"{a}|{b}"
            : $"{b}|{a}";
    }

    #endregion
}
