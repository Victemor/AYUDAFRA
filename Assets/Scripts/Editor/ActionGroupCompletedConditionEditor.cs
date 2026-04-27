using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Conditions;
using Game.Data;

namespace Game.EditorTools
{
    /// <summary>
    /// Editor personalizado para <see cref="ActionGroupCompletedCondition"/>.
    /// Permite seleccionar el grupo de acciones usando los StateId del objeto.
    /// </summary>
    [CustomEditor(typeof(ActionGroupCompletedCondition))]
    public sealed class ActionGroupCompletedConditionEditor : Editor
    {
        private SerializedProperty targetMemoryProp;
        private SerializedProperty targetObjectProp;
        private SerializedProperty targetActionGroupIdProp;

        private void OnEnable()
        {
            targetMemoryProp = serializedObject.FindProperty("targetMemory");
            targetObjectProp = serializedObject.FindProperty("targetObject");
            targetActionGroupIdProp = serializedObject.FindProperty("targetActionGroupId");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(targetMemoryProp);
            EditorGUILayout.PropertyField(targetObjectProp);

            DrawActionGroupPopup();

            DrawWarnings();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawActionGroupPopup()
        {
            ObjectDefinition targetObject = targetObjectProp.objectReferenceValue as ObjectDefinition;

            if (targetObject == null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("Action Group Id", string.Empty);
                }

                return;
            }

            IReadOnlyList<ObjectStateDefinition> states = targetObject.States;

            if (states == null || states.Count == 0)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("Action Group Id", string.Empty);
                }

                return;
            }

            List<string> options = new List<string>();
            int selectedIndex = -1;

            for (int i = 0; i < states.Count; i++)
            {
                ObjectStateDefinition state = states[i];
                string stateId = state != null ? state.StateId : string.Empty;

                if (string.IsNullOrWhiteSpace(stateId))
                {
                    stateId = $"<State {i + 1} vacío>";
                }

                options.Add(stateId);

                if (targetActionGroupIdProp.stringValue == stateId)
                {
                    selectedIndex = i;
                }
            }

            if (selectedIndex < 0)
            {
                selectedIndex = 0;

                ObjectStateDefinition firstState = states[0];
                targetActionGroupIdProp.stringValue = firstState != null ? firstState.StateId : string.Empty;
            }

            int newIndex = EditorGUILayout.Popup("Action Group Id", selectedIndex, options.ToArray());

            if (newIndex >= 0 && newIndex < states.Count)
            {
                ObjectStateDefinition selectedState = states[newIndex];
                targetActionGroupIdProp.stringValue = selectedState != null ? selectedState.StateId : string.Empty;
            }
        }

        private void DrawWarnings()
        {
            MemoryDefinition memory = targetMemoryProp.objectReferenceValue as MemoryDefinition;
            ObjectDefinition obj = targetObjectProp.objectReferenceValue as ObjectDefinition;

            if (memory == null)
            {
                EditorGUILayout.HelpBox("Debes asignar la memoria objetivo.", MessageType.Warning);
            }

            if (obj == null)
            {
                EditorGUILayout.HelpBox("Debes asignar el objeto objetivo.", MessageType.Warning);
                return;
            }

            if (obj.States == null || obj.States.Count == 0)
            {
                EditorGUILayout.HelpBox("El objeto seleccionado no tiene estados configurados.", MessageType.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(targetActionGroupIdProp.stringValue))
            {
                EditorGUILayout.HelpBox("Debes seleccionar un Action Group Id.", MessageType.Warning);
                return;
            }

            bool exists = false;

            for (int i = 0; i < obj.States.Count; i++)
            {
                ObjectStateDefinition state = obj.States[i];
                if (state != null && state.StateId == targetActionGroupIdProp.stringValue)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                EditorGUILayout.HelpBox("El Action Group Id seleccionado no existe dentro de los estados del objeto.", MessageType.Warning);
            }
        }
    }
}