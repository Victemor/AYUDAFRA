#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Data;

/// <summary>
/// PropertyDrawer personalizado para FragmentAction.
/// Permite contraer cada acción individual y muestra únicamente
/// los campos relevantes según el ActionType seleccionado.
/// </summary>
[CustomPropertyDrawer(typeof(FragmentAction))]
public sealed class ActionFlowEditorDrawer : PropertyDrawer
{
    private const float BoxPadding = 6f;
    private const float SectionSpacing = 4f;
    private const float FoldoutIndent = 14f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty actionTypeProp = property.FindPropertyRelative("actionType");
        FragmentActionType actionType = GetActionType(actionTypeProp);

        Rect backgroundRect = new Rect(position.x, position.y, position.width, GetPropertyHeight(property, label));
        EditorGUI.HelpBox(backgroundRect, string.Empty, MessageType.None);

        float y = position.y + BoxPadding;

        DrawFoldoutHeader(ref y, position, property, actionTypeProp);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            DrawProperty(ref y, position, property, "actionType");
            DrawProperty(ref y, position, property, "waitAfter");

            DrawActionSpecificFields(ref y, position, property, actionType);
            DrawConditions(ref y, position, property);
            DrawValidationMessages(ref y, position, property, actionType);

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = BoxPadding * 2f;
        height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        if (!property.isExpanded)
            return height;

        AddPropertyHeight(ref height, property, "actionType");
        AddPropertyHeight(ref height, property, "waitAfter");

        SerializedProperty actionTypeProp = property.FindPropertyRelative("actionType");
        FragmentActionType actionType = GetActionType(actionTypeProp);

        AddActionSpecificHeights(ref height, property, actionType);
        AddConditionsHeight(ref height, property);
        AddValidationHeight(ref height, property, actionType);

        return height;
    }

    private void DrawFoldoutHeader(
        ref float y,
        Rect position,
        SerializedProperty property,
        SerializedProperty actionTypeProp)
    {
        string title = GetActionTitle(actionTypeProp);

        Rect foldoutRect = new Rect(
            position.x + BoxPadding,
            y,
            position.width - BoxPadding * 2f,
            EditorGUIUtility.singleLineHeight);

        property.isExpanded = EditorGUI.Foldout(
            foldoutRect,
            property.isExpanded,
            GUIContent.none,
            true);

        Rect titleRect = new Rect(
            foldoutRect.x + FoldoutIndent,
            foldoutRect.y,
            foldoutRect.width - FoldoutIndent,
            foldoutRect.height);

        EditorGUI.LabelField(titleRect, title, EditorStyles.boldLabel);

        y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
    }

    private void DrawActionSpecificFields(
        ref float y,
        Rect position,
        SerializedProperty property,
        FragmentActionType actionType)
    {
        switch (actionType)
        {
            case FragmentActionType.RainStart:
            case FragmentActionType.RainChangeIntensity:
                DrawProperty(ref y, position, property, "rainIntensity");
                DrawProperty(ref y, position, property, "rainTransitionTime");
                break;

            case FragmentActionType.RainStop:
                DrawProperty(ref y, position, property, "rainTransitionTime");
                break;

            case FragmentActionType.WindStart:
            case FragmentActionType.WindChangeIntensity:
                DrawProperty(ref y, position, property, "windIntensity");
                DrawProperty(ref y, position, property, "windTransitionTime");
                break;

            case FragmentActionType.WindStop:
                DrawProperty(ref y, position, property, "windTransitionTime");
                break;

            case FragmentActionType.SpriteFadeIn:
            case FragmentActionType.SpriteFadeOut:
            case FragmentActionType.SpriteDissolve:
            case FragmentActionType.SpriteDissolveVertical:
            case FragmentActionType.SpriteDissolveBoth:
            case FragmentActionType.SpriteAppearDissolve:
            case FragmentActionType.SpriteAppearDissolveVertical:
            case FragmentActionType.SpriteAppearDissolveBoth:
                DrawProperty(ref y, position, property, "targetSpriteObject");
                DrawProperty(ref y, position, property, "spriteTransitionTime");
                break;

            case FragmentActionType.SpriteFadeMaterialColor:
                DrawProperty(ref y, position, property, "targetSpriteObject");
                DrawProperty(ref y, position, property, "emissiveColor");
                DrawProperty(ref y, position, property, "spriteTransitionTime");
                DrawProperty(ref y, position, property, "fadeMaterialParticles");
                break;

            case FragmentActionType.FireStart:
                DrawProperty(ref y, position, property, "targetFireObject");
                DrawProperty(ref y, position, property, "fireIntensity");
                DrawProperty(ref y, position, property, "fireTransitionInTime");
                break;

            case FragmentActionType.FireStop:
                DrawProperty(ref y, position, property, "targetFireObject");
                DrawProperty(ref y, position, property, "fireTransitionOutTime");
                break;

            case FragmentActionType.SetBloomIntensity:
                DrawProperty(ref y, position, property, "bloomIntensity");
                DrawProperty(ref y, position, property, "bloomTransitionTime");
                break;

            case FragmentActionType.SetBloomTint:
                DrawProperty(ref y, position, property, "bloomTint");
                DrawProperty(ref y, position, property, "bloomTransitionTime");
                break;

            case FragmentActionType.CreateFootprintPathAnimation:
                DrawProperty(ref y, position, property, "footprintPathController");
                DrawProperty(ref y, position, property, "useHalfFootprintAnimation");
                DrawProperty(ref y, position, property, "footprintSpeed");
                break;

            case FragmentActionType.WaitTimeForTheNextAction:
                DrawProperty(ref y, position, property, "legacyWaitTime");
                break;

            case FragmentActionType.WaitForSpecificInputForContinue:
                DrawProperty(ref y, position, property, "inputType");

                SerializedProperty inputTypeProp = property.FindPropertyRelative("inputType");
                if (inputTypeProp != null && (InputType)inputTypeProp.enumValueIndex == InputType.SpecificKey)
                    DrawProperty(ref y, position, property, "specificKey");

                break;

            case FragmentActionType.ShowThoughtInPanel:
                DrawProperty(ref y, position, property, "text");
                break;

            case FragmentActionType.SetCinematicCameraTarget:
                DrawProperty(ref y, position, property, "cinematicTargetMode");

                SerializedProperty modeProp = property.FindPropertyRelative("cinematicTargetMode");
                if (modeProp != null)
                {
                    if ((CinematicTargetMode)modeProp.enumValueIndex == CinematicTargetMode.TargetTransform)
                        DrawProperty(ref y, position, property, "cinematicTarget");
                    else
                        DrawProperty(ref y, position, property, "cinematicManualPosition");
                }

                DrawProperty(ref y, position, property, "cinematicOffset");
                DrawProperty(ref y, position, property, "cameraTransitionTime");
                DrawProperty(ref y, position, property, "overrideCinematicZoom");

                SerializedProperty overrideZoomProp = property.FindPropertyRelative("overrideCinematicZoom");
                if (overrideZoomProp != null && overrideZoomProp.boolValue)
                {
                    DrawProperty(ref y, position, property, "cinematicMinZoom");
                    DrawProperty(ref y, position, property, "cinematicMaxZoom");
                    DrawProperty(ref y, position, property, "cinematicInitialZoom");
                }

                break;

            case FragmentActionType.SwitchExplorationCamera:
                DrawProperty(ref y, position, property, "cameraTransitionTime");
                break;

            case FragmentActionType.DisplayDialoguePanel:
                DrawProperty(ref y, position, property, "dialogController");
                DrawProperty(ref y, position, property, "dialogPoint");
                DrawProperty(ref y, position, property, "dialogText");
                break;

            case FragmentActionType.HideDialoguePanel:
                DrawProperty(ref y, position, property, "dialogController");
                break;

            case FragmentActionType.SetWeatherProfile:
                DrawProperty(ref y, position, property, "weatherProfile");
                break;

            case FragmentActionType.StartEmotionSelection:
                DrawProperty(ref y, position, property, "emotionSequenceController");
                DrawProperty(ref y, position, property, "emotionA");
                DrawProperty(ref y, position, property, "emotionB");
                DrawProperty(ref y, position, property, "emotionMemory");
                DrawProperty(ref y, position, property, "emotionObject");
                break;

            case FragmentActionType.SpawnFirstAvailableDraggableItem:
                DrawProperty(ref y, position, property, "draggableSpawnCandidates");
                DrawProperty(ref y, position, property, "draggableSpawnPoint");
                break;

            case FragmentActionType.ShowTutorial:
                DrawProperty(ref y, position, property, "tutorialId");
                DrawProperty(ref y, position, property, "tutorialOffsetY");
                break;

            case FragmentActionType.HideTutorial:
                DrawProperty(ref y, position, property, "tutorialId");
                break;
        }
    }

    private void AddActionSpecificHeights(
        ref float height,
        SerializedProperty property,
        FragmentActionType actionType)
    {
        switch (actionType)
        {
            case FragmentActionType.RainStart:
            case FragmentActionType.RainChangeIntensity:
                AddPropertyHeight(ref height, property, "rainIntensity");
                AddPropertyHeight(ref height, property, "rainTransitionTime");
                break;

            case FragmentActionType.RainStop:
                AddPropertyHeight(ref height, property, "rainTransitionTime");
                break;

            case FragmentActionType.WindStart:
            case FragmentActionType.WindChangeIntensity:
                AddPropertyHeight(ref height, property, "windIntensity");
                AddPropertyHeight(ref height, property, "windTransitionTime");
                break;

            case FragmentActionType.WindStop:
                AddPropertyHeight(ref height, property, "windTransitionTime");
                break;

            case FragmentActionType.SpriteFadeIn:
            case FragmentActionType.SpriteFadeOut:
            case FragmentActionType.SpriteDissolve:
            case FragmentActionType.SpriteDissolveVertical:
            case FragmentActionType.SpriteDissolveBoth:
            case FragmentActionType.SpriteAppearDissolve:
            case FragmentActionType.SpriteAppearDissolveVertical:
            case FragmentActionType.SpriteAppearDissolveBoth:
                AddPropertyHeight(ref height, property, "targetSpriteObject");
                AddPropertyHeight(ref height, property, "spriteTransitionTime");
                break;

            case FragmentActionType.SpriteFadeMaterialColor:
                AddPropertyHeight(ref height, property, "targetSpriteObject");
                AddPropertyHeight(ref height, property, "emissiveColor");
                AddPropertyHeight(ref height, property, "spriteTransitionTime");
                AddPropertyHeight(ref height, property, "fadeMaterialParticles");
                break;

            case FragmentActionType.FireStart:
                AddPropertyHeight(ref height, property, "targetFireObject");
                AddPropertyHeight(ref height, property, "fireIntensity");
                AddPropertyHeight(ref height, property, "fireTransitionInTime");
                break;

            case FragmentActionType.FireStop:
                AddPropertyHeight(ref height, property, "targetFireObject");
                AddPropertyHeight(ref height, property, "fireTransitionOutTime");
                break;

            case FragmentActionType.SetBloomIntensity:
                AddPropertyHeight(ref height, property, "bloomIntensity");
                AddPropertyHeight(ref height, property, "bloomTransitionTime");
                break;

            case FragmentActionType.SetBloomTint:
                AddPropertyHeight(ref height, property, "bloomTint");
                AddPropertyHeight(ref height, property, "bloomTransitionTime");
                break;

            case FragmentActionType.CreateFootprintPathAnimation:
                AddPropertyHeight(ref height, property, "footprintPathController");
                AddPropertyHeight(ref height, property, "useHalfFootprintAnimation");
                AddPropertyHeight(ref height, property, "footprintSpeed");
                break;

            case FragmentActionType.WaitTimeForTheNextAction:
                AddPropertyHeight(ref height, property, "legacyWaitTime");
                break;

            case FragmentActionType.WaitForSpecificInputForContinue:
                AddPropertyHeight(ref height, property, "inputType");

                SerializedProperty inputTypeProp = property.FindPropertyRelative("inputType");
                if (inputTypeProp != null && (InputType)inputTypeProp.enumValueIndex == InputType.SpecificKey)
                    AddPropertyHeight(ref height, property, "specificKey");

                break;

            case FragmentActionType.ShowThoughtInPanel:
                AddPropertyHeight(ref height, property, "text");
                break;

            case FragmentActionType.SetCinematicCameraTarget:
                AddPropertyHeight(ref height, property, "cinematicTargetMode");

                SerializedProperty modeProp = property.FindPropertyRelative("cinematicTargetMode");
                if (modeProp != null)
                {
                    if ((CinematicTargetMode)modeProp.enumValueIndex == CinematicTargetMode.TargetTransform)
                        AddPropertyHeight(ref height, property, "cinematicTarget");
                    else
                        AddPropertyHeight(ref height, property, "cinematicManualPosition");
                }

                AddPropertyHeight(ref height, property, "cinematicOffset");
                AddPropertyHeight(ref height, property, "cameraTransitionTime");
                AddPropertyHeight(ref height, property, "overrideCinematicZoom");

                SerializedProperty overrideZoomProp = property.FindPropertyRelative("overrideCinematicZoom");
                if (overrideZoomProp != null && overrideZoomProp.boolValue)
                {
                    AddPropertyHeight(ref height, property, "cinematicMinZoom");
                    AddPropertyHeight(ref height, property, "cinematicMaxZoom");
                    AddPropertyHeight(ref height, property, "cinematicInitialZoom");
                }

                break;

            case FragmentActionType.SwitchExplorationCamera:
                AddPropertyHeight(ref height, property, "cameraTransitionTime");
                break;

            case FragmentActionType.DisplayDialoguePanel:
                AddPropertyHeight(ref height, property, "dialogController");
                AddPropertyHeight(ref height, property, "dialogPoint");
                AddPropertyHeight(ref height, property, "dialogText");
                break;

            case FragmentActionType.HideDialoguePanel:
                AddPropertyHeight(ref height, property, "dialogController");
                break;

            case FragmentActionType.SetWeatherProfile:
                AddPropertyHeight(ref height, property, "weatherProfile");
                break;

            case FragmentActionType.StartEmotionSelection:
                AddPropertyHeight(ref height, property, "emotionSequenceController");
                AddPropertyHeight(ref height, property, "emotionA");
                AddPropertyHeight(ref height, property, "emotionB");
                AddPropertyHeight(ref height, property, "emotionMemory");
                AddPropertyHeight(ref height, property, "emotionObject");
                break;

            case FragmentActionType.SpawnFirstAvailableDraggableItem:
                AddPropertyHeight(ref height, property, "draggableSpawnCandidates");
                AddPropertyHeight(ref height, property, "draggableSpawnPoint");
                break;

            case FragmentActionType.ShowTutorial:
                AddPropertyHeight(ref height, property, "tutorialId");
                AddPropertyHeight(ref height, property, "tutorialOffsetY");
                break;

            case FragmentActionType.HideTutorial:
                AddPropertyHeight(ref height, property, "tutorialId");
                break;
        }
    }

    private void DrawConditions(ref float y, Rect position, SerializedProperty property)
    {
        y += SectionSpacing;
        DrawProperty(ref y, position, property, "conditions");
    }

    private void AddConditionsHeight(ref float height, SerializedProperty property)
    {
        height += SectionSpacing;
        AddPropertyHeight(ref height, property, "conditions");
    }

    private void DrawValidationMessages(
        ref float y,
        Rect position,
        SerializedProperty property,
        FragmentActionType actionType)
    {
        List<string> warnings = GetWarnings(property, actionType);
        if (warnings.Count == 0)
            return;

        foreach (string warning in warnings)
        {
            float helpHeight = EditorStyles.helpBox.CalcHeight(
                new GUIContent(warning),
                position.width - BoxPadding * 2f);

            Rect helpRect = new Rect(
                position.x + BoxPadding,
                y,
                position.width - BoxPadding * 2f,
                helpHeight);

            EditorGUI.HelpBox(helpRect, warning, MessageType.Warning);
            y += helpHeight + EditorGUIUtility.standardVerticalSpacing;
        }
    }

    private void AddValidationHeight(
        ref float height,
        SerializedProperty property,
        FragmentActionType actionType)
    {
        List<string> warnings = GetWarnings(property, actionType);

        foreach (string warning in warnings)
        {
            float helpHeight = EditorStyles.helpBox.CalcHeight(
                new GUIContent(warning),
                EditorGUIUtility.currentViewWidth - 50f);

            height += helpHeight + EditorGUIUtility.standardVerticalSpacing;
        }
    }

    private List<string> GetWarnings(SerializedProperty property, FragmentActionType actionType)
    {
        List<string> warnings = new();

        switch (actionType)
        {
            case FragmentActionType.DisplayDialoguePanel:
                AddWarningIfMissing(property, "dialogController", "Falta asignar DialogueController.", warnings);
                AddWarningIfMissing(property, "dialogPoint", "Falta asignar DialogPoint.", warnings);
                AddWarningIfEmpty(property, "dialogText", "DialogText está vacío.", warnings);
                break;

            case FragmentActionType.HideDialoguePanel:
                AddWarningIfMissing(property, "dialogController", "Falta asignar DialogueController.", warnings);
                break;

            case FragmentActionType.SpawnFirstAvailableDraggableItem:
                AddWarningIfMissing(property, "draggableSpawnPoint", "Falta asignar DraggableSpawnPoint.", warnings);
                break;

            case FragmentActionType.ShowTutorial:
            case FragmentActionType.HideTutorial:
                AddWarningIfEmpty(property, "tutorialId", "TutorialId está vacío. El tutorial no se mostrará.", warnings);
                break;
        }

        return warnings;
    }

    private void AddWarningIfMissing(
        SerializedProperty property,
        string name,
        string message,
        List<string> warnings)
    {
        SerializedProperty prop = property.FindPropertyRelative(name);

        if (prop != null &&
            prop.propertyType == SerializedPropertyType.ObjectReference &&
            prop.objectReferenceValue == null)
        {
            warnings.Add(message);
        }
    }

    private void AddWarningIfEmpty(
        SerializedProperty property,
        string name,
        string message,
        List<string> warnings)
    {
        SerializedProperty prop = property.FindPropertyRelative(name);

        if (prop != null && string.IsNullOrWhiteSpace(prop.stringValue))
            warnings.Add(message);
    }

    private void DrawProperty(
        ref float y,
        Rect position,
        SerializedProperty parentProperty,
        string relativePropertyName)
    {
        SerializedProperty childProperty = parentProperty.FindPropertyRelative(relativePropertyName);
        if (childProperty == null)
            return;

        float height = EditorGUI.GetPropertyHeight(childProperty, true);

        Rect rect = new Rect(
            position.x + BoxPadding,
            y,
            position.width - BoxPadding * 2f,
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
            return;

        totalHeight += EditorGUI.GetPropertyHeight(childProperty, true) +
                       EditorGUIUtility.standardVerticalSpacing;
    }

    private string GetActionTitle(SerializedProperty actionTypeProp)
    {
        if (actionTypeProp == null)
            return "Fragment Action";

        return actionTypeProp.enumDisplayNames[actionTypeProp.enumValueIndex];
    }

    private FragmentActionType GetActionType(SerializedProperty actionTypeProp)
    {
        if (actionTypeProp == null)
            return FragmentActionType.SpriteFadeIn;

        return (FragmentActionType)actionTypeProp.intValue;
    }
}
#endif