using UnityEngine;
using Game.Data;
using Game.Runtime;

/// <summary>
/// Binder entre un objeto físico de escena y el runtime narrativo.
/// Aplica al objeto real el world state restaurado del ObjectRuntimeData.
///
/// Para objetos que usan <see cref="VisualObjectSpriteController"/> (alpha-based visibility),
/// asignar <see cref="spriteController"/> para que la restauración visual use el canal
/// correcto (alpha) en lugar de renderer.enabled.
/// </summary>
public sealed class NarrativeWorldObjectBinder : MonoBehaviour
{
    #region Serialized Fields

    [Header("Narrative Binding")]

    [SerializeField]
    [Tooltip("Memoria a la que pertenece este objeto.")]
    private MemoryDefinition memoryDefinition;

    [SerializeField]
    [Tooltip("Objeto narrativo al que pertenece este objeto físico.")]
    private ObjectDefinition objectDefinition;

    [SerializeField]
    [Tooltip("Identificador persistente del objeto físico dentro de los worldStates.")]
    private WorldObjectId worldObjectId;

    [Header("Targets")]

    [SerializeField]
    [Tooltip("Renderers afectados por la restauración visual. Solo se usa si no hay SpriteController asignado.")]
    private Renderer[] targetRenderers;

    [SerializeField]
    [Tooltip("Colliders afectados por la restauración física.")]
    private Collider[] targetColliders;

    [Header("Sprite Override")]

    [SerializeField]
    [Tooltip("(Opcional) Si está asignado, la restauración visual delega en este controlador usando alpha " +
             "en lugar de renderer.enabled. Úsalo cuando el objeto controla visibilidad mediante " +
             "VisualObjectSpriteController.")]
    private VisualObjectSpriteController spriteController;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Aplica el estado runtime restaurado al entrar a la escena.
    /// </summary>
    private void Start()
    {
        ApplyRuntimeState();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Fuerza la aplicación del estado runtime actual al objeto físico.
    /// </summary>
    public void ApplyRuntimeState()
    {
        if (memoryDefinition == null || objectDefinition == null || worldObjectId == null)
        {
            return;
        }

        if (GameStateRepository.Instance == null)
        {
            return;
        }

        MemoryRuntimeData memoryRuntime = GameStateRepository.Instance.GetMemory(memoryDefinition);
        if (memoryRuntime == null)
        {
            return;
        }

        ObjectRuntimeData objectRuntime = memoryRuntime.GetObject(objectDefinition);
        if (objectRuntime == null)
        {
            return;
        }

        ObjectRuntimeData.WorldObjectState state = objectRuntime.GetWorldState(worldObjectId.Id);
        if (state == null)
        {
            return;
        }

        if (state.visible.HasValue)
        {
            ApplyVisibleState(state.visible.Value);
        }

        if (state.colliderEnabled.HasValue)
        {
            ApplyColliderState(state.colliderEnabled.Value);
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Aplica visibilidad al objeto.
    /// Si hay un <see cref="VisualObjectSpriteController"/> asignado, delega en él usando
    /// <see cref="VisualObjectSpriteController.RestoreVisibleStateImmediate"/> (no escribe al runtime,
    /// ya que estamos restaurando, no modificando estado). De lo contrario usa renderer.enabled.
    /// </summary>
    private void ApplyVisibleState(bool visible)
    {
        if (spriteController != null)
        {
            // Usa el canal de alpha, que es el correcto para VisualObjectSpriteController.
            // RestoreVisibleStateImmediate no escribe de vuelta al worldState para evitar bucles.
            spriteController.RestoreVisibleStateImmediate(visible);
            return;
        }

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