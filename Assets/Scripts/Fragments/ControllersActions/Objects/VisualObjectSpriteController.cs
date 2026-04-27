using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;
using Game;
using Game.CursorSystem;
using Game.Data;
using Game.Runtime;

/// <summary>
/// Controla la visibilidad visual e interactiva de un objeto compuesto por SpriteRenderers.
/// 
/// Además de alpha y dissolve, sincroniza automáticamente colliders, componentes interactuables
/// y targets de cursor en el objeto raíz y todos sus hijos.
/// </summary>
public class VisualObjectSpriteController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Opciones")]

    [SerializeField]
    [Tooltip("Si está activo, los sprites empezarán invisibles cuando no exista estado guardado.")]
    private bool startInvisible = true;

    [SerializeField]
    [Tooltip("Si está activo, al aplicar el material dissolve se reinician sus valores internos.")]
    private bool startDissolve = false;

    [Header("Sprites a controlar")]

    [SerializeField]
    [Tooltip("Lista de SpriteRenderers a controlar visualmente.")]
    private List<SpriteRenderer> spriteRenderers = new();

    [Header("Shader Dissolve Settings")]

    [SerializeField]
    [Tooltip("Partícula que se activa cuando se disuelve o aparece.")]
    private ParticleSystem dissolveEffectPrefab;

    [SerializeField]
    [Tooltip("Cantidad de partículas emitidas por el efecto dissolve.")]
    private int dissolveEffectParticles = 200;

    [SerializeField]
    [Tooltip("Material usado para efectos de dissolve.")]
    private Material dissolveMat;

    [SerializeField]
    [Tooltip("Material base que será clonado para evitar modificar assets compartidos.")]
    private Material baseMaterial;

    [Header("Persistence")]

    [SerializeField]
    [Tooltip("Si está activo, guarda y restaura el estado visible usando el sistema narrativo.")]
    private bool enablePersistence = false;

    [SerializeField]
    [Tooltip("Memoria narrativa a la que pertenece este objeto visual.")]
    private MemoryDefinition persistenceMemory;

    [SerializeField]
    [Tooltip("Objeto narrativo al que pertenece este objeto visual.")]
    private ObjectDefinition persistenceObject;

    [SerializeField]
    [Tooltip("Identificador único del world state donde se guarda la visibilidad.")]
    private WorldObjectId persistenceWorldObjectId;

    #endregion

    #region Private Fields

    private readonly List<Material> originalMaterials = new();
    private readonly List<Material> dissolveInstanceMaterials = new();

    private readonly List<BehaviourState> controlledBehaviours = new();
    private readonly List<ColliderState> controlledColliders = new();
    private readonly List<Collider2DState> controlledColliders2D = new();

    #endregion

    #region Unity Messages

    private void Awake()
    {
        CacheInteractionComponents();
        ApplyAndCloneSharedMaterial();
    }

    private void Start()
    {
        bool stateRestored = TryRestorePersistedState();

        if (!stateRestored && startInvisible)
        {
            ApplyAlphaDirectly(0f);
            SetInteractionState(false);
        }
    }

    #endregion

    #region Public API — Visual

    /// <summary>
    /// Cambia la posición global del objeto visual.
    /// </summary>
    public void SetPosition(Vector3 newPosition) => transform.position = newPosition;

    /// <summary>
    /// Anima el alpha de todos los sprites a visible y reactiva interacción.
    /// </summary>
    public void FadeIn(float duration = 1f, Ease ease = Ease.InOutSine)
    {
        SetInteractionState(true);
        RestoreMaterials();

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            spriteRenderer?.DOFade(1f, duration).SetEase(ease);
        }

        WriteVisibleStateToRuntime(true);
    }

    /// <summary>
    /// Anima el alpha de todos los sprites a invisible y desactiva interacción.
    /// </summary>
    public void FadeOut(float duration = 1f, Ease ease = Ease.InOutSine)
    {
        SetInteractionState(false);

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            spriteRenderer?.DOFade(0f, duration).SetEase(ease);
        }

        WriteVisibleStateToRuntime(false);
    }

    /// <summary>
    /// Anima el alpha de todos los sprites al valor indicado.
    /// </summary>
    public void FadeTo(float alpha, float duration = 1f, Ease ease = Ease.InOutSine)
    {
        RestoreMaterials();

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            spriteRenderer?.DOFade(alpha, duration).SetEase(ease);
        }

        SetInteractionState(alpha > 0f);
    }

    /// <summary>
    /// Aplica visibilidad inmediata y sincroniza interacción.
    /// </summary>
    public void SetVisibleImmediate(bool visible)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        RestoreMaterials();
        ApplyAlphaDirectly(visible ? 1f : 0f);
        SetInteractionState(visible);
        WriteVisibleStateToRuntime(visible);
    }

    /// <summary>
    /// Restaura visibilidad inmediata sin escribir al runtime narrativo.
    /// </summary>
    public void RestoreVisibleStateImmediate(bool visible)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        RestoreMaterials();
        ApplyAlphaDirectly(visible ? 1f : 0f);
        SetInteractionState(visible);
    }

    #endregion

    #region Public API — Dissolve Effects

    /// <summary>
    /// Ejecuta dissolve horizontal y desactiva interacción.
    /// </summary>
    public void Dissolve(float duration = 1f)
    {
        SetInteractionState(false);
        ApplyDissolveMaterial();
        AnimateDissolveOnly(1f, duration);
        WriteVisibleStateToRuntime(false);
    }

    /// <summary>
    /// Ejecuta dissolve vertical y desactiva interacción.
    /// </summary>
    public void DissolveVertical(float duration = 1f)
    {
        SetInteractionState(false);
        ApplyDissolveMaterial();
        AnimateVerticalOnly(1f, duration);
        WriteVisibleStateToRuntime(false);
    }

    /// <summary>
    /// Ejecuta ambos dissolves y desactiva interacción.
    /// </summary>
    public void DissolveBoth(float duration = 1f)
    {
        SetInteractionState(false);
        ApplyDissolveMaterial();
        AnimateBoth(1f, 1f, duration);
        WriteVisibleStateToRuntime(false);
    }

    /// <summary>
    /// Aparece usando dissolve horizontal y reactiva interacción.
    /// </summary>
    public void AppearDisolve(float duration = 1f)
    {
        SetInteractionState(true);
        ApplyDissolveMaterial();
        AnimateDissolveOnly(0f, duration);
        WriteVisibleStateToRuntime(true);
    }

    /// <summary>
    /// Aparece usando dissolve vertical y reactiva interacción.
    /// </summary>
    public void AppearDisolveVertical(float duration = 1f)
    {
        SetInteractionState(true);
        ApplyDissolveMaterial();
        AnimateVerticalOnly(0f, duration);
        WriteVisibleStateToRuntime(true);
    }

    /// <summary>
    /// Aparece usando ambos dissolves y reactiva interacción.
    /// </summary>
    public void AppearDisolveBoth(float duration = 1f)
    {
        SetInteractionState(true);
        ApplyDissolveMaterial();
        AnimateBoth(0f, 0f, duration);
        WriteVisibleStateToRuntime(true);
    }

    /// <summary>
    /// Anima el color del material actual de los sprites.
    /// </summary>
    public void FadeMaterialColor(Color targetColor, float duration, bool withParticles = false)
    {
        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null || !spriteRenderer.material.HasProperty("_Color"))
            {
                continue;
            }

            spriteRenderer.material.DOColor(targetColor, "_Color", duration)
                .SetEase(Ease.InOutSine)
                .OnComplete(() =>
                {
                    if (withParticles)
                    {
                        StartDissolveParticles(duration);
                    }
                });
        }
    }

    #endregion

    #region Public API — Materials

    /// <summary>
    /// Aplica un material clonado desde baseMaterial y reactiva interacción.
    /// </summary>
    public void ApplyAndCloneSharedMaterial()
    {
        originalMaterials.Clear();
        dissolveInstanceMaterials.Clear();

        if (baseMaterial == null)
        {
            SetInteractionState(true);
            return;
        }

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null)
            {
                continue;
            }

            Material clonedMaterial = new Material(baseMaterial);
            spriteRenderer.material = clonedMaterial;

            originalMaterials.Add(clonedMaterial);
            dissolveInstanceMaterials.Add(null);
        }

        SetInteractionState(true);
    }

    #endregion

    #region Private — Interaction State

    /// <summary>
    /// Cachea automáticamente colliders, scripts interactuables y targets de cursor en raíz e hijos.
    /// Guarda su estado original para restaurar solo lo que estaba activo.
    /// </summary>
    private void CacheInteractionComponents()
    {
        controlledBehaviours.Clear();
        controlledColliders.Clear();
        controlledColliders2D.Clear();

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider targetCollider in colliders)
        {
            controlledColliders.Add(new ColliderState(targetCollider, targetCollider.enabled));
        }

        Collider2D[] colliders2D = GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D targetCollider in colliders2D)
        {
            controlledColliders2D.Add(new Collider2DState(targetCollider, targetCollider.enabled));
        }

        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || behaviour == this)
            {
                continue;
            }

            if (!ShouldControlBehaviour(behaviour))
            {
                continue;
            }

            controlledBehaviours.Add(new BehaviourState(behaviour, behaviour.enabled));
        }
    }

    /// <summary>
    /// Define qué scripts deben apagarse cuando el objeto visual está invisible.
    /// </summary>
    private bool ShouldControlBehaviour(MonoBehaviour behaviour)
    {
        return behaviour is IInteractable ||
               behaviour is MemoryObjectInteractor ||
               behaviour is WorldCursorTarget;
    }

    /// <summary>
    /// Sincroniza el estado interactivo del objeto sin afectar el GameObject completo.
    /// </summary>
    private void SetInteractionState(bool enabled)
    {
        for (int i = 0; i < controlledColliders.Count; i++)
        {
            ColliderState state = controlledColliders[i];

            if (state.Collider == null)
            {
                continue;
            }

            state.Collider.enabled = enabled && state.WasEnabled;
        }

        for (int i = 0; i < controlledColliders2D.Count; i++)
        {
            Collider2DState state = controlledColliders2D[i];

            if (state.Collider == null)
            {
                continue;
            }

            state.Collider.enabled = enabled && state.WasEnabled;
        }

        for (int i = 0; i < controlledBehaviours.Count; i++)
        {
            BehaviourState state = controlledBehaviours[i];

            if (state.Behaviour == null)
            {
                continue;
            }

            state.Behaviour.enabled = enabled && state.WasEnabled;
        }
    }

    #endregion

    #region Private — Persistence

    /// <summary>
    /// Intenta restaurar el estado visual desde el runtime narrativo.
    /// </summary>
    private bool TryRestorePersistedState()
    {
        ObjectRuntimeData objectRuntime = GetObjectRuntime();

        if (objectRuntime == null)
        {
            return false;
        }

        ObjectRuntimeData.WorldObjectState state = objectRuntime.GetWorldState(persistenceWorldObjectId.Id);

        if (state == null || !state.visible.HasValue)
        {
            return false;
        }

        bool visible = state.visible.Value;

        ApplyAlphaDirectly(visible ? 1f : 0f);
        SetInteractionState(visible);

        return true;
    }

    /// <summary>
    /// Escribe el estado visible al runtime narrativo y notifica el cambio.
    /// </summary>
    private void WriteVisibleStateToRuntime(bool visible)
    {
        ObjectRuntimeData objectRuntime = GetObjectRuntime();

        if (objectRuntime == null)
        {
            return;
        }

        objectRuntime.SetWorldState(persistenceWorldObjectId.Id, state =>
        {
            state.visible = visible;
        });

        GameEvents.RaiseObjectStateChanged(objectRuntime);
    }

    /// <summary>
    /// Obtiene el ObjectRuntimeData si la persistencia está correctamente configurada.
    /// </summary>
    private ObjectRuntimeData GetObjectRuntime()
    {
        if (!enablePersistence)
        {
            return null;
        }

        if (persistenceMemory == null || persistenceObject == null || persistenceWorldObjectId == null)
        {
            return null;
        }

        if (GameStateRepository.Instance == null)
        {
            return null;
        }

        MemoryRuntimeData memoryRuntime = GameStateRepository.Instance.GetMemory(persistenceMemory);

        if (memoryRuntime == null)
        {
            return null;
        }

        return memoryRuntime.GetObject(persistenceObject);
    }

    #endregion

    #region Private — Alpha

    /// <summary>
    /// Aplica alpha directamente a todos los SpriteRenderers sin DOTween.
    /// </summary>
    private void ApplyAlphaDirectly(float alpha)
    {
        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null)
            {
                continue;
            }

            Color color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }
    }

    #endregion

    #region Private — Dissolve

    /// <summary>
    /// Restaura los materiales originales clonados.
    /// </summary>
    private void RestoreMaterials() => RestoreOriginalMaterials();

    /// <summary>
    /// Reasigna los materiales originales a cada SpriteRenderer.
    /// </summary>
    private void RestoreOriginalMaterials()
    {
        for (int i = 0; i < spriteRenderers.Count; i++)
        {
            if (spriteRenderers[i] == null)
            {
                continue;
            }

            if (i >= originalMaterials.Count || originalMaterials[i] == null)
            {
                continue;
            }

            spriteRenderers[i].material = originalMaterials[i];
        }
    }

    /// <summary>
    /// Aplica instancias independientes del material dissolve.
    /// </summary>
    private void ApplyDissolveMaterial()
    {
        if (dissolveMat == null)
        {
            return;
        }

        EnsureDissolveMaterialListSize();

        for (int i = 0; i < spriteRenderers.Count; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];

            if (spriteRenderer == null)
            {
                continue;
            }

            if (dissolveInstanceMaterials[i] == null)
            {
                dissolveInstanceMaterials[i] = new Material(dissolveMat);
            }

            if (startDissolve)
            {
                SetMaterialFloatIfExists(dissolveInstanceMaterials[i], "_DissolveAmount", 0f);
                SetMaterialFloatIfExists(dissolveInstanceMaterials[i], "_VerticalDissolve", 0f);
            }

            spriteRenderer.material = dissolveInstanceMaterials[i];
        }
    }

    /// <summary>
    /// Garantiza que la lista de materiales dissolve tenga el mismo tamaño que los sprites.
    /// </summary>
    private void EnsureDissolveMaterialListSize()
    {
        while (dissolveInstanceMaterials.Count < spriteRenderers.Count)
        {
            dissolveInstanceMaterials.Add(null);
        }
    }

    /// <summary>
    /// Anima únicamente la propiedad de dissolve horizontal.
    /// </summary>
    private void AnimateDissolveOnly(float target, float duration)
    {
        foreach (Material material in dissolveInstanceMaterials)
        {
            if (material == null || !material.HasProperty("_DissolveAmount"))
            {
                continue;
            }

            material.DOFloat(target, "_DissolveAmount", duration).SetEase(Ease.InOutSine);
        }
    }

    /// <summary>
    /// Anima únicamente la propiedad de dissolve vertical.
    /// </summary>
    private void AnimateVerticalOnly(float target, float duration)
    {
        foreach (Material material in dissolveInstanceMaterials)
        {
            if (material == null || !material.HasProperty("_VerticalDissolve"))
            {
                continue;
            }

            material.DOFloat(target, "_VerticalDissolve", duration).SetEase(Ease.InOutSine);
        }
    }

    /// <summary>
    /// Anima simultáneamente dissolve horizontal y vertical.
    /// </summary>
    private void AnimateBoth(float dissolveTarget, float verticalTarget, float duration)
    {
        foreach (Material material in dissolveInstanceMaterials)
        {
            if (material == null)
            {
                continue;
            }

            if (material.HasProperty("_DissolveAmount"))
            {
                material.DOFloat(dissolveTarget, "_DissolveAmount", duration).SetEase(Ease.InOutSine);
            }

            if (material.HasProperty("_VerticalDissolve"))
            {
                material.DOFloat(verticalTarget, "_VerticalDissolve", duration).SetEase(Ease.InOutSine);
            }
        }
    }

    /// <summary>
    /// Asigna un float al material solo si la propiedad existe.
    /// </summary>
    private void SetMaterialFloatIfExists(Material material, string propertyName, float value)
    {
        if (material == null || !material.HasProperty(propertyName))
        {
            return;
        }

        material.SetFloat(propertyName, value);
    }

    /// <summary>
    /// Instancia y reproduce partículas de dissolve.
    /// </summary>
    private void StartDissolveParticles(float duration)
    {
        if (dissolveEffectPrefab == null)
        {
            return;
        }

        ParticleSystem particleSystemInstance = Instantiate(dissolveEffectPrefab, transform.position, Quaternion.identity);

        ParticleSystem.EmissionModule emission = particleSystemInstance.emission;
        emission.SetBurst(0, new ParticleSystem.Burst(0f, dissolveEffectParticles));

        particleSystemInstance.Play();
        Destroy(particleSystemInstance.gameObject, duration + 2f);
    }

    #endregion

    #region Private Structs

    /// <summary>
    /// Estado inicial de un Behaviour controlado por visibilidad.
    /// </summary>
    private readonly struct BehaviourState
    {
        public BehaviourState(MonoBehaviour behaviour, bool wasEnabled)
        {
            Behaviour = behaviour;
            WasEnabled = wasEnabled;
        }

        public MonoBehaviour Behaviour { get; }

        public bool WasEnabled { get; }
    }

    /// <summary>
    /// Estado inicial de un Collider 3D controlado por visibilidad.
    /// </summary>
    private readonly struct ColliderState
    {
        public ColliderState(Collider collider, bool wasEnabled)
        {
            Collider = collider;
            WasEnabled = wasEnabled;
        }

        public Collider Collider { get; }

        public bool WasEnabled { get; }
    }

    /// <summary>
    /// Estado inicial de un Collider2D controlado por visibilidad.
    /// </summary>
    private readonly struct Collider2DState
    {
        public Collider2DState(Collider2D collider, bool wasEnabled)
        {
            Collider = collider;
            WasEnabled = wasEnabled;
        }

        public Collider2D Collider { get; }

        public bool WasEnabled { get; }
    }

    #endregion
}