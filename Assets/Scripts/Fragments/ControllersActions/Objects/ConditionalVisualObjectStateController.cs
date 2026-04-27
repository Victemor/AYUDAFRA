using System.Collections.Generic;
using Game;
using Game.Conditions;
using Game.CursorSystem;
using Game.Runtime;
using UnityEngine;

/// <summary>
/// Controla automáticamente el estado de interacción (colliders, interactables y cursor)
/// en un objeto y todos sus hijos según condiciones narrativas.
/// 
/// No modifica visibilidad ni materiales.
/// </summary>
public sealed class ConditionalVisualObjectStateController : MonoBehaviour
{
    #region Enums

    /// <summary>
    /// Acción que se ejecuta cuando las condiciones se cumplen.
    /// </summary>
    private enum ConditionSuccessAction
    {
        DisableInteraction,
        EnableInteraction
    }

    #endregion

    #region Inspector Fields

    [Header("Conditions")]

    [SerializeField]
    [Tooltip("Grupos de condiciones a evaluar. Lógica OR entre grupos y AND dentro de cada grupo.")]
    private List<ConditionGroup> conditionGroups = new();

    [Header("Behaviour")]

    [SerializeField]
    [Tooltip("Acción que se ejecuta cuando las condiciones se cumplen.")]
    private ConditionSuccessAction successAction = ConditionSuccessAction.DisableInteraction;

    [SerializeField]
    [Tooltip("Si está activo, aplica la acción contraria cuando la condición deja de cumplirse.")]
    private bool revertWhenConditionFails = false;

    [SerializeField]
    [Tooltip("Si está activo, evalúa condiciones al iniciar la escena.")]
    private bool evaluateOnStart = true;

    [SerializeField]
    [Tooltip("Si está activo, ejecuta la acción solo una vez.")]
    private bool executeOnlyOnce = true;

    [Header("Evaluation")]

    [SerializeField]
    [Tooltip("Intervalo de evaluación en segundos.")]
    [Min(0.05f)]
    private float evaluationInterval = 0.25f;

    [Header("Debug")]

    [SerializeField]
    [Tooltip("Activa logs de depuración.")]
    private bool enableDebugLogs = false;

    #endregion

    #region Private Fields

    private readonly List<BehaviourState> controlledBehaviours = new();
    private readonly List<ColliderState> controlledColliders = new();
    private readonly List<Collider2DState> controlledColliders2D = new();

    private RuntimeContext context;
    private bool lastConditionResult;
    private bool hasEvaluatedAtLeastOnce;
    private bool hasExecuted;
    private float nextEvaluationTime;

    #endregion

    #region Unity Messages

    private void Awake()
    {
        CacheControlledComponents();
    }

    private void Start()
    {
        context = new RuntimeContext(GameStateRepository.Instance);

        if (evaluateOnStart)
        {
            EvaluateAndApplyIfNeeded(forceApply: true);
        }
    }

    private void Update()
    {
        if (executeOnlyOnce && hasExecuted)
        {
            return;
        }

        if (Time.time < nextEvaluationTime)
        {
            return;
        }

        nextEvaluationTime = Time.time + evaluationInterval;
        EvaluateAndApplyIfNeeded(false);
    }

    #endregion

    #region Evaluation

    /// <summary>
    /// Evalúa condiciones y aplica cambios si es necesario.
    /// </summary>
    private void EvaluateAndApplyIfNeeded(bool forceApply)
    {
        if (context == null || context.Repository == null)
        {
            context = new RuntimeContext(GameStateRepository.Instance);
        }

        ConditionEvaluationSnapshot snapshot = ConditionEvaluator.EvaluateSnapshot(conditionGroups, context);
        bool currentResult = snapshot.Result;

        bool resultChanged = !hasEvaluatedAtLeastOnce || currentResult != lastConditionResult;

        hasEvaluatedAtLeastOnce = true;
        lastConditionResult = currentResult;

        if (!forceApply && !resultChanged)
        {
            return;
        }

        if (currentResult)
        {
            ApplySuccessAction();
            hasExecuted = true;
            Log("Condición cumplida.");
            return;
        }

        if (revertWhenConditionFails)
        {
            ApplyFailureAction();
            Log("Condición no cumplida. Revert aplicado.");
        }
    }

    /// <summary>
    /// Aplica acción cuando la condición es verdadera.
    /// </summary>
    private void ApplySuccessAction()
    {
        bool enable = successAction == ConditionSuccessAction.EnableInteraction;
        SetInteractionState(enable);
    }

    /// <summary>
    /// Aplica acción contraria cuando la condición es falsa.
    /// </summary>
    private void ApplyFailureAction()
    {
        bool enable = successAction != ConditionSuccessAction.EnableInteraction;
        SetInteractionState(enable);
    }

    #endregion

    #region Interaction Control

    /// <summary>
    /// Cachea automáticamente todos los componentes relevantes.
    /// </summary>
    private void CacheControlledComponents()
    {
        controlledBehaviours.Clear();
        controlledColliders.Clear();
        controlledColliders2D.Clear();

        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            controlledColliders.Add(new ColliderState(col, col.enabled));
        }

        foreach (Collider2D col in GetComponentsInChildren<Collider2D>(true))
        {
            controlledColliders2D.Add(new Collider2DState(col, col.enabled));
        }

        foreach (MonoBehaviour behaviour in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null || behaviour == this)
                continue;

            if (!ShouldControlBehaviour(behaviour))
                continue;

            controlledBehaviours.Add(new BehaviourState(behaviour, behaviour.enabled));
        }

        Log($"Cacheado: {controlledColliders.Count} colliders, {controlledBehaviours.Count} behaviours.");
    }

    /// <summary>
    /// Define qué scripts se controlan automáticamente.
    /// </summary>
    private bool ShouldControlBehaviour(MonoBehaviour behaviour)
    {
        return behaviour is IInteractable ||
               behaviour is MemoryObjectInteractor ||
               behaviour is WorldCursorTarget;
    }

    /// <summary>
    /// Aplica estado de interacción.
    /// </summary>
    private void SetInteractionState(bool enabled)
    {
        foreach (var state in controlledColliders)
        {
            if (state.Collider != null)
                state.Collider.enabled = enabled && state.WasEnabled;
        }

        foreach (var state in controlledColliders2D)
        {
            if (state.Collider != null)
                state.Collider.enabled = enabled && state.WasEnabled;
        }

        foreach (var state in controlledBehaviours)
        {
            if (state.Behaviour != null)
                state.Behaviour.enabled = enabled && state.WasEnabled;
        }
    }

    #endregion

    #region Debug

    /// <summary>
    /// Log controlado por flag.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log($"[{nameof(ConditionalVisualObjectStateController)}] {message}", this);
    }

    #endregion

    #region Structs

    private readonly struct BehaviourState
    {
        public BehaviourState(MonoBehaviour behaviour, bool wasEnabled)
        {
            Behaviour = behaviour;
            WasEnabled = wasEnabled;
        }

        public MonoBehaviour Behaviour { get; }
        public bool WasEnabled { get; }
    }

    private readonly struct ColliderState
    {
        public ColliderState(Collider collider, bool wasEnabled)
        {
            Collider = collider;
            WasEnabled = wasEnabled;
        }

        public Collider Collider { get; }
        public bool WasEnabled { get; }
    }

    private readonly struct Collider2DState
    {
        public Collider2DState(Collider2D collider, bool wasEnabled)
        {
            Collider = collider;
            WasEnabled = wasEnabled;
        }

        public Collider2D Collider { get; }
        public bool WasEnabled { get; }
    }

    #endregion
}