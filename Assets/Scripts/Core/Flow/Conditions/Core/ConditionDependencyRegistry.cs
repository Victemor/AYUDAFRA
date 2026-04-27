using System.Collections.Generic;
using Game.Data;
using Game.Runtime;

namespace Game.Conditions
{
    /// <summary>
    /// Registro runtime de dependencias entre condiciones y objetivos reevaluables.
    /// Se construye al iniciar a partir del MemoryDatabase.
    /// </summary>
    public sealed class ConditionDependencyRegistry
    {
        private readonly Dictionary<string, Dictionary<string, ConditionDependencyTarget>> memoryIndex = new();
        private readonly Dictionary<string, Dictionary<string, ConditionDependencyTarget>> objectIndex = new();
        private readonly Dictionary<string, Dictionary<string, ConditionDependencyTarget>> connectionIndex = new();
        private readonly Dictionary<string, Dictionary<string, ConditionDependencyTarget>> draggableItemIndex = new();
        private readonly Dictionary<string, Dictionary<string, ConditionDependencyTarget>> draggableSlotIndex = new();

        public int DraggableItemDependencyCount => CountNested(draggableItemIndex);
        public int DraggableSlotDependencyCount => CountNested(draggableSlotIndex);

        /// <summary>
        /// Cantidad de dependencias indexadas por memoria.
        /// </summary>
        public int MemoryDependencyCount => CountNested(memoryIndex);

        /// <summary>
        /// Cantidad de dependencias indexadas por objeto.
        /// </summary>
        public int ObjectDependencyCount => CountNested(objectIndex);

        /// <summary>
        /// Cantidad de dependencias indexadas por conexiones.
        /// </summary>
        public int ConnectionDependencyCount => CountNested(connectionIndex);

        /// <summary>
        /// Construye el índice completo de dependencias a partir del database.
        /// </summary>
        public void Build(MemoryDatabase database)
        {
            memoryIndex.Clear();
            objectIndex.Clear();
            connectionIndex.Clear();
            draggableItemIndex.Clear();
            draggableSlotIndex.Clear();

            if (database == null || database.Memories == null)
                return;

            foreach (MemoryDefinition memory in database.Memories)
            {
                if (memory == null)
                    continue;

                ConditionDependencyTarget memoryTarget =
                    new ConditionDependencyTarget(ConditionDependencyTarget.DependencyTargetType.MemoryUnlock, memory, null);

                RegisterGroups(memory.UnlockConditions, memoryTarget);

                if (memory.Objects == null)
                    continue;

                foreach (ObjectDefinition obj in memory.Objects)
                {
                    if (obj == null || obj.States == null)
                        continue;

                    ConditionDependencyTarget objectTarget =
                        new ConditionDependencyTarget(ConditionDependencyTarget.DependencyTargetType.ObjectTransition, memory, obj);

                    foreach (ObjectStateDefinition state in obj.States)
                    {
                        if (state == null)
                            continue;

                        RegisterGroups(state.TransitionConditions, objectTarget);
                    }
                }
            }
        }

        /// <summary>
        /// Obtiene todos los objetivos afectados por una memoria.
        /// </summary>
        public IReadOnlyList<ConditionDependencyTarget> GetTargetsForMemory(MemoryDefinition memory)
        {
            if (memory == null || string.IsNullOrEmpty(memory.Id))
                return Empty();

            return Extract(memoryIndex, memory.Id);
        }

        /// <summary>
        /// Obtiene todos los objetivos afectados por un objeto runtime.
        /// </summary>
        public IReadOnlyList<ConditionDependencyTarget> GetTargetsForObject(MemoryDefinition memory, ObjectDefinition obj)
        {
            if (memory == null || obj == null)
                return Empty();

            string key = BuildObjectKey(memory.Id, obj.Id);
            return Extract(objectIndex, key);
        }

        /// <summary>
        /// Obtiene todos los objetivos afectados por un cambio de conexión entre memorias.
        /// </summary>
        public IReadOnlyList<ConditionDependencyTarget> GetTargetsForConnection(string memoryIdA, string memoryIdB)
        {
            Dictionary<string, ConditionDependencyTarget> merged = new();

            Merge(connectionIndex, memoryIdA, merged);
            Merge(connectionIndex, memoryIdB, merged);

            return new List<ConditionDependencyTarget>(merged.Values);
        }

        private void RegisterGroups(IReadOnlyList<ConditionGroup> groups, ConditionDependencyTarget target)
        {
            if (groups == null || target == null)
                return;

            foreach (ConditionGroup group in groups)
            {
                if (group == null || group.Conditions == null)
                    continue;

                foreach (Condition condition in group.Conditions)
                {
                    RegisterCondition(condition, target);
                }
            }
        }

        private void RegisterCondition(Condition condition, ConditionDependencyTarget target)
        {
            if (condition == null || target == null)
                return;

            switch (condition)
            {
                case MemoryVisitedCondition visitedCondition:
                    RegisterMemoryDependency(visitedCondition.Memory, target);
                    break;

                case FragmentCompletedCondition completedCondition:
                    RegisterMemoryDependency(completedCondition.Memory, target);
                    break;

                case FragmentConnectedCondition connectedCondition:
                    if (connectedCondition.FragmentRequirements != null)
                    {
                        foreach (FragmentConnectionRequirement fragmentRequirement in connectedCondition.FragmentRequirements)
                        {
                            if (fragmentRequirement == null || fragmentRequirement.TargetMemory == null)
                                continue;

                            RegisterConnectionDependency(fragmentRequirement.TargetMemory, target);

                            if (fragmentRequirement.RequiredObjectStates == null)
                                continue;

                            foreach (FragmentConnectionObjectStateRequirement objectRequirement in fragmentRequirement.RequiredObjectStates)
                            {
                                if (objectRequirement == null || objectRequirement.TargetObject == null)
                                    continue;

                                RegisterObjectDependency(
                                    fragmentRequirement.TargetMemory,
                                    objectRequirement.TargetObject,
                                    target);
                            }
                        }
                    }
                    break;
                
                case FragmentReachabilityCondition reachabilityCondition:
                    if (reachabilityCondition.FragmentRequirements != null)
                    {
                        foreach (FragmentConnectionRequirement fragmentRequirement in reachabilityCondition.FragmentRequirements)
                        {
                            if (fragmentRequirement == null || fragmentRequirement.TargetMemory == null)
                                continue;

                            RegisterConnectionDependency(fragmentRequirement.TargetMemory, target);

                            if (fragmentRequirement.RequiredObjectStates == null)
                                continue;

                            foreach (FragmentConnectionObjectStateRequirement objectRequirement in fragmentRequirement.RequiredObjectStates)
                            {
                                if (objectRequirement == null || objectRequirement.TargetObject == null)
                                    continue;

                                RegisterObjectDependency(
                                    fragmentRequirement.TargetMemory,
                                    objectRequirement.TargetObject,
                                    target);
                            }
                        }
                    }
                    break;

                case ObjectStateCondition objectStateCondition:
                    RegisterObjectDependency(objectStateCondition.Memory, objectStateCondition.TargetObject, target);
                    break;

                case ObjectStateConsumedCondition objectStateConsumedCondition:
                    RegisterObjectDependency(objectStateConsumedCondition.TargetMemory, objectStateConsumedCondition.TargetObject, target);
                    break;

                case ObjectStateEmotionCondition objectStateEmotionCondition:
                    RegisterObjectDependency(objectStateEmotionCondition.Memory, objectStateEmotionCondition.TargetObject, target);
                    break;

                case ObjectStateIntensityCondition objectStateIntensityCondition:
                    RegisterObjectDependency(objectStateIntensityCondition.Memory, objectStateIntensityCondition.TargetObject, target);
                    break;
                
                case DraggableItemFinalizedCondition draggableItemFinalizedCondition:
                    RegisterDraggableItemDependency(draggableItemFinalizedCondition.TargetItem, target);
                    break;

                case FragmentDraggableSlotResolvedCondition fragmentDraggableSlotResolvedCondition:
                    RegisterDraggableSlotDependency(fragmentDraggableSlotResolvedCondition.SlotId, target);
                    break;
            }
        }

        private void RegisterMemoryDependency(MemoryDefinition memory, ConditionDependencyTarget target)
        {
            if (memory == null || string.IsNullOrEmpty(memory.Id))
                return;

            Add(memoryIndex, memory.Id, target);
        }

        private void RegisterObjectDependency(MemoryDefinition memory, ObjectDefinition obj, ConditionDependencyTarget target)
        {
            if (memory == null || obj == null)
                return;

            string key = BuildObjectKey(memory.Id, obj.Id);
            Add(objectIndex, key, target);
        }

        private void RegisterConnectionDependency(MemoryDefinition memory, ConditionDependencyTarget target)
        {
            if (memory == null || string.IsNullOrEmpty(memory.Id))
                return;

            Add(connectionIndex, memory.Id, target);
        }

        private static void Add(
            Dictionary<string, Dictionary<string, ConditionDependencyTarget>> index,
            string key,
            ConditionDependencyTarget target)
        {
            if (string.IsNullOrEmpty(key) || target == null)
                return;

            if (!index.TryGetValue(key, out Dictionary<string, ConditionDependencyTarget> bucket))
            {
                bucket = new Dictionary<string, ConditionDependencyTarget>();
                index[key] = bucket;
            }

            bucket[target.Key] = target;
        }

        private static IReadOnlyList<ConditionDependencyTarget> Extract(
            Dictionary<string, Dictionary<string, ConditionDependencyTarget>> index,
            string key)
        {
            if (string.IsNullOrEmpty(key))
                return Empty();

            if (!index.TryGetValue(key, out Dictionary<string, ConditionDependencyTarget> bucket))
                return Empty();

            return new List<ConditionDependencyTarget>(bucket.Values);
        }

        private static void Merge(
            Dictionary<string, Dictionary<string, ConditionDependencyTarget>> index,
            string key,
            Dictionary<string, ConditionDependencyTarget> destination)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (!index.TryGetValue(key, out Dictionary<string, ConditionDependencyTarget> bucket))
                return;

            foreach (KeyValuePair<string, ConditionDependencyTarget> pair in bucket)
            {
                destination[pair.Key] = pair.Value;
            }
        }

        private static string BuildObjectKey(string memoryId, string objectId)
        {
            return $"{memoryId}::{objectId}";
        }

        private static int CountNested(Dictionary<string, Dictionary<string, ConditionDependencyTarget>> index)
        {
            int count = 0;

            foreach (KeyValuePair<string, Dictionary<string, ConditionDependencyTarget>> pair in index)
            {
                count += pair.Value.Count;
            }

            return count;
        }

        private static IReadOnlyList<ConditionDependencyTarget> Empty()
        {
            return new List<ConditionDependencyTarget>();
        }

        private void RegisterDraggableItemDependency(DraggableItemDefinition item, ConditionDependencyTarget target)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
                return;

            Add(draggableItemIndex, item.Id, target);
        }

        private void RegisterDraggableSlotDependency(string slotId, ConditionDependencyTarget target)
        {
            if (string.IsNullOrWhiteSpace(slotId))
                return;

            Add(draggableSlotIndex, slotId, target);
        }

        public IReadOnlyList<ConditionDependencyTarget> GetTargetsForDraggableItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return Empty();

            return Extract(draggableItemIndex, itemId);
        }

        public IReadOnlyList<ConditionDependencyTarget> GetTargetsForDraggableSlot(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId))
                return Empty();

            return Extract(draggableSlotIndex, slotId);
        }
    }
}