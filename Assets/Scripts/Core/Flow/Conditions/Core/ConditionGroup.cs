using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Conditions
{
    /// <summary>
    /// Representa un grupo de condiciones donde todas deben cumplirse (AND).
    /// </summary>
    [Serializable]
    public class ConditionGroup
    {
        [Tooltip("Todas las condiciones dentro de este grupo deben cumplirse (AND).")]
        [SerializeField] private List<Condition> conditions = new();

        /// <summary>
        /// Condiciones pertenecientes al grupo.
        /// </summary>
        public IReadOnlyList<Condition> Conditions => conditions;

        /// <summary>
        /// Evalúa el grupo completo.
        /// </summary>
        public bool Evaluate(RuntimeContext context, out ConditionGroupDebugResult debugResult)
        {
            bool allTrue = true;
            debugResult = new ConditionGroupDebugResult();

            if (conditions == null || conditions.Count == 0)
            {
                debugResult.SetGroupResult(true);
                return true;
            }

            foreach (Condition condition in conditions)
            {
                bool result = condition != null && condition.Evaluate(context);
                debugResult.Add(condition, result);

                if (!result)
                {
                    allTrue = false;
                }
            }

            debugResult.SetGroupResult(allTrue);
            return allTrue;
        }
    }
}