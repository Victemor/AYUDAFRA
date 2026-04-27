using System.Text;
using UnityEngine;
using Game.Conditions;

namespace Game.Debugging
{
    /// <summary>
    /// Controlador global de debugging del sistema completo.
    /// Consume resultados externos de evaluación y centraliza su visualización.
    /// </summary>
    public class ConditionDebugController : MonoBehaviour
    {
        public static ConditionDebugController Instance { get; private set; }

        [Header("Debug Settings")]
        [SerializeField] private bool enableLogs = true;
        [SerializeField] private bool verboseConditions = true;

        private string lastEvaluationSource = string.Empty;
        private ConditionEvaluationSnapshot lastSnapshot;

        /// <summary>
        /// Última fuente evaluada registrada.
        /// </summary>
        public string LastEvaluationSource => lastEvaluationSource;

        /// <summary>
        /// Último snapshot recibido.
        /// </summary>
        public ConditionEvaluationSnapshot LastSnapshot => lastSnapshot;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Log(string message)
        {
            if (!enableLogs)
                return;

            Debug.Log($"[ConditionSystem] {message}");
        }

        /// <summary>
        /// Recibe y registra un snapshot de evaluación.
        /// </summary>
        public void LogEvaluation(string source, ConditionEvaluationSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            lastEvaluationSource = source;
            lastSnapshot = snapshot;

            if (!enableLogs || !verboseConditions)
                return;

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"[ConditionEvaluation] Source: {source}");
            builder.AppendLine($"[ConditionEvaluation] Result: {snapshot.Result}");

            for (int i = 0; i < snapshot.Groups.Count; i++)
            {
                ConditionGroupDebugResult group = snapshot.Groups[i];
                builder.AppendLine($"  Group {i}: {group.GroupResult}");

                for (int j = 0; j < group.Results.Count; j++)
                {
                    ConditionDebugResult result = group.Results[j];
                    builder.AppendLine($"    - {result.Description} => {result.Result}");
                }
            }

            Debug.Log(builder.ToString());
        }
    }
}