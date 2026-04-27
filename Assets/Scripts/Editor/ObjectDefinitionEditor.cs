#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.EditorTools
{
    /// <summary>
    /// Inspector custom para ObjectDefinition.
    /// Muestra ID automática como solo lectura.
    /// </summary>
    [CustomEditor(typeof(ObjectDefinition))]
    public class ObjectDefinitionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            ObjectDefinition objectDefinition = (ObjectDefinition)target;
            objectDefinition.SyncAutoFields();

            SerializedProperty idProperty = serializedObject.FindProperty("id");
            SerializedProperty statesProperty = serializedObject.FindProperty("states");

            EditorGUILayout.LabelField("Identification", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(idProperty);
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(statesProperty, true);

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(objectDefinition);
            }
        }
    }
}
#endif