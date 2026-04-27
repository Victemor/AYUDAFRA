using System;
using System.Collections.Generic;
using Game.Conditions;
using Game.Data;
using UnityEngine;
using Game;

namespace Game.Runtime
{
    /// <summary>
    /// Representa el estado runtime de un objeto interactivo.
    /// 
    /// Reglas del sistema:
    /// - Los estados son lineales y se consumen en orden.
    /// - Un estado solo puede consumirse si sus propias condiciones ya están cumplidas.
    /// - El cumplimiento de condiciones no autoejecuta nada: solo habilita el estado para un clic futuro.
    /// - Un estado consumido nunca puede repetirse.
    /// </summary>
    public class ObjectRuntimeData
    {
        private readonly MemoryRuntimeData parentMemory;
        private readonly Dictionary<int, RuntimeEmotionState> runtimeEmotions = new();
        private readonly Dictionary<string, ActRuntimeData> acts = new();
        private readonly HashSet<int> consumedStates = new();
        private readonly Dictionary<string, WorldObjectState> worldStates = new();

        private int currentStateIndex;

        /// <summary>
        /// Definición estática del objeto.
        /// </summary>
        public ObjectDefinition Definition { get; }

        /// <summary>
        /// Memoria padre del objeto runtime.
        /// </summary>
        public MemoryRuntimeData ParentMemory => parentMemory;

        /// <summary>
        /// Identificador del estado actual pendiente de consumo.
        /// </summary>
        public string CurrentStateId
        {
            get
            {
                if (!IsValidStateIndex(currentStateIndex))
                {
                    return string.Empty;
                }

                ObjectStateDefinition state = Definition.States[currentStateIndex];
                return state != null ? state.StateId : string.Empty;
            }
        }

        /// <summary>
        /// Indica si el estado actual está habilitado y listo para consumirse mediante clic.
        /// </summary>
        public bool HasPendingTransition { get; private set; }

        public ObjectRuntimeData(ObjectDefinition definition, MemoryRuntimeData memory)
        {
            Definition = definition;
            parentMemory = memory;
            currentStateIndex = 0;
            HasPendingTransition = false;
        }

        /// <summary>
        /// Devuelve true si el índice de estado es válido para la definición actual.
        /// </summary>
        public bool IsValidStateIndex(int index)
        {
            return Definition != null &&
                   Definition.States != null &&
                   index >= 0 &&
                   index < Definition.States.Count;
        }

        /// <summary>
        /// Intenta obtener la definición de estado para el índice solicitado.
        /// </summary>
        public bool TryGetStateDefinition(int index, out ObjectStateDefinition stateDefinition)
        {
            if (IsValidStateIndex(index))
            {
                stateDefinition = Definition.States[index];
                return stateDefinition != null;
            }

            stateDefinition = null;
            return false;
        }

        /// <summary>
        /// Devuelve true si el estado identificado por StateId ya fue consumido.
        /// </summary>
        public bool HasConsumedState(string stateId)
        {
            if (Definition == null || Definition.States == null || string.IsNullOrWhiteSpace(stateId))
            {
                return false;
            }

            for (int i = 0; i < Definition.States.Count; i++)
            {
                ObjectStateDefinition state = Definition.States[i];
                if (state != null && state.StateId == stateId)
                {
                    return consumedStates.Contains(i);
                }
            }

            return false;
        }

        /// <summary>
        /// Reevalúa si el estado actual ya está habilitado para consumirse.
        /// No ejecuta acciones ni avanza estados automáticamente.
        /// </summary>
        public void Evaluate(RuntimeContext context)
        {
            bool previousAvailability = HasPendingTransition;
            bool newAvailability = CanConsumeCurrentState(context);

            if (previousAvailability == newAvailability)
            {
                return;
            }

            HasPendingTransition = newAvailability;

            if (HasPendingTransition)
            {
                parentMemory.SetAlert(true);

                NotifyStateAvailable(
                    Definition != null ? Definition.Id : string.Empty,
                    previousAvailability ? "Unavailable" : "Locked",
                    "Available");
            }

            GameEvents.RaiseObjectStateChanged(this);
        }

        /// <summary>
        /// Devuelve true si el estado actual puede consumirse en este momento.
        /// </summary>
        public bool CanConsumeCurrentState(RuntimeContext context)
        {
            if (context == null || context.Repository == null)
            {
                return false;
            }

            if (!IsValidStateIndex(currentStateIndex))
            {
                return false;
            }

            if (consumedStates.Contains(currentStateIndex))
            {
                return false;
            }

            return IsStateEnabled(currentStateIndex, context);
        }

        /// <summary>
        /// Consume el estado actual si está habilitado.
        /// 
        /// Reglas:
        /// - Si el estado no está habilitado, no hace nada.
        /// - Si el estado ya fue consumido, no hace nada.
        /// - Si se consume correctamente, avanza linealmente al siguiente estado.
        /// - El siguiente estado nunca se ejecuta automáticamente: queda esperando un nuevo clic.
        /// </summary>
        public bool ConsumeCurrentState(RuntimeContext context)
        {
            if (!CanConsumeCurrentState(context))
            {
                return false;
            }

            int consumedIndex = currentStateIndex;
            consumedStates.Add(consumedIndex);

            Debug.Log($"[ObjectRuntimeData] Consumed state → {Definition.States[consumedIndex].StateId}");

            bool isLastState = consumedIndex >= Definition.States.Count - 1;

            if (isLastState)
            {
                HasPendingTransition = false;
                parentMemory.MarkAsCompleted();

                GameEvents.RaiseObjectStateChanged(this);
                GameEvents.RaisePlayerAction();

                return true;
            }

            currentStateIndex++;

            bool nextAvailable = IsStateEnabled(currentStateIndex, context);
            HasPendingTransition = nextAvailable;

            if (nextAvailable)
            {
                parentMemory.SetAlert(true);
            }

            GameEvents.RaiseObjectStateChanged(this);
            GameEvents.RaisePlayerAction();

            return true;
        }

        /// <summary>
        /// Evalúa si un índice de estado específico ya está habilitado.
        /// </summary>
        private bool IsStateEnabled(int stateIndex, RuntimeContext context)
        {
            if (!IsValidStateIndex(stateIndex))
            {
                return false;
            }

            ObjectStateDefinition state = Definition.States[stateIndex];
            if (state == null)
            {
                return false;
            }

            return ConditionEvaluator.Evaluate(
                state.TransitionConditions,
                context,
                out _);
        }

        /// <summary>
        /// Devuelve si un índice de estado ya fue consumido.
        /// </summary>
        public bool IsStateConsumed(int index)
        {
            return consumedStates.Contains(index);
        }

        /// <summary>
        /// Devuelve el índice actual pendiente de consumo.
        /// </summary>
        public int GetStateIndex()
        {
            return currentStateIndex;
        }

        /// <summary>
        /// Fuerza el índice actual.
        /// Se usa para persistencia/carga.
        /// </summary>
        public void ForceSetState(int index)
        {
            if (Definition == null || Definition.States == null || Definition.States.Count == 0)
            {
                currentStateIndex = 0;
                HasPendingTransition = false;
                return;
            }

            currentStateIndex = Mathf.Clamp(index, 0, Definition.States.Count - 1);
            HasPendingTransition = false;
        }

        /// <summary>
        /// Expone los índices ya consumidos para persistencia.
        /// </summary>
        public IEnumerable<int> GetConsumedStateIndexes()
        {
            return consumedStates;
        }

        /// <summary>
        /// Restaura la lista de estados consumidos.
        /// </summary>
        public void RestoreConsumedStates(IEnumerable<int> states)
        {
            consumedStates.Clear();

            if (states == null)
            {
                return;
            }

            foreach (int state in states)
            {
                consumedStates.Add(state);
            }
        }

        /// <summary>
        /// Asigna emoción runtime a un estado específico.
        /// </summary>
        public void SetEmotionForState(int stateIndex, RuntimeEmotionState emotion)
        {
            runtimeEmotions[stateIndex] = emotion;
            GameEvents.RaiseObjectStateChanged(this);
        }

        /// <summary>
        /// Obtiene emoción runtime si existe; de lo contrario usa la del data.
        /// </summary>
        public RuntimeEmotionState GetEmotionForState(int stateIndex)
        {
            if (runtimeEmotions.TryGetValue(stateIndex, out RuntimeEmotionState runtime))
            {
                return runtime;
            }

            ObjectStateDefinition def = Definition.States[stateIndex];
            return new RuntimeEmotionState(def.Emotion, def.Intensity);
        }

        /// <summary>
        /// Obtiene o crea el estado runtime de un grupo de acciones por id.
        /// </summary>
        public ActRuntimeData GetAct(string actId)
        {
            if (!acts.TryGetValue(actId, out ActRuntimeData act))
            {
                act = new ActRuntimeData(actId);
                acts.Add(actId, act);
            }

            return act;
        }

        /// <summary>
        /// Estado runtime opcional de objetos del mundo.
        /// Se mantiene por compatibilidad con otros subsistemas.
        /// </summary>
        public class WorldObjectState
        {
            public bool? visible;
            public bool? colliderEnabled;
        }

        public void SetWorldState(string id, Action<WorldObjectState> setter)
        {
            if (!worldStates.ContainsKey(id))
            {
                worldStates[id] = new WorldObjectState();
            }

            setter?.Invoke(worldStates[id]);
        }

        public WorldObjectState GetWorldState(string id)
        {
            return worldStates.TryGetValue(id, out WorldObjectState state) ? state : null;
        }

        private void NotifyStateAvailable(string objectId, string previousState, string newState)
        {
            string memoryId = parentMemory?.Definition != null
                ? parentMemory.Definition.Id
                : string.Empty;

            var message = new UnlockNotificationMessage(
                entityType: UnlockEntityType.ObjectState,
                notificationType: NarrativeNotificationType.StateAvailable,
                memoryId: memoryId,
                entityId: objectId,
                previousState: previousState,
                currentState: newState,
                reason: "Object state became available.",
                displayMessage: UnlockMessageBuilder.BuildStateAvailable(memoryId, objectId)
            );

            GameEvents.RaiseUnlockNotification(message);
        }

        private void NotifyObjectUnlocked(string objectId, string previousState, string newState)
        {
            string memoryId = parentMemory?.Definition != null
                ? parentMemory.Definition.Id
                : string.Empty;

            var message = new UnlockNotificationMessage(
                entityType: UnlockEntityType.Object,
                notificationType: NarrativeNotificationType.ObjectUnlocked,
                memoryId: memoryId,
                entityId: objectId,
                previousState: previousState,
                currentState: newState,
                reason: "Object unlocked.",
                displayMessage: UnlockMessageBuilder.BuildObjectUnlocked(memoryId, objectId)
            );

            GameEvents.RaiseUnlockNotification(message);
        }
        public bool GetHasPendingTransition()
        {
            return HasPendingTransition;
        }

        public IReadOnlyDictionary<int, RuntimeEmotionState> GetRuntimeEmotions()
        {
            return runtimeEmotions;
        }

        public IReadOnlyDictionary<string, ActRuntimeData> GetActs()
        {
            return acts;
        }

        public IReadOnlyDictionary<string, WorldObjectState> GetWorldStates()
        {
            return worldStates;
        }

        public void RestorePendingTransition(bool value)
        {
            HasPendingTransition = value;
        }

        public void RestoreRuntimeEmotion(int stateIndex, RuntimeEmotionState emotion)
        {
            runtimeEmotions[stateIndex] = emotion;
        }

        public void RestoreActState(string actId, bool hasExecuted)
        {
            ActRuntimeData act = GetAct(actId);
            act.hasExecuted = hasExecuted;
        }

        public void RestoreWorldState(
            string id,
            bool hasVisible,
            bool visible,
            bool hasColliderEnabled,
            bool colliderEnabled)
        {
            SetWorldState(id, state =>
            {
                state.visible = hasVisible ? visible : null;
                state.colliderEnabled = hasColliderEnabled ? colliderEnabled : null;
            });
        }
    }
}