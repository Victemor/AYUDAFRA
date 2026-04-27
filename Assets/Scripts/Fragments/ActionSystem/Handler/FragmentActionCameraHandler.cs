using System.Collections;
using UnityEngine;
using Game.Data;

public sealed class FragmentActionCameraHandler
{
    public IEnumerator Execute(FragmentAction action, FragmentActionExecutionContext context, MonoBehaviour host)
    {
        ActionBehaviorController controller = context.Controller;

        switch (action.ActionType)
        {
            case FragmentActionType.SetCinematicCameraTarget:
                yield return ExecuteSetCinematicCameraTarget(action, controller, host);
                yield break;

            case FragmentActionType.SwitchExplorationCamera:
                ExecuteSwitchExplorationCamera(action, controller, host);
                yield break;
        }
    }

    public bool CanHandle(FragmentActionType actionType)
    {
        return actionType == FragmentActionType.SetCinematicCameraTarget ||
               actionType == FragmentActionType.SwitchExplorationCamera;
    }

    private IEnumerator ExecuteSetCinematicCameraTarget(
        FragmentAction action,
        ActionBehaviorController controller,
        MonoBehaviour host)
    {
        if (controller.CameraSystem == null)
        {
            Debug.LogWarning("[FragmentActionCameraHandler] CameraSystemController no asignado.", host);
            yield break;
        }

        Transform finalTarget;
        GameObject temporaryManualTarget = null;

        if (action.CinematicTargetMode == CinematicTargetMode.TargetTransform)
        {
            if (action.CinematicTarget == null)
            {
                Debug.LogWarning("[FragmentActionCameraHandler] Cinematic target null en modo Transform.", host);
                yield break;
            }

            finalTarget = action.CinematicTarget;
        }
        else
        {
            temporaryManualTarget = new GameObject("CinematicManualTarget");
            temporaryManualTarget.transform.position = action.CinematicManualPosition;
            finalTarget = temporaryManualTarget.transform;
        }

        if (action.CameraTransitionTime > 0f)
        {
            controller.CameraSystem.SetTransitionDuration(action.CameraTransitionTime);
        }

        controller.CameraSystem.EnterCinematicMode(
            finalTarget,
            action.CinematicOffset,
            action.OverrideCinematicZoom,
            action.CinematicMinZoom,
            action.CinematicMaxZoom,
            action.CinematicInitialZoom);

        yield return null;

        if (temporaryManualTarget != null)
        {
            UnityEngine.Object.Destroy(temporaryManualTarget);
        }
    }

    private void ExecuteSwitchExplorationCamera(
        FragmentAction action,
        ActionBehaviorController controller,
        MonoBehaviour host)
    {
        if (controller.CameraSystem == null)
        {
            Debug.LogWarning("[FragmentActionCameraHandler] CameraSystemController no asignado.", host);
            return;
        }

        if (action.CameraTransitionTime > 0f)
        {
            controller.CameraSystem.SetTransitionDuration(action.CameraTransitionTime);
        }

        controller.CameraSystem.ExitCinematicMode();
    }
}