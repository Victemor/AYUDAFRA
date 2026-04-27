using UnityEngine;

/// <summary>
/// Define un perfil de clima con todos los parámetros visuales y ambientales.
/// Se utiliza como contenedor de configuración reutilizable.
/// </summary>
[CreateAssetMenu(menuName = "Game/Weather/Weather Profile")]
public class WeatherProfile : ScriptableObject
{
    [Header("Bloom")]

    [SerializeField]
    [Tooltip("Intensidad del bloom.")]
    private float bloomIntensity = 0f;

    [SerializeField]
    [Tooltip("Color del bloom.")]
    private Color bloomColor = Color.white;

    [Header("Rain")]

    [SerializeField]
    [Tooltip("Intensidad de la lluvia.")]
    private float rainIntensity = 0f;

    [Header("Wind")]

    [SerializeField]
    [Tooltip("Intensidad del viento.")]
    private float windIntensity = 0f;

    [Header("Fire")]

    [SerializeField]
    [Tooltip("Intensidad de fuego ambiental.")]
    private float fireIntensity = 0f;

    [Header("Camera Sway")]

    [SerializeField]
    [Tooltip("Sway en eje X.")]
    private float swayX = 0f;

    [SerializeField]
    [Tooltip("Sway en eje Z.")]
    private float swayZ = 0f;

    [SerializeField]
    [Tooltip("Velocidad del sway.")]
    private float swaySpeed = 0f;

    #region Getters

    public float BloomIntensity => bloomIntensity;
    public Color BloomColor => bloomColor;
    public float RainIntensity => rainIntensity;
    public float WindIntensity => windIntensity;
    public float FireIntensity => fireIntensity;

    public float SwayX => swayX;
    public float SwayZ => swayZ;
    public float SwaySpeed => swaySpeed;

    #endregion
}