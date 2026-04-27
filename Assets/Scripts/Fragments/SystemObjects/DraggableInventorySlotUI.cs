using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using Game.Data;
using Game.Runtime;

namespace Game.UI.Inventory
{
    /// <summary>
    /// Vista individual de un slot de inventario draggable.
    /// </summary>
    public sealed class DraggableInventorySlotUI : MonoBehaviour, IPointerClickHandler
    {
        [Header("Binding")]

        [SerializeField]
        [Tooltip("Índice del slot runtime al que representa esta vista.")]
        private int slotIndex;

        [SerializeField]
        [Tooltip("Imagen visual del slot donde se dibuja el sprite del item.")]
        private Image slotImage;

        [SerializeField]
        [Tooltip("CanvasGroup opcional para controlar alpha del slot completo.")]
        private CanvasGroup slotCanvasGroup;

        [Header("Visual State")]

        [SerializeField]
        [Tooltip("Alpha cuando el slot está vacío.")]
        private float emptyAlpha = 0.2f;

        [SerializeField]
        [Tooltip("Alpha cuando el slot está ocupado.")]
        private float occupiedAlpha = 0.82f;

        [SerializeField]
        [Tooltip("Alpha cuando el slot está seleccionado.")]
        private float selectedAlpha = 1f;

        [SerializeField]
        [Tooltip("Escala normal del slot.")]
        private float normalScale = 1f;

        [SerializeField]
        [Tooltip("Escala del slot cuando está seleccionado.")]
        private float selectedScale = 1.08f;

        [SerializeField]
        [Tooltip("Duración del tween visual.")]
        private float tweenDuration = 0.15f;

        [Header("Debug")]

        [SerializeField]
        [Tooltip("Activa logs detallados del slot UI.")]
        private bool debugLogs = true;

        private Tween scaleTween;

        /// <summary>
        /// Índice runtime representado por este slot UI.
        /// </summary>
        public int SlotIndex => slotIndex;

        private void Awake()
        {
            if (slotCanvasGroup == null)
            {
                slotCanvasGroup = GetComponent<CanvasGroup>();
            }

            if (slotImage == null)
            {
                slotImage = GetComponent<Image>();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Log($"OnPointerClick -> UI Object: {name} | SlotIndex: {slotIndex}");

            if (DraggableInventorySystem.Instance == null)
            {
                Debug.LogError("[DraggableInventorySlotUI] DraggableInventorySystem.Instance es null.", this);
                return;
            }

            DraggableInventorySystem.Instance.TryBeginCarryFromInventorySlot(slotIndex);
        }

        /// <summary>
        /// Método de compatibilidad con controladores antiguos.
        /// </summary>
        public void Render()
        {
            Refresh();
        }

        /// <summary>
        /// Método de compatibilidad con controladores antiguos.
        /// </summary>
        public void Render(object _)
        {
            Refresh();
        }

        /// <summary>
        /// Método de compatibilidad con controladores antiguos que enviaban definición y selección.
        /// Los parámetros se ignoran porque la vista resuelve directamente desde runtime.
        /// </summary>
        public void Render(DraggableItemDefinition _, bool __)
        {
            Refresh();
        }

        /// <summary>
        /// Refresca la representación visual del slot.
        /// </summary>
        public void Refresh()
        {
            if (DraggableInventorySystem.Instance == null)
            {
                ApplyEmptyVisual();
                return;
            }

            DraggableInventoryRuntimeData inventory = DraggableInventorySystem.Instance.Inventory;
            if (inventory == null)
            {
                ApplyEmptyVisual();
                return;
            }

            DraggableItemRuntimeData slotItem = inventory.GetSlot(slotIndex);
            bool isSelected = inventory.HasHeldItem && inventory.HeldSourceSlotIndex == slotIndex;

            DraggableItemDefinition displayedDefinition = null;

            if (slotItem != null)
            {
                displayedDefinition = slotItem.Definition;
            }
            else if (isSelected && inventory.HeldItem != null)
            {
                displayedDefinition = inventory.HeldItem.Definition;
            }

            Log(
                $"Refresh -> UI Object: {name} | SlotIndex: {slotIndex} | " +
                $"Displayed: {(displayedDefinition != null ? displayedDefinition.Id : "NULL")} | " +
                $"IsSelected: {isSelected}"
            );

            if (displayedDefinition == null)
            {
                ApplyEmptyVisual();
                return;
            }

            ApplyOccupiedVisual(displayedDefinition, isSelected);
        }

        private void ApplyEmptyVisual()
        {
            SetSprite(null);
            SetAlpha(emptyAlpha);
            AnimateScale(normalScale);
        }

        private void ApplyOccupiedVisual(DraggableItemDefinition definition, bool selected)
        {
            SetSprite(definition != null ? definition.InventorySprite : null);
            SetAlpha(selected ? selectedAlpha : occupiedAlpha);
            AnimateScale(selected ? selectedScale : normalScale);
        }

        private void SetSprite(Sprite sprite)
        {
            if (slotImage == null)
            {
                return;
            }

            slotImage.sprite = sprite;
            slotImage.enabled = sprite != null;
        }

        private void SetAlpha(float alpha)
        {
            if (slotCanvasGroup != null)
            {
                slotCanvasGroup.alpha = alpha;
                return;
            }

            if (slotImage != null)
            {
                Color color = slotImage.color;
                color.a = alpha;
                slotImage.color = color;
            }
        }

        private void AnimateScale(float targetScale)
        {
            scaleTween?.Kill();
            scaleTween = transform
                .DOScale(Vector3.one * targetScale, tweenDuration)
                .SetEase(Ease.OutQuad);
        }

        private void Log(string message)
        {
            if (!debugLogs)
            {
                return;
            }

            Debug.Log($"[DraggableInventorySlotUI] {message}", this);
        }
    }
}