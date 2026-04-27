using System.Collections.Generic;
using UnityEngine;
using Game.Runtime;
using Game.Conditions;
using Game.Data;

namespace Game.Systems
{
    /// <summary>
    /// Sistema central reactivo que reevalúa condiciones del juego.
    /// En esta fase usa un registro de dependencias para reevaluación dirigida.
    /// </summary>
    public class ConditionEvaluationSystem : MonoBehaviour
    {
        public static ConditionEvaluationSystem Instance { get; private set; }

        private RuntimeContext context;
        private ConditionDependencyRegistry dependencyRegistry;

        private readonly Queue<ConditionDependencyTarget> pendingTargets = new();
        private readonly HashSet<string> queuedTargetKeys = new();

        private bool isProcessingQueue;

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

        private void Start()
        {
            if (GameStateRepository.Instance == null)
            {
                Debug.LogError("[ConditionEvaluationSystem] GameStateRepository.Instance es null.");
                return;
            }

            GameStateRepository.Instance.InitializeAllMemories();

            context = new RuntimeContext(GameStateRepository.Instance);
            dependencyRegistry = new ConditionDependencyRegistry();
            dependencyRegistry.Build(GameStateRepository.Instance.Database);

            Game.Debugging.ConditionDebugController.Instance?.Log(
                $"Dependency registry built | Memory: {dependencyRegistry.MemoryDependencyCount} | " +
                $"Object: {dependencyRegistry.ObjectDependencyCount} | " +
                $"Connection: {dependencyRegistry.ConnectionDependencyCount} | " +
                $"DraggableItem: {dependencyRegistry.DraggableItemDependencyCount} | " +
                $"DraggableSlot: {dependencyRegistry.DraggableSlotDependencyCount}");

            Subscribe();
            EvaluateAll();
        }

        private void Subscribe()
        {
            GameEvents.OnMemoryStateChanged += OnMemoryStateChanged;
            GameEvents.OnObjectStateChanged += OnObjectStateChanged;
            GameEvents.OnMemoryConnectionChanged += OnMemoryConnectionChanged;
            GameEvents.OnPlayerAction += OnPlayerAction;
            GameEvents.OnDraggableItemStateChanged += OnDraggableItemStateChanged;
            GameEvents.OnFragmentDraggableSlotStateChanged += OnFragmentDraggableSlotStateChanged;
        }

        private void OnDestroy()
        {
            GameEvents.OnMemoryStateChanged -= OnMemoryStateChanged;
            GameEvents.OnObjectStateChanged -= OnObjectStateChanged;
            GameEvents.OnMemoryConnectionChanged -= OnMemoryConnectionChanged;
            GameEvents.OnPlayerAction -= OnPlayerAction;
            GameEvents.OnDraggableItemStateChanged -= OnDraggableItemStateChanged;
            GameEvents.OnFragmentDraggableSlotStateChanged -= OnFragmentDraggableSlotStateChanged;
        }

        private void OnMemoryStateChanged(MemoryRuntimeData runtimeMemory)
        {
            if (runtimeMemory?.Definition == null || dependencyRegistry == null)
                return;

            EnqueueTargets(dependencyRegistry.GetTargetsForMemory(runtimeMemory.Definition));
            ProcessQueue();
        }

        private void OnObjectStateChanged(ObjectRuntimeData runtimeObject)
        {
            if (runtimeObject?.ParentMemory?.Definition == null || runtimeObject.Definition == null || dependencyRegistry == null)
                return;

            EnqueueTargets(dependencyRegistry.GetTargetsForObject(
                runtimeObject.ParentMemory.Definition,
                runtimeObject.Definition));

            ProcessQueue();
        }

        private void OnMemoryConnectionChanged(string memoryIdA, string memoryIdB)
        {
            if (dependencyRegistry == null)
                return;

            EnqueueTargets(dependencyRegistry.GetTargetsForConnection(memoryIdA, memoryIdB));
            ProcessQueue();
        }

        private void OnPlayerAction()
        {
            EvaluateAll();
        }

        /// <summary>
        /// Reevaluación global de fallback y arranque.
        /// </summary>
        public void EvaluateAll()
        {
            GameStateRepository repository = GameStateRepository.Instance;
            if (repository == null || context == null)
                return;

            foreach (MemoryRuntimeData memory in repository.GetAllMemories())
            {
                if (memory == null)
                    continue;

                memory.EvaluateUnlock(context);

                foreach (ObjectRuntimeData obj in memory.GetAllObjects())
                {
                    if (obj == null)
                        continue;

                    obj.Evaluate(context);
                }
            }

            Game.Debugging.ConditionDebugController.Instance?.Log("Re-evaluación global ejecutada");
        }

        private void EnqueueTargets(IReadOnlyList<ConditionDependencyTarget> targets)
        {
            if (targets == null)
                return;

            for (int i = 0; i < targets.Count; i++)
            {
                ConditionDependencyTarget target = targets[i];
                if (target == null)
                    continue;

                if (queuedTargetKeys.Add(target.Key))
                {
                    pendingTargets.Enqueue(target);
                }
            }
        }

        private void ProcessQueue()
        {
            if (isProcessingQueue)
                return;

            isProcessingQueue = true;

            while (pendingTargets.Count > 0)
            {
                ConditionDependencyTarget target = pendingTargets.Dequeue();
                queuedTargetKeys.Remove(target.Key);

                target.Evaluate(context);
            }

            isProcessingQueue = false;
        }
        private void OnDraggableItemStateChanged(DraggableItemRuntimeData item)
        {
            if (item?.Definition == null || dependencyRegistry == null)
                return;

            EnqueueTargets(dependencyRegistry.GetTargetsForDraggableItem(item.Definition.Id));
            ProcessQueue();
        }

        private void OnFragmentDraggableSlotStateChanged(FragmentDraggableSlotRuntimeData slot)
        {
            if (slot == null || dependencyRegistry == null)
                return;

            EnqueueTargets(dependencyRegistry.GetTargetsForDraggableSlot(slot.SlotId));
            ProcessQueue();
        }
    }
}