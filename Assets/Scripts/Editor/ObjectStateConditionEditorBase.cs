#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.EditorTools
{
    /// <summary>
    /// Base compartida para dibujar dropdowns de StateId según el ObjectDefinition seleccionado.
    /// </summary>
    public abstract class ObjectStateConditionEditorBase : Editor
    {
        protected void DrawStatePopup(
            SerializedProperty targetObjectProperty,
            SerializedProperty stateIdProperty,
            string stateLabel)
        {
            EditorGUILayout.PropertyField(targetObjectProperty);

            ObjectDefinition objectDefinition = targetObjectProperty.objectReferenceValue as ObjectDefinition;
            if (objectDefinition == null)
            {
                EditorGUILayout.HelpBox("Selecciona primero un ObjectDefinition para poder elegir un estado.", MessageType.Info);
                EditorGUILayout.PropertyField(stateIdProperty, new GUIContent(stateLabel));
                return;
            }

            string[] stateIds = objectDefinition.States?
                .Where(state => state != null && !string.IsNullOrEmpty(state.StateId))
                .Select(state => state.StateId)
                .ToArray();

            if (stateIds == null || stateIds.Length == 0)
            {
                EditorGUILayout.HelpBox("El ObjectDefinition seleccionado no tiene estados válidos.", MessageType.Warning);
                EditorGUILayout.PropertyField(stateIdProperty, new GUIContent(stateLabel));
                return;
            }

            string currentValue = stateIdProperty.stringValue;
            int selectedIndex = Mathf.Max(0, System.Array.IndexOf(stateIds, currentValue));
            bool currentValueExists = stateIds.Contains(currentValue);

            if (!string.IsNullOrEmpty(currentValue) && !currentValueExists)
            {
                EditorGUILayout.HelpBox($"El estado guardado '{currentValue}' ya no existe en el objeto seleccionado.", MessageType.Warning);
            }

            int newIndex = EditorGUILayout.Popup(stateLabel, selectedIndex, stateIds);
            stateIdProperty.stringValue = stateIds[newIndex];
        }
    }
}
#endif