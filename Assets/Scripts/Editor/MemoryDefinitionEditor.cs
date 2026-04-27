#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.EditorTools
{
    /// <summary>
    /// Inspector custom para MemoryDefinition.
    /// Muestra campos automáticos como solo lectura y fuerza sincronización.
    /// </summary>
    [CustomEditor(typeof(MemoryDefinition))]
    public class MemoryDefinitionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            MemoryDefinition memoryDefinition = (MemoryDefinition)target;
            memoryDefinition.SyncAutoFields();

            SerializedProperty idProperty = serializedObject.FindProperty("id");
            SerializedProperty sceneNameProperty = serializedObject.FindProperty("sceneName");
            SerializedProperty objectsProperty = serializedObject.FindProperty("objects");
            SerializedProperty unlockConditionsProperty = serializedObject.FindProperty("unlockConditions");

            EditorGUILayout.LabelField("Identification", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(idProperty);
            }

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(objectsProperty, true);
            EditorGUILayout.PropertyField(unlockConditionsProperty, true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(sceneNameProperty);
            }

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(memoryDefinition);
            }
        }
    }
}
#endif