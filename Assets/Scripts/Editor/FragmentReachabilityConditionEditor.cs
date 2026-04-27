using UnityEditor;
using UnityEngine;
using Game.Conditions;

namespace Game.EditorTools
{
    /// <summary>
    /// Editor personalizado para <see cref="FragmentReachabilityCondition"/>.
    /// Valida una red alcanzable y cerrada entre los fragmentos listados.
    /// </summary>
    [CustomEditor(typeof(FragmentReachabilityCondition))]
    public sealed class FragmentReachabilityConditionEditor : Editor
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
                "Esta condición exige que todos los fragmentos listados pertenezcan al mismo subgrafo conectado por camino " +
                "y que ese subgrafo contenga exclusivamente esos fragmentos. " +
                "El orden de la lista no afecta el resultado. Cada fragmento puede además declarar cero o más requisitos de objetos.",
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
                    "La condición necesita al menos dos fragmentos para validar alcanzabilidad.",
                    MessageType.Warning);
            }
        }
    }
}