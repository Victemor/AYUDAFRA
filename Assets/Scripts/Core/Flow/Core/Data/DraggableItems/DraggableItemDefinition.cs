using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Data
{
    /// <summary>
    /// Define un objeto draggable único del juego.
    /// 
    /// Diseño:
    /// - Cada asset representa una entidad única.
    /// - No es stackeable.
    /// - No depende de ObjectRuntimeData.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Draggable Items/Draggable Item Definition")]
    public sealed class DraggableItemDefinition : ScriptableObject
    {
        [Header("Identification")]

        [SerializeField]
        [Tooltip("ID único del objeto draggable. Se sincroniza automáticamente con el nombre del asset.")]
        private string id;

        [SerializeField]
        [Tooltip("Nombre visible opcional para UI y debugging.")]
        private string displayName;

        [Header("Visuals")]

        [SerializeField]
        [Tooltip("Sprite usado en el inventario.")]
        private Sprite inventorySprite;

        [SerializeField]
        [Tooltip("Prefab de mundo asociado al objeto draggable.")]
        private GameObject worldPrefab;

        /// <summary>
        /// Identificador único del objeto.
        /// </summary>
        public string Id => id;

        /// <summary>
        /// Nombre visible configurado.
        /// </summary>
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        /// <summary>
        /// Sprite del inventario.
        /// </summary>
        public Sprite InventorySprite => inventorySprite;

        /// <summary>
        /// Prefab de mundo.
        /// </summary>
        public GameObject WorldPrefab => worldPrefab;

#if UNITY_EDITOR
        private void OnValidate()
        {
            SyncAutoFields();
        }

        /// <summary>
        /// Sincroniza automáticamente el ID con el nombre del asset.
        /// </summary>
        public void SyncAutoFields()
        {
            string assetPath = AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            id = name;

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = name;
            }

            EditorUtility.SetDirty(this);
        }
#endif
    }
}