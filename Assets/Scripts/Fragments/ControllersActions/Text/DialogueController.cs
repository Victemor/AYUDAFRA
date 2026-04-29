using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Game.Core;

/// <summary>
/// Manager de textos con pooling.
/// Controla spawn, posición y ciclo de vida.
/// </summary>
public sealed class DialogueController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Prefab")]

    [SerializeField]
    [Tooltip("Prefab con TextElementController en el root.")]
    private DialogueView textPrefab;

    [Header("Pool")]

    [SerializeField]
    private int initialPoolSize = 5;

    [Header("Animación")]

    [SerializeField]
    private float fadeInDuration = 0.5f;

    [SerializeField]
    private float fadeOutDuration = 0.5f;

    [Header("Posición")]

    [SerializeField]
    [Tooltip("Offset respecto al punto base.")]
    private Vector3 spawnOffset = new Vector3(0, 2f, 0);

    #endregion

    #region Private Fields

    private readonly List<DialogueView> pool = new();
    private int poolIndex = 0;

    private DialogueView currentInstance;
    private Coroutine disableRoutine;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        InitializePool();
    }

    #endregion

    #region Pool

    private void InitializePool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            var instance = Instantiate(textPrefab, transform);
            instance.gameObject.SetActive(false);
            pool.Add(instance);
        }
    }

    private DialogueView GetFromPool()
    {
        int count = pool.Count;

        for (int i = 0; i < count; i++)
        {
            int index = (poolIndex + i) % count;

            if (!pool[index].gameObject.activeInHierarchy)
            {
                poolIndex = (index + 1) % count;
                return pool[index];
            }
        }

        var newInstance = Instantiate(textPrefab, transform);
        newInstance.gameObject.SetActive(false);
        pool.Add(newInstance);

        return newInstance;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Muestra texto en una posición base (manager decide offset).
    /// </summary>
    public void ShowText(string content, Vector3 basePosition)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            Debug.LogWarning("[DialogueController] ShowText recibió texto vacío.", this);
            return;
        }

        if (GamePlayStateController.Instance == null)
        {
            Debug.LogWarning("[DialogueController] GamePlayStateController.Instance es null. El diálogo no se mostrará.", this);
            return;
        }

        GamePlayStateController.Instance.EnterDialogue();
        HideCurrentImmediate();

        var instance = GetFromPool();
        currentInstance = instance;

        Vector3 finalPosition = basePosition + spawnOffset;

        instance.gameObject.SetActive(true);
        instance.SetPosition(finalPosition);
        instance.ShowWithTyping(content, fadeInDuration);
    }

    /// <summary>
    /// Permite cambiar el texto del elemento activo.
    /// </summary>
    public void UpdateCurrentText(string content)
    {
        if (currentInstance == null) return;

        currentInstance.SetText(content);
    }

    /// <summary>
    /// Oculta el texto actual con fade out.
    /// </summary>
    public void HideCurrent()
    {
        if (currentInstance == null) return;

        if (disableRoutine != null)
            StopCoroutine(disableRoutine);

        currentInstance.FadeOut(fadeOutDuration);
        disableRoutine = StartCoroutine(DisableAfterFade(currentInstance, fadeOutDuration));

        currentInstance = null;
    }

    /// <summary>
    /// Oculta inmediatamente sin animación.
    /// </summary>
    public void HideCurrentImmediate()
    {
        if (currentInstance == null) return;

        if (disableRoutine != null)
        {
            StopCoroutine(disableRoutine);
            disableRoutine = null;
        }

        currentInstance.gameObject.SetActive(false);
        currentInstance = null;
    }

    #endregion

    #region Internal

    private IEnumerator DisableAfterFade(DialogueView instance, float duration)
    {
        yield return new WaitForSeconds(duration);

        if (instance != null)
            instance.gameObject.SetActive(false);
    }

    #endregion
}