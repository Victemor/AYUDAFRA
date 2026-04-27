#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.EditorTools
{
    /// <summary>
    /// Utilidades compartidas para inspectores y drawers del sistema draggable.
    /// </summary>
    public static class DraggableEditorUtility
    {
        /// <summary>
        /// Opción serializable para representar un slot detectable en editor.
        /// </summary>
        public readonly struct SlotOption
        {
            public readonly string SlotId;
            public readonly string FragmentId;
            public readonly string SourceLabel;

            public SlotOption(string slotId, string fragmentId, string sourceLabel)
            {
                SlotId = slotId;
                FragmentId = fragmentId;
                SourceLabel = sourceLabel;
            }

            public string GetDisplayName()
            {
                string fragment = string.IsNullOrWhiteSpace(FragmentId) ? "SinFragmento" : FragmentId;
                string source = string.IsNullOrWhiteSpace(SourceLabel) ? "OrigenDesconocido" : SourceLabel;
                return $"{SlotId}  |  {fragment}  |  {source}";
            }
        }

        /// <summary>
        /// Devuelve todas las definiciones draggable del proyecto.
        /// </summary>
        public static List<DraggableItemDefinition> GetAllDraggableItemDefinitions()
        {
            List<DraggableItemDefinition> result = new();
            string[] guids = AssetDatabase.FindAssets("t:DraggableItemDefinition");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                DraggableItemDefinition definition = AssetDatabase.LoadAssetAtPath<DraggableItemDefinition>(path);

                if (definition != null)
                {
                    result.Add(definition);
                }
            }

            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        /// <summary>
        /// Busca todos los FragmentDraggableSlot en escenas abiertas y prefabs del proyecto.
        /// </summary>
        public static List<SlotOption> GetAllFragmentSlotOptions()
        {
            Dictionary<string, SlotOption> optionsById = new();

            CollectFromOpenScenes(optionsById);
            CollectFromPrefabs(optionsById);

            List<SlotOption> result = new(optionsById.Values);
            result.Sort((a, b) => string.CompareOrdinal(a.SlotId, b.SlotId));
            return result;
        }

        /// <summary>
        /// Genera un slot id sugerido a partir de escena y nombre de objeto.
        /// </summary>
        public static string BuildSuggestedSlotId(FragmentDraggableSlot slot)
        {
            if (slot == null)
            {
                return string.Empty;
            }

            string sceneName = slot.gameObject.scene.IsValid() ? slot.gameObject.scene.name : "NoScene";
            string objectPath = GetTransformPath(slot.transform);

            objectPath = objectPath
                .Replace("/", "_")
                .Replace(" ", string.Empty);

            return $"{sceneName}__{objectPath}";
        }

        /// <summary>
        /// Intenta extraer un sprite útil desde el prefab de mundo.
        /// </summary>
        public static Sprite FindBestSpriteFromWorldPrefab(GameObject worldPrefab)
        {
            if (worldPrefab == null)
            {
                return null;
            }

            SpriteRenderer direct = worldPrefab.GetComponent<SpriteRenderer>();
            if (direct != null && direct.sprite != null)
            {
                return direct.sprite;
            }

            SpriteRenderer child = worldPrefab.GetComponentInChildren<SpriteRenderer>(true);
            if (child != null && child.sprite != null)
            {
                return child.sprite;
            }

            return null;
        }

        /// <summary>
        /// Llena una lista serializada con todas las definiciones draggable del proyecto.
        /// </summary>
        public static void FillSerializedDefinitionList(SerializedProperty arrayProperty)
        {
            if (arrayProperty == null || !arrayProperty.isArray)
            {
                return;
            }

            List<DraggableItemDefinition> allDefinitions = GetAllDraggableItemDefinitions();

            arrayProperty.arraySize = allDefinitions.Count;

            for (int i = 0; i < allDefinitions.Count; i++)
            {
                arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue = allDefinitions[i];
            }
        }

        /// <summary>
        /// Limpia nulls y duplicados de una lista serializada de definiciones draggable.
        /// </summary>
        public static void CleanupSerializedDefinitionList(SerializedProperty arrayProperty)
        {
            if (arrayProperty == null || !arrayProperty.isArray)
            {
                return;
            }

            List<DraggableItemDefinition> valid = new();
            HashSet<string> seenIds = new();

            for (int i = 0; i < arrayProperty.arraySize; i++)
            {
                SerializedProperty element = arrayProperty.GetArrayElementAtIndex(i);
                DraggableItemDefinition definition = element.objectReferenceValue as DraggableItemDefinition;

                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                if (seenIds.Add(definition.Id))
                {
                    valid.Add(definition);
                }
            }

            valid.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            arrayProperty.arraySize = valid.Count;

            for (int i = 0; i < valid.Count; i++)
            {
                arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue = valid[i];
            }
        }

        /// <summary>
        /// Agrega un item a una lista serializada evitando duplicados.
        /// </summary>
        public static void AddDefinitionToSerializedArray(
            SerializedProperty arrayProperty,
            DraggableItemDefinition definition)
        {
            if (arrayProperty == null || !arrayProperty.isArray || definition == null)
            {
                return;
            }

            for (int i = 0; i < arrayProperty.arraySize; i++)
            {
                SerializedProperty element = arrayProperty.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue == definition)
                {
                    return;
                }
            }

            int index = arrayProperty.arraySize;
            arrayProperty.arraySize++;
            arrayProperty.GetArrayElementAtIndex(index).objectReferenceValue = definition;
        }

        private static void CollectFromOpenScenes(Dictionary<string, SlotOption> optionsById)
        {
            FragmentDraggableSlot[] sceneSlots = Object.FindObjectsByType<FragmentDraggableSlot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < sceneSlots.Length; i++)
            {
                FragmentDraggableSlot slot = sceneSlots[i];
                if (slot == null || string.IsNullOrWhiteSpace(slot.SlotId))
                {
                    continue;
                }

                string source = $"Escena:{slot.gameObject.scene.name}";
                optionsById[slot.SlotId] = new SlotOption(slot.SlotId, slot.FragmentId, source);
            }
        }

        private static void CollectFromPrefabs(Dictionary<string, SlotOption> optionsById)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                {
                    continue;
                }

                FragmentDraggableSlot[] slots = prefab.GetComponentsInChildren<FragmentDraggableSlot>(true);
                if (slots == null || slots.Length == 0)
                {
                    continue;
                }

                string source = $"Prefab:{Path.GetFileNameWithoutExtension(path)}";

                for (int j = 0; j < slots.Length; j++)
                {
                    FragmentDraggableSlot slot = slots[j];
                    if (slot == null || string.IsNullOrWhiteSpace(slot.SlotId))
                    {
                        continue;
                    }

                    if (!optionsById.ContainsKey(slot.SlotId))
                    {
                        optionsById.Add(slot.SlotId, new SlotOption(slot.SlotId, slot.FragmentId, source));
                    }
                }
            }
        }

        private static string GetTransformPath(Transform target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            List<string> path = new();
            Transform current = target;

            while (current != null)
            {
                path.Add(current.name);
                current = current.parent;
            }

            path.Reverse();
            return string.Join("/", path);
        }
    }
}
#endif