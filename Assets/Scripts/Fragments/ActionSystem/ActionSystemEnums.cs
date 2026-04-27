namespace Game.Data
{
    /// <summary>
    /// Tipos de acción soportados por el sistema de fragmentos.
    /// Los valores son explícitos para proteger la serialización.
    /// </summary>
    public enum FragmentActionType
    {
        SpriteFadeIn = 0,
        SpriteFadeOut = 1,
        SpriteDissolve = 2,
        SpriteDissolveVertical = 3,
        SpriteDissolveBoth = 4,
        SpriteAppearDissolve = 5,
        SpriteAppearDissolveVertical = 6,
        SpriteAppearDissolveBoth = 7,
        SpriteFadeMaterialColor = 8,

        RainStop = 20,
        RainStart = 21,
        RainChangeIntensity = 22,

        WindStop = 30,
        WindStart = 31,
        WindChangeIntensity = 32,

        FireStart = 40,
        FireStop = 41,

        SetBloomIntensity = 50,
        SetBloomTint = 51,

        CreateFootprintPathAnimation = 60,

        WaitTimeForTheNextAction = 70,
        WaitForSpecificInputForContinue = 71,

        ShowThoughtInPanel = 80,

        SetCinematicCameraTarget = 90,
        SwitchExplorationCamera = 91,

        DisplayDialoguePanel = 100,
        HideDialoguePanel = 101,

        SetWeatherProfile = 110,
        ClearWeatherProfile = 111,

        StartEmotionSelection = 120,

        SpawnFirstAvailableDraggableItem = 130,

        /// <summary>Muestra un tutorial por ID. Solo se mostrará si nunca fue mostrado antes.</summary>
        ShowTutorial = 140,

        /// <summary>Oculta el tutorial activo con el ID indicado.</summary>
        HideTutorial = 141
    }

    /// <summary>Define el modo de input esperado por una acción.</summary>
    public enum InputType
    {
        AnyKey = 0,
        SpecificKey = 1,
        MouseClick = 2
    }

    /// <summary>Define el anclaje del diálogo respecto al objetivo visual.</summary>
    public enum DialogAnchor
    {
        Top = 0,
        Bottom = 1,
        Left = 2,
        Right = 3,
        CustomOffset = 4
    }

    /// <summary>Define cómo se resuelve el target del modo cinemático.</summary>
    public enum CinematicTargetMode
    {
        TargetTransform = 0,
        ManualPosition = 1
    }

    /// <summary>
    /// Define qué evento de gameplay cierra automáticamente un tutorial.
    /// </summary>
    public enum TutorialDismissEvent
    {
        /// <summary>Solo se cierra con el botón de omitir.</summary>
        None = 0,

        /// <summary>Se cierra cuando el jugador abre un fragmento (memoria pasa a Seen o superior).</summary>
        OnFragmentOpened = 1,

        /// <summary>Se cierra cuando se conectan dos memorias/fragmentos.</summary>
        OnFragmentConnected = 2,

        /// <summary>Se cierra cuando se confirma el renombre de un fragmento.</summary>
        OnFragmentRenamed = 3,

        /// <summary>Se cierra cuando el jugador mueve el mouse (exploración).</summary>
        OnExplore = 4,

        /// <summary>Se cierra cuando el jugador usa la rueda del mouse (zoom).</summary>
        OnZoom = 5,

        /// <summary>Se cierra cuando finaliza el flujo de selección de emoción.</summary>
        OnEmotionSelected = 6,

        /// <summary>Se cierra cuando el inventario draggable cambia.</summary>
        OnInventoryChanged = 7,

        /// <summary>
        /// Se cierra cuando un ítem draggable entra al inventario (deja de estar Held).
        /// Si DismissItemDefinition está asignado, solo responde a ese ítem.
        /// </summary>
        OnDraggableItemPickedUp = 8,

        /// <summary>
        /// Se cierra cuando el jugador empieza a mover un objeto draggable (estado Held).
        /// Si DismissItemDefinition está asignado, solo responde a ese ítem.
        /// </summary>
        OnDraggableItemMoved = 9,

        /// <summary>Se cierra cuando el jugador arrastra un drop (fragmento) en el menú.</summary>
        OnDropMoved = 10,

        /// <summary>
        /// Se cierra cuando el jugador hace clic en un objeto interactuable del mundo (IInteractable).
        /// </summary>
        OnInteractableClicked = 11,

        /// <summary>
        /// Se cierra cuando el jugador hace clic derecho sobre un drop (fragmento) en el menú.
        /// Se detecta en OnMouseOver de DropController con GetMouseButtonDown(1).
        /// </summary>
        OnDropRightClicked = 12
    }
}