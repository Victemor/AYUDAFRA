#if UNITY_EDITOR
using UnityEditor;
using Game.Conditions;

namespace Game.EditorTools
{
    /// <summary>
    /// Inspector custom para ObjectStateEmotionCondition.
    /// </summary>
    [CustomEditor(typeof(ObjectStateEmotionCondition))]
    public class ObjectStateEmotionConditionEditor : ObjectStateConditionEditorBase
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty memoryProperty = serializedObject.FindProperty("memory");
            SerializedProperty targetObjectProperty = serializedObject.FindProperty("targetObject");
            SerializedProperty requiredEmotionProperty = serializedObject.FindProperty("requiredEmotion");
            SerializedProperty requiredStateIdProperty = serializedObject.FindProperty("requiredStateId");

            EditorGUILayout.PropertyField(memoryProperty);
            DrawStatePopup(targetObjectProperty, requiredStateIdProperty, "Optional Required State Id");
            EditorGUILayout.PropertyField(requiredEmotionProperty);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif