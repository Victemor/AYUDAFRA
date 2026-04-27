#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.EditorTools
{
    /// <summary>
    /// Editor personalizado para FragmentDraggableSlot.
    /// Permite generar SlotId y agregar items válidos más rápido.
    /// </summary>
    [CustomEditor(typeof(FragmentDraggableSlot))]
    public sealed class FragmentDraggableSlotEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            FragmentDraggableSlot slot = (FragmentDraggableSlot)target;

            SerializedProperty slotIdProp = serializedObject.FindProperty("slotId");
            SerializedProperty useCurrentSceneNameProp = serializedObject.FindProperty("useCurrentSceneNameAsFragmentId");
            SerializedProperty fragmentIdProp = serializedObject.FindProperty("fragmentId");
            SerializedProperty placementAnchorProp = serializedObject.FindProperty("placementAnchor");
            SerializedProperty allowedItemsProp = serializedObject.FindProperty("allowedItems");
            SerializedProperty slotItemImageProp = serializedObject.FindProperty("slotItemImage");
            SerializedProperty slotItemSpriteRendererProp = serializedObject.FindProperty("slotItemSpriteRenderer");
            SerializedProperty wrongItemAlphaProp = serializedObject.FindProperty("wrongItemAlpha");
            SerializedProperty correctLockedAlphaProp = serializedObject.FindProperty("correctLockedAlpha");
            SerializedProperty debugLogsProp = serializedObject.FindProperty("debugLogs");

            EditorGUILayout.HelpBox(
                "Este slot acepta una o más definiciones draggable válidas. " +
                "Si el item colocado pertenece a la lista, el slot queda resuelto y bloqueado.",
                MessageType.Info);

            EditorGUILayout.PropertyField(slotIdProp);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Generar Slot Id sugerido"))
            {
                slotIdProp.stringValue = DraggableEditorUtility.BuildSuggestedSlotId(slot);
            }

            if (GUILayout.Button("Limpiar allowed items null/repetidos"))
            {
                CleanupAllowedItems(allowedItemsProp);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(useCurrentSceneNameProp);

            if (!useCurrentSceneNameProp.boolValue)
            {
                EditorGUILayout.PropertyField(fragmentIdProp);
            }

            EditorGUILayout.PropertyField(placementAnchorProp);

            GUILayout.Space(4f);
            EditorGUILayout.LabelField("Allowed Items", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(allowedItemsProp, true);

            if (GUILayout.Button("Agregar DraggableItemDefinition..."))
            {
                ShowAddAllowedItemMenu(allowedItemsProp);
            }

            GUILayout.Space(4f);
            EditorGUILayout.LabelField("Visual del Slot", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(slotItemImageProp);
            EditorGUILayout.PropertyField(slotItemSpriteRendererProp);
            EditorGUILayout.PropertyField(wrongItemAlphaProp);
            EditorGUILayout.PropertyField(correctLockedAlphaProp);

            GUILayout.Space(4f);
            EditorGUILayout.PropertyField(debugLogsProp);

            DrawWarnings(
                slotIdProp,
                useCurrentSceneNameProp,
                fragmentIdProp,
                placementAnchorProp,
                allowedItemsProp,
                slotItemImageProp,
                slotItemSpriteRendererProp);

            serializedObject.ApplyModifiedProperties();
        }

        private void ShowAddAllowedItemMenu(SerializedProperty allowedItemsProp)
        {
            GenericMenu menu = new GenericMenu();
            var allDefinitions = DraggableEditorUtility.GetAllDraggableItemDefinitions();

            if (allDefinitions.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No hay DraggableItemDefinition en el proyecto"));
                menu.ShowAsContext();
                return;
            }

            for (int i = 0; i < allDefinitions.Count; i++)
            {
                DraggableItemDefinition definition = allDefinitions[i];
                if (definition == null)
                {
                    continue;
                }

                string label = definition.name;
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    serializedObject.Update();
                    DraggableEditorUtility.AddDefinitionToSerializedArray(allowedItemsProp, definition);
                    serializedObject.ApplyModifiedProperties();
                });
            }

            menu.ShowAsContext();
        }

        private void CleanupAllowedItems(SerializedProperty allowedItemsProp)
        {
            if (allowedItemsProp == null || !allowedItemsProp.isArray)
            {
                return;
            }

            System.Collections.Generic.List<DraggableItemDefinition> valid = new();
            System.Collections.Generic.HashSet<string> ids = new();

            for (int i = 0; i < allowedItemsProp.arraySize; i++)
            {
                SerializedProperty element = allowedItemsProp.GetArrayElementAtIndex(i);
                DraggableItemDefinition definition = element.objectReferenceValue as DraggableItemDefinition;

                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                if (ids.Add(definition.Id))
                {
                    valid.Add(definition);
                }
            }

            allowedItemsProp.arraySize = valid.Count;

            for (int i = 0; i < valid.Count; i++)
            {
                allowedItemsProp.GetArrayElementAtIndex(i).objectReferenceValue = valid[i];
            }
        }

        private void DrawWarnings(
            SerializedProperty slotIdProp,
            SerializedProperty useCurrentSceneNameProp,
            SerializedProperty fragmentIdProp,
            SerializedProperty placementAnchorProp,
            SerializedProperty allowedItemsProp,
            SerializedProperty slotItemImageProp,
            SerializedProperty slotItemSpriteRendererProp)
        {
            if (string.IsNullOrWhiteSpace(slotIdProp.stringValue))
            {
                EditorGUILayout.HelpBox("Falta Slot Id.", MessageType.Warning);
            }

            if (!useCurrentSceneNameProp.boolValue && string.IsNullOrWhiteSpace(fragmentIdProp.stringValue))
            {
                EditorGUILayout.HelpBox("Falta Fragment Id.", MessageType.Warning);
            }

            if (placementAnchorProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Placement Anchor no está asignado. Se usará el propio transform.",
                    MessageType.Info);
            }

            if (allowedItemsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "Debes asignar al menos un item válido en Allowed Items.",
                    MessageType.Warning);
            }

            if (slotItemImageProp.objectReferenceValue == null &&
                slotItemSpriteRendererProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "No hay visual overlay asignado. El slot seguirá funcionando, pero no mostrará icono del item.",
                    MessageType.Info);
            }
        }
    }
}
#endif