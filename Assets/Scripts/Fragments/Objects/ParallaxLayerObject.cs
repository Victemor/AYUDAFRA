using UnityEngine;
using DG.Tweening;

public class ParallaxLayerObject : MonoBehaviour
{
    [Header("Parallax")]
    public float parallaxFactor = 0.5f;

    [Header("Alpha Zoom Control")]
    public float fadeThreshold = 6f;     // Umbral para bajar opacidad
    public float hideThreshold = 4f;     // Umbral para desaparecer completamente
    public float fadeAlpha = 0.3f;       // Alpha intermedio
    public float fadeDuration = 0.5f;

    private Vector3 lastCameraPosition;
    private Camera mainCamera;
    private SpriteRenderer spriteRenderer;

    private enum FadeState { Full, Faded, Hidden }
    private FadeState currentState = FadeState.Full;

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            lastCameraPosition = mainCamera.transform.position;
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogWarning($"[{name}] No se encontr� SpriteRenderer.");
        }
    }

    void LateUpdate()
    {
        if (mainCamera == null || spriteRenderer == null) return;

        // Movimiento parallax
        Vector3 delta = mainCamera.transform.position - lastCameraPosition;
        transform.position += new Vector3(delta.x * parallaxFactor, delta.y * parallaxFactor, 0f);
        lastCameraPosition = mainCamera.transform.position;

        // Control de alpha por zoom
        float currentZoom = mainCamera.orthographicSize;

        if (currentZoom <= hideThreshold && currentState != FadeState.Hidden)
        {
            currentState = FadeState.Hidden;
            spriteRenderer.DOFade(0f, fadeDuration);
        }
        else if (currentZoom > hideThreshold && currentZoom <= fadeThreshold && currentState != FadeState.Faded)
        {
            currentState = FadeState.Faded;
            spriteRenderer.DOFade(fadeAlpha, fadeDuration);
        }
        else if (currentZoom > fadeThreshold && currentState != FadeState.Full)
        {
            currentState = FadeState.Full;
            spriteRenderer.DOFade(1f, fadeDuration);
        }
    }
}
