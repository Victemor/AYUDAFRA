using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Runtime;
using Game.Save;
using Game.Data;

/// <summary>
/// Controlador encargado de disparar guardado automático y reaplicar
/// persistencia de objetos de escena cuando una nueva escena termina de cargar.
/// </summary>
public sealed class AutoSaveController : MonoBehaviour
{
    #region Serialized Fields

    [SerializeField]
    [Tooltip("Guardar automáticamente al pausar la app.")]
    private bool saveOnApplicationPause = true;

    [SerializeField]
    [Tooltip("Guardar automáticamente al cerrar la app.")]
    private bool saveOnApplicationQuit = true;

    [SerializeField]
    [Tooltip("Guardar automáticamente al descargar una escena.")]
    private bool saveOnSceneUnload = true;

    [SerializeField]
    [Tooltip("Aplicar estado persistido de objetos de escena cuando una escena termine de cargar.")]
    private bool applySceneWorldObjectStateOnSceneLoaded = true;

    [SerializeField]
    [Tooltip("Activa logs de depuración del autosave.")]
    private bool debugLogs = true;

    #endregion

    #region Private Fields

    private bool isSavingScheduled;
    private bool isQuitting;

    #endregion

    #region Unity Messages

    private void OnEnable()
    {
        GameEvents.OnMemoryStateChanged += HandleMemoryChanged;
        GameEvents.OnObjectStateChanged += HandleObjectChanged;
        GameEvents.OnMemoryConnectionChanged += HandleConnectionChanged;
        GameEvents.OnDraggableItemStateChanged += HandleDraggableItemChanged;
        GameEvents.OnFragmentDraggableSlotStateChanged += HandleFragmentDraggableSlotChanged;
        GameEvents.OnDraggableInventoryChanged += HandleDraggableInventoryChanged;

        ConsciousnessSystem.OnThoughtAdded += HandleThoughtAdded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        GameEvents.OnMemoryStateChanged -= HandleMemoryChanged;
        GameEvents.OnObjectStateChanged -= HandleObjectChanged;
        GameEvents.OnMemoryConnectionChanged -= HandleConnectionChanged;
        GameEvents.OnDraggableItemStateChanged -= HandleDraggableItemChanged;
        GameEvents.OnFragmentDraggableSlotStateChanged -= HandleFragmentDraggableSlotChanged;
        GameEvents.OnDraggableInventoryChanged -= HandleDraggableInventoryChanged;

        ConsciousnessSystem.OnThoughtAdded -= HandleThoughtAdded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && saveOnApplicationPause)
        {
            SaveImmediate("Application pause");
        }
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;

        if (saveOnApplicationQuit)
        {
            SaveImmediate("Application quit");
        }
    }

    #endregion

    #region Event Handlers

    private void HandleMemoryChanged(MemoryRuntimeData _)
    {
        ScheduleSave("Memory changed");
    }

    private void HandleObjectChanged(ObjectRuntimeData _)
    {
        ScheduleSave("Object changed");
    }

    private void HandleConnectionChanged(string _, string __)
    {
        ScheduleSave("Memory connection changed");
    }

    private void HandleDraggableItemChanged(DraggableItemRuntimeData item)
    {
        if (item == null)
        {
            return;
        }

        if (item.CurrentState == DraggableItemState.Held)
        {
            Log($"Skip autosave -> Item entered Held state: {item.Definition.Id}");
            return;
        }

        ScheduleSave($"Draggable item changed: {item.Definition.Id} -> {item.CurrentState}");
    }

    private void HandleFragmentDraggableSlotChanged(FragmentDraggableSlotRuntimeData slot)
    {
        if (slot == null)
        {
            return;
        }

        ScheduleSave($"Fragment draggable slot changed: {slot.SlotId} -> {slot.CurrentState}");
    }

    private void HandleDraggableInventoryChanged()
    {
        if (DraggableInventorySystem.Instance != null &&
            DraggableInventorySystem.Instance.Inventory != null &&
            DraggableInventorySystem.Instance.Inventory.HasHeldItem)
        {
            string heldItemId = DraggableInventorySystem.Instance.Inventory.HeldItem != null &&
                                DraggableInventorySystem.Instance.Inventory.HeldItem.Definition != null
                ? DraggableInventorySystem.Instance.Inventory.HeldItem.Definition.Id
                : "UNKNOWN";

            Log($"Skip autosave -> Inventory changed while holding item: {heldItemId}");
            return;
        }

        ScheduleSave("Draggable inventory changed");
    }

    private void HandleThoughtAdded(ConsciousnessSystem.ThoughtData _)
    {
        ScheduleSave("Thought added");
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        if (!saveOnSceneUnload || isQuitting)
        {
            return;
        }

        SaveImmediate($"Scene unloaded: {scene.name}");
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!applySceneWorldObjectStateOnSceneLoaded || !SaveSystem.HasLoadedData)
        {
            return;
        }

        Log($"Apply scene world state -> {scene.name}");
        SaveSystem.ApplyLoadedSceneWorldObjects();
    }

    #endregion

    #region Private Methods

    private void ScheduleSave(string reason)
    {
        if (isSavingScheduled || !isActiveAndEnabled)
        {
            return;
        }

        Log($"Schedule save -> {reason}");
        StartCoroutine(SaveNextFrame(reason));
    }

    private IEnumerator SaveNextFrame(string reason)
    {
        isSavingScheduled = true;
        yield return null;
        SaveImmediate(reason);
        isSavingScheduled = false;
    }

    private void SaveImmediate(string reason)
    {
        Log($"Save -> {reason}");
        SaveSystem.SaveGame();
    }

    private void Log(string message)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log($"[AutoSaveController] {message}", this);
    }

    #endregion
}