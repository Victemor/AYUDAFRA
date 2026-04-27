using System.Collections;
using UnityEngine;
using Game.Data;

public sealed class FragmentActionThoughtHandler
{
    public IEnumerator Execute(FragmentAction action, MonoBehaviour host)
    {
        if (ConsciousnessSystem.Instance == null)
        {
            Debug.LogWarning("[FragmentActionThoughtHandler] ConsciousnessSystem no encontrado.", host);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(action.Text))
        {
            Debug.LogWarning("[FragmentActionThoughtHandler] Texto vacío en ShowThought.", host);
            yield break;
        }

        ConsciousnessSystem.Instance.AddThought(action.Text);
        yield break;
    }

    public bool CanHandle(FragmentActionType actionType)
    {
        return actionType == FragmentActionType.ShowThoughtInPanel;
    }
}