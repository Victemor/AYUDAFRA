using UnityEngine;
using Game.Runtime;

namespace Game.Conditions
{
    /// <summary>
    /// Evalúa si un slot físico draggable específico ya fue correctamente resuelto.
    /// 
    /// Lógica:
    /// - True cuando el slot existe en runtime y su estado es OccupiedCorrectLocked.
    /// - False en cualquier otro caso.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Conditions/Fragment Draggable Slot Resolved")]
    public sealed class FragmentDraggableSlotResolvedCondition : Condition
    {
        [SerializeField]
        [Tooltip("ID único global del slot físico a evaluar.")]
        private string slotId;

        /// <summary>
        /// ID del slot observado por la condición.
        /// </summary>
        public string SlotId => slotId;

        /// <inheritdoc />
        public override bool Evaluate(RuntimeContext context)
        {
            if (string.IsNullOrWhiteSpace(slotId))
            {
                return false;
            }

            if (DraggableInventorySystem.Instance == null)
            {
                return false;
            }

            return DraggableInventorySystem.Instance.IsSlotResolved(slotId);
        }

        /// <inheritdoc />
        public override string GetDescription()
        {
            return $"Fragment Draggable Slot Resolved | SlotId: [{slotId}]";
        }
    }
}