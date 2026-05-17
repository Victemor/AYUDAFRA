using TMPro;
using UnityEngine;

/// <summary>
/// Controlador de playtest para transiciones de nivel manuales.
///
/// Gestiona la visibilidad de un panel de UI y actualiza el texto del nivel completado.
/// No genera ningún elemento de UI por código.
///
/// Setup en escena:
/// <list type="number">
///   <item>Crear un Canvas panel con un TMP_Text y un Button. Dejarlo desactivado.</item>
///   <item>Asignar el panel a <see cref="levelCompletePanel"/>.</item>
///   <item>Asignar el TMP_Text a <see cref="levelCompleteText"/>.</item>
///   <item>En el botón → OnClick → este componente → <see cref="OnNextLevelButtonPressed"/>.</item>
///   <item>Activar <c>Require Input To Advance</c> en <see cref="InfiniteLevelManager"/>.</item>
/// </list>
/// </summary>
public sealed class PlaytestLevelController : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]
    [SerializeField]
    [Tooltip("Manager de niveles. Debe tener 'Require Input To Advance' activo.")]
    private InfiniteLevelManager levelManager;

    [Header("Panel")]
    [SerializeField]
    [Tooltip("Panel de UI que aparece al completar el nivel. Debe estar desactivado en la escena.")]
    private GameObject levelCompletePanel;

    [SerializeField]
    [Tooltip("Componente de texto del panel. Se actualiza automáticamente al completar un nivel.\n" +
             "Puede ser cualquier TMP_Text dentro del panel.")]
    private TMP_Text levelCompleteText;

    [SerializeField]
    [Tooltip("Formato del mensaje de nivel completado.\n" +
             "{0} se reemplaza por el número de nivel.\n" +
             "Ejemplo: '¡Has completado el nivel {0}!'")]
    private string completionMessageFormat = "¡Has completado el nivel {0}!";

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        levelManager = FindAnyObjectByType<InfiniteLevelManager>();
    }

    private void OnEnable()
    {
        GameEvents.OnGoalReached     += HandleGoalReached;
        GameEvents.OnPlayerRespawned += HandlePlayerRespawned;
    }

    private void OnDisable()
    {
        GameEvents.OnGoalReached     -= HandleGoalReached;
        GameEvents.OnPlayerRespawned -= HandlePlayerRespawned;
    }

    private void Start()
    {
        SetPanelVisible(false);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Conectar al OnClick del botón del panel desde el Inspector de Unity.
    /// Oculta el panel y dispara el avance al siguiente nivel.
    /// </summary>
    public void OnNextLevelButtonPressed()
    {
        SetPanelVisible(false);
        levelManager?.AdvanceToNextLevel();
    }

    #endregion

    #region Event Handlers

    private void HandleGoalReached()
    {
        UpdateLevelText();
        SetPanelVisible(true);
    }

    private void HandlePlayerRespawned() => SetPanelVisible(false);

    #endregion

    #region Helpers

    private void UpdateLevelText()
    {
        if (levelCompleteText == null || levelManager == null) return;
        levelCompleteText.text = string.Format(completionMessageFormat, levelManager.CurrentLevelIndex);
    }

    private void SetPanelVisible(bool visible)
    {
        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(visible);
    }

    #endregion
}