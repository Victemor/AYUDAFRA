using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base de datos de fragmentos narrativos.
/// Permite resolver FragmentSO a partir de su identificador.
/// </summary>
[CreateAssetMenu(fileName = "FragmentDatabaseSO", menuName = "Scriptable Objects/FragmentDatabaseSO")]
public class FragmentDatabaseSO : ScriptableObject
{
    [SerializeField] private List<FragmentSO> fragments = new();

    private Dictionary<string, FragmentSO> fragmentsByName;

    /// <summary>
    /// Inicializa la base de datos en memoria.
    /// </summary>
    public void Initialize()
    {
        fragmentsByName = new Dictionary<string, FragmentSO>();

        foreach (FragmentSO fragment in fragments)
        {
            if (fragment == null)
                continue;

            if (fragmentsByName.ContainsKey(fragment.FragmentName))
            {
                Debug.LogError($"Fragment duplicado: {fragment.FragmentName}");
                continue;
            }

            fragmentsByName.Add(fragment.FragmentName, fragment);
        }
    }

    /// <summary>
    /// Obtiene un fragmento por su identificador.
    /// </summary>
    public FragmentSO GetByName(string fragmentName)
    {
        fragmentsByName.TryGetValue(fragmentName, out FragmentSO fragment);
        return fragment;
    }

    public FragmentSO GetFirstFragment()
    {
        return fragments.Count > 0 ? fragments[0] : null;
    }

    public List<FragmentSO> GetAllFragments()
    {
        return fragments;
    }


}
