using System.Collections;
using UnityEngine;
using Game.Data;

public sealed class FragmentActionDialogueHandler
{
    public IEnumerator Execute(FragmentAction action, MonoBehaviour host)
    {
        switch (action.ActionType)
        {
            case FragmentActionType.DisplayDialoguePanel:
                ExecuteDisplayDialoguePanel(action, host);
                break;

            case FragmentActionType.HideDialoguePanel:
                ExecuteHideDialoguePanel(action, host);
                break;
        }

        yield break;
    }

    public bool CanHandle(FragmentActionType actionType)
    {
        return actionType == FragmentActionType.DisplayDialoguePanel ||
               actionType == FragmentActionType.HideDialoguePanel;
    }

    private void ExecuteDisplayDialoguePanel(FragmentAction action, MonoBehaviour host)
    {
        if (action.DialogController == null)
        {
            Debug.LogWarning("[FragmentActionDialogueHandler] DialogueController no asignado.", host);
            return;
        }

        if (action.DialogPoint == null)
        {
            Debug.LogWarning("[FragmentActionDialogueHandler] DialogPoint no asignado.", host);
            return;
        }

        if (string.IsNullOrWhiteSpace(action.DialogText))
        {
            Debug.LogWarning("[FragmentActionDialogueHandler] DialogText vacío.", host);
            return;
        }

        action.DialogController.ShowText(
            action.DialogText,
            action.DialogPoint.position);
    }
    private void ExecuteHideDialoguePanel(FragmentAction action, MonoBehaviour host)
    {
        if (action.DialogController == null)
        {
            Debug.LogWarning("[FragmentActionDialogueHandler] DialogueController no asignado.", host);
            return;
        }

        action.DialogController.HideCurrent();
    }
}