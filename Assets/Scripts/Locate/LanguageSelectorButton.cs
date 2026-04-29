using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// Botón que cambia el locale activo del juego al hacer clic.
/// El cambio es inmediato: dispara <see cref="LocalizationSettings.SelectedLocaleChanged"/>
/// que propaga la actualización a todos los sistemas suscritos
/// (<see cref="LocalizedText"/>, <see cref="ConsciousnessDisplay"/>,
/// <see cref="ThoughtUIManager"/>, etc.) sin recargar la escena.
///
/// Reemplaza la versión anterior que dependía de LocalizationManager
/// (sistema custom eliminado en migración Opción A).
///
/// Setup: adjunta a cada botón de idioma y arrastra el asset Locale
/// desde Assets/LocalizationSettings/Local/.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class LanguageSelectorButton : MonoBehaviour
{
    private const string PrefsKey = "SelectedLocaleCode";

    [Tooltip("Locale que este botón activa. " +
             "Arrastra el asset desde Assets/LocalizationSettings/Local/.")]
    [SerializeField] private Locale targetLocale;

    [Tooltip("GameObject que se activa cuando este idioma está seleccionado " +
             "(indicador visual de selección activa).")]
    [SerializeField] private GameObject selectedIndicator;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private IEnumerator Start()
    {
        // Espera que Unity Localization esté listo antes de actualizar el indicador.
        yield return LocalizationSettings.InitializationOperation;
        RefreshSelectedIndicator();
    }

    private void OnEnable()
    {
        button.onClick.AddListener(OnButtonClicked);
        LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
        RefreshSelectedIndicator();
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(OnButtonClicked);
        LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
    }

    private void OnButtonClicked()
    {
        if (targetLocale == null)
        {
            Debug.LogWarning("[LanguageSelectorButton] targetLocale no asignado.", this);
            return;
        }

        // Cambio inmediato: dispara SelectedLocaleChanged en todos los suscriptores.
        LocalizationSettings.SelectedLocale = targetLocale;

        // Persistencia como respaldo al selector automático de Unity Localization.
        PlayerPrefs.SetString(PrefsKey, targetLocale.Identifier.Code);
        PlayerPrefs.Save();
    }

    private void HandleLocaleChanged(Locale locale) => RefreshSelectedIndicator();

    private void RefreshSelectedIndicator()
    {
        if (selectedIndicator == null || targetLocale == null)
        {
            return;
        }

        Locale current = LocalizationSettings.SelectedLocale;
        bool isSelected = current != null &&
                          current.Identifier.Code == targetLocale.Identifier.Code;

        selectedIndicator.SetActive(isSelected);
    }
}