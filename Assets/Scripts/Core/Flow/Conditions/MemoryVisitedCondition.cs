using UnityEngine;
using Game.Data;
using Game.Runtime;

namespace Game.Conditions
{
    /// <summary>
    /// Evalúa si una memoria ha sido visitada (Seen o superior).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Conditions/Memory Visited")]
    public class MemoryVisitedCondition : Condition
    {
        [Tooltip("Memoria a evaluar.")]
        [SerializeField] private MemoryDefinition memory;

        /// <summary>
        /// Memoria observada por la condición.
        /// </summary>
        public MemoryDefinition Memory => memory;

        public override bool Evaluate(RuntimeContext context)
        {
            if (context == null || context.Repository == null || memory == null)
                return false;

            MemoryRuntimeData runtime = context.Repository.GetMemory(memory);
            if (runtime == null)
                return false;

            return runtime.CurrentState == MemoryState.Seen ||
                   runtime.CurrentState == MemoryState.Completed;
        }

        public override string GetDescription()
        {
            string memoryName = memory != null ? memory.name : "NULL";
            return $"Memory [{memoryName}] Visited";
        }
    }
}