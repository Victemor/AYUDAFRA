using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Data;

[CustomPropertyDrawer(typeof(FragmentConnectionRequirement))]
public sealed class FragmentConnectionRequirementDrawer : PropertyDrawer
{
    private static readonly Dictionary<string, bool> FoldoutStates = new();
    private const float BoxPadding = 6f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string path = property.propertyPath;

        if (!FoldoutStates.ContainsKey(path))
        {
            FoldoutStates[path] = true;
        }

        SerializedProperty targetMemoryProp = property.FindPropertyRelative("targetMemory");
        SerializedProperty requiredObjectStatesProp = property.FindPropertyRelative("requiredObjectStates");

        Rect backgroundRect = new Rect(
            position.x,
            position.y,
            position.width,
            GetPropertyHeight(property, label));

        EditorGUI.HelpBox(backgroundRect, GUIContent.none.text, MessageType.None);

        string title = GetTitle(targetMemoryProp, label);

        Rect foldoutRect = new Rect(
            position.x + BoxPadding,
            position.y + BoxPadding,
            position.width - (BoxPadding * 2f),
            EditorGUIUtility.singleLineHeight);

        FoldoutStates[path] = EditorGUI.Foldout(foldoutRect, FoldoutStates[path], title, true);

        if (!FoldoutStates[path])
        {
            return;
        }

        float y = foldoutRect.yMax + EditorGUIUtility.standardVerticalSpacing;

        DrawProperty(ref y, position, targetMemoryProp);
        DrawProperty(ref y, position, requiredObjectStatesProp);
        DrawWarnings(ref y, position, targetMemoryProp);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        string path = property.propertyPath;

        float height = BoxPadding * 2f + EditorGUIUtility.singleLineHeight;

        if (!FoldoutStates.ContainsKey(path) || !FoldoutStates[path])
        {
            return height;
        }

        SerializedProperty targetMemoryProp = property.FindPropertyRelative("targetMemory");
        SerializedProperty requiredObjectStatesProp = property.FindPropertyRelative("requiredObjectStates");

        height += EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(targetMemoryProp, true) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(requiredObjectStatesProp, true) + EditorGUIUtility.standardVerticalSpacing;

        List<string> warnings = GetWarnings(targetMemoryProp);

        foreach (string warning in warnings)
        {
            float helpHeight = EditorStyles.helpBox.CalcHeight(
                new GUIContent(warning),
                EditorGUIUtility.currentViewWidth - 60f);

            height += helpHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        return height;
    }

    private string GetTitle(SerializedProperty targetMemoryProp, GUIContent label)
    {
        MemoryDefinition memory = targetMemoryProp.objectReferenceValue as MemoryDefinition;
        if (memory != null)
        {
            return memory.name;
        }

        return label.text;
    }

    private void DrawWarnings(ref float y, Rect position, SerializedProperty targetMemoryProp)
    {
        List<string> warnings = GetWarnings(targetMemoryProp);

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

    private List<string> GetWarnings(SerializedProperty targetMemoryProp)
    {
        List<string> warnings = new List<string>();

        if (targetMemoryProp.objectReferenceValue == null)
        {
            warnings.Add("Debes asignar una MemoryDefinition.");
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