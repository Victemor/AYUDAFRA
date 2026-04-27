#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Conditions;

namespace Game.EditorTools
{
    /// <summary>
    /// Editor personalizado para FragmentDraggableSlotResolvedCondition.
    /// Permite elegir el SlotId desde slots detectados en escenas abiertas y prefabs.
    /// </summary>
    [CustomEditor(typeof(FragmentDraggableSlotResolvedCondition))]
    public sealed class FragmentDraggableSlotResolvedConditionEditor : Editor
    {
        private SerializedProperty slotIdProp;
        private bool manualMode;

        private void OnEnable()
        {
            slotIdProp = serializedObject.FindProperty("slotId");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Esta condición se cumple cuando el slot draggable indicado ya fue resuelto correctamente.",
                MessageType.Info);

            List<DraggableEditorUtility.SlotOption> options = DraggableEditorUtility.GetAllFragmentSlotOptions();

            if (!manualMode && options.Count > 0)
            {
                DrawPopupSelector(options);
            }
            else
            {
                EditorGUILayout.PropertyField(slotIdProp);
            }

            GUILayout.Space(4f);
            manualMode = EditorGUILayout.ToggleLeft("Modo manual", manualMode);

            if (GUILayout.Button("Refrescar lista de slots"))
            {
                GUI.FocusControl(null);
            }

            DrawWarnings(options);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPopupSelector(List<DraggableEditorUtility.SlotOption> options)
        {
            string currentSlotId = slotIdProp.stringValue;
            int selectedIndex = 0;

            string[] display = new string[options.Count];

            for (int i = 0; i < options.Count; i++)
            {
                display[i] = options[i].GetDisplayName();

                if (options[i].SlotId == currentSlotId)
                {
                    selectedIndex = i;
                }
            }

            int newIndex = EditorGUILayout.Popup("Resolved Slot", selectedIndex, display);

            if (newIndex >= 0 && newIndex < options.Count)
            {
                slotIdProp.stringValue = options[newIndex].SlotId;
            }
        }

        private void DrawWarnings(List<DraggableEditorUtility.SlotOption> options)
        {
            if (string.IsNullOrWhiteSpace(slotIdProp.stringValue))
            {
                EditorGUILayout.HelpBox(
                    "Debes seleccionar un SlotId.",
                    MessageType.Warning);
                return;
            }

            if (!manualMode && options.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No se detectaron FragmentDraggableSlot en escenas abiertas ni prefabs. " +
                    "Puedes usar modo manual si aún no están en escena/prefab.",
                    MessageType.Info);
            }
        }
    }
}
#endif