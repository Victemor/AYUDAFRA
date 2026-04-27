using UnityEngine;
using Game.Data;
using Game.Runtime;

namespace Game.Conditions
{
    /// <summary>
    /// Evalúa si un objeto draggable específico ya fue finalizado.
    /// 
    /// Lógica:
    /// - True cuando el item existe en runtime y su estado actual es Finalized.
    /// - False en cualquier otro caso.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Conditions/Draggable Item Finalized")]
    public sealed class DraggableItemFinalizedCondition : Condition
    {
        [SerializeField]
        [Tooltip("Objeto draggable a evaluar.")]
        private DraggableItemDefinition targetItem;

        /// <summary>
        /// Objeto draggable observado por la condición.
        /// </summary>
        public DraggableItemDefinition TargetItem => targetItem;

        /// <inheritdoc />
        public override bool Evaluate(RuntimeContext context)
        {
            if (targetItem == null)
            {
                return false;
            }

            if (DraggableInventorySystem.Instance == null)
            {
                return false;
            }

            return DraggableInventorySystem.Instance.IsItemFinalized(targetItem.Id);
        }

        /// <inheritdoc />
        public override string GetDescription()
        {
            string itemName = targetItem != null ? targetItem.name : "NULL";
            return $"Draggable Item Finalized | Item: [{itemName}]";
        }
    }
}