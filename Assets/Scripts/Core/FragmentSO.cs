using UnityEngine;

[CreateAssetMenu(fileName = "FragmentSO", menuName = "Scriptable Objects/FragmentSO")]
public class FragmentSO : ScriptableObject
{
    [SerializeField] private string fragmentName;

    /// <summary>
    /// Nombre único del fragmento.
    /// Debe coincidir con el nombre de la gota.
    /// </summary>
    public string FragmentName => fragmentName;
    
}
