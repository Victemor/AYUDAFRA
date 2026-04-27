using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

/// <summary>
/// Sistema de drag optimizado para juegos isométricos.
/// Permite seleccionar, arrastrar y soltar objetos sobre superficies válidas,
/// con restricciones de área y reglas por layer.
/// </summary>
public sealed class DragSystem : MonoBehaviour
{
    #region Inspector

    [Header("Raycast")]

    [SerializeField]
    [Tooltip("Cámara usada para detectar input. Si no se asigna, se intentará resolver automáticamente.")]
    private Camera mainCamera;

    [SerializeField]
    [Tooltip("Capas que pueden ser seleccionadas.")]
    private LayerMask interactableLayers;

    [SerializeField]
    [Tooltip("Distancia máxima del raycast.")]
    private float maxRayDistance = 200f;

    [Header("Surface")]

    [SerializeField]
    [Tooltip("Capas válidas donde el objeto puede moverse.")]
    private LayerMask surfaceLayers;

    [SerializeField]
    [Tooltip("Offset adicional sobre la superficie.")]
    private float surfaceOffset = 0.1f;

    [Header("Drag Limits")]

    [SerializeField]
    [Tooltip("Centro del área de movimiento.")]
    private Vector3 dragAreaCenter = Vector3.zero;

    [SerializeField]
    [Tooltip("Tamaño del área en X (ancho) y Z (profundidad).")]
    private Vector2 dragAreaSize = new Vector2(20f, 20f);

    [Header("Layer Rules")]

    [SerializeField]
    [Tooltip("Reglas aplicadas al soltar el objeto según su layer.")]
    private List<LayerDragRule> layerRules = new();

    [Header("Debug")]

    [SerializeField]
    [Tooltip("Activa logs de depuración del sistema.")]
    private bool debugLogs = false;

    #endregion

    #region Runtime

    private DraggableObject currentObject;

    /// <summary>
    /// Evento cuando se selecciona un objeto.
    /// </summary>
    public event Action<DraggableObject> OnObjectSelected;

    /// <summary>
    /// Evento cuando se libera un objeto.
    /// </summary>
    public event Action<DraggableObject> OnObjectReleased;

    #endregion

    #region Unity Methods

    /// <summary>
    /// Resuelve referencias críticas del sistema.
    /// </summary>
    private void Awake()
    {
        ResolveCamera();
    }

    /// <summary>
    /// Reintenta resolver referencias que podrían no existir aún en Awake.
    /// </summary>
    private void Start()
    {
        ResolveCamera();
    }

    /// <summary>
    /// Ejecuta el flujo de input del drag.
    /// </summary>
    private void FixedUpdate()
    {
        if (!EnsureCamera())
        {
            return;
        }

        HandleInput();
    }

    /// <summary>
    /// Dibuja el área de arrastre en escena.
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;

        Vector3 size = new Vector3(dragAreaSize.x, 0.1f, dragAreaSize.y);
        Gizmos.DrawWireCube(dragAreaCenter, size);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Fuerza el inicio de drag sobre un objeto recién instanciado.
    /// </summary>
    public void ForceStartDrag(DraggableObject draggable)
    {
        if (draggable == null)
        {
            return;
        }

        if (!EnsureCamera())
        {
            LogWarning("No se pudo iniciar drag porque no hay una cámara válida.");
            return;
        }

        currentObject = draggable;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance))
        {
            currentObject.InitializeDrag(hit.point);
        }

        OnObjectSelected?.Invoke(currentObject);
    }

    #endregion

    #region Input

    /// <summary>
    /// Maneja el flujo de input del mouse.
    /// </summary>
    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TrySelect();
        }

        if (Input.GetMouseButton(0) && currentObject != null)
        {
            Drag();
        }

        if (Input.GetMouseButtonUp(0) && currentObject != null)
        {
            Release();
        }
    }

    /// <summary>
    /// Intenta seleccionar un objeto arrastrable.
    /// Usa RaycastAll para evitar bloqueos por otros colliders.
    /// </summary>
    private void TrySelect()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance, interactableLayers);

        if (hits == null || hits.Length == 0)
        {
            return;
        }

        Array.Sort(hits, CompareHitDistance);

        for (int i = 0; i < hits.Length; i++)
        {
            if (!hits[i].collider.TryGetComponent(out DraggableObject draggable))
            {
                continue;
            }

            if (!draggable.CanStartDrag())
            {
                continue;
            }

            currentObject = draggable;
            currentObject.InitializeDrag(hits[i].point);

            OnObjectSelected?.Invoke(currentObject);
            return;
        }
    }

    #endregion

    #region Drag Logic

    /// <summary>
    /// Mueve el objeto sobre una superficie válida,
    /// manteniéndolo siempre por encima de ella.
    /// </summary>
    private void Drag()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, surfaceLayers))
        {
            return;
        }

        Vector3 targetPosition = hit.point;

        if (currentObject.TryGetComponent(out Collider objectCollider))
        {
            targetPosition.y = hit.point.y + objectCollider.bounds.extents.y + surfaceOffset;
        }
        else
        {
            targetPosition.y = hit.point.y + surfaceOffset;
        }

        targetPosition = ClampToDragArea(targetPosition);
        currentObject.SetPosition(targetPosition);
    }

    /// <summary>
    /// Limita la posición dentro del área definida.
    /// </summary>
    private Vector3 ClampToDragArea(Vector3 position)
    {
        float halfX = dragAreaSize.x * 0.5f;
        float halfZ = dragAreaSize.y * 0.5f;

        float minX = dragAreaCenter.x - halfX;
        float maxX = dragAreaCenter.x + halfX;

        float minZ = dragAreaCenter.z - halfZ;
        float maxZ = dragAreaCenter.z + halfZ;

        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.z = Mathf.Clamp(position.z, minZ, maxZ);

        return position;
    }

    #endregion

    #region Release

    /// <summary>
    /// Libera el objeto y aplica reglas según su layer.
    /// </summary>
    private void Release()
    {
        ApplyLayerRule(currentObject);

        OnObjectReleased?.Invoke(currentObject);
        currentObject = null;
    }

    /// <summary>
    /// Aplica reglas configuradas según el layer del objeto.
    /// </summary>
    private void ApplyLayerRule(DraggableObject obj)
    {
        if (obj == null)
        {
            return;
        }

        int objectLayer = obj.gameObject.layer;

        for (int i = 0; i < layerRules.Count; i++)
        {
            LayerDragRule rule = layerRules[i];

            if ((rule.Layer.value & (1 << objectLayer)) == 0)
            {
                continue;
            }

            if (rule.TargetParent != null)
            {
                obj.transform.SetParent(rule.TargetParent);
            }

            return;
        }
    }

    #endregion

    #region Camera Resolution

    /// <summary>
    /// Intenta resolver automáticamente la cámara si no fue asignada manualmente.
    /// </summary>
    private void ResolveCamera()
    {
        if (mainCamera != null)
        {
            return;
        }

        if (Camera.main != null)
        {
            mainCamera = Camera.main;
            Log($"Cámara resuelta automáticamente con Camera.main: {mainCamera.name}");
            return;
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (cameras == null || cameras.Length == 0)
        {
            LogWarning("No se encontró ninguna cámara disponible para el DragSystem.");
            return;
        }

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].isActiveAndEnabled)
            {
                mainCamera = cameras[i];
                Log($"Cámara resuelta automáticamente por búsqueda en escena: {mainCamera.name}");
                return;
            }
        }

        mainCamera = cameras[0];
        Log($"Se asignó una cámara no activa como fallback: {mainCamera.name}");
    }

    /// <summary>
    /// Garantiza que exista una cámara válida antes de procesar input.
    /// </summary>
    private bool EnsureCamera()
    {
        if (mainCamera != null)
        {
            return true;
        }

        ResolveCamera();
        return mainCamera != null;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Comparador por distancia para resultados de raycast.
    /// </summary>
    private static int CompareHitDistance(RaycastHit a, RaycastHit b)
    {
        return a.distance.CompareTo(b.distance);
    }

    /// <summary>
    /// Escribe un log de depuración.
    /// </summary>
    private void Log(string message)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log($"[DragSystem] {message}", this);
    }

    /// <summary>
    /// Escribe una advertencia de depuración.
    /// </summary>
    private void LogWarning(string message)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.LogWarning($"[DragSystem] {message}", this);
    }

    #endregion
}

/// <summary>
/// Define el comportamiento de un objeto según su layer.
/// </summary>
[Serializable]
public struct LayerDragRule
{
    [field: SerializeField]
    [field: Tooltip("Layer al que aplica esta regla.")]
    public LayerMask Layer { get; private set; }

    [field: SerializeField]
    [field: Tooltip("Nuevo parent al soltar el objeto.")]
    public Transform TargetParent { get; private set; }
}