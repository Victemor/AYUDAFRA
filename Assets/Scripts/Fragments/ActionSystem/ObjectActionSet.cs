using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using Game.Conditions;

/// <summary>
/// Un ObjectActionSet agrupa acciones y condiciones de activación para un objeto específico.
/// El identificador técnico del grupo se vincula a un StateId del objeto seleccionado.
/// </summary>
[System.Serializable]
public class ObjectActionSet
{
    [Header("Binding")]

    [Tooltip("Objeto del sistema narrativo al que pertenece este grupo de acciones.")]
    [SerializeField] private ObjectDefinition objectDefinition;

    [Tooltip("ID técnico del grupo. Se vincula a un StateId del objeto seleccionado.")]
    [SerializeField] private string id;

    [Tooltip("Nombre opcional para identificar el grupo en inspector y debug.")]
    [SerializeField] private string name;

    [Tooltip("Acciones que se ejecutan secuencialmente cuando el grupo se activa.")]
    [SerializeField] private List<FragmentAction> actions = new();

    [Header("Condiciones")]
    [Tooltip("Grupos de condiciones. Se interpreta OR entre grupos y AND dentro de cada grupo.")]
    [SerializeField] private List<ConditionGroup> conditions = new();

    [HideInInspector]
    [SerializeField] private bool hasExecuted;

    /// <summary>
    /// Objeto asociado al grupo.
    /// </summary>
    public ObjectDefinition ObjectDefinition => objectDefinition;

    /// <summary>
    /// Identificador técnico del grupo.
    /// Debe corresponder a un StateId del objeto asociado.
    /// </summary>
    public string Id => id;

    /// <summary>
    /// Nombre descriptivo para inspector y debug.
    /// </summary>
    public string Name => name;

    /// <summary>
    /// Acciones del grupo.
    /// </summary>
    public IReadOnlyList<FragmentAction> Actions => actions;

    /// <summary>
    /// Condiciones de activación del grupo.
    /// </summary>
    public IReadOnlyList<ConditionGroup> Conditions => conditions;

    /// <summary>
    /// Estado serializado legacy del grupo.
    /// </summary>
    public bool HasExecuted
    {
        get => hasExecuted;
        set => hasExecuted = value;
    }
}