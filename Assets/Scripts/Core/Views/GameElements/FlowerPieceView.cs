using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//=^..^=   =^..^=   VERSION 1.0.2 (April 2026)    =^..^=    =^..^=
//                    Last Update 01/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// View for a single flower. Handles visuals, supports both drag (via FlowerDragHandler)
// and click-to-select (via OnPointerClick which fires GameEvents.FlowerSelected).


[RequireComponent(typeof(Image))]
public class FlowerPieceView : MonoBehaviour, IPointerClickHandler
{
    #region PROPERTIES AND FIELDS
    [Header("State")]
    [SerializeField] private FlowerColor flowerColor;
    [SerializeField] private bool isFirstPlayerToken;

    [Header("Selection colours")]
    [SerializeField] private Color selectedTint = new Color(0.4f, 1f, 0.4f, 1f);
    [SerializeField] private Color defaultTint  = Color.white;

    private Image flowerImage;
    private FlowerSpriteData spriteData;
    private DisplayView sourceDisplay;
    private bool _selected;

    public FlowerColor FlowerColor
    {
        get { return flowerColor; }
    }
    public bool IsFirstPlayerToken
    {
        get { return isFirstPlayerToken; }
    }
    public DisplayView SourceDisplay
    {
        get { return sourceDisplay; }
    }
    public bool IsDraggable
    {
        get { return sourceDisplay != null && !isFirstPlayerToken; }
    }
    public bool IsSelectable
    {
        get { return sourceDisplay != null && !isFirstPlayerToken; }
    }

    #endregion

    #region CONSTRUCTOR AND INITIALIZATION
    private void Awake()
    {
        flowerImage = GetComponent<Image>();
    }

    private void OnEnable()
    {
        GameEvents.OnSelectionCleared += HandleSelectionCleared;
    }

    private void OnDisable()
    {
        GameEvents.OnSelectionCleared -= HandleSelectionCleared;
    }

    public void Initialize(FlowerColor color, DisplayView display)
    {
        if (flowerImage == null) flowerImage = GetComponent<Image>();
        sourceDisplay      = display;
        flowerColor          = color;
        isFirstPlayerToken = color == FlowerColor.FirstPlayer;
        _selected          = false;
        UpdateVisual();
    }

    public void Initialize(FlowerColor color)
    {
        if (flowerImage == null) flowerImage = GetComponent<Image>();
        flowerColor          = color;
        isFirstPlayerToken = false;
        sourceDisplay      = null;
        _selected          = false;
        UpdateVisual();
    }

    #endregion


    #region INTERACTION METHODS

    //DRAG & DROP is handled by FlowerDragHandler

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsSelectable) return;
        if (sourceDisplay == null) return;

        GameController gc = null;
        if (GameResources.Instance != null && GameResources.Instance.GameController != null)
            gc = GameResources.Instance.GameController;
        else
            gc = GameController.Instance;
        if (gc == null || gc.IsGameOver) return;

        if (gc.Model.CurrentPhase != RoundPhaseEnum.PlacementPhase) return;

        int playerIndex = gc.Model.CurrentPlayerIndex;

        // only human players can click-to-select
        PlayerSlotConfig[] slots = gc.PlayerSlots;
        if (slots == null || playerIndex >= slots.Length) return;
        if (slots[playerIndex].PlayerType != PlayerType.Human) return;

        PlayerModel player = gc.Model.Players[playerIndex];

        // figure out which placement lines can accept this color (0-based)
        List<int> validLines = new List<int>();
        for (int i = 0; i < GameConfig.NUM_PLACEMENT_LINES; i++)
        {
            if (player.CanPlaceInLine(i, flowerColor))
                validLines.Add(i);
        }

        Debug.Log("[FlowerPieceView] Selected " + flowerColor + " from " + sourceDisplay.name
            + " (isCentral=" + sourceDisplay.IsCentral + ", factoryIndex=" + sourceDisplay.FactoryIndex + ")");

        // Clear previous selections first
        GameEvents.SelectionCleared();

        // Highlight flowers of the same colour in this display
        FlowerPieceView[] allViews = sourceDisplay.GetComponentsInChildren<FlowerPieceView>();
        for (int i = 0; i < allViews.Length; i++)
        {
            if (allViews[i].flowerColor == flowerColor && allViews[i].IsSelectable)
                allViews[i].SetSelected(true);
        }

        GameEvents.FlowerSelected(sourceDisplay, flowerColor, validLines, true);
    }
    #endregion

    #region HELPER METHODS

    // When selection is cleared, every flower deselects itself.
    private void HandleSelectionCleared()
    {
        if (_selected)
            SetSelected(false);
    }

    public void SetSpriteData(FlowerSpriteData data)
    {
        spriteData = data;
    }

    // Highlight or un-highlight this flower
    public void SetSelected(bool selected)
    {
        _selected = selected;
        if (flowerImage != null)
            flowerImage.color = selected ? selectedTint : defaultTint;
    }

    private void UpdateVisual()
    {
        if (flowerImage == null || spriteData == null) return;
        Sprite sprite = spriteData.GetSprite(flowerColor);
        if (sprite != null)
        {
            flowerImage.sprite = sprite;
            flowerImage.color  = _selected ? selectedTint : defaultTint;
        }
    }
    #endregion
}
