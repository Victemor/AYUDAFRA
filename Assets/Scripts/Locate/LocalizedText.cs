using TMPro;
using UnityEngine;

/// <summary>
/// Componente que enlaza un <see cref="TMP_Text"/> con el sistema de localización.
/// Actualiza automáticamente el texto cada vez que el idioma cambia,
/// sin necesidad de recargar la escena.
/// 
/// Adjuntar este componente al mismo GameObject que tenga el TMP_Text.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Tooltip("Clave de localización que identifica este texto. Ej: 'ui.menu.play'")]
    [SerializeField] private string localizationKey;

    // ─── Estado ───────────────────────────────────────────────────────────────

    private TMP_Text _textComponent;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _textComponent = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += OnLanguageChanged;

        // Aplica el idioma actual al habilitarse, cubriendo el caso donde
        // el LocalizationManager ya existe antes de que este objeto despierte.
        RefreshText();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    // ─── API Pública ──────────────────────────────────────────────────────────

    /// <summary>
    /// Cambia la clave de localización en tiempo de ejecución y actualiza el texto.
    /// Útil para reutilizar el mismo componente con diferentes claves (ej: tooltips dinámicos).
    /// </summary>
    /// <param name="newKey">Nueva clave de localización.</param>
    public void SetKey(string newKey)
    {
        localizationKey = newKey;
        RefreshText();
    }

    // ─── Privado ──────────────────────────────────────────────────────────────

    private void OnLanguageChanged(Language _) => RefreshText();

    private void RefreshText()
    {
        if (LocalizationManager.Instance == null) return;
        if (string.IsNullOrEmpty(localizationKey)) return;

        _textComponent.text = LocalizationManager.Instance.GetText(localizationKey);
    }

#if UNITY_EDITOR
    // Recarga la preview en el Editor cuando se cambia la clave en el Inspector.
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        RefreshText();
    }
#endif
}