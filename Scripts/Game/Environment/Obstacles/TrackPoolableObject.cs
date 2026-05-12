using UnityEngine;

/// <summary>
/// Marca una instancia generada como reutilizable por el pool local de contenido de pista.
/// </summary>
public sealed class TrackPoolableObject : MonoBehaviour
{
    private TrackGeneratedObjectPool ownerPool;
    private GameObject sourcePrefab;

    /// <summary>
    /// Inicializa la relación entre esta instancia y su pool propietario.
    /// </summary>
    public void Initialize(TrackGeneratedObjectPool pool, GameObject prefab)
    {
        ownerPool = pool;
        sourcePrefab = prefab;
    }

    /// <summary>
    /// Devuelve esta instancia al pool si tiene uno asignado.
    /// </summary>
    public void Despawn()
    {
        if (ownerPool == null || sourcePrefab == null)
        {
            Destroy(gameObject);
            return;
        }

        ownerPool.Despawn(gameObject, sourcePrefab);
    }
}