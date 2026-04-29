using System.Collections;
using UnityEngine;
using Game.Data;

/// <summary>
/// Handler que ejecuta la acción <see cref="FragmentActionType.ShowThoughtInPanel"/>.
///
/// Lógica de resolución (prioridad descendente):
/// 1. Si <c>action.LocalizedText</c> está asignado → resuelve async y usa ruta localizada.
/// 2. Si no → usa <c>action.Text</c> como texto plano directo (fallback).
///
/// Esto permite mantener los textos existentes sin migrar mientras se va
/// añadiendo la localización de forma gradual entrada por entrada.
/// </summary>
public sealed class FragmentActionThoughtHandler
{
    public IEnumerator Execute(FragmentAction action, MonoBehaviour host)
    {
        if (ConsciousnessSystem.Instance == null)
        {
            Debug.LogWarning("[FragmentActionThoughtHandler] ConsciousnessSystem no encontrado.", host);
            yield break;
        }

        // ── Ruta localizada ──────────────────────────────────────────────────
        if (action.LocalizedText != null && !action.LocalizedText.IsEmpty)
        {
            var handle = action.LocalizedText.GetLocalizedStringAsync();
            yield return handle;

            if (!handle.IsDone || string.IsNullOrWhiteSpace(handle.Result))
            {
                Debug.LogWarning("[FragmentActionThoughtHandler] No se pudo resolver LocalizedText.", host);
                yield break;
            }

            string tableName = action.LocalizedText.TableReference.TableCollectionName;
            string key       = action.LocalizedText.TableEntryReference.Key;
            ConsciousnessSystem.Instance.AddThought(tableName, key);
            yield break;
        }

        // ── Ruta raw (fallback) ──────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(action.Text))
        {
            ConsciousnessSystem.Instance.AddThoughtRaw(action.Text);
            yield break;
        }

        Debug.LogWarning("[FragmentActionThoughtHandler] Ni LocalizedText ni Text tienen contenido.", host);
    }

    public bool CanHandle(FragmentActionType actionType)
    {
        return actionType == FragmentActionType.ShowThoughtInPanel;
    }
}