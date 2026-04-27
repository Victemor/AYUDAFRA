using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Data;

[CustomPropertyDrawer(typeof(ObjectActionSet))]
public sealed class ObjectActionSetPropertyDrawer : PropertyDrawer
{
    private static readonly Dictionary<string, bool> FoldoutStates = new();

    private const float BoxPadding = 6f;
    private const float SectionSpacing = 4f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string path = property.propertyPath;

        if (!FoldoutStates.ContainsKey(path))
        {
            FoldoutStates[path] = true;
        }

        Rect backgroundRect = new Rect(
            position.x,
            position.y,
            position.width,
            GetPropertyHeight(property, label));

        EditorGUI.HelpBox(backgroundRect, GUIContent.none.text, MessageType.None);

        string title = GetTitle(property, label);

        Rect foldoutRect = new Rect(
            position.x + BoxPadding,
            position.y + BoxPadding,
            position.width - (BoxPadding * 2f),
            EditorGUIUtility.singleLineHeight);

        FoldoutStates[path] = EditorGUI.Foldout(
            foldoutRect,
            FoldoutStates[path],
            title,
            true);

        if (!FoldoutStates[path])
        {
            return;
        }

        float y = foldoutRect.yMax + EditorGUIUtility.standardVerticalSpacing;

        DrawObjectDefinitionField(ref y, position, property);
        DrawStateSelector(ref y, position, property);
        DrawNameField(ref y, position, property);
        DrawActionsField(ref y, position, property);
        DrawConditionsField(ref y, position, property);
        DrawWarnings(ref y, position, property);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        string path = property.propertyPath;

        float height = BoxPadding * 2f + EditorGUIUtility.singleLineHeight;

        if (!FoldoutStates.ContainsKey(path) || !FoldoutStates[path])
        {
            return height;
        }

        height += EditorGUIUtility.standardVerticalSpacing;

        AddPropertyHeight(ref height, property, "objectDefinition");
        AddStateSelectorHeight(ref height, property);
        AddPropertyHeight(ref height, property, "name");
        AddPropertyHeight(ref height, property, "actions");
        AddPropertyHeight(ref height, property, "conditions");
        AddWarningsHeight(ref height, property);

        return height;
    }

    private void DrawObjectDefinitionField(ref float y, Rect position, SerializedProperty property)
    {
        DrawProperty(ref y, position, property, "objectDefinition");
    }

    private void DrawStateSelector(ref float y, Rect position, SerializedProperty property)
    {
        SerializedProperty objectDefinitionProp = property.FindPropertyRelative("objectDefinition");
        SerializedProperty idProp = property.FindPropertyRelative("id");

        float lineHeight = EditorGUIUtility.singleLineHeight;
        Rect rect = new Rect(
            position.x + BoxPadding,
            y,
            position.width - (BoxPadding * 2f),
            lineHeight);

        if (objectDefinitionProp == null || idProp == null)
        {
            y += lineHeight + EditorGUIUtility.standardVerticalSpacing;
            return;
        }

        ObjectDefinition objectDefinition = objectDefinitionProp.objectReferenceValue as ObjectDefinition;

        if (objectDefinition == null)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.TextField(rect, "State Id", string.Empty);
            }

            y += lineHeight + EditorGUIUtility.standardVerticalSpacing;
            return;
        }

        IReadOnlyList<ObjectStateDefinition> states = objectDefinition.States;

        if (states == null || states.Count == 0)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.TextField(rect, "State Id", string.Empty);
            }

            y += lineHeight + EditorGUIUtility.standardVerticalSpacing;
            return;
        }

        List<string> validStateIds = new List<string>();
        int selectedIndex = -1;

        for (int i = 0; i < states.Count; i++)
        {
            ObjectStateDefinition state = states[i];
            string stateId = state != null ? state.StateId : string.Empty;

            if (string.IsNullOrWhiteSpace(stateId))
            {
                stateId = $"<State {i + 1} vacío>";
            }

            validStateIds.Add(stateId);

            if (idProp.stringValue == stateId)
            {
                selectedIndex = i;
            }
        }

        if (selectedIndex < 0)
        {
            selectedIndex = 0;

            ObjectStateDefinition firstState = states[0];
            if (firstState != null && !string.IsNullOrWhiteSpace(firstState.StateId))
            {
                idProp.stringValue = firstState.StateId;
            }
            else
            {
                idProp.stringValue = string.Empty;
            }
        }

        int newIndex = EditorGUI.Popup(rect, "State Id", selectedIndex, validStateIds.ToArray());

        if (newIndex >= 0 && newIndex < states.Count)
        {
            ObjectStateDefinition selectedState = states[newIndex];
            idProp.stringValue = selectedState != null ? selectedState.StateId : string.Empty;
        }

        y += lineHeight + EditorGUIUtility.standardVerticalSpacing;
    }

    private void AddStateSelectorHeight(ref float height, SerializedProperty property)
    {
        height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
    }

    private void DrawNameField(ref float y, Rect position, SerializedProperty property)
    {
        DrawProperty(ref y, position, property, "name");
    }

    private void DrawActionsField(ref float y, Rect position, SerializedProperty property)
    {
        y += SectionSpacing;
        DrawProperty(ref y, position, property, "actions");
    }

    private void DrawConditionsField(ref float y, Rect position, SerializedProperty property)
    {
        y += SectionSpacing;
        DrawProperty(ref y, position, property, "conditions");
    }

    private void DrawWarnings(ref float y, Rect position, SerializedProperty property)
    {
        List<string> warnings = GetWarnings(property);
        if (warnings.Count == 0)
        {
            return;
        }

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

    private void AddWarningsHeight(ref float height, SerializedProperty property)
    {
        List<string> warnings = GetWarnings(property);

        foreach (string warning in warnings)
        {
            float helpHeight = EditorStyles.helpBox.CalcHeight(
                new GUIContent(warning),
                EditorGUIUtility.currentViewWidth - 50f);

            height += helpHeight + EditorGUIUtility.standardVerticalSpacing;
        }
    }

    private List<string> GetWarnings(SerializedProperty property)
    {
        List<string> warnings = new List<string>();

        SerializedProperty objectDefinitionProp = property.FindPropertyRelative("objectDefinition");
        SerializedProperty idProp = property.FindPropertyRelative("id");

        ObjectDefinition objectDefinition = objectDefinitionProp != null
            ? objectDefinitionProp.objectReferenceValue as ObjectDefinition
            : null;

        if (objectDefinition == null)
        {
            warnings.Add("Debes asignar un ObjectDefinition para poder vincular este grupo a un estado.");
            return warnings;
        }

        IReadOnlyList<ObjectStateDefinition> states = objectDefinition.States;

        if (states == null || states.Count == 0)
        {
            warnings.Add("El ObjectDefinition seleccionado no tiene estados configurados.");
            return warnings;
        }

        bool containsState = false;

        for (int i = 0; i < states.Count; i++)
        {
            ObjectStateDefinition state = states[i];
            if (state != null && state.StateId == idProp.stringValue)
            {
                containsState = true;
                break;
            }
        }

        if (!containsState)
        {
            warnings.Add("El State Id actual no coincide con ningún estado válido del objeto seleccionado.");
        }

        return warnings;
    }

    private string GetTitle(SerializedProperty property, GUIContent label)
    {
        SerializedProperty nameProp = property.FindPropertyRelative("name");

        if (nameProp != null && !string.IsNullOrWhiteSpace(nameProp.stringValue))
        {
            return nameProp.stringValue;
        }

        return label.text;
    }

    private void DrawProperty(
        ref float y,
        Rect position,
        SerializedProperty parentProperty,
        string relativePropertyName)
    {
        SerializedProperty childProperty = parentProperty.FindPropertyRelative(relativePropertyName);
        if (childProperty == null)
        {
            return;
        }

        float height = EditorGUI.GetPropertyHeight(childProperty, true);

        Rect rect = new Rect(
            position.x + BoxPadding,
            y,
            position.width - (BoxPadding * 2f),
            height);

        EditorGUI.PropertyField(rect, childProperty, true);

        y += height + EditorGUIUtility.standardVerticalSpacing;
    }

    private void AddPropertyHeight(
        ref float totalHeight,
        SerializedProperty parentProperty,
        string relativePropertyName)
    {
        SerializedProperty childProperty = parentProperty.FindPropertyRelative(relativePropertyName);
        if (childProperty == null)
        {
            return;
        }

        totalHeight += EditorGUI.GetPropertyHeight(childProperty, true)
            + EditorGUIUtility.standardVerticalSpacing;
    }
}