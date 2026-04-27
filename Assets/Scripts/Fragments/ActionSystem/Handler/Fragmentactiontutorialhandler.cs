using System.Collections;
using UnityEngine;
using Game.Data;

/// <summary>
/// Handler del sistema de acciones para mostrar y ocultar tutoriales.
///
/// ShowTutorial: muestra la instrucción con el ID y offset configurados en la acción.
///   Solo la muestra si nunca fue mostrada antes (delegado a TutorialController).
/// HideTutorial: oculta el tutorial activo si coincide con el ID configurado.
/// </summary>
public sealed class FragmentActionTutorialHandler
{
    public IEnumerator Execute(FragmentAction action, MonoBehaviour host)
    {
        TutorialController controller = GetController();

        if (controller == null)
        {
            Debug.LogWarning(
                "[FragmentActionTutorialHandler] TutorialController no encontrado. " +
                "Asegúrate de que SystemsGameplay esté en escena.", host);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(action.TutorialId))
        {
            Debug.LogWarning(
                $"[FragmentActionTutorialHandler] TutorialId vacío en acción {action.ActionType}.", host);
            yield break;
        }

        switch (action.ActionType)
        {
            case FragmentActionType.ShowTutorial:
                controller.ShowInstruction(action.TutorialId, action.TutorialOffsetY);
                break;

            case FragmentActionType.HideTutorial:
                controller.HideTutorial(action.TutorialId);
                break;
        }

        yield break;
    }

    public bool CanHandle(FragmentActionType actionType)
    {
        return actionType == FragmentActionType.ShowTutorial ||
               actionType == FragmentActionType.HideTutorial;
    }

    private TutorialController GetController()
    {
        return SystemsGameplay.Instance != null
            ? SystemsGameplay.Instance.GetTutorialController()
            : null;
    }
}