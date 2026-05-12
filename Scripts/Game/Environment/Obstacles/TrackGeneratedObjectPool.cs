using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pool local para objetos generados sobre la pista.
/// Limpia estado físico al reutilizar objetos para evitar velocidades residuales,
/// rebotes inesperados o spawns inestables.
/// </summary>
public sealed class TrackGeneratedObjectPool
{
    private readonly Dictionary<GameObject, Queue<GameObject>> inactiveObjectsByPrefab = new Dictionary<GameObject, Queue<GameObject>>();
    private readonly List<GameObject> activeObjects = new List<GameObject>();
    private readonly Transform poolRoot;

    /// <summary>
    /// Crea un nuevo pool local asociado a un transform raíz.
    /// </summary>
    public TrackGeneratedObjectPool(Transform poolRoot)
    {
        this.poolRoot = poolRoot;
    }

    /// <summary>
    /// Obtiene una instancia activa del prefab indicado.
    /// </summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = GetInactiveInstance(prefab);

        if (instance == null)
        {
            instance = Object.Instantiate(prefab);

            TrackPoolableObject poolable = instance.GetComponent<TrackPoolableObject>();

            if (poolable == null)
            {
                poolable = instance.AddComponent<TrackPoolableObject>();
            }

            poolable.Initialize(this, prefab);
        }

        SpawnedPhysicsStabilizer.ResetPhysics(instance);

        instance.transform.SetParent(parent);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.transform.localScale = prefab.transform.localScale;
        instance.SetActive(true);

        SpawnedPhysicsStabilizer.ResetPhysics(instance);

        if (!activeObjects.Contains(instance))
        {
            activeObjects.Add(instance);
        }

        return instance;
    }

    /// <summary>
    /// Desactiva una instancia y la devuelve al pool local.
    /// </summary>
    public void Despawn(GameObject instance, GameObject prefab)
    {
        if (instance == null || prefab == null)
        {
            return;
        }

        if (!inactiveObjectsByPrefab.TryGetValue(prefab, out Queue<GameObject> inactiveObjects))
        {
            inactiveObjects = new Queue<GameObject>();
            inactiveObjectsByPrefab.Add(prefab, inactiveObjects);
        }

        activeObjects.Remove(instance);

        SpawnedPhysicsStabilizer.ResetPhysics(instance);

        instance.SetActive(false);
        instance.transform.SetParent(poolRoot);
        inactiveObjects.Enqueue(instance);
    }

    /// <summary>
    /// Desactiva todos los objetos activos generados por este pool.
    /// </summary>
    public void DespawnAllActive()
    {
        for (int i = activeObjects.Count - 1; i >= 0; i--)
        {
            GameObject instance = activeObjects[i];

            if (instance == null)
            {
                activeObjects.RemoveAt(i);
                continue;
            }

            TrackPoolableObject poolable = instance.GetComponent<TrackPoolableObject>();

            if (poolable == null)
            {
                Object.Destroy(instance);
                activeObjects.RemoveAt(i);
                continue;
            }

            poolable.Despawn();
        }
    }

    private GameObject GetInactiveInstance(GameObject prefab)
    {
        if (!inactiveObjectsByPrefab.TryGetValue(prefab, out Queue<GameObject> inactiveObjects))
        {
            return null;
        }

        while (inactiveObjects.Count > 0)
        {
            GameObject instance = inactiveObjects.Dequeue();

            if (instance != null)
            {
                return instance;
            }
        }

        return null;
    }
}