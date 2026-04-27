#if UNITY_EDITOR
using UnityEditor;
using Game.Conditions;

namespace Game.EditorTools
{
    /// <summary>
    /// Inspector custom para ObjectStateCondition.
    /// </summary>
    [CustomEditor(typeof(ObjectStateCondition))]
    public class ObjectStateConditionEditor : ObjectStateConditionEditorBase
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty memoryProperty = serializedObject.FindProperty("memory");
            SerializedProperty targetObjectProperty = serializedObject.FindProperty("targetObject");
            SerializedProperty requiredStateIdProperty = serializedObject.FindProperty("requiredStateId");

            EditorGUILayout.PropertyField(memoryProperty);
            DrawStatePopup(targetObjectProperty, requiredStateIdProperty, "Required State Id");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif