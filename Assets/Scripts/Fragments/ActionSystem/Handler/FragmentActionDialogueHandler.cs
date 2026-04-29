using System.Collections;
using UnityEngine;
using Game.Data;

/// <summary>
/// Versión DIAGNÓSTICA del handler de diálogo.
/// Reemplaza temporalmente el original para encontrar exactamente dónde falla.
/// Una vez identificado el problema, vuelve a la versión limpia.
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
        Debug.Log("[DIAG-Dialogue] ► ExecuteDisplayDialoguePanel START", host);

        // ── Check 1: DialogController ────────────────────────────────────────
        if (action.DialogController == null)
        {
            Debug.LogError("[DIAG-Dialogue] ✘ DialogController es NULL", host);
            yield break;
        }
        Debug.Log("[DIAG-Dialogue] ✔ DialogController OK", host);

        // ── Check 2: DialogPoint ─────────────────────────────────────────────
        if (action.DialogPoint == null)
        {
            Debug.LogError("[DIAG-Dialogue] ✘ DialogPoint es NULL", host);
            yield break;
        }
        Debug.Log($"[DIAG-Dialogue] ✔ DialogPoint OK → posición: {action.DialogPoint.position}", host);

        // ── Check 3: LocalizedDialogText ─────────────────────────────────────
        if (action.LocalizedDialogText == null)
        {
            Debug.LogError("[DIAG-Dialogue] ✘ LocalizedDialogText es NULL (campo no serializado)", host);
            yield break;
        }

        if (action.LocalizedDialogText.IsEmpty)
        {
            Debug.LogError("[DIAG-Dialogue] ✘ LocalizedDialogText está vacío (sin tabla/clave asignada)", host);
            yield break;
        }

        string tableName = action.LocalizedDialogText.TableReference.TableCollectionName;
        string key       = action.LocalizedDialogText.TableEntryReference.Key;
        long   keyId     = action.LocalizedDialogText.TableEntryReference.KeyId;

        Debug.Log($"[DIAG-Dialogue] ✔ LocalizedDialogText → Tabla: '{tableName}' | Key: '{key}' | KeyId: {keyId}", host);

        // ── Check 4: Resolución async ─────────────────────────────────────────
        Debug.Log("[DIAG-Dialogue] ► Iniciando GetLocalizedStringAsync...", host);

        var handle = action.LocalizedDialogText.GetLocalizedStringAsync();
        yield return handle;

        Debug.Log($"[DIAG-Dialogue] ► Async completado. IsDone: {handle.IsDone} | Status: {handle.Status}", host);

        if (!handle.IsDone)
        {
            Debug.LogError("[DIAG-Dialogue] ✘ Async no completó (IsDone = false)", host);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(handle.Result))
        {
            Debug.LogError($"[DIAG-Dialogue] ✘ Result vacío. La entrada '{key}'/'{keyId}' no existe en la tabla para el locale activo.", host);
            yield break;
        }

        Debug.Log($"[DIAG-Dialogue] ✔ Texto resuelto: '{handle.Result}'", host);

        // ── Check 5: Llamada a ShowText ───────────────────────────────────────
        Debug.Log($"[DIAG-Dialogue] ► Llamando DialogController.ShowText en posición {action.DialogPoint.position}", host);

        action.DialogController.ShowText(handle.Result, action.DialogPoint.position);

        Debug.Log("[DIAG-Dialogue] ✔ ShowText llamado correctamente", host);
    }

    private void ExecuteHideDialoguePanel(FragmentAction action, MonoBehaviour host)
    {
        if (action.DialogController == null)
        {
            Debug.LogWarning("[DIAG-Dialogue] HideDialoguePanel: DialogController NULL", host);
            return;
        }

        action.DialogController.HideCurrent();
        Debug.Log("[DIAG-Dialogue] HideDialoguePanel ejecutado", host);
    }
}