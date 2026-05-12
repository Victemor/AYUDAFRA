using System;
using UnityEngine;

/// <summary>
/// Entrada de prefab disponible en el catálogo global de generación.
/// </summary>
[Serializable]
public sealed class TrackSpawnPrefabEntry
{
    [Tooltip("ID único usado para identificar este prefab desde las configuraciones por nivel. Ejemplo: box_wood_small.")]
    [SerializeField] private string id;

    [Tooltip("Nombre legible para diseñadores. Se usa solo para ordenar y entender mejor el catálogo.")]
    [SerializeField] private string displayName;

    [Tooltip("Prefab que será instanciado cuando esta entrada sea seleccionada.")]
    [SerializeField] private GameObject prefab;

    [Tooltip("Qué tanto se elige este prefab frente a otros de la misma lista. Un valor mayor aparece más. Si es 0, no aparece.")]
    [SerializeField] private float baseSelectionWeight = 1f;

    [Tooltip("Ancho aproximado que ocupa este objeto sobre la pista. Se usa para evitar solapamientos.")]
    [SerializeField] private float reservationWidth = 1f;

    [Tooltip("Largo aproximado que ocupa este objeto sobre la pista. Se usa para evitar solapamientos.")]
    [SerializeField] private float reservationLength = 1f;

    [Tooltip("Altura extra al colocar este objeto sobre la pista. Útil si el pivot queda en el centro o debajo del modelo.")]
    [SerializeField] private float verticalOffset;

    public string Id => id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
    public GameObject Prefab => prefab;
    public float BaseSelectionWeight => Mathf.Max(0f, baseSelectionWeight);
    public float ReservationWidth => Mathf.Max(0.1f, reservationWidth);
    public float ReservationLength => Mathf.Max(0.1f, reservationLength);
    public float VerticalOffset => verticalOffset;

    /// <summary>
    /// Normaliza valores inválidos configurados desde el Inspector.
    /// </summary>
    public void Normalize()
    {
        if (baseSelectionWeight < 0f)
        {
            baseSelectionWeight = 0f;
        }

        if (reservationWidth < 0.1f)
        {
            reservationWidth = 0.1f;
        }

        if (reservationLength < 0.1f)
        {
            reservationLength = 0.1f;
        }
    }
}