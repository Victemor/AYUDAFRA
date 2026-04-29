using System.Collections;
using UnityEngine;
using Game.Data;

/// <summary>
/// Handler que ejecuta la acción <see cref="FragmentActionType.ShowThoughtInPanel"/>.
///
/// Pasa el <see cref="UnityEngine.Localization.LocalizedString"/> directamente a
/// <see cref="ConsciousnessSystem.AddThought(UnityEngine.Localization.LocalizedString)"/>
/// sin intentar extraer tabla + clave manualmente. Esto evita el problema donde
/// <c>TableEntryReference.Key</c> devuelve string vacío cuando Unity guardó
/// la referencia internamente por ID numérico en lugar de por nombre de clave.
///
/// El campo <c>action.Text</c> (string plano) es solo referencia visual para el
/// diseñador en el Inspector y no tiene efecto en runtime.
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

        if (action.LocalizedText == null || action.LocalizedText.IsEmpty)
        {
            Debug.LogWarning(
                "[FragmentActionThoughtHandler] LocalizedText vacío. " +
                "Asigna tabla y clave en el Inspector. " +
                "El campo Text es solo referencia visual.",
                host);
            yield break;
        }

        // Verificamos que el string se resuelve correctamente antes de registrar.
        var handle = action.LocalizedText.GetLocalizedStringAsync();
        yield return handle;

        if (!handle.IsDone || string.IsNullOrWhiteSpace(handle.Result))
        {
            Debug.LogWarning("[FragmentActionThoughtHandler] No se pudo resolver LocalizedText.", host);
            yield break;
        }

        // Pasamos el LocalizedString completo — ConsciousnessSystem extrae
        // internamente Key y KeyId manejando ambos tipos de referencia.
        ConsciousnessSystem.Instance.AddThought(action.LocalizedText);
    }

    public bool CanHandle(FragmentActionType actionType)
    {
        return actionType == FragmentActionType.ShowThoughtInPanel;
    }
}