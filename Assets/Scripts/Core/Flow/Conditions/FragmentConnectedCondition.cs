using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using Game.Runtime;

namespace Game.Conditions
{
    /// <summary>
    /// Evalúa si un conjunto de fragmentos forma una conexión completa y cerrada:
    /// todos deben estar conectados directamente entre sí y ninguno puede tener
    /// conexiones hacia fragmentos externos al conjunto.
    /// 
    /// Además, cada fragmento puede declarar cero o más requisitos de estado
    /// para objetos internos.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Conditions/Fragment Connected")]
    public sealed class FragmentConnectedCondition : Condition
    {
        [Tooltip("Fragmentos requeridos y sus restricciones opcionales de objetos.")]
        [SerializeField] private List<FragmentConnectionRequirement> fragmentRequirements = new();

        /// <summary>
        /// Requisitos de fragmentos de la condición.
        /// </summary>
        public IReadOnlyList<FragmentConnectionRequirement> FragmentRequirements => fragmentRequirements;

        public override bool Evaluate(RuntimeContext context)
        {
            if (context == null || context.Repository == null)
            {
                return false;
            }

            if (fragmentRequirements == null || fragmentRequirements.Count < 2)
            {
                Log(false);
                return false;
            }

            List<MemoryDefinition> requiredMemories = new List<MemoryDefinition>();

            for (int i = 0; i < fragmentRequirements.Count; i++)
            {
                FragmentConnectionRequirement requirement = fragmentRequirements[i];
                if (requirement == null || requirement.TargetMemory == null)
                {
                    Log(false);
                    return false;
                }

                requiredMemories.Add(requirement.TargetMemory);

                if (!ValidateObjectStateRequirements(context, requirement))
                {
                    Log(false);
                    return false;
                }
            }

            bool result = context.Repository.AreMemoriesConnectedExclusive(requiredMemories);
            Log(result);
            return result;
        }

        public override string GetDescription()
        {
            int count = fragmentRequirements != null ? fragmentRequirements.Count : 0;
            return $"Fragments Connected Exclusive ({count})";
        }

        private bool ValidateObjectStateRequirements(RuntimeContext context, FragmentConnectionRequirement requirement)
        {
            if (requirement.TargetMemory == null)
            {
                return false;
            }

            MemoryRuntimeData memoryRuntime = context.Repository.GetMemory(requirement.TargetMemory);
            if (memoryRuntime == null)
            {
                return false;
            }

            IReadOnlyList<FragmentConnectionObjectStateRequirement> objectRequirements = requirement.RequiredObjectStates;
            if (objectRequirements == null || objectRequirements.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < objectRequirements.Count; i++)
            {
                FragmentConnectionObjectStateRequirement objectRequirement = objectRequirements[i];

                if (!ValidateObjectRequirement(memoryRuntime, objectRequirement))
                {
                    return false;
                }
            }

            return true;
        }

        private bool ValidateObjectRequirement(
            MemoryRuntimeData memoryRuntime,
            FragmentConnectionObjectStateRequirement objectRequirement)
        {
            if (objectRequirement == null ||
                objectRequirement.TargetObject == null ||
                string.IsNullOrWhiteSpace(objectRequirement.RequiredStateId))
            {
                return false;
            }

            ObjectRuntimeData objectRuntime = memoryRuntime.GetObject(objectRequirement.TargetObject);
            if (objectRuntime == null)
            {
                return false;
            }

            int requiredIndex = GetStateIndex(objectRequirement.TargetObject, objectRequirement.RequiredStateId);
            if (requiredIndex < 0)
            {
                return false;
            }

            int currentIndex = objectRuntime.GetStateIndex();

            switch (objectRequirement.ComparisonMode)
            {
                case ObjectStateComparisonMode.Exact:
                    return currentIndex == requiredIndex;

                case ObjectStateComparisonMode.ThisStateOrLater:
                    return currentIndex >= requiredIndex;

                default:
                    return false;
            }
        }

        private int GetStateIndex(ObjectDefinition objectDefinition, string stateId)
        {
            if (objectDefinition == null || objectDefinition.States == null)
            {
                return -1;
            }

            for (int i = 0; i < objectDefinition.States.Count; i++)
            {
                ObjectStateDefinition state = objectDefinition.States[i];
                if (state != null && state.StateId == stateId)
                {
                    return i;
                }
            }

            return -1;
        }

        private void Log(bool result)
        {
            // Game.Debugging.ConditionDebugController.Instance?
            //     .LogConditionResult(GetDescription(), result);
        }
    }
}