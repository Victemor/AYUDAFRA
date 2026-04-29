using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using Game.Data;
using Game.Runtime;
using Game.Core;

/// <summary>
/// Sistema central de tutoriales del juego.
/// Controla visualización, persistencia y auto-cierre de instrucciones.
///
/// Localización:
/// - Usa <c>LocalizedText</c> (LocalizedString) si está asignado en <c>TutorialInstruction</c>.
/// - El campo <c>Text</c> (string plano) queda solo como referencia visual para el diseñador
///   y NO se ejecuta en la lógica. Si no hay LocalizedString asignado, el tutorial no muestra nada.
/// - Al cambiar de idioma mientras un tutorial está abierto, el texto se re-resuelve y
///   actualiza inmediatamente sin cerrar ni re-animar el panel.
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
    private float currentOffsetY;
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

        // Suscripción al cambio de idioma para actualizar el tutorial abierto.
        LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
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

        LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;

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
        currentOffsetY       = offsetY;

        StartCoroutine(ShowInstructionAsync(instruction, offsetY));

        if (instruction.DismissEvent == TutorialDismissEvent.OnEmotionSelected)
        {
            SubscribeToSubState();
        }
    }

    /// <summary>
    /// Resuelve el texto localizado y muestra el panel con fade.
    /// Solo usa <c>LocalizedText</c> (LocalizedString). El campo <c>Text</c>
    /// string plano queda solo como referencia visual para el diseñador.
    /// </summary>
    private IEnumerator ShowInstructionAsync(TutorialInstruction instruction, float offsetY)
    {
        if (instruction.LocalizedText == null || instruction.LocalizedText.IsEmpty)
        {
            Debug.LogWarning(
                $"[TutorialController] '{instruction.Id}' no tiene LocalizedText asignado. " +
                "Asigna tabla y clave en el Inspector. El campo Text es solo referencia visual.",
                this);
            yield break;
        }

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
    }

    /// <summary>
    /// Re-resuelve y actualiza el texto del tutorial activo cuando cambia el idioma.
    /// Usa <c>view.UpdateText()</c> para cambiar solo el texto sin re-animar el fade.
    /// </summary>
    private IEnumerator RefreshCurrentInstructionTextAsync()
    {
        if (currentInstruction == null)
        {
            yield break;
        }

        if (currentInstruction.LocalizedText == null || currentInstruction.LocalizedText.IsEmpty)
        {
            yield break;
        }

        var handle = currentInstruction.LocalizedText.GetLocalizedStringAsync();
        yield return handle;

        if (!handle.IsDone || string.IsNullOrWhiteSpace(handle.Result))
        {
            yield break;
        }

        view.UpdateText(handle.Result);
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

    /// <summary>
    /// Al cambiar de idioma, si hay un tutorial activo actualiza su texto inmediatamente
    /// sin cerrar ni re-animar el panel.
    /// </summary>
    private void HandleLocaleChanged(Locale locale)
    {
        if (!HasActiveTutorial)
        {
            return;
        }

        StartCoroutine(RefreshCurrentInstructionTextAsync());
    }

    private void HandleMemoryStateChanged(MemoryRuntimeData memory)
    {
        if (memory == null) return;

        if (memory.CurrentState == MemoryState.Seen || memory.CurrentState == MemoryState.Completed)
        {
            TryAutoDismiss(TutorialDismissEvent.OnFragmentOpened);
        }
    }

    private void HandleMemoryConnectionChanged(string idA, string idB) =>
        TryAutoDismiss(TutorialDismissEvent.OnFragmentConnected);

    private void HandleFragmentRenamed() =>
        TryAutoDismiss(TutorialDismissEvent.OnFragmentRenamed);

    private void HandleInventoryChanged() =>
        TryAutoDismiss(TutorialDismissEvent.OnInventoryChanged);

    private void HandleDropMoved() =>
        TryAutoDismiss(TutorialDismissEvent.OnDropMoved);

    private void HandleDropRightClicked() =>
        TryAutoDismiss(TutorialDismissEvent.OnDropRightClicked);

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
        if (!debugLogs) return;
        Debug.Log($"[TutorialController] {message}", this);
    }
}