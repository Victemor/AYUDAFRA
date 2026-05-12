using UnityEngine;

/// <summary>
/// Convierte una jerarquía generada en una versión puramente visual.
/// 
/// Responsabilidades:
/// - Desactivar Colliders.
/// - Desactivar Rigidbody sin destruirlos.
/// - Desactivar MonoBehaviours funcionales.
/// - Mantener Renderers, MeshFilters, Animators y ParticleSystems.
/// 
/// Uso:
/// Se aplica sobre las pistas decorativas anterior/siguiente para que no tengan gameplay,
/// física, triggers, scripts de monedas, scripts de obstáculos, scripts de meta ni scripts de checkpoints.
/// </summary>
public static class DecorativeGeneratedObjectSanitizer
{
    #region Public API

    /// <summary>
    /// Sanitiza una jerarquía completa para dejarla como contenido decorativo.
    /// </summary>
    /// <param name="root">Raíz que contiene la pista decorativa.</param>
    /// <param name="disableRenderers">Si está activo, también apaga renderers. Normalmente debe estar en false.</param>
    public static void SanitizeHierarchy(Transform root, bool disableRenderers = false)
    {
        if (root == null)
        {
            return;
        }

        DisableColliders(root);
        DisableRigidbodies(root);
        DisableFunctionalBehaviours(root);
        DisableOptionalRenderers(root, disableRenderers);
    }

    #endregion

    #region Colliders

    /// <summary>
    /// Desactiva todos los colliders de la jerarquía.
    /// </summary>
    private static void DisableColliders(Transform root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
            {
                continue;
            }

            colliders[i].enabled = false;
            colliders[i].isTrigger = false;
        }
    }

    #endregion

    #region Rigidbodies

    /// <summary>
    /// Desactiva la simulación física de todos los Rigidbody.
    /// No destruye el componente para evitar romper prefabs o referencias internas.
    /// </summary>
    private static void DisableRigidbodies(Transform root)
    {
        Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rigidbody = rigidbodies[i];

            if (rigidbody == null)
            {
                continue;
            }

            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;
            rigidbody.detectCollisions = false;
            rigidbody.interpolation = RigidbodyInterpolation.None;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }
    }

    #endregion

    #region MonoBehaviours

    /// <summary>
    /// Desactiva scripts funcionales de gameplay.
    /// Mantiene vivos los componentes visuales nativos como Renderer, MeshFilter, Animator y ParticleSystem.
    /// </summary>
    private static void DisableFunctionalBehaviours(Transform root)
    {
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour == null)
            {
                continue;
            }

            // Este sanitizer es estático, pero se deja esta protección por si luego
            // agregas una versión MonoBehaviour.
            if (behaviour.GetType().Name == nameof(DecorativeGeneratedObjectSanitizer))
            {
                continue;
            }

            behaviour.enabled = false;
        }
    }

    #endregion

    #region Renderers

    /// <summary>
    /// Permite apagar renderers si se requiere debug o pooling agresivo.
    /// En el flujo normal decorativo no se apagan.
    /// </summary>
    private static void DisableOptionalRenderers(Transform root, bool disableRenderers)
    {
        if (!disableRenderers)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = false;
            }
        }
    }

    #endregion
}