using System.Collections;
using UnityEngine;
using Game.Data;

public sealed class FragmentActionFootprintHandler
{
    public IEnumerator Execute(FragmentAction action, MonoBehaviour host)
    {
        if (action.FootprintPathController == null)
        {
            Debug.LogWarning("[FragmentActionFootprintHandler] FootprintPathController no asignado.", host);
            yield break;
        }

        action.FootprintPathController.PlayFootprints(
            action.UseHalfFootprintAnimation,
            action.FootprintSpeed);

        yield break;
    }

    public bool CanHandle(FragmentActionType actionType)
    {
        return actionType == FragmentActionType.CreateFootprintPathAnimation;
    }
}