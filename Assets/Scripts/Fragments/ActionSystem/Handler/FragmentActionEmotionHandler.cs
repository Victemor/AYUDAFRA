using System.Collections;
using UnityEngine;
using Game.Data;
using Game.Runtime;

public sealed class FragmentActionEmotionHandler
{
    public IEnumerator Execute(FragmentAction action, FragmentActionExecutionContext context, MonoBehaviour host)
    {
        if (context.RuntimeContext?.Repository == null)
        {
            Debug.LogError("[FragmentActionEmotionHandler] Repository null.", host);
            yield break;
        }

        if (action.EmotionSequenceController == null)
        {
            Debug.LogError("[FragmentActionEmotionHandler] EmotionSequenceController no asignado.", host);
            yield break;
        }

        if (action.EmotionMemory == null)
        {
            Debug.LogError("[FragmentActionEmotionHandler] EmotionMemory no asignada.", host);
            yield break;
        }

        if (action.EmotionObject == null)
        {
            Debug.LogError("[FragmentActionEmotionHandler] EmotionObject no asignado.", host);
            yield break;
        }

        MemoryRuntimeData memory = context.RuntimeContext.Repository.GetMemory(action.EmotionMemory);
        if (memory == null)
        {
            Debug.LogError("[FragmentActionEmotionHandler] Memory runtime null.", host);
            yield break;
        }

        ObjectRuntimeData runtimeObject = memory.GetObject(action.EmotionObject);
        if (runtimeObject == null)
        {
            Debug.LogError("[FragmentActionEmotionHandler] RuntimeObject null.", host);
            yield break;
        }

        bool completed = false;
        EmotionResult result = default;

        void OnSelected(EmotionResult selectionResult)
        {
            result = selectionResult;
            completed = true;
        }

        action.EmotionSequenceController.OnEmotionSelected += OnSelected;
        action.EmotionSequenceController.StartSequence(action.EmotionA, action.EmotionB);

        yield return new WaitUntil(() => completed);

        action.EmotionSequenceController.OnEmotionSelected -= OnSelected;

        if (!result.IsNeutral)
        {
            int index = runtimeObject.GetStateIndex();
            EmotionType emotion = ConvertEmotion(result.Emotion);
            IntensityLevel intensity = ConvertIntensity(result.Intensity);

            runtimeObject.SetEmotionForState(index, new RuntimeEmotionState(emotion, intensity));
        }

        GameEvents.RaisePlayerAction();
    }

    public bool CanHandle(FragmentActionType actionType)
    {
        return actionType == FragmentActionType.StartEmotionSelection;
    }

    private EmotionType ConvertEmotion(EmotionData data)
    {
        switch (data.EmotionType)
        {
            case EmotionTypeGlobal.Guilt:
                return EmotionType.Guilt;
            case EmotionTypeGlobal.Repression:
                return EmotionType.Repression;
            case EmotionTypeGlobal.Nostalgia:
                return EmotionType.Nostalgia;
            case EmotionTypeGlobal.Confusion:
                return EmotionType.Confusion;
            case EmotionTypeGlobal.Acceptance:
                return EmotionType.Acceptance;
            case EmotionTypeGlobal.Neutral:
                return EmotionType.Neutral;
            default:
                return EmotionType.Neutral;
        }
    }

    private IntensityLevel ConvertIntensity(float value)
    {
        if (value < 0.33f)
        {
            return IntensityLevel.Low;
        }

        if (value < 0.66f)
        {
            return IntensityLevel.Medium;
        }

        return IntensityLevel.High;
    }
}