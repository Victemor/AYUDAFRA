using System.Collections.Generic;
using Game.Conditions;
using Game.Data;
using Game;

namespace Game.Runtime
{
    /// <summary>
    /// Representa el estado runtime de una memoria.
    /// Gestiona su ciclo de vida, desbloqueo y estado interno.
    /// </summary>
    public class MemoryRuntimeData
    {
        private readonly GameStateRepository repository;
        private readonly Dictionary<ObjectDefinition, ObjectRuntimeData> objects = new();

        /// <summary>
        /// Definición estática asociada.
        /// </summary>
        public MemoryDefinition Definition { get; }

        /// <summary>
        /// Estado actual de la memoria.
        /// </summary>
        public MemoryState CurrentState { get; private set; }

        /// <summary>
        /// Indica si la memoria tiene contenido nuevo.
        /// </summary>
        public bool HasNewContentAlert { get; private set; }

        /// <summary>
        /// Indica si la memoria es visible en runtime.
        /// </summary>
        public bool IsVisible => CurrentState != MemoryState.Locked;

        /// <summary>
        /// Indica si la memoria fue visitada al menos una vez.
        /// </summary>
        public bool IsVisited =>
            CurrentState == MemoryState.Seen ||
            CurrentState == MemoryState.Completed;

        public MemoryRuntimeData(MemoryDefinition definition, GameStateRepository repository)
        {
            Definition = definition;
            this.repository = repository;

            InitializeState();
            InitializeObjects();
        }

        /// <summary>
        /// Fuerza un estado runtime.
        /// Usado principalmente por el sistema de carga.
        /// </summary>
        public void ForceSetState(MemoryState state, bool alert)
        {
            CurrentState = state;
            HasNewContentAlert = alert;
        }

        private void InitializeState()
        {
            if (Definition == null)
            {
                CurrentState = MemoryState.Locked;
                return;
            }

            if (Definition.UnlockConditions == null || Definition.UnlockConditions.Count == 0)
            {
                CurrentState = MemoryState.Visible;
            }
            else
            {
                CurrentState = MemoryState.Locked;
            }
        }

        private void InitializeObjects()
        {
            if (Definition == null || Definition.Objects == null)
                return;

            foreach (ObjectDefinition obj in Definition.Objects)
            {
                if (obj == null)
                    continue;

                objects[obj] = new ObjectRuntimeData(obj, this);
            }
        }

        /// <summary>
        /// Obtiene el runtime de un objeto definido dentro de esta memoria.
        /// </summary>
        public ObjectRuntimeData GetObject(ObjectDefinition definition)
        {
            return definition != null && objects.TryGetValue(definition, out ObjectRuntimeData runtime)
                ? runtime
                : null;
        }

        /// <summary>
        /// Reevalúa si la memoria puede desbloquearse.
        /// </summary>
        public void EvaluateUnlock(RuntimeContext context)
        {
            if (CurrentState != MemoryState.Locked)
                return;

            if (Definition == null)
                return;

            if (Definition.UnlockConditions == null || Definition.UnlockConditions.Count == 0)
            {
                SetStateInternal(MemoryState.Visible);
                GameEvents.RaiseMemoryUnlocked(Definition);
                return;
            }

            ConditionEvaluationSnapshot snapshot = ConditionEvaluator.EvaluateSnapshot(
                Definition.UnlockConditions,
                context);

            ConditionEvaluationDebugListener.Publish($"Memory Unlock [{Definition.Id}]", snapshot);

            if (snapshot.Result)
            {
                SetStateInternal(MemoryState.Visible);
                GameEvents.RaiseMemoryUnlocked(Definition);
            }
        }

        /// <summary>
        /// Marca la memoria como vista si estaba visible.
        /// </summary>
        public void MarkAsSeen()
        {
            if (CurrentState == MemoryState.Visible)
            {
                SetStateInternal(MemoryState.Seen);
            }
        }

        /// <summary>
        /// Marca la memoria como completada.
        /// </summary>
        public void MarkAsCompleted()
        {
            if (CurrentState != MemoryState.Completed)
            {
                SetStateInternal(MemoryState.Completed);
            }
        }

        /// <summary>
        /// Ajusta el estado de alerta de nuevo contenido.
        /// </summary>
        public void SetAlert(bool value)
        {
            if (HasNewContentAlert == value)
                return;

            HasNewContentAlert = value;

            if (value && Definition != null)
            {
                GameEvents.RaiseMemoryAlert(Definition);
            }
        }

        /// <summary>
        /// Devuelve todos los objetos runtime de la memoria.
        /// </summary>
        public IEnumerable<ObjectRuntimeData> GetAllObjects()
        {
            return objects.Values;
        }

        /// <summary>
        /// Aplica una transición de estado runtime y notifica el cambio.
        /// </summary>
        private void SetStateInternal(MemoryState newState)
        {
            if (CurrentState == newState)   
                return;
            MemoryState previousState = CurrentState;
            CurrentState = newState;

            NotifyIfUnlocked(previousState, newState);
            GameEvents.RaiseMemoryStateChanged(this);
        }

        private void NotifyIfUnlocked(MemoryState previousState, MemoryState newState)
        {
            if (previousState == newState)
            {
                return;
            }

            if (previousState == MemoryState.Locked && newState != MemoryState.Locked)
            {
                var message = new UnlockNotificationMessage(
                    entityType: UnlockEntityType.Memory,
                    notificationType: NarrativeNotificationType.FragmentUnlocked,
                    memoryId: Definition.Id,
                    entityId: Definition.Id,
                    previousState: previousState.ToString(),
                    currentState: newState.ToString(),
                    reason: "Memory unlock conditions satisfied.",
                    displayMessage: UnlockMessageBuilder.BuildMemoryUnlocked(Definition.Id)
                );

                GameEvents.RaiseUnlockNotification(message);
            }
        }
    }
}