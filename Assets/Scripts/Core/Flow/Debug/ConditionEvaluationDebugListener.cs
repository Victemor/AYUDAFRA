using System.Collections.Generic;
using UnityEngine;
using Game.Debugging;

namespace Game.Conditions
{
    /// <summary>
    /// Consumidor externo de resultados de evaluación.
    /// Su responsabilidad es observar snapshots y enviarlos al sistema de debug.
    /// </summary>
    public static class ConditionEvaluationDebugListener
    {
        /// <summary>
        /// Publica un snapshot en el debug controller si existe.
        /// </summary>
        public static void Publish(string source, ConditionEvaluationSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            Game.Debugging.ConditionDebugController controller = Game.Debugging.ConditionDebugController.Instance;
            if (controller == null)
                return;

            controller.LogEvaluation(source, snapshot);
        }
    }
}