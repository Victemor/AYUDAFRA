using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using Game.Data;
using Game.Runtime;
using System.Linq;

public class FragmentsGraphController : MonoBehaviour
{
    #region Inspector References

    [Header("Referencias")]
    [SerializeField] private GameObject dropPrefab;
    private DropPhysicsController physics;
    [SerializeField] private DropPlacementService placementService;
    [SerializeField] private DropConnectionsController connectionsController;

    [Header("UI")]
    [SerializeField] private DropContextMenuController contextMenu;
    [SerializeField] private DropLabelController dropLabel;

    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask dropLayer;

    [SerializeField] private Transform dropsParent;
    [SerializeField] private MemoryDatabase memoryDatabase;

    #endregion

    #region Drop Settings

    [SerializeField] private float sizeDrop = 1f;
    [SerializeField] private float timeToAnimation = 1.4f;

    #endregion

    #region Runtime State

    private readonly Dictionary<string, DropController> dropsByFragment = new();
    private DropController activeMenuDrop;

    public enum ConnectionMode
    {
        None,
        Connecting
    }

    private ConnectionMode connectionMode = ConnectionMode.None;
    private DropController connectionOwner;

    private bool dropClickedThisFrame;

    private DropController editingDrop;
    private bool isEditingLabel;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        physics = GetComponent<DropPhysicsController>();
    }

    private void Update()
    {
        bool leftClick = Input.GetMouseButtonDown(0);
        bool rightClick = Input.GetMouseButtonDown(1);

        if (leftClick || (rightClick && IsInConnectMode()))
        {
            HandleGlobalClick();
        }
    }

    private void OnEnable()
    {
        GameEvents.OnMemoryUnlocked += HandleMemoryUnlocked;
        GameEvents.OnMemoryConnectionChanged += HandleMemoryConnectionChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnMemoryUnlocked -= HandleMemoryUnlocked;
        GameEvents.OnMemoryConnectionChanged -= HandleMemoryConnectionChanged;
    }

    private void Start()
    {
        CreateDropsFromProgress();
    }

    #endregion

    #region Drop Creation

    private void CreateDropsFromProgress()
    {
        var repo = GameStateRepository.Instance;
        GameManager.Instance.RebuildProgressFromRuntime();

        foreach (var memory in memoryDatabase.Memories)
        {
            var runtime = repo.GetMemory(memory);

            if (runtime.CurrentState == MemoryState.Locked)
                continue;

            DropData dropData = GetOrCreateDropData(memory.Id, runtime);
            CreateDrop(memory, dropData);
        }

        connectionsController.BuildConnections(dropsByFragment);
    }

    private DropData GetOrCreateDropData(string id, MemoryRuntimeData runtime)
    {
        var progress = GameManager.Instance.FragmentProgress;
        var existing = progress.drops.Find(d => d.FragmentName == id);

        if (existing != null)
        {
            existing.WasVisited = runtime.CurrentState >= MemoryState.Seen;
            existing.SyncConnections(GameStateRepository.Instance.GetConnections(id));
            return existing;
        }

        var newDrop = new DropData(id, Vector2.zero);
        newDrop.WasVisited = runtime.CurrentState >= MemoryState.Seen;
        newDrop.SyncConnections(GameStateRepository.Instance.GetConnections(id));

        progress.drops.Add(newDrop);
        return newDrop;
    }

    public bool HasDrop(string fragmentName)
    {
        return dropsByFragment.ContainsKey(fragmentName);
    }

    private void CreateDrop(MemoryDefinition memory, DropData dropData)
    {
        if (dropsByFragment.ContainsKey(dropData.FragmentName))
            return;

        if (memory == null)
        {
            Debug.LogError($"MemoryDefinition NULL para: {dropData.FragmentName}");
            return;
        }

        GameObject instance = Instantiate(dropPrefab, dropsParent);

        if (!instance.TryGetComponent(out DropController controller))
        {
            Debug.LogError("El prefab de gota no contiene DropController.");
            Destroy(instance);
            return;
        }

        Vector3 position;

        if (dropData.Position != Vector2.zero)
        {
            position = new Vector3(dropData.Position.x, 0f, dropData.Position.y);
        }
        else
        {
            position = placementService.GeneratePosition(
                dropsByFragment.Count + 1,
                dropsByFragment.Values
            );

            dropData.Position = new Vector2(position.x, position.z);
        }

        controller.Initialize(memory, dropData, this);
        controller.transform.localPosition = position;

        AnimateDropSpawn(controller.transform);

        connectionsController.RegisterDropConnections(dropData);

        dropsByFragment.Add(dropData.FragmentName, controller);

        physics.ApplySpawnImpulse(controller);
        physics.StartRelaxation();
    }

    public void CreateNewDrop(DropData dropData)
    {
        var memory = memoryDatabase.Memories
            .FirstOrDefault(m => m.Id == dropData.FragmentName);

        if (memory == null)
        {
            Debug.LogError($"MemoryDefinition no encontrado: {dropData.FragmentName}");
            return;
        }

        CreateDrop(memory, dropData);
    }

    private void AnimateDropSpawn(Transform dropTransform)
    {
        Vector3 finalScale = dropTransform.localScale;

        dropTransform.localScale = Vector3.zero;

        dropTransform.DOScale(finalScale, timeToAnimation)
            .SetEase(Ease.OutBack);
    }

    #endregion

    #region Drop Interaction

    public void ResolveChainRepulsion()
    {
        physics.ResolveChainRepulsion();
    }

    public void ApplyReleaseImpulse(DropController drop)
    {
        physics.ApplyReleaseImpulse(drop);
    }

    public void StartRelaxation()
    {
        physics.StartRelaxation();
    }

    #endregion

    public void CommitDropPositions()
    {
        var drops = GetAllDrops();

        foreach (var drop in drops)
        {
            drop.CommitPosition();
        }

        GameManager.Instance.SaveProgress();
    }

    #region UI

    public void NotifyDropClicked()
    {
        dropClickedThisFrame = true;
    }

    public void OnDropHoverEnter(DropController hovered)
    {
        if (isEditingLabel)
            return;

        if (connectionMode != ConnectionMode.Connecting)
        {
            if (activeMenuDrop != null && activeMenuDrop != hovered)
            {
                contextMenu.Hide();
                activeMenuDrop = null;
            }

            if (activeMenuDrop == null)
            {
                ShowDropLabel(hovered);
            }

            return;
        }

        ShowDropLabel(hovered);

        if (hovered == connectionOwner)
            return;

        bool alreadyConnected =
            connectionOwner.dropData.IsConnectedTo(
                hovered.dropData.FragmentName
            );

        if (alreadyConnected)
        {
            hovered.SetDimmed();

            connectionsController.SetConnectionDimmed(
                connectionOwner.dropData.FragmentName,
                hovered.dropData.FragmentName,
                dimmed: true
            );
        }
        else
        {
            hovered.SetNormal();

            connectionsController.SetConnectionDimmed(
                connectionOwner.dropData.FragmentName,
                hovered.dropData.FragmentName,
                dimmed: false
            );
        }
    }

    public void OnDropHoverExit(DropController hovered)
    {
        if (isEditingLabel)
            return;

        HideDropLabel();

        if (connectionMode != ConnectionMode.Connecting)
            return;

        if (hovered == connectionOwner)
            return;

        bool alreadyConnected =
            connectionOwner.dropData.IsConnectedTo(
                hovered.dropData.FragmentName
            );

        if (alreadyConnected)
        {
            hovered.SetNormal();
            connectionsController.SetConnectionDimmed(
                connectionOwner.dropData.FragmentName,
                hovered.dropData.FragmentName,
                dimmed: false
            );
        }
        else
        {
            hovered.SetDimmed();
        }
    }

    public void OnDropLeftClick(DropController drop)
    {
        if (IsInConnectMode())
        {
            Debug.Log("[INPUT BLOCK] Context menu blocked by Connect Mode");
            return;
        }

        if (isEditingLabel)
        {
            Debug.Log("Clic en gota mientras se edita etiqueta: salir de edición");
            ExitLabelEditIfActive();
            return;
        }

        dropLabel.Hide();

        if (activeMenuDrop != null && activeMenuDrop != drop)
        {
            contextMenu.Hide();
        }
        else if (activeMenuDrop == drop)
        {
            contextMenu.Hide();
            activeMenuDrop = null;
            return;
        }

        activeMenuDrop = drop;
        ShowContextMenu(drop);
    }

    private void ShowContextMenu(DropController drop)
    {
        contextMenu.Show(drop, this);
    }

    private void ShowDropLabel(DropController drop)
    {
        dropLabel.Show(drop);
    }

    public void HideDropLabel()
    {
        dropLabel.Hide();
    }

    public void HideAllUI()
    {
        dropLabel.Hide();
        contextMenu.Hide();
        activeMenuDrop = null;
    }

    #endregion

    #region Acciones de menú

    private void HandleGlobalClick()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (!dropClickedThisFrame)
        {
            ExitLabelEditIfActive();
            CancelAllInteractions();
        }

        dropClickedThisFrame = false;
    }

    private void CancelAllInteractions()
    {
        Debug.Log("Clic en espacio vacío: cancelar interacciones.");

        if (isEditingLabel)
        {
            ExitLabelEditIfActive();
            return;
        }

        if (activeMenuDrop != null)
        {
            contextMenu.Hide();
            activeMenuDrop = null;
        }

        if (connectionMode == ConnectionMode.Connecting)
        {
            ExitConnectMode();
        }
    }

    public void EnterConnectMode(DropController owner)
    {
        Debug.Log($"[CONNECT MODE] ENTER with {owner.dropData.FragmentName}");
        connectionMode = ConnectionMode.Connecting;
        connectionOwner = owner;

        owner.SetHighlighted(true);

        DimAllExcept(owner, owner.dropData.ConnectedFragments);
        DimConnectionsExcept(owner.dropData.FragmentName, owner.dropData.ConnectedFragments);
    }

    public void ExitConnectMode()
    {
        connectionMode = ConnectionMode.None;

        if (connectionOwner != null)
            connectionOwner.SetHighlighted(false);

        connectionOwner = null;
        RestoreAllVisuals();
    }

    public void DimAllExcept(DropController owner, IReadOnlyList<string> connectedFragments = null)
    {
        foreach (var drop in dropsByFragment.Values)
        {
            if (drop == owner)
            {
                drop.SetNormal();
                continue;
            }

            bool isConnected = false;

            foreach (string fragment in connectedFragments)
            {
                if (fragment == drop.dropData.FragmentName)
                {
                    isConnected = true;
                    break;
                }
            }

            if (isConnected)
            {
                drop.SetNormal();
            }
            else
            {
                drop.SetDimmed();
            }
        }
    }

    public void DimConnectionsExcept(string ownerFragment, IReadOnlyList<string> connectedFragments)
    {
        connectionsController.DimAllExcept(ownerFragment, connectedFragments);
    }

    public void RestoreAllVisuals()
    {
        foreach (var drop in dropsByFragment.Values)
            drop.SetNormal();

        connectionsController.RestoreAll();
    }

    public void HandleConnectClick(DropController target)
    {
        if (target == connectionOwner)
            return;

        var repo = GameStateRepository.Instance;

        var ownerMemory = repo.GetMemory(connectionOwner.GetMemoryDefinition());
        var targetMemory = repo.GetMemory(target.GetMemoryDefinition());

        if (ownerMemory.CurrentState < MemoryState.Seen ||
            targetMemory.CurrentState < MemoryState.Seen)
        {
            Debug.LogWarning(
                $"[CONNECT BLOCKED] Cannot connect {connectionOwner.dropData.FragmentName} ↔ {target.dropData.FragmentName} (not seen)"
            );
            return;
        }

        string ownerName = connectionOwner.dropData.FragmentName;
        string targetName = target.dropData.FragmentName;

        bool alreadyConnected = repo.AreConnected(ownerName, targetName);

        if (alreadyConnected)
        {
            repo.DisconnectMemories(ownerName, targetName);

            connectionOwner.dropData.RemoveConnection(targetName);
            target.dropData.RemoveConnection(ownerName);

            connectionsController.RemoveConnection(ownerName, targetName);

            Debug.Log($"[CONNECT] Removed {ownerName} ↔ {targetName}");
        }
        else
        {
            repo.ConnectMemories(ownerName, targetName);

            connectionOwner.dropData.ConnectTo(targetName);
            target.dropData.ConnectTo(ownerName);

            connectionsController.AddConnection(connectionOwner, target);

            Debug.Log($"[CONNECT] Created {ownerName} ↔ {targetName}");
        }

        GameManager.Instance.SyncAllDropConnectionsFromRepository();
        GameManager.Instance.SaveProgress();
    }

    private void HandleMemoryUnlocked(MemoryDefinition memory)
    {
        Debug.Log($"[MENU] Creating drop for {memory.Id}");

        var repo = GameStateRepository.Instance;
        var runtime = repo.GetMemory(memory);
        var dropData = GetOrCreateDropData(memory.Id, runtime);

        if (!HasDrop(memory.Id))
        {
            CreateDrop(memory, dropData);
        }
    }

    private void HandleMemoryConnectionChanged(string memoryIdA, string memoryIdB)
    {
        GameManager.Instance.SyncAllDropConnectionsFromRepository();
    }

    public void EnterLabelEditMode(DropController drop)
    {
        Debug.Log("Entrar en modo edición de etiqueta de gota");
        ExitLabelEditIfActive();

        editingDrop = drop;
        isEditingLabel = true;

        dropLabel.Show(drop);
        dropLabel.EnterEditMode();

        drop.SetHighlighted(true);

        foreach (var currentDrop in dropsByFragment.Values)
        {
            if (currentDrop == drop)
            {
                currentDrop.SetNormal();
                continue;
            }

            currentDrop.SetDimmed();
        }

        connectionsController.DimAllConnections();
    }

    public void ExitLabelEditIfActive()
    {
        Debug.Log("Salir del modo edición de etiqueta de gota");
        if (!isEditingLabel)
            return;

        editingDrop.SetHighlighted(false);
        RestoreAllVisuals();

        dropLabel.ConfirmAndExitEditMode();

        editingDrop = null;
        isEditingLabel = false;
    }

    #endregion

    #region Public API

    public List<DropController> GetAllDrops()
    {
        return new List<DropController>(dropsByFragment.Values);
    }

    public bool IsInConnectMode()
    {
        return connectionMode == ConnectionMode.Connecting;
    }

    public bool IsInEditLabelMode()
    {
        return isEditingLabel;
    }

    public DropController GetOwnerDrop()
    {
        return connectionOwner;
    }

    public DropController GetDrop(string fragmentName)
    {
        dropsByFragment.TryGetValue(fragmentName, out DropController controller);
        return controller;
    }

    public void NotifyDropClicked(DropController drop)
    {
        dropClickedThisFrame = true;
    }

    #endregion
}