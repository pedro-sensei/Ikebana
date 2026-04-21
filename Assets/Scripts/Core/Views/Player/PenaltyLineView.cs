using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//=^..^=   =^..^=   VERSION 1.0.2 (April 2026)    =^..^=    =^..^=
//                    Last Update 01/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// View for the penalty line. Handles drag-and-drop and click-to-confirm.
public class PenaltyLineView : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [Header("Configuration")]
    [SerializeField] private int playerIndex = 0;

    [Header("References")]
    [SerializeField] private Transform flowerContainer;

    [Header("Highlight")]
    [SerializeField] private Image highlightOverlay;
    [SerializeField] private Color highlightColor = new Color(1f, 0.6f, 0.1f, 0.35f);

    [Header("State")]
    [SerializeField] private bool isInteractable = false;

    private bool _highlighted = false;
    private DisplayView _pendingSource;
    private FlowerColor _pendingColor;

    private List<GameObject> spawnedflowers = new List<GameObject>();

    private void Awake()
    {
        if (flowerContainer == null)
            flowerContainer = transform;

        ViewRegistry.RegisterPenaltyLine(playerIndex, GetComponent<RectTransform>());

        Image img = GetComponent<Image>();
        if (img == null)
        {
            img = gameObject.AddComponent<Image>();
            img.color = Color.clear;
        }
        img.raycastTarget = true;

        // If flowerContainer is a separate child dont let it block raycasts
        if (flowerContainer != transform)
        {
            Image containerImg = flowerContainer.GetComponent<Image>();
            if (containerImg != null)
                containerImg.raycastTarget = false;

            CanvasGroup containerCg = flowerContainer.GetComponent<CanvasGroup>();
            if (containerCg != null)
                containerCg.blocksRaycasts = false;
        }

        if (highlightOverlay != null)
        {
            highlightOverlay.gameObject.SetActive(false);
            highlightOverlay.raycastTarget = false;
        }

        GameEvents.OnflowerSelected    += HandleflowerSelected;
        GameEvents.OnSelectionCleared += HandleSelectionCleared;
    }

    private void OnDestroy()
    {
        ViewRegistry.UnregisterPenaltyLine(playerIndex);
        GameEvents.OnflowerSelected    -= HandleflowerSelected;
        GameEvents.OnSelectionCleared -= HandleSelectionCleared;
    }

    public void SetInteractable(bool interactable)
    {
        isInteractable = interactable;
        if (!interactable) SetHighlighted(false);
    }

    public void SetHighlighted(bool on)
    {
        _highlighted = on;
        if (highlightOverlay != null)
        {
            highlightOverlay.gameObject.SetActive(on);
            highlightOverlay.color = highlightColor;
            highlightOverlay.raycastTarget = false;
        }
    }


    private void HandleflowerSelected(DisplayView source, FlowerColor color,
                                    List<int> validLines, bool hasPenalty)
    {
        if (!isInteractable) return;
        _pendingSource = source;
        _pendingColor  = color;
        SetHighlighted(hasPenalty);
    }

    private void HandleSelectionCleared()
    {
        _pendingSource = null;
        _pendingColor  = FlowerColor.Empty;
        SetHighlighted(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("[PenaltyLineView] OnPointerClick interactable=" + isInteractable
            + " highlighted=" + _highlighted
            + " pendingSource=" + (_pendingSource != null ? _pendingSource.name : "null"));

        if (!isInteractable || !_highlighted) return;
        if (_pendingSource == null) return;

        GameController gc = null;
        if (GameResources.Instance != null && GameResources.Instance.GameController != null)
            gc = GameResources.Instance.GameController;
        else
            gc = GameController.Instance;
        if (gc == null) return;

        bool success;
        if (_pendingSource.IsCentral)
            success = gc.PickFromCentralAndPlace(_pendingColor, -1);       // -1 = penalty
        else
            success = gc.PickFromFactoryAndPlace(_pendingSource.FactoryIndex, _pendingColor, -1);

        if (success)
        {
            GameEvents.SelectionCleared();
            Debug.Log("[Click] Flowers sent to penalty line");
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log("[PenaltyLineView] OnDrop interactable=" + isInteractable);

        if (!isInteractable) return;

        GameController gc = null;
        if (GameResources.Instance != null && GameResources.Instance.GameController != null)
            gc = GameResources.Instance.GameController;
        else
            gc = GameController.Instance;
        if (gc == null) return;

        if (eventData.pointerDrag == null) return;
        FlowerDragHandler dragHandler = eventData.pointerDrag.GetComponent<FlowerDragHandler>();
        if (dragHandler == null) return;

        FlowerPieceView draggedflower = dragHandler.FlowerPieceView;
        if (draggedflower == null) return;

        DisplayView sourceDisplay = draggedflower.SourceDisplay;
        if (sourceDisplay == null) return;

        FlowerColor color = draggedflower.FlowerColor;

        bool success;
        if (sourceDisplay.IsCentral)
            success = gc.PickFromCentralAndPlace(color, -1);          // -1 = penalty
        else
            success = gc.PickFromFactoryAndPlace(sourceDisplay.FactoryIndex, color, -1);

        if (success)
        {
            dragHandler.MarkAsPlaced();
            Debug.Log("[Drag] Flowers sent to penalty line");
        }
    }

    public void SetPlayerIndex(int index)
    {
        ViewRegistry.UnregisterPenaltyLine(playerIndex);
        playerIndex = index;
        ViewRegistry.RegisterPenaltyLine(playerIndex, GetComponent<RectTransform>());
    }

    public void Refresh()
    {
        GameController gc = null;
        if (GameResources.Instance != null && GameResources.Instance.GameController != null)
            gc = GameResources.Instance.GameController;
        else
            gc = GameController.Instance;
        if (gc == null) return;

        PlayerModel[] players = gc.Model.Players;
        if (playerIndex < 0 || playerIndex >= players.Length) return;

        PenaltyLineModel penaltyModel = players[playerIndex].PenaltyLine;
        ClearflowerViews();

        for (int i = 0; i < penaltyModel.Flowers.Count; i++)
            CreateflowerVisual(penaltyModel.Flowers[i].Color, i);
    }

    private void CreateflowerVisual(FlowerColor color, int index)
    {
        GameObject flowerPrefab = null;
        FlowerSpriteData spriteData = null;
        if (GameResources.Instance != null)
        {
            flowerPrefab = GameResources.Instance.FlowerPrefab;
            spriteData = GameResources.Instance.SpriteData;
        }

        GameObject flowerObj;
        if (flowerPrefab != null)
            flowerObj = Instantiate(flowerPrefab, flowerContainer);
        else
            flowerObj = CreateFallback("Penaltyflower_" + color + "_" + index);

        FlowerPieceView view = flowerObj.GetComponent<FlowerPieceView>();
        if (view == null)
            view = flowerObj.AddComponent<FlowerPieceView>();
        if (spriteData != null) view.SetSpriteData(spriteData);
        view.Initialize(color);

        // penalty flowers are display-only, dont block clicks
        Image img = flowerObj.GetComponent<Image>();
        if (img != null)
            img.raycastTarget = false;

        CanvasGroup cg = flowerObj.GetComponent<CanvasGroup>();
        if (cg != null)
            cg.blocksRaycasts = false;

        FlowerDragHandler drag = flowerObj.GetComponent<FlowerDragHandler>();
        if (drag != null)
            Destroy(drag);

        spawnedflowers.Add(flowerObj);
    }

    private GameObject CreateFallback(string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(flowerContainer);
        obj.transform.localScale = Vector3.one;
        obj.AddComponent<Image>();
        obj.GetComponent<RectTransform>().sizeDelta = new Vector2(60, 60);
        return obj;
    }

    private void ClearflowerViews()
    {
        for (int i = 0; i < spawnedflowers.Count; i++)
        {
            if (spawnedflowers[i] != null)
            {
                spawnedflowers[i].transform.SetParent(null);
                Destroy(spawnedflowers[i]);
            }
        }
        spawnedflowers.Clear();
    }
}
