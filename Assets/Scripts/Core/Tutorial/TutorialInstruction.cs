using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using Game.Core;
using Game.Data;

/// <summary>
/// Instrucción de tutorial con soporte de auto-cierre por evento de gameplay
/// y restricción opcional por estado global del juego.
///
/// Estrategia de localización con retrocompatibilidad:
/// - <c>text</c> (string) se conserva para no perder los textos ya escritos.
/// - <c>localizedText</c> (LocalizedString) es opcional. Cuando está asignado
///   tiene prioridad sobre <c>text</c>.
/// - <see cref="TutorialController"/> aplica: si <c>localizedText</c> está asignado
///   → ruta localizada; si no → <c>text</c> directo.
/// </summary>
[Serializable]
public class TutorialInstruction
{
    [Header("Identity")]

    [SerializeField]
    [Tooltip("Identificador único de la instrucción. Debe ser único en todo el juego.")]
    private string id;

    [Header("Content")]

    [SerializeField]
    [TextArea]
    [Tooltip("Texto del tutorial (fallback). Se usa cuando Localized Text no está asignado.")]
    private string text;

    [SerializeField]
    [Tooltip("(Opcional) Clave de localización del tutorial. Si está asignada, tiene prioridad sobre el texto de arriba.")]
    private LocalizedString localizedText;

    [SerializeField]
    [Tooltip("Imagen opcional para acompañar la instrucción.")]
    private Sprite image;

    [Header("State Visibility")]

    [SerializeField]
    [Tooltip("Si está activo, esta instrucción puede mostrarse en cualquier estado del juego.")]
    private bool allowAnyGameState = true;

    [SerializeField]
    [Tooltip("Estados permitidos para mostrar esta instrucción cuando Allow Any Game State está desactivado.")]
    private List<GamePlayState> allowedGameStates = new();

    [Header("Dismiss")]

    [SerializeField]
    [Tooltip("Evento de gameplay que cierra este tutorial automáticamente. None = solo se cierra manualmente.")]
    private TutorialDismissEvent dismissEvent = TutorialDismissEvent.None;

    [SerializeField]
    [Tooltip("Ítem específico que dispara el cierre. Si está vacío, cualquier ítem válido cierra el tutorial.")]
    private DraggableItemDefinition dismissItemDefinition;

    public bool HasBeenShown { get; private set; }

    public string Id => id;

    /// <summary>Texto plano (fallback cuando LocalizedText no está asignado).</summary>
    public string Text => text;

    /// <summary>
    /// Referencia de localización opcional.
    /// Si está asignada, tiene prioridad sobre <see cref="Text"/>.
    /// </summary>
    public LocalizedString LocalizedText => localizedText;

    public Sprite Image => image;
    public TutorialDismissEvent DismissEvent => dismissEvent;
    public DraggableItemDefinition DismissItemDefinition => dismissItemDefinition;

    /// <summary>Indica si esta instrucción puede mostrarse en el estado actual.</summary>
    public bool CanShowInState(GamePlayState state)
    {
        return allowAnyGameState || allowedGameStates.Contains(state);
    }

    public void MarkAsShown()       => HasBeenShown = true;
    public void RestoreShownState(bool shown) => HasBeenShown = shown;
    public void ResetShownState()   => HasBeenShown = false;
}