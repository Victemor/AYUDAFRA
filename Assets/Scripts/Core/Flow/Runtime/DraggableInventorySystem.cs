using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Data;
using Game.Save;

namespace Game.Runtime
{
    /// <summary>
    /// Sistema backend central del subsistema draggable.
    /// </summary>
    public sealed class DraggableInventorySystem : MonoBehaviour
    {
        public static DraggableInventorySystem Instance { get; private set; }

        [Header("Definitions")]

        [SerializeField]
        [Tooltip("Todos los objetos draggable únicos del juego.")]
        private List<DraggableItemDefinition> draggableItems = new();

        [Header("Debug")]

        [SerializeField]
        [Tooltip("Activa logs detallados del sistema draggable.")]
        private bool debugLogs = true;

        private readonly Dictionary<string, DraggableItemRuntimeData> itemsById = new();
        private readonly Dictionary<string, DraggableItemWorldInstance> sceneInstancesByItemId = new();
        private readonly Dictionary<string, FragmentDraggableSlot> sceneSlotsById = new();

        private DraggableInventoryRuntimeData inventory;

        /// <summary>
        /// Runtime del inventario draggable.
        /// </summary>
        public DraggableInventoryRuntimeData Inventory => inventory;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        private void Initialize()
        {
            inventory = new DraggableInventoryRuntimeData();
            itemsById.Clear();

            Log($"Initialize -> Definitions: {draggableItems.Count}");

            foreach (DraggableItemDefinition definition in draggableItems)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                if (!itemsById.ContainsKey(definition.Id))
                {
                    itemsById.Add(definition.Id, new DraggableItemRuntimeData(definition));
                    Log($"Registered item definition -> {definition.Id}");
                }
            }
        }

        /// <summary>
        /// Obtiene el runtime de un item por definición.
        /// </summary>
        public DraggableItemRuntimeData GetItem(DraggableItemDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                return null;
            }

            itemsById.TryGetValue(definition.Id, out DraggableItemRuntimeData item);
            return item;
        }

        /// <summary>
        /// Obtiene el runtime de un item por ID.
        /// </summary>
        public DraggableItemRuntimeData GetItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return null;
            }

            itemsById.TryGetValue(itemId, out DraggableItemRuntimeData item);
            return item;
        }

        /// <summary>
        /// Devuelve todos los items runtime.
        /// </summary>
        public IEnumerable<DraggableItemRuntimeData> GetAllItems()
        {
            return itemsById.Values;
        }

        /// <summary>
        /// Devuelve true si el item ya existe runtime en un estado distinto de NotSpawned.
        /// </summary>
        public bool ExistsInRuntime(DraggableItemDefinition definition)
        {
            DraggableItemRuntimeData item = GetItem(definition);
            return item != null && item.CurrentState != DraggableItemState.NotSpawned;
        }

        /// <summary>
        /// Intenta spawnear el primer item disponible de la lista en el punto indicado.
        /// Recorre la lista en orden y se detiene en el primer candidato válido.
        /// No emite pensamientos fallback porque esto se considera flujo backend.
        /// </summary>
        public bool TrySpawnFirstAvailableItem(IReadOnlyList<DraggableItemDefinition> candidates, Transform spawnPoint)
        {
            if (candidates == null || candidates.Count == 0)
            {
                Debug.LogWarning("[DraggableInventorySystem] TrySpawnFirstAvailableItem -> lista vacía.", this);
                return false;
            }

            if (spawnPoint == null)
            {
                Debug.LogWarning("[DraggableInventorySystem] TrySpawnFirstAvailableItem -> spawnPoint null.", this);
                return false;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                DraggableItemDefinition definition = candidates[i];

                if (definition == null)
                {
                    continue;
                }

                DraggableItemRuntimeData item = GetItem(definition);
                if (item == null)
                {
                    Debug.LogWarning(
                        $"[DraggableInventorySystem] TrySpawnFirstAvailableItem -> no existe runtime para {definition.name}.",
                        this);
                    continue;
                }

                if (item.CurrentState != DraggableItemState.NotSpawned)
                {
                    Log(
                        $"TrySpawnFirstAvailableItem -> skip {definition.Id} | " +
                        $"State: {item.CurrentState}");
                    continue;
                }

                if (definition.WorldPrefab == null)
                {
                    Debug.LogWarning(
                        $"[DraggableInventorySystem] TrySpawnFirstAvailableItem -> WorldPrefab null para {definition.Id}.",
                        this);
                    continue;
                }

                bool registered = TryRegisterSpawnedInWorld(definition, spawnPoint.position);
                if (!registered)
                {
                    continue;
                }

                GameObject instance = Instantiate(
                    definition.WorldPrefab,
                    spawnPoint.position,
                    spawnPoint.rotation);

                DraggableItemWorldInstance worldInstance = instance.GetComponent<DraggableItemWorldInstance>();

                if (worldInstance == null)
                {
                    worldInstance = instance.AddComponent<DraggableItemWorldInstance>();
                }

                worldInstance.Initialize(definition, false);
                worldInstance.ConfigureAsWorldItem();

                sceneInstancesByItemId[definition.Id] = worldInstance;

                Log($"TrySpawnFirstAvailableItem -> spawned {definition.Id} at {spawnPoint.position}");

                RaiseItemChanged(item);
                RaiseInventoryChanged();
                return true;
            }

            Log("TrySpawnFirstAvailableItem -> no candidate could be spawned.");
            return false;
        }

        /// <summary>
        /// Registra una instancia visual de mundo.
        /// </summary>
        public void RegisterWorldInstance(DraggableItemWorldInstance instance)
        {
            Log(
                $"RegisterWorldInstance -> " +
                $"Instance: {(instance != null ? instance.name : "NULL")} | " +
                $"Item: {(instance != null && instance.ItemDefinition != null ? instance.ItemDefinition.Id : "NULL")}"
            );

            if (instance == null || instance.ItemDefinition == null)
            {
                Debug.LogWarning("[DraggableInventorySystem] RegisterWorldInstance recibió una instancia inválida.", this);
                return;
            }

            DraggableItemRuntimeData item = GetItem(instance.ItemDefinition);
            if (item == null)
            {
                Debug.LogWarning(
                    $"[DraggableInventorySystem] No existe runtime para el item {instance.ItemDefinition.Id}. " +
                    "Verifica que esté agregado en draggableItems.",
                    this
                );

                instance.gameObject.SetActive(false);
                return;
            }

            sceneInstancesByItemId[item.Definition.Id] = instance;

            string currentScene = SceneManager.GetActiveScene().name;

            Log($"World instance runtime state -> {item.Definition.Id} | State: {item.CurrentState}");

            switch (item.CurrentState)
            {
                case DraggableItemState.NotSpawned:
                    if (instance.IsSceneAuthored)
                    {
                        item.SetInWorld(instance.transform.position, currentScene);
                        instance.ConfigureAsWorldItem();
                        instance.gameObject.SetActive(true);

                        Log(
                            $"Scene-authored item promoted to InWorld -> " +
                            $"{item.Definition.Id} | Scene: {currentScene}"
                        );

                        RaiseItemChanged(item);
                        RaiseInventoryChanged();
                    }
                    else
                    {
                        instance.gameObject.SetActive(false);
                    }
                    break;

                case DraggableItemState.InWorld:
                    if (item.CurrentSceneName == currentScene)
                    {
                        instance.transform.position = item.WorldPosition;
                        instance.ConfigureAsWorldItem();
                        instance.gameObject.SetActive(true);
                    }
                    else
                    {
                        instance.gameObject.SetActive(false);
                    }
                    break;

                case DraggableItemState.InInventory:
                    instance.gameObject.SetActive(false);
                    break;

                case DraggableItemState.Held:
                    instance.gameObject.SetActive(false);
                    break;

                case DraggableItemState.InFragmentSlot:
                case DraggableItemState.Finalized:
                    if (instance.OwningSlotId == item.CurrentFragmentSlotId)
                    {
                        instance.gameObject.SetActive(true);
                    }
                    else
                    {
                        instance.gameObject.SetActive(false);
                    }
                    break;
            }
        }

        /// <summary>
        /// Desregistra una instancia visual de mundo.
        /// </summary>
        public void UnregisterWorldInstance(DraggableItemWorldInstance instance)
        {
            if (instance == null || instance.ItemDefinition == null)
            {
                return;
            }

            if (sceneInstancesByItemId.TryGetValue(instance.ItemDefinition.Id, out DraggableItemWorldInstance current) &&
                current == instance)
            {
                sceneInstancesByItemId.Remove(instance.ItemDefinition.Id);
            }
        }

        /// <summary>
        /// Registra un slot físico de escena.
        /// </summary>
        public void RegisterSceneSlot(FragmentDraggableSlot slot)
        {
            if (slot == null || string.IsNullOrWhiteSpace(slot.SlotId))
            {
                return;
            }

            sceneSlotsById[slot.SlotId] = slot;
        }

        /// <summary>
        /// Desregistra un slot físico de escena.
        /// </summary>
        public void UnregisterSceneSlot(FragmentDraggableSlot slot)
        {
            if (slot == null || string.IsNullOrWhiteSpace(slot.SlotId))
            {
                return;
            }

            if (sceneSlotsById.TryGetValue(slot.SlotId, out FragmentDraggableSlot current) && current == slot)
            {
                sceneSlotsById.Remove(slot.SlotId);
            }
        }

        /// <summary>
        /// Intenta registrar un item como presente en mundo.
        /// </summary>
        public bool TryRegisterSpawnedInWorld(DraggableItemDefinition definition, Vector3 worldPosition)
        {
            DraggableItemRuntimeData item = GetItem(definition);
            if (item == null)
            {
                return false;
            }

            if (item.CurrentState != DraggableItemState.NotSpawned)
            {
                return false;
            }

            item.SetInWorld(worldPosition, SceneManager.GetActiveScene().name);
            RaiseItemChanged(item);
            RaiseInventoryChanged();
            return true;
        }

        /// <summary>
        /// Intenta recoger un objeto visual de mundo o slot incorrecto.
        /// </summary>
        public bool TryPickupWorldInstance(DraggableItemWorldInstance instance)
        {
            Log(
                $"TryPickupWorldInstance -> " +
                $"{(instance != null && instance.ItemDefinition != null ? instance.ItemDefinition.Id : "NULL")}"
            );

            if (instance == null || instance.ItemDefinition == null)
            {
                Debug.LogWarning("[DraggableInventorySystem] TryPickupWorldInstance recibió una instancia inválida.", this);
                return false;
            }

            DraggableItemRuntimeData item = GetItem(instance.ItemDefinition);
            if (item == null)
            {
                Debug.LogWarning(
                    $"[DraggableInventorySystem] No existe runtime para el item {instance.ItemDefinition.Id}.",
                    this
                );
                return false;
            }

            Log($"Pickup runtime state -> {item.Definition.Id} | {item.CurrentState}");

            if (item.CurrentState == DraggableItemState.NotSpawned && instance.IsSceneAuthored)
            {
                item.SetInWorld(instance.transform.position, SceneManager.GetActiveScene().name);

                Log(
                    $"Pickup hot-fix -> Scene-authored item was NotSpawned, promoted to InWorld -> {item.Definition.Id}"
                );
            }

            switch (item.CurrentState)
            {
                case DraggableItemState.InWorld:
                    Log($"Pickup from world -> {item.Definition.Id}");

                    if (!inventory.TryAddItem(item))
                    {
                        Debug.LogWarning(
                            $"[DraggableInventorySystem] Inventory full while picking up -> {item.Definition.Id}",
                            this
                        );

                        PublishThought(DraggableInventoryThoughtBuilder.BuildInventoryFull());
                        return false;
                    }

                    Destroy(instance.gameObject);

                    Log($"Added to inventory -> {item.Definition.Id}");

                    RaiseItemChanged(item);
                    RaiseInventoryChanged();
                    return true;

                case DraggableItemState.InFragmentSlot:
                    {
                        FragmentDraggableSlot sceneSlot = GetSceneSlot(item.CurrentFragmentSlotId);
                        FragmentDraggableSlotRuntimeData slotRuntime = GetSlotRuntime(item.CurrentFragmentSlotId);

                        if (slotRuntime == null || slotRuntime.CurrentState != FragmentDraggableSlotState.OccupiedWrong)
                        {
                            return false;
                        }

                        if (!inventory.TryAddItem(item))
                        {
                            Debug.LogWarning(
                                $"[DraggableInventorySystem] Inventory full while retrieving wrong placed item -> {item.Definition.Id}",
                                this
                            );

                            PublishThought(DraggableInventoryThoughtBuilder.BuildInventoryFull());
                            return false;
                        }

                        slotRuntime.Clear();

                        if (sceneSlot != null)
                        {
                            sceneSlot.ClearOccupantVisual();
                        }
                        else
                        {
                            Destroy(instance.gameObject);
                        }

                        RaiseItemChanged(item);
                        RaiseSlotChanged(slotRuntime);
                        RaiseInventoryChanged();
                        return true;
                    }

                case DraggableItemState.Finalized:
                    PublishThought(DraggableInventoryThoughtBuilder.BuildFinalizedItemLocked());
                    return false;

                default:
                    Debug.LogWarning(
                        $"[DraggableInventorySystem] Pickup ignored because item state is {item.CurrentState} -> {item.Definition.Id}",
                        this
                    );
                    return false;
            }
        }

        /// <summary>
        /// Intenta comenzar la selección lógica desde un slot de inventario.
        /// En esta entrega no se usa visual en mano.
        /// </summary>
        public bool TryBeginCarryFromInventorySlot(int slotIndex)
        {
            Log($"TryBeginCarryFromInventorySlot -> Slot {slotIndex}");

            if (inventory == null)
            {
                Debug.LogError("[DraggableInventorySystem] Inventory runtime es null.", this);
                return false;
            }

            if (!inventory.TryBeginCarryFromSlot(slotIndex, out DraggableItemRuntimeData item))
            {
                Debug.LogWarning($"[DraggableInventorySystem] Cannot begin carry from slot {slotIndex}", this);
                return false;
            }

            if (item == null)
            {
                Debug.LogError("[DraggableInventorySystem] Item runtime null after TryBeginCarryFromSlot.", this);
                inventory.ReturnHeldItemToInventory();
                RaiseInventoryChanged();
                return false;
            }

            if (item.Definition == null)
            {
                Debug.LogError("[DraggableInventorySystem] Item definition is null after TryBeginCarryFromSlot.", this);
                inventory.ReturnHeldItemToInventory();
                RaiseItemChanged(item);
                RaiseInventoryChanged();
                return false;
            }

            // No se usa en esta entrega representación visual del objeto en mano.
            // El item queda solamente en estado lógico Held.
            Log($"Carry logical-only started -> {item.Definition.Id}");

            RaiseItemChanged(item);
            RaiseInventoryChanged();
            return true;
        }

        /// <summary>
        /// Cancela el item en mano y lo devuelve al inventario.
        /// </summary>
        public bool CancelHeldItemAndReturnToInventory()
        {
            Log("CancelHeldItemAndReturnToInventory");

            if (!inventory.HasHeldItem)
            {
                return false;
            }

            if (DraggableCarryController.Instance != null)
            {
                // No se usa en esta entrega representación visual del objeto en mano.
                DraggableCarryController.Instance.ClearCarryVisual();
            }

            DraggableItemRuntimeData heldItem = inventory.HeldItem;
            bool returned = inventory.ReturnHeldItemToInventory();

            if (returned)
            {
                Log("Held item returned to inventory");

                if (heldItem != null)
                {
                    RaiseItemChanged(heldItem);
                }

                RaiseInventoryChanged();
            }

            return returned;
        }

        /// <summary>
        /// Intenta colocar el item en mano dentro de un slot físico.
        /// </summary>
        public bool TryPlaceHeldItemInSceneSlot(FragmentDraggableSlot sceneSlot)
        {
            Log(
                $"TryPlaceHeldItemInSceneSlot -> " +
                $"Slot: {(sceneSlot != null ? sceneSlot.SlotId : "NULL")}"
            );

            if (sceneSlot == null)
            {
                PublishThought(DraggableInventoryThoughtBuilder.BuildInvalidPlacement());
                CancelHeldItemAndReturnToInventory();
                return false;
            }

            if (!inventory.HasHeldItem || inventory.HeldItem == null)
            {
                return false;
            }

            FragmentDraggableSlotRuntimeData slotRuntime = sceneSlot.GetRuntime();

            Log(
                $"Slot runtime -> " +
                $"State: {(slotRuntime != null ? slotRuntime.CurrentState.ToString() : "NULL")} | " +
                $"HeldItem: {(inventory.HeldItem != null && inventory.HeldItem.Definition != null ? inventory.HeldItem.Definition.Id : "NULL")}"
            );

            if (slotRuntime == null)
            {
                PublishThought(DraggableInventoryThoughtBuilder.BuildInvalidPlacement());
                CancelHeldItemAndReturnToInventory();
                return false;
            }

            DraggableItemRuntimeData item = inventory.HeldItem;

            if (!item.IsMovable)
            {
                PublishThought(DraggableInventoryThoughtBuilder.BuildFinalizedItemLocked());
                return false;
            }

            if (slotRuntime.IsOccupied)
            {
                if (slotRuntime.CurrentState == FragmentDraggableSlotState.OccupiedCorrectLocked)
                {
                    PublishThought(DraggableInventoryThoughtBuilder.BuildOccupiedWrongObject());
                }
                else
                {
                    PublishThought(DraggableInventoryThoughtBuilder.BuildOccupiedSlot());
                }

                CancelHeldItemAndReturnToInventory();
                return false;
            }

            DraggableItemWorldInstance carryVisual = null;

            if (DraggableCarryController.Instance != null)
            {
                // No se usa en esta entrega representación visual del objeto en mano.
                carryVisual = DraggableCarryController.Instance.ConsumeCarryVisual();
            }

            if (carryVisual == null)
            {
                if (item.Definition == null || item.Definition.WorldPrefab == null)
                {
                    Debug.LogError(
                        $"[DraggableInventorySystem] No existe WorldPrefab para colocar item -> {(item.Definition != null ? item.Definition.Id : "NULL")}",
                        this);

                    CancelHeldItemAndReturnToInventory();
                    return false;
                }

                GameObject instance = Instantiate(item.Definition.WorldPrefab);
                carryVisual = instance.GetComponent<DraggableItemWorldInstance>();

                if (carryVisual == null)
                {
                    carryVisual = instance.AddComponent<DraggableItemWorldInstance>();
                }

                carryVisual.Initialize(item.Definition, false);
            }

            inventory.ConsumeHeldItem();

            if (slotRuntime.Accepts(item.Definition))
            {
                Log($"Correct placement -> {item.Definition.Id} in slot {sceneSlot.SlotId}");

                slotRuntime.SetResolved(item.Definition.Id);
                item.SetFinalized(sceneSlot.SlotId, sceneSlot.FragmentId);
                sceneSlot.AttachIncomingInstance(carryVisual, true);
            }
            else
            {
                Debug.LogWarning(
                    $"[DraggableInventorySystem] Wrong placement -> {item.Definition.Id} in slot {sceneSlot.SlotId}",
                    this
                );

                slotRuntime.SetOccupiedWrong(item.Definition.Id);
                item.SetInFragmentSlot(sceneSlot.SlotId, sceneSlot.FragmentId);
                sceneSlot.AttachIncomingInstance(carryVisual, false);
                PublishThought(DraggableInventoryThoughtBuilder.BuildInvalidPlacement());
            }

            RaiseItemChanged(item);
            RaiseSlotChanged(slotRuntime);
            RaiseInventoryChanged();

            return true;
        }

        /// <summary>
        /// Intenta devolver al inventario un objeto incorrectamente colocado en un slot físico.
        /// </summary>
        public bool TryReturnWrongPlacedItemToInventory(FragmentDraggableSlot sceneSlot)
        {
            if (sceneSlot == null)
            {
                return false;
            }

            FragmentDraggableSlotRuntimeData slotRuntime = sceneSlot.GetRuntime();
            if (slotRuntime == null || slotRuntime.CurrentState != FragmentDraggableSlotState.OccupiedWrong)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(slotRuntime.CurrentItemId))
            {
                return false;
            }

            DraggableItemRuntimeData item = GetItem(slotRuntime.CurrentItemId);
            if (item == null)
            {
                return false;
            }

            if (!inventory.TryAddItem(item))
            {
                PublishThought(DraggableInventoryThoughtBuilder.BuildInventoryFull());
                return false;
            }

            slotRuntime.Clear();
            sceneSlot.ClearOccupantVisual();

            RaiseItemChanged(item);
            RaiseSlotChanged(slotRuntime);
            RaiseInventoryChanged();
            return true;
        }

        public bool IsItemFinalized(string itemId)
        {
            DraggableItemRuntimeData item = GetItem(itemId);
            return item != null && item.IsFinalized;
        }

        public bool IsSlotResolved(string slotId)
        {
            FragmentDraggableSlotRuntimeData slot = GetSlotRuntime(slotId);
            return slot != null && slot.IsResolved;
        }

        public void NormalizeBeforeSave()
        {
            if (inventory != null && inventory.HasHeldItem)
            {
                CancelHeldItemAndReturnToInventory();
            }
        }

        public void Restore(
            IEnumerable<DraggableItemSaveData> itemSaves,
            DraggableInventorySaveData inventorySave,
            IEnumerable<FragmentDraggableSlotSaveData> slotSaves)
        {
            if (itemSaves != null)
            {
                foreach (DraggableItemSaveData save in itemSaves)
                {
                    DraggableItemRuntimeData item = GetItem(save.id);
                    if (item == null)
                    {
                        continue;
                    }

                    item.Restore(
                        (DraggableItemState)save.state,
                        save.inventorySlotIndex,
                        save.currentFragmentSlotId,
                        save.currentSceneName,
                        save.worldPosition);
                }
            }

            DraggableItemRuntimeData[] restoredSlots = new DraggableItemRuntimeData[inventory.Capacity];

            if (inventorySave != null && inventorySave.slots != null)
            {
                for (int i = 0; i < inventorySave.slots.Count && i < inventory.Capacity; i++)
                {
                    string itemId = inventorySave.slots[i].itemId;
                    restoredSlots[i] = string.IsNullOrWhiteSpace(itemId) ? null : GetItem(itemId);
                }
            }

            inventory.RestoreSlots(restoredSlots);

            if (slotSaves != null && DraggableSlotRegistry.Instance != null)
            {
                foreach (FragmentDraggableSlotSaveData save in slotSaves)
                {
                    FragmentDraggableSlotRuntimeData slot = DraggableSlotRegistry.Instance.GetSlot(save.slotId);
                    if (slot == null)
                    {
                        continue;
                    }

                    slot.Restore((FragmentDraggableSlotState)save.state, save.currentItemId);
                }
            }

            NormalizeBeforeSave();
            RaiseInventoryChanged();
            StartCoroutine(DelayedSyncScenePresentation());
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StartCoroutine(DelayedSyncScenePresentation());
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            CancelHeldItemAndReturnToInventory();
        }

        private IEnumerator DelayedSyncScenePresentation()
        {
            yield return null;

            SyncLooseWorldItemsForActiveScene();
            SyncRegisteredSlotsForActiveScene();
            RaiseInventoryChanged();
        }

        private void SyncLooseWorldItemsForActiveScene()
        {
            string currentScene = SceneManager.GetActiveScene().name;

            foreach (DraggableItemRuntimeData item in itemsById.Values)
            {
                if (item == null || item.Definition == null)
                {
                    continue;
                }

                if (item.CurrentState != DraggableItemState.InWorld)
                {
                    continue;
                }

                if (item.CurrentSceneName != currentScene)
                {
                    continue;
                }

                if (sceneInstancesByItemId.TryGetValue(item.Definition.Id, out DraggableItemWorldInstance existing) &&
                    existing != null)
                {
                    existing.transform.position = item.WorldPosition;
                    existing.ConfigureAsWorldItem();
                    existing.gameObject.SetActive(true);
                    continue;
                }

                if (item.Definition.WorldPrefab == null)
                {
                    continue;
                }

                GameObject instance = Instantiate(item.Definition.WorldPrefab, item.WorldPosition, Quaternion.identity);
                DraggableItemWorldInstance worldInstance = instance.GetComponent<DraggableItemWorldInstance>();

                if (worldInstance == null)
                {
                    worldInstance = instance.AddComponent<DraggableItemWorldInstance>();
                }

                worldInstance.Initialize(item.Definition, false);
                worldInstance.ConfigureAsWorldItem();

                sceneInstancesByItemId[item.Definition.Id] = worldInstance;
            }
        }

        private void SyncRegisteredSlotsForActiveScene()
        {
            foreach (FragmentDraggableSlot slot in sceneSlotsById.Values)
            {
                if (slot != null)
                {
                    slot.SyncVisualFromRuntime();
                }
            }
        }

        private FragmentDraggableSlotRuntimeData GetSlotRuntime(string slotId)
        {
            return DraggableSlotRegistry.Instance != null
                ? DraggableSlotRegistry.Instance.GetSlot(slotId)
                : null;
        }

        private FragmentDraggableSlot GetSceneSlot(string slotId)
        {
            sceneSlotsById.TryGetValue(slotId, out FragmentDraggableSlot slot);
            return slot;
        }

        private void PublishThought(string text)
        {
            if (ConsciousnessSystem.Instance != null && !string.IsNullOrWhiteSpace(text))
            {
                ConsciousnessSystem.Instance.AddThought(text);
            }
        }

        private void RaiseItemChanged(DraggableItemRuntimeData item)
        {
            GameEvents.RaiseDraggableItemStateChanged(item);
        }

        private void RaiseSlotChanged(FragmentDraggableSlotRuntimeData slot)
        {
            GameEvents.RaiseFragmentDraggableSlotStateChanged(slot);
        }

        private void RaiseInventoryChanged()
        {
            GameEvents.RaiseDraggableInventoryChanged();
        }

        private void Log(string message)
        {
            if (!debugLogs)
            {
                return;
            }

            Debug.Log($"[DraggableInventorySystem] {message}", this);
        }
    }
}