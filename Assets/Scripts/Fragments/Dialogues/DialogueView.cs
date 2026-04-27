using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections;

/// <summary>
/// Controlador visual del diálogo.
///
/// Responsabilidades:
/// - Mostrar texto con fade de entrada y salida.
/// - Ejecutar efecto máquina de escribir.
/// - Reproducir audio de tipeo de forma controlada para evitar saturación.
/// - Mantener desacoplada la lógica visual del controlador superior.
///
/// Decisiones de diseño:
/// - El sonido de tipeo no se reproduce en cada carácter indiscriminadamente.
/// - Se aplica un cooldown mínimo y una cadencia por cantidad de caracteres visibles.
/// - Se ignoran espacios y saltos de línea para evitar ruido innecesario.
/// </summary>
public sealed class DialogueView : MonoBehaviour
{
    #region Serialized Fields

    [Header("Referencias")]

    [SerializeField]
    [Tooltip("Texto visual del diálogo.")]
    private TMP_Text text;

    [SerializeField]
    [Tooltip("Sprite de fondo o contenedor visual del diálogo.")]
    private SpriteRenderer container;

    [Header("Typewriter")]

    [SerializeField]
    [Tooltip("Velocidad de escritura expresada en caracteres por segundo.")]
    [Min(0.01f)]
    private float typingSpeed = 30f;

    [Header("Typing Audio")]

    [SerializeField]
    [Tooltip("Indica si debe reproducirse sonido durante el efecto de tipeo.")]
    private bool playTypingSound = true;

    [SerializeField]
    [Tooltip("Identificador de sonido usado para el tipeo.")]
    private SoundId typingSoundId = SoundId.Teclado;

    [SerializeField]
    [Tooltip("Multiplicador de volumen aplicado al sonido de tipeo.")]
    [Range(0f, 1f)]
    private float typingSoundVolumeMultiplier = 0.252f;

    [SerializeField]
    [Tooltip("Tiempo mínimo entre sonidos consecutivos para evitar saturación.")]
    [Min(0f)]
    private float typingSoundCooldown = 0.1f;

    [SerializeField]
    [Tooltip("Cantidad mínima de caracteres visibles entre sonidos.")]
    [Min(1)]
    private int charactersPerSound = 1;

    #endregion

    #region Private Fields

    /// <summary>
    /// Rutina activa de tipeo.
    /// </summary>
    private Coroutine typingRoutine;

    /// <summary>
    /// Marca temporal del último sonido de tipeo reproducido.
    /// </summary>
    private float lastTypingSoundTime = float.NegativeInfinity;

    /// <summary>
    /// Tween activo del texto.
    /// </summary>
    private Tween textFadeTween;

    /// <summary>
    /// Tween activo del contenedor.
    /// </summary>
    private Tween containerFadeTween;

    #endregion

    #region Unity Methods

    /// <summary>
    /// Resuelve referencias faltantes automáticamente.
    /// </summary>
    private void Awake()
    {
        if (text == null)
        {
            text = GetComponentInChildren<TMP_Text>();
        }

        if (container == null)
        {
            container = GetComponent<SpriteRenderer>();
        }
    }

    /// <summary>
    /// Libera tweens y rutinas activas al destruirse.
    /// </summary>
    private void OnDestroy()
    {
        StopTypingRoutine();
        KillFadeTweens();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Asigna el contenido completo del texto sin animación de tipeo.
    /// </summary>
    /// <param name="content">Contenido a mostrar.</param>
    public void SetText(string content)
    {
        if (text == null)
        {
            return;
        }

        text.text = content;
        text.maxVisibleCharacters = int.MaxValue;
    }

    /// <summary>
    /// Ajusta la posición global del diálogo.
    /// </summary>
    /// <param name="position">Nueva posición.</param>
    public void SetPosition(Vector3 position)
    {
        transform.position = position;
    }

    /// <summary>
    /// Ejecuta fade de entrada y luego el efecto máquina de escribir.
    /// </summary>
    /// <param name="content">Contenido del diálogo.</param>
    /// <param name="fadeDuration">Duración del fade de entrada.</param>
    public void ShowWithTyping(string content, float fadeDuration)
    {
        if (text == null)
        {
            return;
        }

        StopTypingRoutine();
        KillFadeTweens();

        text.text = content;
        text.maxVisibleCharacters = 0;
        lastTypingSoundTime = float.NegativeInfinity;

        Fade(0f, 1f, fadeDuration, () =>
        {
            typingRoutine = StartCoroutine(TypeText(content));
        });
    }

    /// <summary>
    /// Ejecuta fade de salida y detiene cualquier tipeo en curso.
    /// </summary>
    /// <param name="duration">Duración del fade de salida.</param>
    public void FadeOut(float duration)
    {
        StopTypingRoutine();
        KillFadeTweens();
        Fade(1f, 0f, duration, null);
    }

    #endregion

    #region Internal

    /// <summary>
    /// Ejecuta el fade del texto y del contenedor.
    /// </summary>
    /// <param name="from">Alpha inicial.</param>
    /// <param name="to">Alpha final.</param>
    /// <param name="duration">Duración de la transición.</param>
    /// <param name="onComplete">Callback al finalizar el fade del texto.</param>
    private void Fade(float from, float to, float duration, TweenCallback onComplete)
    {
        if (text != null)
        {
            Color color = text.color;
            color.a = from;
            text.color = color;

            textFadeTween = text.DOFade(to, duration)
                .SetEase(Ease.InOutSine)
                .OnComplete(onComplete);
        }

        if (container != null)
        {
            Color color = container.color;
            color.a = from;
            container.color = color;

            containerFadeTween = container.DOFade(to, duration)
                .SetEase(Ease.InOutSine);
        }
    }

    /// <summary>
    /// Escribe el texto progresivamente carácter por carácter.
    /// </summary>
    /// <param name="content">Contenido total a mostrar.</param>
    /// <returns>Rutina de tipeo progresivo.</returns>
    private IEnumerator TypeText(string content)
    {
        if (text == null)
        {
            yield break;
        }

        int totalCharacters = content.Length;
        float delay = 1f / typingSpeed;
        int visibleNonWhitespaceCount = 0;

        for (int i = 0; i <= totalCharacters; i++)
        {
            text.maxVisibleCharacters = i;

            if (i > 0)
            {
                char currentCharacter = content[i - 1];

                if (!char.IsWhiteSpace(currentCharacter))
                {
                    visibleNonWhitespaceCount++;
                }

                if (ShouldPlayTypingSound(currentCharacter, visibleNonWhitespaceCount))
                {
                    PlayTypingSound();
                }
            }

            yield return new WaitForSecondsRealtime(delay);
        }

        typingRoutine = null;
    }

    /// <summary>
    /// Determina si un carácter debe disparar sonido de tipeo.
    /// </summary>
    /// <param name="character">Carácter evaluado.</param>
    /// <param name="visibleCharacterCount">Cantidad de caracteres visibles no vacíos.</param>
    /// <returns>True si corresponde reproducir sonido.</returns>
    private bool ShouldPlayTypingSound(char character, int visibleCharacterCount)
    {
        if (!playTypingSound)
        {
            return false;
        }

        if (char.IsWhiteSpace(character))
        {
            return false;
        }

        if (visibleCharacterCount <= 0)
        {
            return false;
        }

        if (visibleCharacterCount % charactersPerSound != 0)
        {
            return false;
        }

        if (Time.unscaledTime - lastTypingSoundTime < typingSoundCooldown)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reproduce el sonido de tipeo y registra el tiempo de reproducción.
    /// </summary>
    private void PlayTypingSound()
    {
        AudioManager.PlaySfx(typingSoundId, typingSoundVolumeMultiplier);
        lastTypingSoundTime = Time.unscaledTime;
    }

    /// <summary>
    /// Detiene la rutina de tipeo activa, si existe.
    /// </summary>
    private void StopTypingRoutine()
    {
        if (typingRoutine == null)
        {
            return;
        }

        StopCoroutine(typingRoutine);
        typingRoutine = null;
    }

    /// <summary>
    /// Finaliza los tweens de fade activos para evitar solapamientos.
    /// </summary>
    private void KillFadeTweens()
    {
        if (textFadeTween != null && textFadeTween.IsActive())
        {
            textFadeTween.Kill();
        }

        if (containerFadeTween != null && containerFadeTween.IsActive())
        {
            containerFadeTween.Kill();
        }

        textFadeTween = null;
        containerFadeTween = null;
    }

    #endregion
}