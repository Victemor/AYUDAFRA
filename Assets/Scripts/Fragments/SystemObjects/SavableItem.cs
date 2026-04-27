using UnityEngine;

/// <summary>
/// Define un objeto guardable basado en prefab.
/// Obtiene automáticamente su sprite desde un SpriteRenderer.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SavableItem : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteSource;
    [SerializeField] private GameObject prefabReference;

    private Sprite inventorySprite;

    public Sprite InventorySprite => inventorySprite;
    public GameObject PrefabReference => prefabReference;

    private void Awake()
    {
        if (spriteSource == null)
            spriteSource = GetComponentInChildren<SpriteRenderer>();

        if (spriteSource != null)
            inventorySprite = spriteSource.sprite;
    }
}