using UnityEngine;
using Game.Save;

/// <summary>
/// Persistencia física para objetos de escena que no pertenecen al runtime narrativo.
/// Mantiene visibilidad, colliders y posición entre escenas y entre sesiones.
/// </summary>
public sealed class PersistentSceneObject : MonoBehaviour
{
    #region Serialized Fields

    [Header("Identity")]

    [SerializeField]
    [Tooltip("Identificador persistente del objeto de escena.")]
    private WorldObjectId worldObjectId;

    [Header("Persistence")]

    [SerializeField]
    [Tooltip("Si está activo, se persiste la visibilidad de los renderers asignados.")]
    private bool persistVisibility = true;

    [SerializeField]
    [Tooltip("Si está activo, se persiste el estado enabled de los colliders asignados.")]
    private bool persistColliders = true;

    [SerializeField]
    [Tooltip("Si está activo, se persiste la posición global del objeto.")]
    private bool persistWorldPosition = false;

    [Header("Targets")]

    [SerializeField]
    [Tooltip("Renderers afectados por la persistencia visual.")]
    private Renderer[] targetRenderers;

    [SerializeField]
    [Tooltip("Colliders afectados por la persistencia física.")]
    private Collider[] targetColliders;


    public PersistentWorldObject persistentWorldObject;

    #endregion

    #region Properties

    /// <summary>
    /// Id persistente del objeto.
    /// </summary>
    public string WorldObjectIdValue => worldObjectId != null ? worldObjectId.Id : string.Empty;

    #endregion

    #region Public API

    /// <summary>
    /// Construye el DTO persistente del objeto actual.
    /// </summary>
    public SceneWorldObjectSaveData BuildSaveData()
    {
        if (string.IsNullOrWhiteSpace(WorldObjectIdValue))
        {
            return null;
        }

        SceneWorldObjectSaveData data = new SceneWorldObjectSaveData
        {
            sceneName = gameObject.scene.name,
            worldObjectId = WorldObjectIdValue
        };

        if (persistVisibility)
        {
            data.hasVisible = true;
            data.visible = ResolveVisibleState();
        }

        if (persistColliders)
        {
            data.hasColliderEnabled = true;
            data.colliderEnabled = ResolveColliderState();
        }

        if (persistWorldPosition)
        {
            data.hasWorldPosition = true;
            data.worldPosition = transform.position;
        }

        return data;
    }

    /// <summary>
    /// Aplica al objeto el DTO persistido.
    /// </summary>
    public void ApplySaveData(SceneWorldObjectSaveData data)
    {
        if (data == null)
        {
            return;
        }

        if (data.hasVisible)
        {
            ApplyVisibleState(data.visible);
        }

        if (data.hasColliderEnabled)
        {
            ApplyColliderState(data.colliderEnabled);
        }

        if (data.hasWorldPosition)
        {
            transform.position = data.worldPosition;
        }
        persistentWorldObject = gameObject.GetComponent<PersistentWorldObject>();
        persistentWorldObject.ApplySaveData(data);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Resuelve el estado visual actual.
    /// </summary>
    private bool ResolveVisibleState()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] != null)
            {
                return targetRenderers[i].enabled;
            }
        }

        return true;
    }

    /// <summary>
    /// Resuelve el estado físico actual.
    /// </summary>
    private bool ResolveColliderState()
    {
        if (targetColliders == null || targetColliders.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < targetColliders.Length; i++)
        {
            if (targetColliders[i] != null)
            {
                return targetColliders[i].enabled;
            }
        }

        return true;
    }

    /// <summary>
    /// Aplica visibilidad a renderers configurados.
    /// </summary>
    private void ApplyVisibleState(bool visible)
    {
        if (targetRenderers == null)
        {
            return;
        }

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] != null)
            {
                targetRenderers[i].enabled = visible;
            }
        }
    }

    /// <summary>
    /// Aplica estado enabled a colliders configurados.
    /// </summary>
    private void ApplyColliderState(bool enabledState)
    {
        if (targetColliders == null)
        {
            return;
        }

        for (int i = 0; i < targetColliders.Length; i++)
        {
            if (targetColliders[i] != null)
            {
                targetColliders[i].enabled = enabledState;
            }
        }
    }

    #endregion
}