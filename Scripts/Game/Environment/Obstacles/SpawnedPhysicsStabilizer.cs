using UnityEngine;

/// <summary>
/// Utilidad central para estabilizar objetos físicos generados proceduralmente.
/// Evita que objetos reutilizados desde pool conserven velocidad,
/// nazcan intersectando la pista o empiecen rebotando de forma artificial.
/// </summary>
public static class SpawnedPhysicsStabilizer
{
    private const float ColliderSurfaceSkin = 0.01f;

    /// <summary>
    /// Limpia velocidades físicas de todos los Rigidbodies del objeto.
    /// </summary>
    public static void ResetPhysics(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        Rigidbody[] rigidbodies = instance.GetComponentsInChildren<Rigidbody>(true);

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rigidbody = rigidbodies[i];

            if (rigidbody == null)
            {
                continue;
            }

            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;

            if (!rigidbody.isKinematic)
            {
                rigidbody.Sleep();
            }
        }
    }

    /// <summary>
    /// Alinea el punto más bajo del collider principal con la superficie indicada.
    /// Útil para cubos, pelotas y muros físicos cuyo pivot no está en la base.
    /// </summary>
    public static void AlignColliderBottomToSurface(
        GameObject instance,
        float surfaceY,
        float additionalVerticalOffset = 0f)
    {
        if (instance == null)
        {
            return;
        }

        if (!TryGetCombinedColliderBounds(instance, out Bounds bounds))
        {
            return;
        }

        float targetBottomY = surfaceY + additionalVerticalOffset + ColliderSurfaceSkin;
        float deltaY = targetBottomY - bounds.min.y;

        if (Mathf.Abs(deltaY) <= 0.0001f)
        {
            return;
        }

        Transform instanceTransform = instance.transform;
        instanceTransform.position += Vector3.up * deltaY;

        Physics.SyncTransforms();
        ResetPhysics(instance);
    }

    /// <summary>
    /// Despierta los Rigidbodies después de estabilizarlos.
    /// Si quedan dormidos, Unity normalmente los despierta al contacto,
    /// pero esto permite controlar explícitamente ese comportamiento.
    /// </summary>
    public static void WakePhysics(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        Rigidbody[] rigidbodies = instance.GetComponentsInChildren<Rigidbody>(true);

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rigidbody = rigidbodies[i];

            if (rigidbody == null || rigidbody.isKinematic)
            {
                continue;
            }

            rigidbody.WakeUp();
        }
    }

    private static bool TryGetCombinedColliderBounds(GameObject instance, out Bounds combinedBounds)
    {
        combinedBounds = default;

        Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
        bool hasBounds = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];

            if (collider == null || collider.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = collider.bounds;
                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(collider.bounds);
        }

        if (hasBounds)
        {
            return true;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];

            if (collider == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = collider.bounds;
                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(collider.bounds);
        }

        return hasBounds;
    }
}