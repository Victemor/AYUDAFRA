using UnityEngine;

public sealed class WorldObjectId : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Identificador persistente único del objeto en la escena.")]
    private string id;

    public string Id => id;
}