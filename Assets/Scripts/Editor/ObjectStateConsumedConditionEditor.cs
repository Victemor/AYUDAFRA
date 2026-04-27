#if UNITY_EDITOR
using UnityEditor;
using Game.Conditions;

namespace Game.EditorTools
{
    /// <summary>
    /// Inspector custom para ObjectStateConsumedCondition.
    /// </summary>
    [CustomEditor(typeof(ObjectStateConsumedCondition))]
    public class ObjectStateConsumedConditionEditor : ObjectStateConditionEditorBase
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty memoryProperty = serializedObject.FindProperty("targetMemory");
            SerializedProperty targetObjectProperty = serializedObject.FindProperty("targetObject");
            SerializedProperty stateIdProperty = serializedObject.FindProperty("stateId");

            EditorGUILayout.PropertyField(memoryProperty);
            DrawStatePopup(targetObjectProperty, stateIdProperty, "Consumed State Id");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif