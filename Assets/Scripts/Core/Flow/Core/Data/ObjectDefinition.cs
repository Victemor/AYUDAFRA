using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Data
{
    /// <summary>
    /// Define un objeto interactivo dentro de una memoria.
    /// Su ID se sincroniza automáticamente con el nombre del asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Object Definition")]
    public class ObjectDefinition : ScriptableObject
    {
        [Header("Identification")]

        [Tooltip("ID único del objeto. Se sincroniza automáticamente con el nombre del asset.")]
        [SerializeField] private string id;

        [Header("States")]

        [Tooltip("Estados secuenciales del objeto.")]
        [SerializeField] private List<ObjectStateDefinition> states = new();

        /// <summary>
        /// Identificador único del objeto.
        /// </summary>
        public string Id => id;

        /// <summary>
        /// Estados configurados para este objeto.
        /// </summary>
        public IReadOnlyList<ObjectStateDefinition> States => states;

#if UNITY_EDITOR
        private void OnValidate()
        {
            SyncAutoFields();
        }

        /// <summary>
        /// Sincroniza los campos automáticos derivados del nombre del asset.
        /// </summary>
        public void SyncAutoFields()
        {
            string assetPath = AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(assetPath))
                return;

            id = name;
            EditorUtility.SetDirty(this);
        }
#endif
    }
}