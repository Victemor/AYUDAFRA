using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Estado principal del juego.
    /// Define qué contexto domina la experiencia actual.
    /// </summary>
    public enum GamePlayState
    {
        None = 0,
        Menu = 1,
        Exploration = 2,
        ActionFlow = 3,
        Gameplay = 4,
        Paused = 5,
        Transition = 6,
        Blocked = 7
    }

    /// <summary>
    /// Subestado contextual dentro del estado principal.
    /// Refina qué sistema concreto tiene el control en ese contexto.
    /// </summary>
    public enum GamePlaySubState
    {
        None = 0,

        FreeExploration = 1,
        DraggingObject = 2,

        Cinematic = 10,
        Dialogue = 11,
        EmotionSelection = 12,

        GameplayActive = 20
    }

    /// <summary>
    /// Snapshot inmutable del estado jugable actual.
    /// Útil para debugging, logging y consumo desacoplado.
    /// </summary>
    [Serializable]
    public struct GamePlayStateSnapshot
    {
        /// <summary>
        /// Estado principal actual.
        /// </summary>
        public GamePlayState State;

        /// <summary>
        /// Subestado actual.
        /// </summary>
        public GamePlaySubState SubState;

        /// <summary>
        /// Indica si el input global debe considerarse bloqueado.
        /// </summary>
        public bool IsInputBlocked;

        /// <summary>
        /// Indica si la exploración libre está habilitada.
        /// </summary>
        public bool IsExplorationAvailable;

        /// <summary>
        /// Indica si el juego está actualmente dentro de un flujo de acciones.
        /// </summary>
        public bool IsInActionFlow;

        /// <summary>
        /// Indica si el juego está actualmente dentro de una cinemática.
        /// </summary>
        public bool IsInCinematic;

        /// <summary>
        /// Indica si el juego está actualmente en diálogo.
        /// </summary>
        public bool IsInDialogue;

        /// <summary>
        /// Indica si el juego está actualmente en selección emocional.
        /// </summary>
        public bool IsInEmotionSelection;

        /// <summary>
        /// Indica si el jugador está arrastrando un objeto con el cursor.
        /// </summary>
        public bool IsDraggingObject;
    }

    /// <summary>
    /// Controlador global del estado jugable.
    /// 
    /// Responsabilidades:
    /// - Ser la fuente única de verdad del estado jugable actual.
    /// - Validar combinaciones entre estado principal y subestado.
    /// - Exponer eventos de cambio para desacoplar sistemas.
    /// - Ofrecer una API concreta para los flujos reales del proyecto.
    /// 
    /// No contiene lógica específica de cámara, UI, inventario, fragmentos o diálogos.
    /// Esos sistemas deben consumir este controlador o solicitar cambios a través de él.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class GamePlayStateController : MonoBehaviour
    {
        #region Singleton

        /// <summary>
        /// Instancia global del controlador de estado jugable.
        /// </summary>
        public static GamePlayStateController Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("Initial State")]

        [SerializeField]
        [Tooltip("Estado principal inicial del juego.")]
        private GamePlayState initialState = GamePlayState.Menu;

        [SerializeField]
        [Tooltip("Subestado inicial del juego.")]
        private GamePlaySubState initialSubState = GamePlaySubState.None;

        [Header("Debug")]

        [SerializeField]
        [Tooltip("Activa logs detallados cuando cambian el estado o subestado.")]
        private bool verboseLogging = true;

        #endregion

        #region Properties

        /// <summary>
        /// Estado principal actual del juego.
        /// </summary>
        public GamePlayState CurrentState { get; private set; }

        /// <summary>
        /// Subestado actual del juego.
        /// </summary>
        public GamePlaySubState CurrentSubState { get; private set; }

        /// <summary>
        /// Indica si el input global debe considerarse bloqueado.
        /// </summary>
        public bool IsInputBlocked =>
            CurrentState == GamePlayState.Blocked ||
            CurrentState == GamePlayState.Transition ||
            CurrentState == GamePlayState.Paused ||
            CurrentState == GamePlayState.Menu ||
            CurrentState == GamePlayState.ActionFlow;

        /// <summary>
        /// Indica si la exploración libre está activa.
        /// </summary>
        public bool IsFreeExploration =>
            CurrentState == GamePlayState.Exploration &&
            CurrentSubState == GamePlaySubState.FreeExploration;

        /// <summary>
        /// Indica si el juego está en exploración, incluyendo dragging.
        /// </summary>
        public bool IsInExploration =>
            CurrentState == GamePlayState.Exploration;

        /// <summary>
        /// Indica si el juego está dentro de un flujo de acciones.
        /// </summary>
        public bool IsInActionFlow =>
            CurrentState == GamePlayState.ActionFlow;

        /// <summary>
        /// Indica si el juego está actualmente en una cinemática.
        /// </summary>
        public bool IsInCinematic =>
            CurrentState == GamePlayState.ActionFlow &&
            CurrentSubState == GamePlaySubState.Cinematic;

        /// <summary>
        /// Indica si el juego está actualmente en diálogo.
        /// </summary>
        public bool IsInDialogue =>
            CurrentState == GamePlayState.ActionFlow &&
            CurrentSubState == GamePlaySubState.Dialogue;

        /// <summary>
        /// Indica si el juego está actualmente en selección emocional.
        /// </summary>
        public bool IsInEmotionSelection =>
            CurrentState == GamePlayState.ActionFlow &&
            CurrentSubState == GamePlaySubState.EmotionSelection;

        /// <summary>
        /// Indica si el jugador está arrastrando un objeto con el cursor.
        /// </summary>
        public bool IsDraggingObject =>
            CurrentState == GamePlayState.Exploration &&
            CurrentSubState == GamePlaySubState.DraggingObject;

        /// <summary>
        /// Indica si el juego está en un estado de gameplay embebido.
        /// </summary>
        public bool IsInGameplay =>
            CurrentState == GamePlayState.Gameplay;

        /// <summary>
        /// Indica si el juego está en pausa.
        /// </summary>
        public bool IsPaused =>
            CurrentState == GamePlayState.Paused;

        #endregion

        #region Events

        /// <summary>
        /// Se dispara cuando cambia el estado principal.
        /// </summary>
        public event Action<GamePlayState, GamePlayState> StateChanged;

        /// <summary>
        /// Se dispara cuando cambia el subestado.
        /// </summary>
        public event Action<GamePlaySubState, GamePlaySubState> SubStateChanged;

        /// <summary>
        /// Se dispara cuando cambia cualquier parte del estado jugable.
        /// </summary>
        public event Action<GamePlayStateSnapshot> SnapshotChanged;

        #endregion

        #region Unity Methods

        /// <summary>
        /// Inicializa el singleton y el estado inicial.
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GAMEPLAY STATE] Duplicate instance detected. Destroying the new instance.");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (!IsCombinationValid(initialState, initialSubState))
            {
                Debug.LogWarning($"[GAMEPLAY STATE] Invalid initial combination '{initialState} / {initialSubState}'. Falling back to Menu / None.");
                initialState = GamePlayState.Menu;
                initialSubState = GamePlaySubState.None;
            }

            CurrentState = initialState;
            CurrentSubState = initialSubState;

            Log($"Initialized | State={CurrentState} | SubState={CurrentSubState}");
            RaiseSnapshotChanged();
        }

        #endregion

        #region Public API - Queries

        /// <summary>
        /// Indica si el estado principal actual coincide con el consultado.
        /// </summary>
        /// <param name="state">Estado a comparar.</param>
        /// <returns>True si coincide.</returns>
        public bool IsInState(GamePlayState state)
        {
            return CurrentState == state;
        }

        /// <summary>
        /// Indica si el subestado actual coincide con el consultado.
        /// </summary>
        /// <param name="subState">Subestado a comparar.</param>
        /// <returns>True si coincide.</returns>
        public bool IsInSubState(GamePlaySubState subState)
        {
            return CurrentSubState == subState;
        }

        /// <summary>
        /// Indica si el estado principal y subestado actuales coinciden con la combinación consultada.
        /// </summary>
        /// <param name="state">Estado principal esperado.</param>
        /// <param name="subState">Subestado esperado.</param>
        /// <returns>True si ambos coinciden.</returns>
        public bool IsInState(GamePlayState state, GamePlaySubState subState)
        {
            return CurrentState == state && CurrentSubState == subState;
        }

        /// <summary>
        /// Devuelve un snapshot del estado jugable actual.
        /// </summary>
        /// <returns>Snapshot del estado actual.</returns>
        public GamePlayStateSnapshot GetSnapshot()
        {
            return new GamePlayStateSnapshot
            {
                State = CurrentState,
                SubState = CurrentSubState,
                IsInputBlocked = IsInputBlocked,
                IsExplorationAvailable = IsFreeExploration,
                IsInActionFlow = IsInActionFlow,
                IsInCinematic = IsInCinematic,
                IsInDialogue = IsInDialogue,
                IsInEmotionSelection = IsInEmotionSelection,
                IsDraggingObject = IsDraggingObject
            };
        }

        #endregion

        #region Public API - Generic State Changes

        /// <summary>
        /// Establece únicamente el estado principal.
        /// Si el subestado actual deja de ser válido, se reinicia automáticamente a None.
        /// </summary>
        /// <param name="newState">Nuevo estado principal.</param>
        public void SetState(GamePlayState newState)
        {
            if (CurrentState == newState)
            {
                return;
            }

            GamePlayState previousState = CurrentState;
            CurrentState = newState;

            if (!IsCombinationValid(CurrentState, CurrentSubState))
            {
                SetSubStateInternal(GamePlaySubState.None);
            }

            StateChanged?.Invoke(previousState, CurrentState);
            Log($"State changed: {previousState} -> {CurrentState}");
            RaiseSnapshotChanged();
        }

        /// <summary>
        /// Establece únicamente el subestado si es compatible con el estado principal actual.
        /// </summary>
        /// <param name="newSubState">Nuevo subestado.</param>
        /// <returns>True si el cambio fue aplicado.</returns>
        public bool SetSubState(GamePlaySubState newSubState)
        {
            if (!IsCombinationValid(CurrentState, newSubState))
            {
                Debug.LogWarning($"[GAMEPLAY STATE] Invalid substate '{newSubState}' for state '{CurrentState}'.");
                return false;
            }

            SetSubStateInternal(newSubState);
            RaiseSnapshotChanged();
            return true;
        }

        /// <summary>
        /// Establece simultáneamente estado principal y subestado si la combinación es válida.
        /// </summary>
        /// <param name="newState">Nuevo estado principal.</param>
        /// <param name="newSubState">Nuevo subestado.</param>
        /// <returns>True si el cambio fue aplicado.</returns>
        public bool SetState(GamePlayState newState, GamePlaySubState newSubState)
        {
            if (!IsCombinationValid(newState, newSubState))
            {
                Debug.LogWarning($"[GAMEPLAY STATE] Invalid state combination '{newState} / {newSubState}'.");
                return false;
            }

            bool stateChanged = CurrentState != newState;
            bool subStateChanged = CurrentSubState != newSubState;

            if (!stateChanged && !subStateChanged)
            {
                return true;
            }

            GamePlayState previousState = CurrentState;
            GamePlaySubState previousSubState = CurrentSubState;

            CurrentState = newState;
            CurrentSubState = newSubState;

            if (stateChanged)
            {
                StateChanged?.Invoke(previousState, CurrentState);
            }

            if (subStateChanged)
            {
                SubStateChanged?.Invoke(previousSubState, CurrentSubState);
            }

            Log($"State changed: {previousState} -> {CurrentState} | SubState changed: {previousSubState} -> {CurrentSubState}");
            RaiseSnapshotChanged();
            return true;
        }

        /// <summary>
        /// Fuerza el estado principal y subestado sin validar combinaciones.
        /// Debe reservarse para restauraciones controladas o debugging.
        /// </summary>
        /// <param name="newState">Nuevo estado principal.</param>
        /// <param name="newSubState">Nuevo subestado.</param>
        public void ForceSetState(GamePlayState newState, GamePlaySubState newSubState)
        {
            GamePlayState previousState = CurrentState;
            GamePlaySubState previousSubState = CurrentSubState;

            bool stateChanged = previousState != newState;
            bool subStateChanged = previousSubState != newSubState;

            CurrentState = newState;
            CurrentSubState = newSubState;

            if (stateChanged)
            {
                StateChanged?.Invoke(previousState, CurrentState);
            }

            if (subStateChanged)
            {
                SubStateChanged?.Invoke(previousSubState, CurrentSubState);
            }

            Log($"State forced: {previousState} -> {CurrentState} | SubState forced: {previousSubState} -> {CurrentSubState}");
            RaiseSnapshotChanged();
        }

        #endregion

        #region Public API - Project-Specific Convenience Methods

        /// <summary>
        /// Entra al menú.
        /// </summary>
        public void EnterMenu()
        {
            SetState(GamePlayState.Menu, GamePlaySubState.None);
        }

        /// <summary>
        /// Entra en exploración libre.
        /// </summary>
        public void EnterFreeExploration()
        {
            SetState(GamePlayState.Exploration, GamePlaySubState.FreeExploration);
        }

        /// <summary>
        /// Entra en exploración con un objeto draggable tomado por el cursor.
        /// </summary>
        public void EnterDraggingObject()
        {
            SetState(GamePlayState.Exploration, GamePlaySubState.DraggingObject);
        }

        /// <summary>
        /// Sale del modo dragging y vuelve a exploración libre.
        /// </summary>
        public void ExitDraggingObject()
        {
            EnterFreeExploration();
        }

        /// <summary>
        /// Entra en flujo de acciones sin subestado específico.
        /// </summary>
        public void EnterActionFlow()
        {
            SetState(GamePlayState.ActionFlow, GamePlaySubState.None);
        }

        /// <summary>
        /// Entra en un flujo de acciones con subestado cinemático.
        /// </summary>
        public void EnterActionFlowCinematic()
        {
            SetState(GamePlayState.ActionFlow, GamePlaySubState.Cinematic);
        }

        /// <summary>
        /// Entra en un flujo de acciones con diálogo.
        /// </summary>
        public void EnterDialogue()
        {
            SetState(GamePlayState.ActionFlow, GamePlaySubState.Dialogue);
        }

        /// <summary>
        /// Entra en un flujo de acciones con selección emocional.
        /// </summary>
        public void EnterEmotionSelection()
        {
            SetState(GamePlayState.ActionFlow, GamePlaySubState.EmotionSelection);
        }

        /// <summary>
        /// Sale del flujo de acciones y vuelve a exploración libre.
        /// </summary>
        public void ExitActionFlowToExploration()
        {
            EnterFreeExploration();
        }

        /// <summary>
        /// Entra en gameplay embebido o minijuego.
        /// </summary>
        public void EnterGameplay()
        {
            SetState(GamePlayState.Gameplay, GamePlaySubState.GameplayActive);
        }

        /// <summary>
        /// Sale del gameplay y vuelve a exploración libre.
        /// </summary>
        public void ExitGameplayToExploration()
        {
            EnterFreeExploration();
        }

        /// <summary>
        /// Entra en pausa.
        /// </summary>
        public void EnterPause()
        {
            SetState(GamePlayState.Paused, GamePlaySubState.None);
        }

        /// <summary>
        /// Sale de pausa y vuelve a exploración libre.
        /// </summary>
        public void ExitPauseToExploration()
        {
            EnterFreeExploration();
        }

        /// <summary>
        /// Entra en transición.
        /// </summary>
        public void EnterTransition()
        {
            SetState(GamePlayState.Transition, GamePlaySubState.None);
        }

        /// <summary>
        /// Entra en bloqueo total temporal.
        /// </summary>
        public void EnterBlocked()
        {
            SetState(GamePlayState.Blocked, GamePlaySubState.None);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Cambia internamente el subestado actual.
        /// </summary>
        /// <param name="newSubState">Nuevo subestado.</param>
        private void SetSubStateInternal(GamePlaySubState newSubState)
        {
            if (CurrentSubState == newSubState)
            {
                return;
            }

            GamePlaySubState previousSubState = CurrentSubState;
            CurrentSubState = newSubState;

            SubStateChanged?.Invoke(previousSubState, CurrentSubState);
            Log($"SubState changed: {previousSubState} -> {CurrentSubState}");
        }

        /// <summary>
        /// Valida si una combinación entre estado principal y subestado es permitida.
        /// </summary>
        /// <param name="state">Estado principal a validar.</param>
        /// <param name="subState">Subestado a validar.</param>
        /// <returns>True si la combinación es válida.</returns>
        private bool IsCombinationValid(GamePlayState state, GamePlaySubState subState)
        {
            switch (state)
            {
                case GamePlayState.None:
                case GamePlayState.Menu:
                case GamePlayState.Paused:
                case GamePlayState.Transition:
                case GamePlayState.Blocked:
                    return subState == GamePlaySubState.None;

                case GamePlayState.Exploration:
                    return subState == GamePlaySubState.None ||
                           subState == GamePlaySubState.FreeExploration ||
                           subState == GamePlaySubState.DraggingObject;

                case GamePlayState.ActionFlow:
                    return subState == GamePlaySubState.None ||
                           subState == GamePlaySubState.Cinematic ||
                           subState == GamePlaySubState.Dialogue ||
                           subState == GamePlaySubState.EmotionSelection;

                case GamePlayState.Gameplay:
                    return subState == GamePlaySubState.None ||
                           subState == GamePlaySubState.GameplayActive;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Lanza el evento de snapshot actualizado.
        /// </summary>
        private void RaiseSnapshotChanged()
        {
            SnapshotChanged?.Invoke(GetSnapshot());
        }

        /// <summary>
        /// Escribe logs si la verbosidad está habilitada.
        /// </summary>
        /// <param name="message">Mensaje a registrar.</param>
        private void Log(string message)
        {
            if (!verboseLogging)
            {
                return;
            }

            Debug.Log($"[GAMEPLAY STATE] {message}");
        }

        #endregion
    }
}