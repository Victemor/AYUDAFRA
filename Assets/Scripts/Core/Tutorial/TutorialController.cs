using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using Game.Runtime;
using Game.Core;

/// <summary>
/// Sistema central de tutoriales del juego.
/// Controla visualización, persistencia y auto-cierre de instrucciones.
///
/// Resolución de texto (prioridad descendente):
/// 1. Si <c>instruction.LocalizedText</c> está asignado → ruta localizada (async).
/// 2. Si no → <c>instruction.Text</c> directo (fallback, preserva textos existentes).
/// </summary>
public class TutorialController : MonoBehaviour
{
    [Header("Configuration")]

    [SerializeField]
    [Tooltip("Lista global de instrucciones del juego. Todas deben tener IDs únicos.")]
    private List<TutorialInstruction> instructions;

    [SerializeField]
    [Tooltip("Referencia a la vista del tutorial.")]
    private TutorialView view;

    [Header("Input Detection")]

    [SerializeField]
    [Tooltip("Umbral de movimiento de mouse para disparar el dismiss OnExplore.")]
    private float mouseMoveThreshold = 0.1f;

    [Header("Debug")]

    [SerializeField]
    private string debugId;

    [SerializeField]
    private float debugOffset;

    [SerializeField]
    private bool debugLogs;

    private Dictionary<string, TutorialInstruction> instructionMap;
    private string currentInstructionId;
    private TutorialInstruction currentInstruction;
    private bool isSubscribedToSubState;

    public System.Action<string> OnInstructionClosed;

    public bool HasActiveTutorial => !string.IsNullOrEmpty(currentInstructionId);

    private void Awake()
    {
        BuildDictionary();
    }

    private void Start()
    {
        RestoreProgress();
    }

    private void OnEnable()
    {
        GameEvents.OnMemoryStateChanged       += HandleMemoryStateChanged;
        GameEvents.OnMemoryConnectionChanged   += HandleMemoryConnectionChanged;
        GameEvents.OnFragmentRenamed           += HandleFragmentRenamed;
        GameEvents.OnDraggableInventoryChanged += HandleInventoryChanged;
        GameEvents.OnDraggableItemStateChanged += HandleDraggableItemStateChanged;
        GameEvents.OnDropMoved                 += HandleDropMoved;
        GameEvents.OnDropRightClicked          += HandleDropRightClicked;
        GameEvents.OnInteractableClicked       += HandleInteractableClicked;
    }

    private void OnDisable()
    {
        GameEvents.OnMemoryStateChanged       -= HandleMemoryStateChanged;
        GameEvents.OnMemoryConnectionChanged   -= HandleMemoryConnectionChanged;
        GameEvents.OnFragmentRenamed           -= HandleFragmentRenamed;
        GameEvents.OnDraggableInventoryChanged -= HandleInventoryChanged;
        GameEvents.OnDraggableItemStateChanged -= HandleDraggableItemStateChanged;
        GameEvents.OnDropMoved                 -= HandleDropMoved;
        GameEvents.OnDropRightClicked          -= HandleDropRightClicked;
        GameEvents.OnInteractableClicked       -= HandleInteractableClicked;

        UnsubscribeFromSubState();
    }

    private void Update()
    {
        if (!HasActiveTutorial || currentInstruction == null)
        {
            return;
        }

        switch (currentInstruction.DismissEvent)
        {
            case TutorialDismissEvent.OnExplore:
                if (Mathf.Abs(Input.GetAxis("Mouse X")) > mouseMoveThreshold ||
                    Mathf.Abs(Input.GetAxis("Mouse Y")) > mouseMoveThreshold)
                {
                    TryAutoDismiss(TutorialDismissEvent.OnExplore);
                }
                break;

            case TutorialDismissEvent.OnZoom:
                if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
                {
                    TryAutoDismiss(TutorialDismissEvent.OnZoom);
                }
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public void ShowInstruction(string id, float offsetY = 0f)
    {
        if (!TryGetInstruction(id, out TutorialInstruction instruction))
        {
            return;
        }

        if (instruction.HasBeenShown)
        {
            return;
        }

        if (!CanShowTutorialInCurrentState(instruction))
        {
            Log($"Tutorial '{id}' no puede mostrarse en el estado actual.");
            return;
        }

        instruction.MarkAsShown();
        PersistShownInstruction(id);

        ShowInstructionInternal(id, instruction, offsetY);
        SaveProgressSafe();
    }

    public void ForceShowInstruction(string id, float offsetY = 0f)
    {
        if (!TryGetInstruction(id, out TutorialInstruction instruction))
        {
            return;
        }

        if (!CanShowTutorialInCurrentState(instruction))
        {
            Log($"ForceShow bloqueado para tutorial '{id}' por estado actual.");
            return;
        }

        ShowInstructionInternal(id, instruction, offsetY);
    }

    public void HideTutorial() => ExecuteHide();

    public void HideTutorial(string id)
    {
        if (currentInstructionId == id)
        {
            ExecuteHide();
        }
    }

    public bool HasBeenShown(string id)
    {
        return instructionMap.TryGetValue(id, out TutorialInstruction instruction) && instruction.HasBeenShown;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private — Show
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowInstructionInternal(string id, TutorialInstruction instruction, float offsetY)
    {
        currentInstructionId = id;
        currentInstruction   = instruction;

        StartCoroutine(ShowInstructionAsync(instruction, offsetY));

        if (instruction.DismissEvent == TutorialDismissEvent.OnEmotionSelected)
        {
            SubscribeToSubState();
        }
    }

    /// <summary>
    /// Resuelve el texto de la instrucción y lo muestra.
    ///
    /// Prioridad:
    /// 1. <c>LocalizedText</c> asignado → ruta localizada (async).
    /// 2. <c>Text</c> no vacío → ruta raw directa (fallback, preserva textos existentes).
    /// </summary>
    private IEnumerator ShowInstructionAsync(TutorialInstruction instruction, float offsetY)
    {
        // ── Ruta localizada ──────────────────────────────────────────────────
        if (instruction.LocalizedText != null && !instruction.LocalizedText.IsEmpty)
        {
            var handle = instruction.LocalizedText.GetLocalizedStringAsync();
            yield return handle;

            if (!handle.IsDone || string.IsNullOrWhiteSpace(handle.Result))
            {
                Debug.LogWarning(
                    $"[TutorialController] No se pudo resolver LocalizedText para '{instruction.Id}'.",
                    this);
                yield break;
            }

            view.Show(handle.Result, instruction.Image, offsetY);
            yield break;
        }

        // ── Ruta raw (fallback) ──────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(instruction.Text))
        {
            view.Show(instruction.Text, instruction.Image, offsetY);
            yield break;
        }

        Debug.LogWarning(
            $"[TutorialController] Instrucción '{instruction.Id}' sin texto ni clave de localización.",
            this);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private — Dismiss
    // ─────────────────────────────────────────────────────────────────────────

    private void TryAutoDismiss(TutorialDismissEvent eventType)
    {
        if (!HasActiveTutorial || currentInstruction == null)
        {
            return;
        }

        if (currentInstruction.DismissEvent == eventType)
        {
            ExecuteHide();
        }
    }

    private bool CanShowTutorialInCurrentState(TutorialInstruction instruction)
    {
        if (GamePlayStateController.Instance == null)
        {
            return true;
        }

        return instruction.CanShowInState(GamePlayStateController.Instance.CurrentState);
    }

    private bool TryGetInstruction(string id, out TutorialInstruction instruction)
    {
        instruction = null;

        if (instructionMap == null || !instructionMap.TryGetValue(id, out instruction))
        {
            Debug.LogError($"[TutorialController] El ID '{id}' no existe.", this);
            return false;
        }

        return true;
    }

    private void SubscribeToSubState()
    {
        if (isSubscribedToSubState || GamePlayStateController.Instance == null)
        {
            return;
        }

        GamePlayStateController.Instance.SubStateChanged += HandleSubStateChanged;
        isSubscribedToSubState = true;
    }

    private void UnsubscribeFromSubState()
    {
        if (!isSubscribedToSubState)
        {
            return;
        }

        if (GamePlayStateController.Instance != null)
        {
            GamePlayStateController.Instance.SubStateChanged -= HandleSubStateChanged;
        }

        isSubscribedToSubState = false;
    }

    private void ExecuteHide()
    {
        view.Hide();
        UnsubscribeFromSubState();

        if (string.IsNullOrEmpty(currentInstructionId))
        {
            return;
        }

        string closedId = currentInstructionId;

        currentInstructionId = null;
        currentInstruction   = null;

        OnInstructionClosed?.Invoke(closedId);
        SaveProgressSafe();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private — Dictionary / Persistence
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildDictionary()
    {
        instructionMap = new Dictionary<string, TutorialInstruction>();

        if (instructions == null)
        {
            return;
        }

        foreach (TutorialInstruction instruction in instructions)
        {
            if (instruction == null || string.IsNullOrEmpty(instruction.Id))
            {
                Debug.LogWarning("[TutorialController] Instrucción nula o sin ID.", this);
                continue;
            }

            if (!instructionMap.TryAdd(instruction.Id, instruction))
            {
                Debug.LogError($"[TutorialController] ID duplicado: {instruction.Id}", this);
            }
        }
    }

    private void RestoreProgress()
    {
        FragmentProgressData progress = GameManager.Instance?.FragmentProgress;

        if (progress?.tutorialProgress == null || instructions == null)
        {
            return;
        }

        foreach (TutorialInstruction instruction in instructions)
        {
            if (instruction == null || string.IsNullOrWhiteSpace(instruction.Id))
            {
                continue;
            }

            instruction.RestoreShownState(progress.tutorialProgress.HasShown(instruction.Id));
        }
    }

    private void PersistShownInstruction(string instructionId)
    {
        FragmentProgressData progress = GameManager.Instance?.FragmentProgress;

        if (progress == null)
        {
            return;
        }

        progress.tutorialProgress ??= new TutorialProgressData();
        progress.tutorialProgress.MarkAsShown(instructionId);
    }

    private void SaveProgressSafe() => GameManager.Instance?.SaveProgress();

    // ─────────────────────────────────────────────────────────────────────────
    // Private — Event Handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleMemoryStateChanged(MemoryRuntimeData memory)
    {
        if (memory == null)
        {
            return;
        }

        if (memory.CurrentState == MemoryState.Seen || memory.CurrentState == MemoryState.Completed)
        {
            TryAutoDismiss(TutorialDismissEvent.OnFragmentOpened);
        }
    }

    private void HandleMemoryConnectionChanged(string idA, string idB) =>
        TryAutoDismiss(TutorialDismissEvent.OnFragmentConnected);

    /// <summary>GameEvents.OnFragmentRenamed es Action (sin parámetros).</summary>
    private void HandleFragmentRenamed() =>
        TryAutoDismiss(TutorialDismissEvent.OnFragmentRenamed);

    /// <summary>GameEvents.OnDraggableInventoryChanged es Action (sin parámetros).</summary>
    private void HandleInventoryChanged() =>
        TryAutoDismiss(TutorialDismissEvent.OnInventoryChanged);

    /// <summary>GameEvents.OnDropMoved es Action (sin parámetros).</summary>
    private void HandleDropMoved() =>
        TryAutoDismiss(TutorialDismissEvent.OnDropMoved);

    /// <summary>GameEvents.OnDropRightClicked es Action (sin parámetros).</summary>
    private void HandleDropRightClicked() =>
        TryAutoDismiss(TutorialDismissEvent.OnDropRightClicked);

    /// <summary>GameEvents.OnInteractableClicked es Action (sin parámetros).</summary>
    private void HandleInteractableClicked() =>
        TryAutoDismiss(TutorialDismissEvent.OnInteractableClicked);

    private void HandleDraggableItemStateChanged(DraggableItemRuntimeData item)
    {
        if (!HasActiveTutorial || currentInstruction == null || item == null)
        {
            return;
        }

        TutorialDismissEvent dismissEvent = currentInstruction.DismissEvent;

        if (dismissEvent != TutorialDismissEvent.OnDraggableItemPickedUp &&
            dismissEvent != TutorialDismissEvent.OnDraggableItemMoved)
        {
            return;
        }

        if (currentInstruction.DismissItemDefinition != null)
        {
            if (item.Definition == null || item.Definition.Id != currentInstruction.DismissItemDefinition.Id)
            {
                return;
            }
        }

        if (dismissEvent == TutorialDismissEvent.OnDraggableItemMoved &&
            item.CurrentState == DraggableItemState.Held)
        {
            ExecuteHide();
            return;
        }

        if (dismissEvent == TutorialDismissEvent.OnDraggableItemPickedUp &&
            item.CurrentState != DraggableItemState.Held)
        {
            ExecuteHide();
        }
    }

    /// <summary>
    /// El dismiss se activa cuando el subestado SALE de EmotionSelection
    /// (es decir, cuando la selección emocional acaba de completarse).
    /// </summary>
    private void HandleSubStateChanged(GamePlaySubState previous, GamePlaySubState current)
    {
        if (previous == GamePlaySubState.EmotionSelection)
        {
            TryAutoDismiss(TutorialDismissEvent.OnEmotionSelected);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private — Debug
    // ─────────────────────────────────────────────────────────────────────────

    private void Log(string message)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log($"[TutorialController] {message}", this);
    }
}