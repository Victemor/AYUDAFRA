using System.Collections;
using UnityEngine;
using Game.Data;

/// <summary>
/// Handler que ejecuta acciones del sistema de diálogo.
///
/// Lógica de resolución (prioridad descendente):
/// 1. Si <c>action.LocalizedDialogText</c> está asignado → resuelve async y usa ruta localizada.
/// 2. Si no → usa <c>action.DialogText</c> como texto plano directo (fallback).
///
/// Esto permite mantener los textos existentes sin migrar mientras se va
/// añadiendo la localización de forma gradual entrada por entrada.
/// </summary>
public sealed class FragmentActionDialogueHandler
{
    public IEnumerator Execute(FragmentAction action, MonoBehaviour host)
    {
        switch (action.ActionType)
        {
            case FragmentActionType.DisplayDialoguePanel:
                yield return ExecuteDisplayDialoguePanel(action, host);
                break;

            case FragmentActionType.HideDialoguePanel:
                ExecuteHideDialoguePanel(action, host);
                break;
        }
    }

    public bool CanHandle(FragmentActionType actionType)
    {
        return actionType == FragmentActionType.DisplayDialoguePanel ||
               actionType == FragmentActionType.HideDialoguePanel;
    }

    private IEnumerator ExecuteDisplayDialoguePanel(FragmentAction action, MonoBehaviour host)
    {
        if (action.DialogController == null)
        {
            Debug.LogWarning("[FragmentActionDialogueHandler] DialogueController no asignado.", host);
            yield break;
        }

        if (action.DialogPoint == null)
        {
            Debug.LogWarning("[FragmentActionDialogueHandler] DialogPoint no asignado.", host);
            yield break;
        }

        // ── Ruta localizada ──────────────────────────────────────────────────
        if (action.LocalizedDialogText != null && !action.LocalizedDialogText.IsEmpty)
        {
            var handle = action.LocalizedDialogText.GetLocalizedStringAsync();
            yield return handle;

            if (!handle.IsDone || string.IsNullOrWhiteSpace(handle.Result))
            {
                Debug.LogWarning("[FragmentActionDialogueHandler] No se pudo resolver LocalizedDialogText.", host);
                yield break;
            }

            action.DialogController.ShowText(handle.Result, action.DialogPoint.position);
            yield break;
        }

        // ── Ruta raw (fallback) ──────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(action.DialogText))
        {
            action.DialogController.ShowText(action.DialogText, action.DialogPoint.position);
            yield break;
        }

        Debug.LogWarning("[FragmentActionDialogueHandler] Ni LocalizedDialogText ni DialogText tienen contenido.", host);
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