using System.Collections.Generic;
using UnityEngine;
using Game.Data;

/// <summary>
/// Requisito de un fragmento dentro de una condición de fragmentos conectados.
/// Permite declarar una memoria y cero o más estados de objetos requeridos.
/// </summary>
[System.Serializable]
public sealed class FragmentConnectionRequirement
{
    [Tooltip("Fragmento que debe participar en la conexión.")]
    [SerializeField] private MemoryDefinition targetMemory;

    [Tooltip("Objetos que deben estar en estados específicos dentro de este fragmento.")]
    [SerializeField] private List<FragmentConnectionObjectStateRequirement> requiredObjectStates = new();

    /// <summary>
    /// Memoria objetivo del requisito.
    /// </summary>
    public MemoryDefinition TargetMemory => targetMemory;

    /// <summary>
    /// Requisitos de estado de objetos dentro de la memoria.
    /// Puede estar vacío.
    /// </summary>
    public IReadOnlyList<FragmentConnectionObjectStateRequirement> RequiredObjectStates => requiredObjectStates;
}