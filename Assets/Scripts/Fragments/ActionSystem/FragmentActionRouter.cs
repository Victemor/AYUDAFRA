using System.Collections;
using UnityEngine;
using Game.Data;

/// <summary>
/// Router central que delega la ejecución de cada FragmentAction al handler apropiado.
/// Cada handler es responsable de un grupo de tipos de acción relacionados.
/// </summary>
public sealed class FragmentActionRouter
{
    private readonly FragmentActionUtilityHandler utilityHandler = new();
    private readonly FragmentActionEnvironmentHandler environmentHandler = new();
    private readonly FragmentActionSpriteHandler spriteHandler = new();
    private readonly FragmentActionCameraHandler cameraHandler = new();
    private readonly FragmentActionDialogueHandler dialogueHandler = new();
    private readonly FragmentActionEmotionHandler emotionHandler = new();
    private readonly FragmentActionThoughtHandler thoughtHandler = new();
    private readonly FragmentActionFootprintHandler footprintHandler = new();
    private readonly FragmentActionDraggableHandler draggableHandler = new();
    private readonly FragmentActionTutorialHandler tutorialHandler = new();

    public IEnumerator Execute(FragmentAction action, FragmentActionExecutionContext context, MonoBehaviour host)
    {
        if (action == null)
        {
            yield break;
        }

        FragmentActionType actionType = action.ActionType;

        if (utilityHandler.CanHandle(actionType))
        {
            yield return utilityHandler.Execute(action);
        }
        else if (environmentHandler.CanHandle(actionType))
        {
            yield return environmentHandler.Execute(action, context, host);
        }
        else if (spriteHandler.CanHandle(actionType))
        {
            yield return spriteHandler.Execute(action, host);
        }
        else if (cameraHandler.CanHandle(actionType))
        {
            yield return cameraHandler.Execute(action, context, host);
        }
        else if (dialogueHandler.CanHandle(actionType))
        {
            yield return dialogueHandler.Execute(action, host);
        }
        else if (emotionHandler.CanHandle(actionType))
        {
            yield return emotionHandler.Execute(action, context, host);
        }
        else if (thoughtHandler.CanHandle(actionType))
        {
            yield return thoughtHandler.Execute(action, host);
        }
        else if (footprintHandler.CanHandle(actionType))
        {
            yield return footprintHandler.Execute(action, host);
        }
        else if (draggableHandler.CanHandle(actionType))
        {
            yield return draggableHandler.Execute(action, host);
        }
        else if (tutorialHandler.CanHandle(actionType))
        {
            yield return tutorialHandler.Execute(action, host);
        }
        else
        {
            Debug.LogWarning($"[FragmentActionRouter] No existe handler para {actionType}.", host);
        }

        if (action.WaitAfter > 0f)
        {
            yield return new WaitForSeconds(action.WaitAfter);
        }
    }
}