using UnityEngine;
using Game.Save;

/// <summary>
/// Representa un objeto físico de escena cuyo estado debe persistirse entre cambios de escena.
/// Usa <see cref="WorldObjectId"/> como clave persistente.
/// </summary>
public sealed class PersistentWorldObject : MonoBehaviour
{
    #region Serialized Fields

    [Header("Identity")]
    [SerializeField]
    [Tooltip("Identificador persistente único del objeto.")]
    private WorldObjectId worldObjectId;

    [Header("Persistence")]
    [SerializeField]
    [Tooltip("Si está activo, guarda y restaura la visibilidad de los renderers asignados.")]
    private bool persistVisibility = true;

    [SerializeField]
    [Tooltip("Si está activo, guarda y restaura el estado enabled de los colliders asignados.")]
    private bool persistColliders = true;

    [SerializeField]
    [Tooltip("Si está activo, guarda y restaura la posición global del transform.")]
    private bool persistWorldPosition;

    [Header("Targets")]
    [SerializeField]
    [Tooltip("Renderers afectados por la persistencia visual.")]
    private Renderer[] targetRenderers;

    [SerializeField]
    [Tooltip("Colliders afectados por la persistencia física.")]
    private Collider[] targetColliders;

    #endregion

    #region Properties

    /// <summary>
    /// Id persistente del objeto.
    /// </summary>
    public string WorldObjectIdValue => worldObjectId != null ? worldObjectId.Id : string.Empty;

    #endregion

    #region Unity Messages

    /// <summary>
    /// Intenta restaurar automáticamente el estado persistido cuando el objeto entra en escena.
    /// Esto cubre objetos activados o instanciados después de la carga inicial.
    /// </summary>
    private void OnEnable()
    {
        SaveSystem.TryApplyToPersistentWorldObject(this);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Construye un DTO persistente con el estado actual del objeto.
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
    /// Aplica un estado persistido al objeto actual.
    /// </summary>
    /// <param name="data">DTO persistido.</param>
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
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Determina la visibilidad lógica actual del objeto.
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
    /// Determina el estado lógico actual de colliders.
    /// </summary>
    /// <summary>
    /// Determina el estado lógico actual de colliders.
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
    /// Aplica visibilidad a todos los renderers configurados.
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
    /// Aplica el estado enabled a todos los colliders configurados.
    /// </summary>
    private void ApplyColliderState(bool enabled)
    {
        if (targetColliders == null)
        {
            return;
        }

        for (int i = 0; i < targetColliders.Length; i++)
        {
            if (targetColliders[i] != null)
            {
                targetColliders[i].enabled = enabled;
            }
        }
    }

    #endregion
}