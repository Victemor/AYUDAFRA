using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// Enlaza un <see cref="TMP_Text"/> con Unity Localization.
/// Actualiza el texto inmediatamente cuando cambia el locale activo,
/// sin necesidad de recargar la escena.
///
/// Reemplaza la versión anterior que dependía de LocalizationManager
/// (sistema custom eliminado en migración Opción A).
///
/// Uso: adjunta al mismo GameObject que tenga el TMP_Text y
/// asigna el campo <c>localizedString</c> (tabla + clave) en el Inspector.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public sealed class LocalizedText : MonoBehaviour
{
    [Tooltip("Texto localizado. Selecciona tabla y clave desde el Inspector.")]
    [SerializeField] private LocalizedString localizedString;

    private TMP_Text textComponent;
    private Coroutine refreshCoroutine;

    private void Awake()
    {
        textComponent = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
        StartRefresh();
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;

        if (refreshCoroutine != null)
        {
            StopCoroutine(refreshCoroutine);
            refreshCoroutine = null;
        }
    }

    /// <summary>
    /// Cambia la referencia de localización en runtime y refresca el texto inmediatamente.
    /// Equivalente al antiguo SetKey().
    /// </summary>
    public void SetLocalizedString(LocalizedString newLocalizedString)
    {
        localizedString = newLocalizedString;
        StartRefresh();
    }

    private void HandleLocaleChanged(Locale locale) => StartRefresh();

    private void StartRefresh()
    {
        if (refreshCoroutine != null)
        {
            StopCoroutine(refreshCoroutine);
        }

        refreshCoroutine = StartCoroutine(RefreshText());
    }

    private IEnumerator RefreshText()
    {
        if (localizedString == null || localizedString.IsEmpty)
        {
            yield break;
        }

        var handle = localizedString.GetLocalizedStringAsync();
        yield return handle;

        if (handle.IsDone && !string.IsNullOrWhiteSpace(handle.Result))
        {
            textComponent.text = handle.Result;
        }

        refreshCoroutine = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        StartRefresh();
    }
#endif
}