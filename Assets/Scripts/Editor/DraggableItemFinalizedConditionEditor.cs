#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Conditions;
using Game.Data;

namespace Game.EditorTools
{
    /// <summary>
    /// Editor personalizado para DraggableItemFinalizedCondition.
    /// </summary>
    [CustomEditor(typeof(DraggableItemFinalizedCondition))]
    public sealed class DraggableItemFinalizedConditionEditor : Editor
    {
        private SerializedProperty targetItemProp;

        private void OnEnable()
        {
            targetItemProp = serializedObject.FindProperty("targetItem");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Esta condición se cumple cuando el objeto draggable seleccionado ya encontró su lugar final " +
                "y quedó bloqueado definitivamente.",
                MessageType.Info);

            DrawItemSelector();
            EditorGUILayout.PropertyField(targetItemProp);

            if (targetItemProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Debes asignar un DraggableItemDefinition.",
                    MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawItemSelector()
        {
            List<DraggableItemDefinition> items = DraggableEditorUtility.GetAllDraggableItemDefinitions();

            if (items.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No se encontraron DraggableItemDefinition en el proyecto.",
                    MessageType.Warning);
                return;
            }

            string[] options = new string[items.Count];
            int selectedIndex = 0;

            DraggableItemDefinition current = targetItemProp.objectReferenceValue as DraggableItemDefinition;

            for (int i = 0; i < items.Count; i++)
            {
                options[i] = items[i] != null ? items[i].name : "NULL";

                if (items[i] == current)
                {
                    selectedIndex = i;
                }
            }

            int newIndex = EditorGUILayout.Popup("Target Item", selectedIndex, options);
            if (newIndex >= 0 && newIndex < items.Count)
            {
                targetItemProp.objectReferenceValue = items[newIndex];
            }
        }
    }
}
#endif