using UnityEditor;
using UnityEngine;
using Game.Conditions;

namespace Game.EditorTools
{
    /// <summary>
    /// Editor personalizado para <see cref="FragmentConnectedCondition"/>.
    /// Valida una red completa y cerrada entre los fragmentos listados.
    /// </summary>
    [CustomEditor(typeof(FragmentConnectedCondition))]
    public sealed class FragmentConnectedConditionEditor : Editor
    {
        private SerializedProperty fragmentRequirementsProp;

        private void OnEnable()
        {
            fragmentRequirementsProp = serializedObject.FindProperty("fragmentRequirements");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Esta condición exige que todos los fragmentos listados estén conectados directamente entre sí " +
                "y que ninguno tenga conexiones hacia fragmentos externos al grupo. " +
                "Cada fragmento puede además declarar cero o más requisitos de objetos.",
                MessageType.Info);

            EditorGUILayout.PropertyField(fragmentRequirementsProp, true);

            DrawWarnings();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawWarnings()
        {
            if (fragmentRequirementsProp == null)
            {
                return;
            }

            if (fragmentRequirementsProp.arraySize < 2)
            {
                EditorGUILayout.HelpBox(
                    "La condición necesita al menos dos fragmentos para validar conexión completa.",
                    MessageType.Warning);
            }
        }
    }
}