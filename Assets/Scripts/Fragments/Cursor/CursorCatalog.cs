using System.Collections.Generic;
using UnityEngine;

namespace Game.CursorSystem
{
    /// <summary>
    /// Catálogo central de cursores del juego.
    /// Permite resolver un tipo lógico a su definición visual.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Cursor/Cursor Catalog")]
    public sealed class CursorCatalog : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Listado de definiciones disponibles.")]
        private CursorDefinition[] definitions;

        private Dictionary<CursorType, CursorDefinition> lookup;

        /// <summary>
        /// Inicializa el catálogo en memoria.
        /// </summary>
        public void Initialize()
        {
            lookup = new Dictionary<CursorType, CursorDefinition>();

            if (definitions == null)
            {
                return;
            }

            for (int i = 0; i < definitions.Length; i++)
            {
                CursorDefinition definition = definitions[i];

                if (!lookup.ContainsKey(definition.Type))
                {
                    lookup.Add(definition.Type, definition);
                }
            }
        }

        /// <summary>
        /// Intenta obtener la definición de un cursor.
        /// </summary>
        /// <param name="type">Tipo solicitado.</param>
        /// <param name="definition">Definición encontrada.</param>
        /// <returns>True si existe una definición registrada.</returns>
        public bool TryGet(CursorType type, out CursorDefinition definition)
        {
            if (lookup == null)
            {
                Initialize();
            }

            return lookup.TryGetValue(type, out definition);
        }
    }
}