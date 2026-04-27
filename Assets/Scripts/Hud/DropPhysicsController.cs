using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla TODA la física suave de las gotas:
/// - Repulsión en cadena
/// - Impulsos al soltar
/// - Impulsos al spawn
/// - Relajación y amortiguación
/// </summary>
public class DropPhysicsController : MonoBehaviour
{
    [Header("Chain Repulsion Settings")]
    [SerializeField] private int repulsionIterations = 3;
    [SerializeField] private float repulsionForce = 0.05f;
    [SerializeField] private float repulsionDistance = 1.2f;
    [SerializeField] private float minRepulsionDistance = 0.25f;
    [SerializeField] private float maxRepulsionStep = 0.03f;

    [Header("Relaxation Settings")]
    [SerializeField] private float relaxationSpeed = 6f;
    [SerializeField] private float relaxationThreshold = 0.001f;

    [Header("Release Impulse")]
    [SerializeField] private float releaseImpulseForce = 0.015f;
    [SerializeField] private float releaseImpulseRadius = 1.1f;

    [Header("Spawn Impulse")]
    [SerializeField] private float spawnImpulseRadius = 1.5f;
    [SerializeField] private float spawnImpulseForce = 0.015f;

    private FragmentsGraphController graph;
    private Coroutine relaxationCoroutine;

    private void Awake()
    {
        graph = GetComponent<FragmentsGraphController>();
    }

    #region Repulsion

    public void ResolveChainRepulsion()
    {
        var drops = graph.GetAllDrops();

        for (int iteration = 0; iteration < repulsionIterations; iteration++)
        {
            for (int i = 0; i < drops.Count; i++)
            {
                for (int j = i + 1; j < drops.Count; j++)
                {
                    DropController a = drops[i];
                    DropController b = drops[j];

                    if (a.isDragging && b.isDragging)
                        continue;

                    Vector3 posA = a.transform.position;
                    Vector3 posB = b.transform.position;

                    float distance = Vector3.Distance(posA, posB);

                    if (distance >= repulsionDistance)
                        continue;

                    float safeDistance = Mathf.Max(distance, minRepulsionDistance);

                    Vector3 direction = (posA - posB).normalized;

                    float t = 1f - (safeDistance / repulsionDistance);
                    t = Mathf.Clamp01(t);

                    float force = repulsionForce * Mathf.SmoothStep(0f, 1f, t);
                    Vector3 offset = direction * force;

                    offset = Vector3.ClampMagnitude(offset, maxRepulsionStep);

                    if (!a.isDragging)
                        a.ApplyRepulsion(offset);

                    if (!b.isDragging)
                        b.ApplyRepulsion(-offset);
                }
            }
        }
    }

    #endregion

    #region Relaxation

    public void StartRelaxation()
    {
        if (relaxationCoroutine != null)
            StopCoroutine(relaxationCoroutine);

        relaxationCoroutine = StartCoroutine(RelaxationRoutine());
    }

    private IEnumerator RelaxationRoutine()
    {
        var drops = graph.GetAllDrops();

        while (true)
        {
            bool anyMoving = false;

            foreach (var drop in drops)
            {
                if (drop.isDragging)
                    continue;

                Vector3 velocity = drop.GetResidualVelocity();

                velocity = Vector3.Lerp(
                    velocity,
                    Vector3.zero,
                    Time.deltaTime * relaxationSpeed
                );

                if (velocity.magnitude > relaxationThreshold)
                {
                    drop.ApplyVelocity(velocity);
                    anyMoving = true;
                }
                else
                {
                    drop.ClearResidualVelocity();
                }
            }

            if (!anyMoving)
            {
                graph.CommitDropPositions();
                yield break;
            }

            yield return null;
        }
    }

    #endregion

    #region Impulses

    public void ApplySpawnImpulse(DropController newDrop)
    {
        var drops = graph.GetAllDrops();
        Vector3 sourcePos = newDrop.transform.position;

        foreach (var drop in drops)
        {
            if (drop == newDrop)
                continue;

            float distance = Vector3.Distance(
                sourcePos,
                drop.transform.position
            );

            if (distance > spawnImpulseRadius || distance < 0.001f)
                continue;

            Vector3 direction =
                (drop.transform.position - sourcePos).normalized;

            float t = 1f - (distance / spawnImpulseRadius);
            float force = spawnImpulseForce * Mathf.SmoothStep(0f, 1f, t);

            drop.ApplyRepulsion(direction * force);
        }
    }

    public void ApplyReleaseImpulse(DropController releasedDrop)
    {
        var drops = graph.GetAllDrops();
        Vector3 sourcePos = releasedDrop.transform.position;

        foreach (var drop in drops)
        {
            if (drop == releasedDrop)
                continue;

            float distance = Vector3.Distance(
                sourcePos,
                drop.transform.position
            );

            if (distance > releaseImpulseRadius || distance < 0.001f)
                continue;

            Vector3 direction =
                (drop.transform.position - sourcePos).normalized;

            float t = 1f - (distance / releaseImpulseRadius);
            float force = releaseImpulseForce * Mathf.SmoothStep(0f, 1f, t);

            drop.ApplyRepulsion(direction * force);
        }
    }

    #endregion
}
