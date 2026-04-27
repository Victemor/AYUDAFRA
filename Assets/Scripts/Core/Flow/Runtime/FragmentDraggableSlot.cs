using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Game.Data;
using Game.Runtime;

public sealed class FragmentDraggableSlot : MonoBehaviour
{
    [Header("Identification")]

    [SerializeField]
    [Tooltip("Identificador único global del slot.")]
    private string slotId;

    [SerializeField]
    [Tooltip("Si está activo, usa automáticamente el nombre de la escena actual como fragmentId.")]
    private bool useCurrentSceneNameAsFragmentId = true;

    [SerializeField]
    [Tooltip("Identificador del fragmento al que pertenece el slot.")]
    private string fragmentId;

    [Header("Placement")]

    [SerializeField]
    [Tooltip("Punto exacto donde se posiciona el objeto colocado.")]
    private Transform placementAnchor;

    [SerializeField]
    [Tooltip("Lista de items válidos para resolver este slot.")]
    private DraggableItemDefinition[] allowedItems;

    [Header("Slot Visual")]

    [SerializeField]
    [Tooltip("Imagen opcional de UI para mostrar el sprite del item encima del slot.")]
    private Image slotItemImage;

    [SerializeField]
    [Tooltip("SpriteRenderer opcional para mostrar el sprite del item encima del slot en mundo.")]
    private SpriteRenderer slotItemSpriteRenderer;

    [SerializeField]
    [Tooltip("Alpha del icono cuando el slot está ocupado incorrectamente.")]
    private float wrongItemAlpha = 0.9f;

    [SerializeField]
    [Tooltip("Alpha del icono cuando el slot está resuelto y bloqueado.")]
    private float correctLockedAlpha = 1f;

    [Header("Debug")]

    [SerializeField]
    [Tooltip("Activa logs de depuración del slot.")]
    private bool debugLogs = false;

    private DraggableItemWorldInstance currentVisualInstance;
    private FragmentDraggableSlotRuntimeData runtime;

    /// <summary>
    /// ID único del slot.
    /// </summary>
    public string SlotId => slotId;

    /// <summary>
    /// ID del fragmento dueño del slot.
    /// </summary>
    public string FragmentId => fragmentId;

    private void Awake()
    {
        if (useCurrentSceneNameAsFragmentId)
        {
            fragmentId = SceneManager.GetActiveScene().name;
        }

        if (placementAnchor == null)
        {
            placementAnchor = transform;
        }
    }

    private void OnEnable()
    {
        if (DraggableSlotRegistry.Instance != null)
        {
            runtime = DraggableSlotRegistry.Instance.GetOrCreateSlot(slotId, fragmentId, allowedItems);
        }

        if (DraggableInventorySystem.Instance != null)
        {
            DraggableInventorySystem.Instance.RegisterSceneSlot(this);
        }

        SyncVisualFromRuntime();
    }

    private void OnDisable()
    {
        if (DraggableInventorySystem.Instance != null)
        {
            DraggableInventorySystem.Instance.UnregisterSceneSlot(this);
        }
    }

    private void OnMouseOver()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Log($"Left click -> {slotId}");
            DraggableInventorySystem.Instance?.TryPlaceHeldItemInSceneSlot(this);
        }

        if (Input.GetMouseButtonDown(1))
        {
            Log($"Right click -> {slotId}");
            DraggableInventorySystem.Instance?.TryReturnWrongPlacedItemToInventory(this);
        }
    }

    /// <summary>
    /// Devuelve el runtime asociado al slot.
    /// </summary>
    public FragmentDraggableSlotRuntimeData GetRuntime()
    {
        return runtime;
    }

    /// <summary>
    /// Conecta visualmente una instancia entrante al slot.
    /// </summary>
    public void AttachIncomingInstance(DraggableItemWorldInstance instance, bool locked)
    {
        if (instance == null)
        {
            return;
        }

        if (currentVisualInstance != null && currentVisualInstance != instance)
        {
            Destroy(currentVisualInstance.gameObject);
        }

        currentVisualInstance = instance;

        Transform targetAnchor = placementAnchor != null ? placementAnchor : transform;

        currentVisualInstance.transform.SetParent(targetAnchor);
        currentVisualInstance.transform.position = targetAnchor.position;
        currentVisualInstance.transform.rotation = targetAnchor.rotation;
        currentVisualInstance.gameObject.SetActive(true);
        currentVisualInstance.ConfigureAsPlacedInSlot(slotId, locked);

        UpdateItemVisual(
            currentVisualInstance.ItemDefinition,
            locked
                ? FragmentDraggableSlotState.OccupiedCorrectLocked
                : FragmentDraggableSlotState.OccupiedWrong
        );

        Log(
            $"AttachIncomingInstance -> " +
            $"Item: {(currentVisualInstance.ItemDefinition != null ? currentVisualInstance.ItemDefinition.Id : "NULL")} | " +
            $"Locked: {locked}"
        );
    }

    /// <summary>
    /// Limpia la representación visual actual del slot.
    /// </summary>
    public void ClearOccupantVisual()
    {
        if (currentVisualInstance != null)
        {
            Destroy(currentVisualInstance.gameObject);
            currentVisualInstance = null;
        }

        ClearItemVisual();
        Log("ClearOccupantVisual");
    }

    /// <summary>
    /// Sincroniza el visual del slot a partir del runtime restaurado.
    /// </summary>
    public void SyncVisualFromRuntime()
    {
        if (runtime == null)
        {
            ClearItemVisual();
            return;
        }

        if (runtime.CurrentState == FragmentDraggableSlotState.Empty)
        {
            ClearOccupantVisual();
            return;
        }

        if (string.IsNullOrWhiteSpace(runtime.CurrentItemId))
        {
            ClearOccupantVisual();
            return;
        }

        DraggableItemRuntimeData item = DraggableInventorySystem.Instance != null
            ? DraggableInventorySystem.Instance.GetItem(runtime.CurrentItemId)
            : null;

        if (item == null || item.Definition == null)
        {
            ClearOccupantVisual();
            return;
        }

        UpdateItemVisual(item.Definition, runtime.CurrentState);

        if (item.Definition.WorldPrefab == null)
        {
            return;
        }

        if (currentVisualInstance != null &&
            currentVisualInstance.ItemDefinition != null &&
            currentVisualInstance.ItemDefinition.Id == item.Definition.Id)
        {
            Transform targetAnchor = placementAnchor != null ? placementAnchor : transform;
            currentVisualInstance.transform.SetParent(targetAnchor);
            currentVisualInstance.transform.position = targetAnchor.position;
            currentVisualInstance.transform.rotation = targetAnchor.rotation;
            currentVisualInstance.ConfigureAsPlacedInSlot(
                slotId,
                runtime.CurrentState == FragmentDraggableSlotState.OccupiedCorrectLocked
            );

            return;
        }

        ClearWorldInstanceOnly();

        GameObject instance = Instantiate(item.Definition.WorldPrefab);
        DraggableItemWorldInstance worldInstance = instance.GetComponent<DraggableItemWorldInstance>();

        if (worldInstance == null)
        {
            worldInstance = instance.AddComponent<DraggableItemWorldInstance>();
        }

        worldInstance.Initialize(item.Definition, false);

        AttachIncomingInstance(
            worldInstance,
            runtime.CurrentState == FragmentDraggableSlotState.OccupiedCorrectLocked
        );
    }

    /// <summary>
    /// Limpia solo la instancia world, sin tocar el icono overlay.
    /// </summary>
    private void ClearWorldInstanceOnly()
    {
        if (currentVisualInstance != null)
        {
            Destroy(currentVisualInstance.gameObject);
            currentVisualInstance = null;
        }
    }

    /// <summary>
    /// Actualiza el icono visual del slot usando el sprite del item.
    /// </summary>
    private void UpdateItemVisual(DraggableItemDefinition definition, FragmentDraggableSlotState state)
    {
        Sprite sprite = definition != null ? definition.InventorySprite : null;
        float alpha = state == FragmentDraggableSlotState.OccupiedCorrectLocked
            ? correctLockedAlpha
            : wrongItemAlpha;

        if (slotItemImage != null)
        {
            slotItemImage.sprite = sprite;
            slotItemImage.enabled = sprite != null;

            Color color = slotItemImage.color;
            color.a = sprite != null ? alpha : 0f;
            slotItemImage.color = color;
        }

        if (slotItemSpriteRenderer != null)
        {
            slotItemSpriteRenderer.sprite = sprite;
            slotItemSpriteRenderer.enabled = sprite != null;

            Color color = slotItemSpriteRenderer.color;
            color.a = sprite != null ? alpha : 0f;
            slotItemSpriteRenderer.color = color;
        }
    }

    /// <summary>
    /// Limpia el icono overlay del slot.
    /// </summary>
    private void ClearItemVisual()
    {
        if (slotItemImage != null)
        {
            slotItemImage.sprite = null;
            slotItemImage.enabled = false;
        }

        if (slotItemSpriteRenderer != null)
        {
            slotItemSpriteRenderer.sprite = null;
            slotItemSpriteRenderer.enabled = false;
        }
    }

    private void Log(string message)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log($"[FragmentDraggableSlot] {message}", this);
    }
}