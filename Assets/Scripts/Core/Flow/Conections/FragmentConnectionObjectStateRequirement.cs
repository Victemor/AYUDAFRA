using UnityEngine;
using Game.Data;

/// <summary>
/// Requisito de estado para un objeto dentro de un fragmento conectado.
/// </summary>
[System.Serializable]
public sealed class FragmentConnectionObjectStateRequirement
{
    [Tooltip("Objeto que debe validarse dentro del fragmento.")]
    [SerializeField] private ObjectDefinition targetObject;

    [Tooltip("StateId requerido para el objeto seleccionado.")]
    [SerializeField] private string requiredStateId;

    [Tooltip("Define si el estado debe coincidir exactamente o si también aceptan estados posteriores.")]
    [SerializeField] private ObjectStateComparisonMode comparisonMode = ObjectStateComparisonMode.Exact;

    /// <summary>
    /// Objeto objetivo del requisito.
    /// </summary>
    public ObjectDefinition TargetObject => targetObject;

    /// <summary>
    /// Estado requerido para el objeto objetivo.
    /// </summary>
    public string RequiredStateId => requiredStateId;

    /// <summary>
    /// Modo de comparación del estado.
    /// </summary>
    public ObjectStateComparisonMode ComparisonMode => comparisonMode;
}