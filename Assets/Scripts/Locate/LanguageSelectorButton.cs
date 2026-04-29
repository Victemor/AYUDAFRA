using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Componente que conecta un botón de la UI con el sistema de localización.
/// Al hacer clic, cambia el idioma global del juego al idioma configurado.
/// 
/// Adjuntar a cada botón de selección de idioma. No requiere código adicional
/// en el OnClick del Inspector; la suscripción se hace por código.
/// </summary>
[RequireComponent(typeof(Button))]
public class LanguageSelectorButton : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Tooltip("Idioma que este botón seleccionará al ser presionado.")]
    [SerializeField] private Language targetLanguage;

    [Tooltip("(Opcional) Si se asigna, este GameObject se activará cuando el idioma de este botón esté seleccionado, " +
             "permitiendo mostrar un indicador visual de selección activa.")]
    [SerializeField] private GameObject selectedIndicator;

    // ─── Estado ───────────────────────────────────────────────────────────────

    private Button _button;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        _button.onClick.AddListener(OnButtonClicked);
        LocalizationManager.OnLanguageChanged += OnLanguageChanged;

        // Sincroniza el indicador visual con el estado actual al habilitarse.
        RefreshSelectedIndicator();
    }

    private void OnDisable()
    {
        _button.onClick.RemoveListener(OnButtonClicked);
        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    // ─── Privado ──────────────────────────────────────────────────────────────

    private void OnButtonClicked()
    {
        if (LocalizationManager.Instance == null)
        {
            Debug.LogError("[LanguageSelectorButton] LocalizationManager no encontrado en la escena.");
            return;
        }
        LocalizationManager.Instance.SetLanguage(targetLanguage);
    }

    private void OnLanguageChanged(Language newLanguage) => RefreshSelectedIndicator();

    private void RefreshSelectedIndicator()
    {
        if (selectedIndicator == null) return;
        if (LocalizationManager.Instance == null) return;

        bool isActive = LocalizationManager.Instance.CurrentLanguage == targetLanguage;
        selectedIndicator.SetActive(isActive);
    }
}