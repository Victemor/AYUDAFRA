using UnityEngine;
using Game.Runtime;
using Game.Data;
using Game.Conditions;

namespace Game.Debugging
{
    /// <summary>
    /// Panel de debug runtime para visualizar memorias, objetos y estados.
    /// Diseñado para testeo real del sistema.
    /// </summary>
    public class RuntimeDebugPanel : MonoBehaviour
    {
        [SerializeField] private bool showPanel = true;

        private Vector2 scroll;

        private void OnGUI()
        {
            if (!showPanel)
                return;

            if (GameStateRepository.Instance == null)
                return;

            GUILayout.BeginArea(new Rect(10, 10, 550, 780), GUI.skin.box);

            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Label("=== MEMORY DEBUG SYSTEM ===");
            GUILayout.Space(10);

            DrawLastEvaluation();

            GUILayout.Space(20);

            foreach (MemoryRuntimeData memory in GameStateRepository.Instance.GetAllMemories())
            {
                DrawMemory(memory);
                GUILayout.Space(15);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawLastEvaluation()
        {
            ConditionDebugController debugController = ConditionDebugController.Instance;
            if (debugController == null || debugController.LastSnapshot == null)
                return;

            GUILayout.BeginVertical("box");
            GUILayout.Label("Last Condition Evaluation");
            GUILayout.Label($"Source: {debugController.LastEvaluationSource}");
            GUILayout.Label($"Result: {debugController.LastSnapshot.Result}");

            for (int i = 0; i < debugController.LastSnapshot.Groups.Count; i++)
            {
                ConditionGroupDebugResult group = debugController.LastSnapshot.Groups[i];
                GUILayout.Label($"Group {i}: {group.GroupResult}");

                for (int j = 0; j < group.Results.Count; j++)
                {
                    ConditionDebugResult result = group.Results[j];
                    GUILayout.Label($" - {result.Description} => {result.Result}");
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawMemory(MemoryRuntimeData memory)
        {
            if (memory == null)
                return;

            GUILayout.Label($"[MEMORY] {memory.Definition.name}");
            GUILayout.Label($"State: {memory.CurrentState}");
            GUILayout.Label($"Alert: {memory.HasNewContentAlert}");

            GUILayout.Space(5);

            foreach (ObjectRuntimeData obj in memory.GetAllObjects())
            {
                DrawObject(obj);
            }
        }

        private void DrawObject(ObjectRuntimeData obj)
        {
            if (obj == null)
                return;

            GUILayout.BeginVertical("box");

            GUILayout.Label($"Object: {obj.Definition.name}");
            GUILayout.Label($"Current StateId: {obj.CurrentStateId}");
            GUILayout.Label($"Ready To Consume: {obj.HasPendingTransition}");

            GUILayout.Space(5);
            GUILayout.Label("States:");

            var states = obj.Definition.States;

            for (int i = 0; i < states.Count; i++)
            {
                ObjectStateDefinition state = states[i];
                bool isCurrent = obj.CurrentStateId == state.StateId;
                bool consumed = obj.IsStateConsumed(i);

                string label =
                    $"[{i}] {state.StateId} | " +
                    $"Consumed: {consumed} | " +
                    $"Current: {isCurrent}";

                GUILayout.Label(label);
            }

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Evaluate"))
            {
                obj.Evaluate(new RuntimeContext(GameStateRepository.Instance));
                Debug.Log($"[DEBUG] Evaluate llamado en {obj.Definition.name}");
            }

            if (GUILayout.Button("Consume"))
            {
                bool consumed = obj.ConsumeCurrentState(new RuntimeContext(GameStateRepository.Instance));
                Debug.Log($"[DEBUG] Consume llamado en {obj.Definition.name} → {consumed}");
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
    }
}