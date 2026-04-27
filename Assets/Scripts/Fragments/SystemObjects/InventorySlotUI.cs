using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Slot de inventario con control de cursor UI.
/// Permite que la referencia de la imagen esté en este objeto, en un hijo o en el padre.
/// </summary>
public class InventorySlotUI : MonoBehaviour,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("UI")]
    
    [Tooltip("Imagen que representa el ítem dentro del slot. Puede estar en este objeto, un hijo o el padre.")]
    [SerializeField] private Image slotImage;

    [Tooltip("Alpha cuando el slot está vacío.")]
    [SerializeField] private float emptyAlpha = 0.2f;

    private GameObject storedInstance;
    private GameObject storedPrefab;

    private bool isOccupied;
    private bool isSelected;

    /// <summary>
    /// Indica si el slot contiene un ítem.
    /// </summary>
    public bool IsOccupied => isOccupied;

    /// <summary>
    /// Instancia almacenada del ítem.
    /// </summary>
    public GameObject StoredInstance => storedInstance;

    /// <summary>
    /// Prefab original del ítem.
    /// </summary>
    public GameObject StoredPrefab => storedPrefab;

    /// <summary>
    /// Inicializa referencias necesarias.
    /// </summary>
    private void Awake()
    {
        ResolveImageReference();
    }

    /// <summary>
    /// Intenta resolver automáticamente la referencia de la imagen si no fue asignada.
    /// Prioridad: mismo objeto → hijos → padre.
    /// </summary>
    private void ResolveImageReference()
    {
        if (slotImage != null)
            return;

        slotImage = GetComponent<Image>();

        if (slotImage == null)
            slotImage = GetComponentInChildren<Image>(true);

        if (slotImage == null && transform.parent != null)
            slotImage = transform.parent.GetComponentInChildren<Image>(true);

#if UNITY_EDITOR
        if (slotImage == null)
        {
            Debug.LogWarning($"[{nameof(InventorySlotUI)}] No se encontró Image en {name}. Asignar manualmente.", this);
        }
#endif
    }

    /// <summary>
    /// Asigna un ítem al slot.
    /// </summary>
    public void SetItem(GameObject instance, GameObject prefab, Sprite sprite)
    {
        storedInstance = instance;
        storedPrefab = prefab;
        isOccupied = true;

        slotImage.sprite = sprite;
        SetAlpha(1f);
    }

    /// <summary>
    /// Limpia el slot.
    /// </summary>
    public void Clear()
    {
        storedInstance = null;
        storedPrefab = null;
        isOccupied = false;

        slotImage.sprite = null;
        SetAlpha(emptyAlpha);

        Deselect();
    }

    /// <summary>
    /// Maneja click sobre el slot.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isOccupied)
            return;

        PlacementSystem.Instance.SelectSlot(this);

        InventorySelectionManager.Instance?.SelectSlot(this);
    }

    /// <summary>
    /// Maneja entrada del cursor.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isOccupied || slotImage == null)
            return;

        if (!IsPointerOverImage(eventData))
            return;

    }

    /// <summary>
    /// Maneja salida del cursor.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {

    }

    /// <summary>
    /// Verifica si el puntero está sobre el rectángulo de la imagen.
    /// </summary>
    private bool IsPointerOverImage(PointerEventData eventData)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(
            slotImage.rectTransform,
            eventData.position,
            eventData.enterEventCamera
        );
    }

    /// <summary>
    /// Marca el slot como seleccionado.
    /// </summary>
    public void Select()
    {
        isSelected = true;
    }

    /// <summary>
    /// Desmarca el slot.
    /// </summary>
    public void Deselect()
    {
        isSelected = false;
    }
    /// <summary>
    /// Permite al manager controlar el alpha del slot.
    /// </summary>
    public void SetVisualAlpha(float alpha)
    {
        SetAlpha(alpha);
    }
    /// <summary>
    /// Ajusta el alpha de la imagen.
    /// </summary>
    private void SetAlpha(float alpha)
    {
        if (slotImage == null)
            return;

        Color color = slotImage.color;
        color.a = alpha;
        slotImage.color = color;
    }

    /// <summary>
    /// Estado inicial del slot.
    /// </summary>
    private void Start()
    {
        Clear();
    }
    private void OnEnable()
    {
        InventorySelectionManager.Instance?.RegisterSlot(this);
    }

    private void OnDisable()
    {
        InventorySelectionManager.Instance?.UnregisterSlot(this);
    }
}