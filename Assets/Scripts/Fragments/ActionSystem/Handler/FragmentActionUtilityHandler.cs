using System.Collections;
using UnityEngine;
using Game.Data;

public sealed class FragmentActionUtilityHandler
{
    public IEnumerator Execute(FragmentAction action)
    {
        switch (action.ActionType)
        {
            case FragmentActionType.WaitTimeForTheNextAction:
                if (action.LegacyWaitTime > 0f)
                {
                    yield return new WaitForSeconds(action.LegacyWaitTime);
                }
                yield break;

            case FragmentActionType.WaitForSpecificInputForContinue:
                yield return new WaitUntil(() => IsInputTriggered(action));
                yield break;
        }
    }

    public bool CanHandle(FragmentActionType actionType)
    {
        return actionType == FragmentActionType.WaitTimeForTheNextAction ||
               actionType == FragmentActionType.WaitForSpecificInputForContinue;
    }

    private bool IsInputTriggered(FragmentAction action)
    {
        if (FragmentInputController.Instance != null)
        {
            return FragmentInputController.Instance.IsInputTriggered(
                action.InputType,
                action.SpecificKey);
        }

        switch (action.InputType)
        {
            case InputType.AnyKey:
                return Input.anyKeyDown || Input.GetMouseButtonDown(0);

            case InputType.SpecificKey:
                return Input.GetKeyDown(action.SpecificKey);

            case InputType.MouseClick:
                return Input.GetMouseButtonDown(0);

            default:
                return false;
        }
    }
}