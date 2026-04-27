using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game;
using Game.Conditions;
using Game.Core;
using Game.Data;
using Game.Runtime;

/// <summary>
/// Ejecuta actos y acciones del sistema de fragmentos.
/// Solo entra en ActionFlow cuando existe un acto realmente ejecutable.
/// </summary>
public class ActionBehaviorExecutor : MonoBehaviour
{
    [Header("Object Action Sets")]

    [SerializeField]
    [Tooltip("Lista de actos que componen el fragmento.")]
    private List<ObjectActionSet> objectActionSets = new();

    [Header("Debug")]

    [SerializeField]
    [Tooltip("Activa logs detallados del sistema de fragmentos.")]
    private bool debugLogs = true;

    [Header("Runtime Binding")]

    [SerializeField]
    [Tooltip("Memoria base usada para consultar y persistir estado del fragmento.")]
    private MemoryDefinition memoryDefinition;

    private readonly FragmentActionRouter actionRouter = new();

    private Coroutine executionRoutine;
    private bool ownsActionFlowState;
    private GamePlayState previousState;
    private GamePlaySubState previousSubState;

    /// <summary>
    /// Inicia la ejecución del fragmento.
    /// </summary>
    public void Play(ActionBehaviorController controller)
    {
        if (executionRoutine != null)
        {
            StopCoroutine(executionRoutine);
            executionRoutine = null;
            ExitActionFlowIfOwned();
        }

        executionRoutine = StartCoroutine(Execute(controller));
    }

    /// <summary>
    /// Ejecuta el ciclo reactivo del fragmento hasta que todos los actos queden ejecutados.
    /// </summary>
    public IEnumerator Execute(ActionBehaviorController controller)
    {
        yield return null;

        if (controller == null)
        {
            Debug.LogError("[ActionBehaviorExecutor] ActionBehaviorController es null.", this);
            yield break;
        }

        if (memoryDefinition == null)
        {
            Debug.LogError("[ActionBehaviorExecutor] MemoryDefinition no está asignado.", this);
            yield break;
        }

        if (GameStateRepository.Instance == null)
        {
            Debug.LogError("[ActionBehaviorExecutor] GameStateRepository.Instance es null.", this);
            yield break;
        }

        RuntimeContext runtimeContext = new RuntimeContext(GameStateRepository.Instance);

        while (true)
        {
            bool anyActRunning = false;

            MemoryRuntimeData memoryRuntime = GameStateRepository.Instance.GetMemory(memoryDefinition);
            if (memoryRuntime == null)
            {
                Debug.LogError("[ActionBehaviorExecutor] No fue posible obtener la memoria runtime del fragmento.", this);
                ExitActionFlowIfOwned();
                executionRoutine = null;
                yield break;
            }

            FragmentActionExecutionContext executionContext = new FragmentActionExecutionContext(
                runtimeContext,
                controller,
                memoryDefinition,
                memoryRuntime);

            for (int i = 0; i < objectActionSets.Count; i++)
            {
                ObjectActionSet act = objectActionSets[i];

                if (act == null || act.ObjectDefinition == null)
                {
                    continue;
                }

                ObjectRuntimeData runtimeObject = memoryRuntime.GetObject(act.ObjectDefinition);
                if (runtimeObject == null)
                {
                    continue;
                }

                ActRuntimeData actState = runtimeObject.GetAct(act.Id);
                if (actState == null || actState.hasExecuted)
                {
                    continue;
                }

                bool canExecute = ConditionEvaluator.Evaluate(
                    act.Conditions,
                    runtimeContext,
                    out List<ConditionGroupDebugResult> _);

                if (!canExecute)
                {
                    continue;
                }

                EnterActionFlowIfNeeded();

                yield return ExecuteAct(act, executionContext, i);

                actState.hasExecuted = true;
                anyActRunning = true;

                GameEvents.RaisePlayerAction();

                ExitActionFlowIfOwned();
            }

            if (AllActsExecuted(memoryRuntime))
            {
                Log("END Fragment");
                ExitActionFlowIfOwned();
                executionRoutine = null;
                yield break;
            }

            if (!anyActRunning)
            {
                yield return null;
            }
        }
    }

    /// <summary>
    /// Ejecuta todas las acciones de un acto en orden secuencial.
    /// </summary>
    private IEnumerator ExecuteAct(ObjectActionSet act, FragmentActionExecutionContext executionContext, int index)
    {
        Log($"ACT {index} START: {act.Name}");

        if (act.Actions == null || act.Actions.Count == 0)
        {
            Log($"ACT {index} WITHOUT ACTIONS");
            yield break;
        }

        for (int i = 0; i < act.Actions.Count; i++)
        {
            FragmentAction action = act.Actions[i];

            if (action == null)
            {
                continue;
            }

            bool canExecute = ConditionEvaluator.Evaluate(
                action.Conditions,
                executionContext.RuntimeContext,
                out List<ConditionGroupDebugResult> _);

            if (!canExecute)
            {
                Log($"Action skipped by conditions: {action.ActionType}");
                continue;
            }

            Log($"Action START: {action.ActionType}");
            yield return actionRouter.Execute(action, executionContext, this);
        }

        Log($"ACT {index} END");
    }

    /// <summary>
    /// Verifica si todos los actos ya fueron ejecutados en runtime.
    /// </summary>
    private bool AllActsExecuted(MemoryRuntimeData memoryRuntime)
    {
        if (memoryRuntime == null)
        {
            return false;
        }

        foreach (ObjectActionSet act in objectActionSets)
        {
            if (act == null || act.ObjectDefinition == null)
            {
                continue;
            }

            ObjectRuntimeData runtimeObject = memoryRuntime.GetObject(act.ObjectDefinition);
            if (runtimeObject == null)
            {
                continue;
            }

            ActRuntimeData actState = runtimeObject.GetAct(act.Id);
            if (actState == null || !actState.hasExecuted)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Entra al estado ActionFlow únicamente si este executor va a ejecutar acciones reales.
    /// </summary>
    private void EnterActionFlowIfNeeded()
    {
        if (GamePlayStateController.Instance == null)
        {
            Debug.LogWarning("[ActionBehaviorExecutor] GamePlayStateController no encontrado.", this);
            return;
        }

        if (GamePlayStateController.Instance.IsInActionFlow)
        {
            ownsActionFlowState = false;
            return;
        }

        previousState = GamePlayStateController.Instance.CurrentState;
        previousSubState = GamePlayStateController.Instance.CurrentSubState;
        ownsActionFlowState = true;

        GamePlayStateController.Instance.EnterActionFlow();

        Log($"ActionFlow entered. Previous state: {previousState} / {previousSubState}");
    }

    /// <summary>
    /// Restaura el estado previo si este executor inició el ActionFlow.
    /// </summary>
    private void ExitActionFlowIfOwned()
    {
        if (!ownsActionFlowState)
        {
            return;
        }

        ownsActionFlowState = false;

        if (GamePlayStateController.Instance == null)
        {
            return;
        }

        if (!GamePlayStateController.Instance.IsInActionFlow)
        {
            return;
        }

        GamePlayStateController.Instance.ForceSetState(previousState, previousSubState);

        Log($"ActionFlow exited. Restored state: {previousState} / {previousSubState}");
    }

    /// <summary>
    /// Escribe logs internos del fragmento cuando el modo debug está activo.
    /// </summary>
    private void Log(string message)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log($"[ActionBehaviorExecutor] {message}", this);
    }
}