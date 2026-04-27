using UnityEngine;

namespace Game.CursorSystem
{
    /// <summary>
    /// Configuración visual de un cursor.
    /// </summary>
    [System.Serializable]
    public struct CursorDefinition
    {
        [Tooltip("Tipo lógico del cursor.")]
        [SerializeField] private CursorType type;

        [Tooltip("Textura usada por Unity para este cursor.")]
        [SerializeField] private Texture2D texture;

        [Tooltip("Punto activo del cursor en píxeles.")]
        [SerializeField] private Vector2 hotspot;

        [Tooltip("Modo de render del cursor.")]
        [SerializeField] private CursorMode mode;

        /// <summary>
        /// Tipo lógico del cursor.
        /// </summary>
        public CursorType Type => type;

        /// <summary>
        /// Textura del cursor.
        /// </summary>
        public Texture2D Texture => texture;

        /// <summary>
        /// Punto activo del cursor.
        /// </summary>
        public Vector2 Hotspot => hotspot;

        /// <summary>
        /// Modo de cursor.
        /// </summary>
        public CursorMode Mode => mode;
    }
}