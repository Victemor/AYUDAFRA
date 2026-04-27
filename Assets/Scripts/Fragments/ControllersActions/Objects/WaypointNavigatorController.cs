using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Mueve un objeto entre puntos en secuencia con espera en cada uno.
/// Además permite interacción para cargar una escena.
/// Sistema completamente independiente.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WaypointNavigatorController : MonoBehaviour
{
    #region Movement

    [Header("Configuración")]
    [SerializeField] private bool startOnAwake = true;

    [Header("Puntos")]
    [SerializeField] private List<Transform> points = new();

    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 3f;

    [Tooltip("Si está activo, usa interpolación suave.")]
    [SerializeField] private bool smoothMovement = true;

    [Header("Espera")]
    [SerializeField] private float waitTimePerPoint = 1f;

    [Header("Loop")]
    [SerializeField] private bool loop = true;

    private Coroutine routine;

    #endregion

    private void Start()
    {
        if (startOnAwake)
            StartLoop();
    }

    /// <summary>
    /// Inicia el loop de movimiento.
    /// </summary>
    public void StartLoop()
    {
        if (points == null || points.Count == 0)
        {
            Debug.LogWarning("No hay puntos asignados.");
            return;
        }

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(LoopRoutine());
    }

    /// <summary>
    /// Detiene el loop de movimiento.
    /// </summary>
    public void StopLoop()
    {
        if (routine != null)
            StopCoroutine(routine);
    }

    private IEnumerator LoopRoutine()
    {
        int index = 0;

        while (true)
        {
            Transform target = points[index];

            yield return MoveTo(target.position);

            yield return new WaitForSeconds(waitTimePerPoint);

            index++;

            if (index >= points.Count)
            {
                if (loop)
                    index = 0;
                else
                    yield break;
            }
        }
    }

    private IEnumerator MoveTo(Vector3 target)
    {
        while (Vector3.Distance(transform.position, target) > 0.05f)
        {
            if (smoothMovement)
            {
                transform.position = Vector3.Lerp(
                    transform.position,
                    target,
                    Time.deltaTime * moveSpeed
                );
            }
            else
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    target,
                    moveSpeed * Time.deltaTime
                );
            }

            yield return null;
        }

        transform.position = target;
    }
}