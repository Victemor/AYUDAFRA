using System.Collections.Generic;
using UnityEngine;
using Game.Conditions;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Data
{
    /// <summary>
    /// Define un fragmento de memoria configurable.
    /// Su identidad y escena asociada se sincronizan automáticamente
    /// con el nombre del asset para evitar inconsistencias manuales.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Memory Definition")]
    public class MemoryDefinition : ScriptableObject
    {
        [Header("Identification")]

        [Tooltip("ID único del fragmento. Se sincroniza automáticamente con el nombre del asset.")]
        [SerializeField] private string id;

        [Header("Objects")]

        [Tooltip("Objetos presentes en este fragmento.")]
        [SerializeField] private List<ObjectDefinition> objects = new();

        [Header("Unlock Conditions (OR of ANDs)")]

        [Tooltip("Condiciones necesarias para desbloquear este fragmento.")]
        [SerializeField] private List<ConditionGroup> unlockConditions = new();

        [Header("Scene")]

        [Tooltip("Nombre de la escena asociada. Se sincroniza automáticamente con el nombre del asset.")]
        [SerializeField] private string sceneName;

        /// <summary>
        /// Identificador único de la memoria.
        /// </summary>
        public string Id => id;

        /// <summary>
        /// Nombre de escena asociado a esta memoria.
        /// </summary>
        public string SceneName => sceneName;

        /// <summary>
        /// Objetos definidos dentro de esta memoria.
        /// </summary>
        public IReadOnlyList<ObjectDefinition> Objects => objects;

        /// <summary>
        /// Grupos de condiciones de desbloqueo.
        /// </summary>
        public IReadOnlyList<ConditionGroup> UnlockConditions => unlockConditions;

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

            string assetName = name;
            id = assetName;
            sceneName = assetName;

            EditorUtility.SetDirty(this);
        }
#endif
    }
}