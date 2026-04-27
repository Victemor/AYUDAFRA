using System.Collections.Generic;

namespace Game.Conditions
{
    /// <summary>
    /// Motor central de evaluación de condiciones.
    /// Maneja lógica OR entre grupos y genera resultados estructurados.
    /// </summary>
    public static class ConditionEvaluator
    {
        /// <summary>
        /// Evalúa una colección de grupos y devuelve un snapshot con detalle.
        /// </summary>
        public static ConditionEvaluationSnapshot EvaluateSnapshot(
            IReadOnlyList<ConditionGroup> groups,
            RuntimeContext context)
        {
            List<ConditionGroupDebugResult> debugResults = new();

            if (groups == null || groups.Count == 0)
            {
                return new ConditionEvaluationSnapshot(true, debugResults);
            }

            bool anyGroupValid = false;

            for (int i = 0; i < groups.Count; i++)
            {
                ConditionGroup group = groups[i];
                if (group == null)
                    continue;

                bool result = group.Evaluate(context, out ConditionGroupDebugResult groupDebug);
                debugResults.Add(groupDebug);

                if (result)
                {
                    anyGroupValid = true;
                }
            }

            return new ConditionEvaluationSnapshot(anyGroupValid, debugResults);
        }

        /// <summary>
        /// Overload de compatibilidad con el flujo existente.
        /// </summary>
        public static bool Evaluate(
            IReadOnlyList<ConditionGroup> groups,
            RuntimeContext context,
            out List<ConditionGroupDebugResult> debugResults)
        {
            ConditionEvaluationSnapshot snapshot = EvaluateSnapshot(groups, context);
            debugResults = new List<ConditionGroupDebugResult>(snapshot.Groups);
            return snapshot.Result;
        }
    }
}