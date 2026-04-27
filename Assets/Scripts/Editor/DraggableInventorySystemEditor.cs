#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Data;
using Game.Runtime;

namespace Game.EditorTools
{
    /// <summary>
    /// Editor personalizado para DraggableInventorySystem.
    /// Permite poblar automáticamente todas las definiciones draggable del proyecto.
    /// </summary>
    [CustomEditor(typeof(DraggableInventorySystem))]
    public sealed class DraggableInventorySystemEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty draggableItemsProp = serializedObject.FindProperty("draggableItems");
            SerializedProperty debugLogsProp = serializedObject.FindProperty("debugLogs");

            EditorGUILayout.HelpBox(
                "Registra todas las definiciones draggable únicas del proyecto que este sistema debe gestionar.",
                MessageType.Info);

            EditorGUILayout.PropertyField(draggableItemsProp, true);
            EditorGUILayout.PropertyField(debugLogsProp);

            GUILayout.Space(6f);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Cargar todos los DraggableItemDefinition"))
            {
                DraggableEditorUtility.FillSerializedDefinitionList(draggableItemsProp);
            }

            if (GUILayout.Button("Limpiar nulls y duplicados"))
            {
                DraggableEditorUtility.CleanupSerializedDefinitionList(draggableItemsProp);
            }

            EditorGUILayout.EndHorizontal();

            DrawWarnings(draggableItemsProp);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawWarnings(SerializedProperty listProp)
        {
            if (listProp == null || !listProp.isArray)
            {
                return;
            }

            if (listProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "La lista está vacía. El sistema no podrá resolver items draggable.",
                    MessageType.Warning);
            }

            if (HasDuplicateIds(listProp))
            {
                EditorGUILayout.HelpBox(
                    "Hay IDs duplicados o definiciones repetidas dentro de Draggable Items.",
                    MessageType.Warning);
            }

            List<DraggableItemDefinition> allProjectItems = DraggableEditorUtility.GetAllDraggableItemDefinitions();
            if (allProjectItems.Count > listProp.arraySize)
            {
                EditorGUILayout.HelpBox(
                    "Hay más DraggableItemDefinition en el proyecto que en esta lista. " +
                    "Puedes usar el botón de carga automática.",
                    MessageType.Info);
            }
        }

        private bool HasDuplicateIds(SerializedProperty listProp)
        {
            HashSet<string> ids = new();

            for (int i = 0; i < listProp.arraySize; i++)
            {
                SerializedProperty element = listProp.GetArrayElementAtIndex(i);
                DraggableItemDefinition definition = element.objectReferenceValue as DraggableItemDefinition;

                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                if (!ids.Add(definition.Id))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif