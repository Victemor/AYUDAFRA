using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gestiona múltiples sistemas de fuego en escena.
/// Aplica intensidad global de forma sincronizada.
/// </summary>
public sealed class FireGroupController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Fuegos en escena")]

    [SerializeField]
    [Tooltip("Lista de sistemas de fuego a controlar.")]
    private List<FireController> fireSystems = new();

    [Header("Configuración")]

    [SerializeField]
    [Tooltip("Duración de transición por defecto.")]
    private float defaultTransition = 0.5f;

    [SerializeField]
    [Tooltip("Multiplicador global de intensidad.")]
    private float globalMultiplier = 1f;
    [SerializeField]
    [Tooltip("Valor mínimo del sistema de fuego.")]
    private float fireMin = 0.3f;

    [SerializeField]
    [Tooltip("Valor máximo del sistema de fuego.")]
    private float fireMax = 0.6f;
    #endregion

    #region Public API

    /// <summary>
    /// Aplica intensidad a todos los sistemas de fuego.
    /// </summary>
    public void SetFireIntensity(float intensity, float duration = -1f)
    {
        float time = duration > 0f ? duration : defaultTransition;

        float normalized = Mathf.Clamp01(intensity * globalMultiplier);

        // Remapeo 0–1 → fireMin–fireMax
        float finalIntensity = Mathf.Lerp(fireMin, fireMax, normalized);

        foreach (var fire in fireSystems)
        {
            if (fire == null) continue;

            if (finalIntensity <= 0.001f)
                fire.StopFire(time);
            else
                fire.StartFire(finalIntensity, time);
        }
    }

    /// <summary>
    /// Apaga todos los fuegos.
    /// </summary>
    public void StopAll(float duration = -1f)
    {
        float time = duration > 0f ? duration : defaultTransition;

        foreach (var fire in fireSystems)
        {
            if (fire == null) continue;
            fire.StopFire(time);
        }
    }

    /// <summary>
    /// Permite agregar fuego dinámicamente.
    /// </summary>
    public void Register(FireController fire)
    {
        if (fire == null || fireSystems.Contains(fire)) return;
        fireSystems.Add(fire);
    }

    /// <summary>
    /// Permite remover fuego dinámicamente.
    /// </summary>
    public void Unregister(FireController fire)
    {
        if (fire == null) return;
        fireSystems.Remove(fire);
    }

    #endregion
}