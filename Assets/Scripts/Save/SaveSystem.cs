using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Runtime;
using Game.Data;
using System;

namespace Game.Save
{
    /// <summary>
    /// Sistema centralizado de persistencia.
    /// Serializa el estado global del runtime y restaura el estado de objetos persistentes por escena.
    /// </summary>
    public static class SaveSystem
    {
        #region Private Fields

        private static UnifiedSaveData cachedLoadedData;

        #endregion

        #region Properties

        /// <summary>Ruta absoluta del archivo de guardado.</summary>
        private static string SavePath =>
            Path.Combine(Application.persistentDataPath, "save.json");

        /// <summary>Indica si existe un save cargado en memoria y disponible para aplicar a escenas.</summary>
        public static bool HasLoadedData => cachedLoadedData != null;

        /// <summary>
        /// Se dispara al finalizar LoadGame(). Úsalo para sincronizar sistemas
        /// que dependen del momento exacto en que la restauración completó.
        /// </summary>
        public static event Action OnSaveLoaded;

        #endregion

        #region Public API

        /// <summary>Guarda el estado completo actual del juego.</summary>
        public static void SaveGame()
        {
            GameStateRepository repository = GameStateRepository.Instance;
            FragmentProgressData progress = GameManager.Instance != null ? GameManager.Instance.FragmentProgress : null;

            if (repository == null || progress == null)
            {
                Debug.LogWarning("[SAVE] Repository or Progress not ready");
                return;
            }

            if (DraggableInventorySystem.Instance != null)
            {
                DraggableInventorySystem.Instance.NormalizeBeforeSave();
            }

            SyncDropConnectionsWithRepository(progress, repository);

            UnifiedSaveData data = new UnifiedSaveData();

            foreach (MemoryRuntimeData memory in repository.GetAllMemories())
            {
                MemorySaveData memoryData = new MemorySaveData
                {
                    id       = memory.Definition.Id,
                    state    = (int)memory.CurrentState,
                    hasAlert = memory.HasNewContentAlert
                };

                foreach (ObjectRuntimeData obj in memory.GetAllObjects())
                {
                    ObjectSaveData objectData = new ObjectSaveData
                    {
                        id                  = obj.Definition.Id,
                        stateIndex          = obj.GetStateIndex(),
                        hasPendingTransition = obj.GetHasPendingTransition()
                    };

                    foreach (int consumedIndex in obj.GetConsumedStateIndexes())
                    {
                        objectData.consumedStateIndexes.Add(consumedIndex);
                    }

                    foreach (KeyValuePair<int, RuntimeEmotionState> emotionPair in obj.GetRuntimeEmotions())
                    {
                        objectData.runtimeEmotions.Add(new RuntimeEmotionSaveData
                        {
                            stateIndex = emotionPair.Key,
                            emotion    = (int)emotionPair.Value.Emotion,
                            intensity  = (int)emotionPair.Value.Intensity
                        });
                    }

                    foreach (KeyValuePair<string, ActRuntimeData> actPair in obj.GetActs())
                    {
                        objectData.acts.Add(new ActSaveData
                        {
                            id          = actPair.Key,
                            hasExecuted = actPair.Value.hasExecuted
                        });
                    }

                    foreach (KeyValuePair<string, ObjectRuntimeData.WorldObjectState> worldPair in obj.GetWorldStates())
                    {
                        ObjectRuntimeData.WorldObjectState state = worldPair.Value;

                        objectData.worldStates.Add(new ObjectWorldStateSaveData
                        {
                            id                 = worldPair.Key,
                            hasVisible         = state.visible.HasValue,
                            visible            = state.visible.GetValueOrDefault(),
                            hasColliderEnabled = state.colliderEnabled.HasValue,
                            colliderEnabled    = state.colliderEnabled.GetValueOrDefault()
                        });
                    }

                    memoryData.objects.Add(objectData);
                }

                data.memories.Add(memoryData);
            }

            data.connections      = repository.GetAllConnectionKeys();
            data.drops            = progress.drops;
            data.tutorialProgress = progress.tutorialProgress ?? new TutorialProgressData();
            data.consciousness    = BuildConsciousnessSaveData();

            BuildDraggableSaveData(data);
            BuildSceneWorldObjectSaveData(data);

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);

            cachedLoadedData = data;

            Debug.Log("[SAVE] Game Saved");
        }

        /// <summary>
        /// Carga el estado global del juego desde disco y deja disponible en memoria
        /// la restauración de objetos persistentes por escena.
        /// </summary>
        public static void LoadGame()
        {
            GameStateRepository repository = GameStateRepository.Instance;
            FragmentProgressData progress = GameManager.Instance != null ? GameManager.Instance.FragmentProgress : null;

            if (repository == null || progress == null)
            {
                Debug.LogError("[SAVE] Repository or Progress not initialized");
                return;
            }

            repository.InitializeAllMemories();

            if (!File.Exists(SavePath))
            {
                cachedLoadedData = null;
                Debug.Log("[SAVE] No save file found");
                return;
            }

            string json = File.ReadAllText(SavePath);
            UnifiedSaveData data = JsonUtility.FromJson<UnifiedSaveData>(json);

            if (data == null)
            {
                cachedLoadedData = null;
                Debug.LogError("[SAVE] Save file invalid.");
                return;
            }

            cachedLoadedData = data;

            foreach (MemorySaveData memoryData in data.memories)
            {
                MemoryDefinition memoryDef = FindMemoryDefinition(memoryData.id);
                if (memoryDef == null)
                {
                    continue;
                }

                MemoryRuntimeData memoryRuntime = repository.GetMemory(memoryDef);
                if (memoryRuntime == null)
                {
                    continue;
                }

                memoryRuntime.ForceSetState((MemoryState)memoryData.state, memoryData.hasAlert);

                foreach (ObjectSaveData objData in memoryData.objects)
                {
                    ObjectDefinition objDef = FindObjectDefinition(memoryDef, objData.id);
                    if (objDef == null)
                    {
                        continue;
                    }

                    ObjectRuntimeData objRuntime = memoryRuntime.GetObject(objDef);
                    if (objRuntime == null)
                    {
                        continue;
                    }

                    objRuntime.ForceSetState(objData.stateIndex);
                    objRuntime.RestorePendingTransition(objData.hasPendingTransition);
                    objRuntime.RestoreConsumedStates(objData.consumedStateIndexes);

                    foreach (RuntimeEmotionSaveData emotionData in objData.runtimeEmotions)
                    {
                        objRuntime.RestoreRuntimeEmotion(
                            emotionData.stateIndex,
                            new RuntimeEmotionState(
                                (EmotionType)emotionData.emotion,
                                (IntensityLevel)emotionData.intensity));
                    }

                    foreach (ActSaveData actData in objData.acts)
                    {
                        objRuntime.RestoreActState(actData.id, actData.hasExecuted);
                    }

                    foreach (ObjectWorldStateSaveData worldData in objData.worldStates)
                    {
                        objRuntime.RestoreWorldState(
                            worldData.id,
                            worldData.hasVisible,
                            worldData.visible,
                            worldData.hasColliderEnabled,
                            worldData.colliderEnabled);
                    }
                }
            }

            repository.ClearConnections();

            foreach (string key in data.connections)
            {
                repository.RestoreConnection(key);
            }

            progress.drops            = data.drops ?? new List<DropData>();
            progress.tutorialProgress = data.tutorialProgress ?? new TutorialProgressData();

            SyncDropConnectionsWithRepository(progress, repository);
            RestoreConsciousness(data.consciousness);
            RestoreDraggableSaveData(data);
            ApplyLoadedSceneWorldObjects();

            Debug.Log("[SAVE] Game Loaded");
            OnSaveLoaded?.Invoke();

        }

        /// <summary>Aplica el estado persistido a todos los PersistentWorldObject de la escena activa.</summary>
        public static void ApplyLoadedSceneWorldObjects()
        {
            ApplySceneWorldObjectSaveData(cachedLoadedData);
        }

        /// <summary>Intenta aplicar el estado persistido a un único objeto recién activado o instanciado.</summary>
        public static void TryApplyToPersistentWorldObject(PersistentWorldObject worldObject)
        {
            if (worldObject == null || cachedLoadedData == null || cachedLoadedData.sceneWorldObjects == null)
            {
                return;
            }

            string activeSceneName = worldObject.gameObject.scene.name;
            string worldObjectId   = worldObject.WorldObjectIdValue;

            if (string.IsNullOrWhiteSpace(activeSceneName) || string.IsNullOrWhiteSpace(worldObjectId))
            {
                return;
            }

            for (int i = 0; i < cachedLoadedData.sceneWorldObjects.Count; i++)
            {
                SceneWorldObjectSaveData savedObject = cachedLoadedData.sceneWorldObjects[i];
                if (savedObject == null)
                {
                    continue;
                }

                if (savedObject.sceneName != activeSceneName || savedObject.worldObjectId != worldObjectId)
                {
                    continue;
                }

                worldObject.ApplySaveData(savedObject);
                return;
            }
        }

        /// <summary>Elimina el archivo de guardado actual.</summary>
        public static void DeleteSave()
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }

            cachedLoadedData = null;
            Debug.Log("[SAVE] Save file deleted");
        }

        /// <summary>Resetea el runtime y elimina cualquier persistencia almacenada.</summary>
        public static void ResetGame()
        {
            DeleteSave();

            GameStateRepository repo = GameStateRepository.Instance;
            if (repo != null)
            {
                repo.ClearConnections();
                repo.ClearMemories();
                repo.InitializeAllMemories();
            }

            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.ResetProgressData();
            }

            if (ConsciousnessSystem.Instance != null)
            {
                ConsciousnessSystem.Instance.Clear();
            }

            Debug.Log("[SAVE] Runtime reset complete");
        }

        #endregion

        #region Private Methods

        private static MemoryDefinition FindMemoryDefinition(string id)
        {
            MemoryDefinition[] all = Resources.FindObjectsOfTypeAll<MemoryDefinition>();

            foreach (MemoryDefinition memory in all)
            {
                if (memory.Id == id)
                {
                    return memory;
                }
            }

            return null;
        }

        private static ObjectDefinition FindObjectDefinition(MemoryDefinition memory, string id)
        {
            if (memory == null || memory.Objects == null)
            {
                return null;
            }

            foreach (ObjectDefinition obj in memory.Objects)
            {
                if (obj != null && obj.Id == id)
                {
                    return obj;
                }
            }

            return null;
        }

        private static void SyncDropConnectionsWithRepository(FragmentProgressData progress, GameStateRepository repository)
        {
            if (progress == null || repository == null)
            {
                return;
            }

            foreach (DropData drop in progress.drops)
            {
                if (drop == null)
                {
                    continue;
                }

                drop.SyncConnections(repository.GetConnections(drop.FragmentName));
            }
        }

        /// <summary>
        /// Construye el bloque serializable del sistema de consciencia.
        /// Soporta dos rutas: localizada (tableName + key) y raw (rawText fallback).
        /// </summary>
        private static ConsciousnessSaveData BuildConsciousnessSaveData()
        {
            ConsciousnessSaveData data = new ConsciousnessSaveData();
 
            if (ConsciousnessSystem.Instance == null)
            {
                return data;
            }
 
            IReadOnlyList<ConsciousnessSystem.ThoughtData> thoughts =
                ConsciousnessSystem.Instance.GetAllThoughts();
 
            for (int i = 0; i < thoughts.Count; i++)
            {
                ConsciousnessSystem.ThoughtData thought = thoughts[i];
 
                if (thought.IsLocalized)
                {
                    data.thoughts.Add(new ThoughtSaveData
                    {
                        tableName = thought.TableName,
                        key       = thought.Key,
                        keyId     = thought.KeyId,
                        timestamp = thought.Timestamp
                    });
                }
                else if (!string.IsNullOrWhiteSpace(thought.RawText))
                {
                    data.thoughts.Add(new ThoughtSaveData
                    {
                        rawText   = thought.RawText,
                        timestamp = thought.Timestamp
                    });
                }
            }
 
            return data;
        }

        /// <summary>
        /// Restaura el historial persistido del sistema de consciencia.
        /// Soporta saves localizados (tableName + key) y saves raw legacy (rawText).
        /// </summary>
         private static void RestoreConsciousness(ConsciousnessSaveData data)
        {
            if (ConsciousnessSystem.Instance == null)
            {
                return;
            }
 
            List<ConsciousnessSystem.ThoughtData> restored = new();
 
            if (data != null && data.thoughts != null)
            {
                for (int i = 0; i < data.thoughts.Count; i++)
                {
                    ThoughtSaveData saved = data.thoughts[i];
 
                    if (saved.IsLocalized)
                    {
                        restored.Add(new ConsciousnessSystem.ThoughtData
                        {
                            TableName = saved.tableName,
                            Key       = saved.key,
                            KeyId     = saved.keyId,
                            Timestamp = saved.timestamp
                        });
                    }
                    else if (!string.IsNullOrWhiteSpace(saved.rawText))
                    {
                        restored.Add(new ConsciousnessSystem.ThoughtData
                        {
                            RawText   = saved.rawText,
                            Timestamp = saved.timestamp
                        });
                    }
                }
            }
 
            ConsciousnessSystem.Instance.RestoreThoughts(restored, notifyListeners: false);
        }

        private static void BuildDraggableSaveData(UnifiedSaveData data)
        {
            if (data == null || DraggableInventorySystem.Instance == null)
            {
                return;
            }

            DraggableInventorySystem system = DraggableInventorySystem.Instance;

            foreach (DraggableItemRuntimeData item in system.GetAllItems())
            {
                if (item == null || item.Definition == null)
                {
                    continue;
                }

                data.draggableItems.Add(new DraggableItemSaveData
                {
                    id                    = item.Definition.Id,
                    state                 = (int)item.CurrentState,
                    inventorySlotIndex    = item.InventorySlotIndex,
                    currentFragmentSlotId = item.CurrentFragmentSlotId,
                    currentSceneName      = item.CurrentSceneName,
                    worldPosition         = item.WorldPosition
                });
            }

            if (system.Inventory != null)
            {
                for (int i = 0; i < system.Inventory.Capacity; i++)
                {
                    DraggableItemRuntimeData slotItem = system.Inventory.GetSlot(i);

                    data.draggableInventory.slots.Add(new DraggableInventorySlotSaveData
                    {
                        index  = i,
                        itemId = slotItem != null && slotItem.Definition != null
                            ? slotItem.Definition.Id
                            : string.Empty
                    });
                }

                data.draggableInventory.heldItemId = string.Empty;
            }

            if (DraggableSlotRegistry.Instance != null)
            {
                foreach (FragmentDraggableSlotRuntimeData slot in DraggableSlotRegistry.Instance.GetAllSlots())
                {
                    if (slot == null)
                    {
                        continue;
                    }

                    data.draggableFragmentSlots.Add(new FragmentDraggableSlotSaveData
                    {
                        slotId        = slot.SlotId,
                        state         = (int)slot.CurrentState,
                        currentItemId = slot.CurrentItemId
                    });
                }
            }
        }

        private static void RestoreDraggableSaveData(UnifiedSaveData data)
        {
            if (data == null || DraggableInventorySystem.Instance == null)
            {
                return;
            }

            DraggableInventorySystem.Instance.Restore(
                data.draggableItems,
                data.draggableInventory,
                data.draggableFragmentSlots);
        }

        private static void BuildSceneWorldObjectSaveData(UnifiedSaveData data)
        {
            if (data == null)
            {
                return;
            }

            PersistentWorldObject[] sceneObjects = UnityEngine.Object.FindObjectsByType<PersistentWorldObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < sceneObjects.Length; i++)
            {
                if (sceneObjects[i] == null)
                {
                    continue;
                }

                SceneWorldObjectSaveData objectData = sceneObjects[i].BuildSaveData();
                if (objectData == null)
                {
                    continue;
                }

                data.sceneWorldObjects.Add(objectData);
            }

            Debug.Log($"[SAVE] SceneWorldObjects serialized: {data.sceneWorldObjects.Count}");
        }

        private static void ApplySceneWorldObjectSaveData(UnifiedSaveData data)
        {
            if (data == null || data.sceneWorldObjects == null || data.sceneWorldObjects.Count == 0)
            {
                Debug.Log("[SAVE] No SceneWorldObjectSaveData to apply.");
                return;
            }

            string activeSceneName = SceneManager.GetActiveScene().name;

            PersistentWorldObject[] sceneObjects = UnityEngine.Object.FindObjectsByType<PersistentWorldObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            Dictionary<string, PersistentWorldObject> objectMap = new Dictionary<string, PersistentWorldObject>();

            for (int i = 0; i < sceneObjects.Length; i++)
            {
                if (sceneObjects[i] == null)
                {
                    continue;
                }

                string objectId = sceneObjects[i].WorldObjectIdValue;
                if (string.IsNullOrWhiteSpace(objectId))
                {
                    Debug.LogWarning("[SAVE] PersistentWorldObject without valid WorldObjectId.");
                    continue;
                }

                if (!objectMap.ContainsKey(objectId))
                {
                    objectMap.Add(objectId, sceneObjects[i]);
                }
                else
                {
                    Debug.LogWarning($"[SAVE] Duplicate PersistentWorldObject id detected: {objectId}");
                }
            }

            int appliedCount = 0;

            for (int i = 0; i < data.sceneWorldObjects.Count; i++)
            {
                SceneWorldObjectSaveData savedObject = data.sceneWorldObjects[i];

                if (savedObject == null || savedObject.sceneName != activeSceneName)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(savedObject.worldObjectId))
                {
                    continue;
                }

                if (!objectMap.TryGetValue(savedObject.worldObjectId, out PersistentWorldObject worldObject))
                {
                    continue;
                }

                worldObject.ApplySaveData(savedObject);
                appliedCount++;
            }

            Debug.Log($"[SAVE] SceneWorldObjects applied in '{activeSceneName}': {appliedCount}");
        }

        #endregion
    }
}