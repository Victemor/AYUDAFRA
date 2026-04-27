using UnityEngine;

/// <summary>
/// Contenedor que admite un solo objeto.
/// Incluye debug visible en inspector.
/// </summary>
public class PlacementContainer : MonoBehaviour
{
    [SerializeField] private Transform containerRoot;

    [Header("Debug")]
    [SerializeField] private bool isOccupiedDebug;
    [SerializeField] private GameObject currentObjectDebug;

    private GameObject currentObject;

    public bool IsOccupied => currentObject != null;
    public GameObject CurrentObject => currentObject;

    private void Update()
    {
        isOccupiedDebug = currentObject != null;
        currentObjectDebug = currentObject;

        if (currentObject != null && !currentObject.activeInHierarchy)
            currentObject = null;
    }

    public bool TryPlace(GameObject instance, GameObject prefab, out GameObject result)
    {
        if (currentObject != null)
        {
            result = null;
            return false;
        }

        if (instance != null)
        {
            result = instance;
            result.SetActive(true);

            result.transform.SetParent(containerRoot);
            result.transform.position = containerRoot.position;
            result.transform.rotation = containerRoot.rotation;

            currentObject = result;
            return true;
        }

        if (prefab != null)
        {
            result = Instantiate(prefab, containerRoot.position, containerRoot.rotation, containerRoot);
            currentObject = result;
            return true;
        }

        result = null;
        return false;
    }

    public void Clear()
    {
        if (currentObject == null)
            return;

        currentObject.transform.SetParent(null);
        currentObject = null;
    }
}