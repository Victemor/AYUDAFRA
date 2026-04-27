using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Data;

[CustomPropertyDrawer(typeof(FragmentConnectionObjectStateRequirement))]
public sealed class FragmentConnectionObjectStateRequirementDrawer : PropertyDrawer
{
    private const float BoxPadding = 6f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty targetObjectProp = property.FindPropertyRelative("targetObject");
        SerializedProperty requiredStateIdProp = property.FindPropertyRelative("requiredStateId");

        Rect backgroundRect = new Rect(
            position.x,
            position.y,
            position.width,
            GetPropertyHeight(property, label));

        EditorGUI.HelpBox(backgroundRect, GUIContent.none.text, MessageType.None);

        float y = position.y + BoxPadding;

        DrawProperty(ref y, position, targetObjectProp);

        Rect popupRect = new Rect(
            position.x + BoxPadding,
            y,
            position.width - (BoxPadding * 2f),
            EditorGUIUtility.singleLineHeight);

        DrawStatePopup(popupRect, targetObjectProp, requiredStateIdProp);

        y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        DrawWarnings(ref y, position, targetObjectProp, requiredStateIdProp);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = BoxPadding * 2f;

        SerializedProperty targetObjectProp = property.FindPropertyRelative("targetObject");
        height += EditorGUI.GetPropertyHeight(targetObjectProp, true) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        SerializedProperty requiredStateIdProp = property.FindPropertyRelative("requiredStateId");
        List<string> warnings = GetWarnings(targetObjectProp, requiredStateIdProp);

        foreach (string warning in warnings)
        {
            float helpHeight = EditorStyles.helpBox.CalcHeight(
                new GUIContent(warning),
                EditorGUIUtility.currentViewWidth - 60f);

            height += helpHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        return height;
    }

    private void DrawStatePopup(Rect rect, SerializedProperty targetObjectProp, SerializedProperty requiredStateIdProp)
    {
        ObjectDefinition targetObject = targetObjectProp.objectReferenceValue as ObjectDefinition;

        if (targetObject == null)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.TextField(rect, "Required State", string.Empty);
            }

            return;
        }

        IReadOnlyList<ObjectStateDefinition> states = targetObject.States;
        if (states == null || states.Count == 0)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.TextField(rect, "Required State", string.Empty);
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

            if (requiredStateIdProp.stringValue == stateId)
            {
                selectedIndex = i;
            }
        }

        if (selectedIndex < 0)
        {
            selectedIndex = 0;
            requiredStateIdProp.stringValue = states[0] != null ? states[0].StateId : string.Empty;
        }

        int newIndex = EditorGUI.Popup(rect, "Required State", selectedIndex, options.ToArray());

        if (newIndex >= 0 && newIndex < states.Count)
        {
            requiredStateIdProp.stringValue = states[newIndex] != null ? states[newIndex].StateId : string.Empty;
        }
    }

    private void DrawWarnings(
        ref float y,
        Rect position,
        SerializedProperty targetObjectProp,
        SerializedProperty requiredStateIdProp)
    {
        List<string> warnings = GetWarnings(targetObjectProp, requiredStateIdProp);

        foreach (string warning in warnings)
        {
            float helpHeight = EditorStyles.helpBox.CalcHeight(
                new GUIContent(warning),
                position.width - (BoxPadding * 2f));

            Rect helpRect = new Rect(
                position.x + BoxPadding,
                y,
                position.width - (BoxPadding * 2f),
                helpHeight);

            EditorGUI.HelpBox(helpRect, warning, MessageType.Warning);
            y += helpHeight + EditorGUIUtility.standardVerticalSpacing;
        }
    }

    private List<string> GetWarnings(SerializedProperty targetObjectProp, SerializedProperty requiredStateIdProp)
    {
        List<string> warnings = new List<string>();

        ObjectDefinition targetObject = targetObjectProp.objectReferenceValue as ObjectDefinition;

        if (targetObject == null)
        {
            warnings.Add("Debes asignar un ObjectDefinition.");
            return warnings;
        }

        if (targetObject.States == null || targetObject.States.Count == 0)
        {
            warnings.Add("El objeto seleccionado no tiene estados configurados.");
            return warnings;
        }

        bool found = false;

        for (int i = 0; i < targetObject.States.Count; i++)
        {
            ObjectStateDefinition state = targetObject.States[i];
            if (state != null && state.StateId == requiredStateIdProp.stringValue)
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            warnings.Add("El Required State actual no coincide con ningún estado del objeto seleccionado.");
        }

        return warnings;
    }

    private void DrawProperty(ref float y, Rect position, SerializedProperty property)
    {
        float height = EditorGUI.GetPropertyHeight(property, true);

        Rect rect = new Rect(
            position.x + BoxPadding,
            y,
            position.width - (BoxPadding * 2f),
            height);

        EditorGUI.PropertyField(rect, property, true);
        y += height + EditorGUIUtility.standardVerticalSpacing;
    }
}