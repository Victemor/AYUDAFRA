using UnityEngine;

/// <summary>
/// Reproduce una secuencia de sprites en loop para simular animación tipo dibujo.
/// Ideal para efectos de líneas vibrantes / sketch.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SketchAnimator : MonoBehaviour
{
    [Header("Frames")]
    [SerializeField] private Sprite[] frames;

    [Header("Timing")]
    [SerializeField] private float frameRate = 7f; // frames por segundo

    private SpriteRenderer spriteRenderer;
    private int currentFrame;
    private float timer;
    private float currentFrameRate;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

      
            currentFrame = Random.Range(0, frames.Length);
       

        // Variación de velocidad
        currentFrameRate =  frameRate;
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;

        timer += Time.deltaTime;

        float interval = 1f / currentFrameRate;

        if (timer >= interval)
        {
            timer -= interval;

            currentFrame = (currentFrame + 1) % frames.Length;
            spriteRenderer.sprite = frames[currentFrame];
        }
    }
}