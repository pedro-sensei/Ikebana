using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


// View for a placement line on the player board.
// Two interaction modes:
//   - Drag and drop (IDropHandler): flowers are dropped here from the drag handler.
//   - Click to confirm (IPointerClickHandler): line gets highlighted after selecting 
//     flowers, clicking a highlighted line confirms the move.
public class PlacementLineView : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [Header("Configuration")]
    [SerializeField] private int lineIndex = 0;
    [SerializeField] private int playerIndex = 0;

    [Header("References")]
    [SerializeField] private Transform flowerContainer;

    [Header("Highlight")]
    [SerializeField] private Image highlightOverlay;
    [SerializeField] private Color highlightColor = new Color(0.2f, 1f, 0.2f, 0.35f);

    [Header("State")]
    [SerializeField] private bool isInteractable = false;

    private bool _highlighted = false;

    // click-to-confirm state (set when FlowerSelected fires)
    private DisplayView _pendingSource;
    private FlowerColor _pendingColor;

    private List<GameObject> spawnedflowers = new List<GameObject>();

    public int LineIndex
    {
        get { return lineIndex; }
    }
    public int PlayerIndex
    {
        get { return playerIndex; }
    }

    private void Awake()
    {
        if (flowerContainer == null)
            flowerContainer = transform;

        ViewRegistry.RegisterPlacementLine(playerIndex, lineIndex, GetComponent<RectTransform>());

        // Make sure theres a raycast-target Image so clicks and drops work
        Image img = GetComponent<Image>();
        if (img == null)
        {
            img = gameObject.AddComponent<Image>();
            img.color = Color.clear;
        }
        img.raycastTarget = true;

        // If flowerContainer is a separate child, dont let it block raycasts
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
        ViewRegistry.UnregisterPlacementLine(playerIndex, lineIndex);
        GameEvents.OnflowerSelected    -= HandleflowerSelected;
        GameEvents.OnSelectionCleared -= HandleSelectionCleared;
    }

    public void SetInteractable(bool interactable)
    {
        isInteractable = interactable;
        if (!interactable) SetHighlighted(false);
    }

    // Shows or hides the green overlay that marks this line as a valid target
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
        SetHighlighted(validLines.Contains(lineIndex));
    }

    private void HandleSelectionCleared()
    {
        _pendingSource = null;
        _pendingColor  = FlowerColor.Empty;
        SetHighlighted(false);
    }



    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("[PlacementLineView] OnPointerClick line=" + lineIndex
            + " interactable=" + isInteractable + " highlighted=" + _highlighted
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
            success = gc.PickFromCentralAndPlace(_pendingColor, lineIndex);
        else
            success = gc.PickFromFactoryAndPlace(_pendingSource.FactoryIndex, _pendingColor, lineIndex);

        if (success)
        {
            GameEvents.SelectionCleared();
            AnimateLand();
            Debug.Log("[Click] Flowers placed in line " + lineIndex);
        }
        else
        {
            Debug.LogWarning("[PlacementLineView] Move FAILED: line=" + lineIndex + " color=" + _pendingColor);
        }
    }

   
    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log("[PlacementLineView] OnDrop line=" + lineIndex + " interactable=" + isInteractable);

        if (!isInteractable) return;

        GameController gc = null;
        if (GameResources.Instance != null && GameResources.Instance.GameController != null)
            gc = GameResources.Instance.GameController;
        else
            gc = GameController.Instance;
        if (gc == null) return;

        if (eventData.pointerDrag == null) return;
        FlowerDragHandler dragHandler = eventData.pointerDrag.GetComponent<FlowerDragHandler>();
        if (dragHandler == null)
        {
            Debug.LogWarning("[PlacementLineView] OnDrop: no flowerDragHandler on dragged object");
            return;
        }

        FlowerPieceView draggedflower = dragHandler.FlowerPieceView;
        if (draggedflower == null) return;

        DisplayView sourceDisplay = draggedflower.SourceDisplay;
        if (sourceDisplay == null) return;

        FlowerColor color = draggedflower.FlowerColor;

        bool success;
        if (sourceDisplay.IsCentral)
            success = gc.PickFromCentralAndPlace(color, lineIndex);
        else
            success = gc.PickFromFactoryAndPlace(sourceDisplay.FactoryIndex, color, lineIndex);

        if (success)
        {
            dragHandler.MarkAsPlaced();
            AnimateLand();
            Debug.Log("[Drag] Flowers placed in line " + lineIndex);
        }
    }



    // Reads the current model state and rebuilds the flower visuals.
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

        PlacementLineModel[] lines = players[playerIndex].PlacementLines;
        if (lineIndex < 0 || lineIndex >= lines.Length) return;

        PlacementLineModel lineModel = lines[lineIndex];
        ClearflowerViews();
        for (int i = 0; i < lineModel.Count; i++)
            CreateflowerVisual(lineModel.Flowers[i].Color, i);
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
            flowerObj = CreateFallbackflower(color, index);

        FlowerPieceView view = flowerObj.GetComponent<FlowerPieceView>();
        if (view == null)
            view = flowerObj.AddComponent<FlowerPieceView>();
        if (spriteData != null) view.SetSpriteData(spriteData);
        view.Initialize(color);

        // Flowers on placement lines are display-only, they must not eat clicks
        Image img = flowerObj.GetComponent<Image>();
        if (img != null)
            img.raycastTarget = false;

        CanvasGroup cg = flowerObj.GetComponent<CanvasGroup>();
        if (cg != null)
            cg.blocksRaycasts = false;

        // Remove drag handler if the prefab has one since these flowers arent draggable
        FlowerDragHandler drag = flowerObj.GetComponent<FlowerDragHandler>();
        if (drag != null)
            Destroy(drag);

        spawnedflowers.Add(flowerObj);
    }

    private GameObject CreateFallbackflower(FlowerColor color, int index)
    {
        GameObject obj = new GameObject("Lineflower_" + color + "_" + index);
        obj.transform.SetParent(flowerContainer);
        obj.transform.localScale = Vector3.one;
        obj.AddComponent<Image>();
        obj.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 80);
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

    public void SetPlayerIndex(int index)
    {
        ViewRegistry.UnregisterPlacementLine(playerIndex, lineIndex);
        playerIndex = index;
        ViewRegistry.RegisterPlacementLine(playerIndex, lineIndex, GetComponent<RectTransform>());
    }

    public void AnimateLand()
    {
        flowerContainer.DOKill(false);
        flowerContainer.localScale = Vector3.one;
        flowerContainer
            .DOPunchScale(Vector3.one * 0.20f, 0.25f, 5, 0.4f)
            .SetEase(Ease.OutQuad)
            .OnComplete(delegate { flowerContainer.localScale = Vector3.one; });
    }
}
