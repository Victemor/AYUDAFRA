using System.Collections.Generic;
using UnityEngine;
using Game.Data;

namespace Game.Data
{
    /// <summary>
    /// Base de datos central de todos los fragmentos del juego.
    /// Es la única fuente de verdad para el sistema.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Memory Database")]
    public class MemoryDatabase : ScriptableObject
    {
        [Tooltip("Todos los fragmentos del juego.")]
        [SerializeField] private List<MemoryDefinition> memories;

        public IReadOnlyList<MemoryDefinition> Memories => memories;
    }
}