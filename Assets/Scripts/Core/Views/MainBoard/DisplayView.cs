using System.Collections.Generic;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//=^..^=   =^..^=   VERSION 1.1.1 (April 2026)    =^..^=    =^..^=
//                    Last Update 27/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// View for a Display (factory or central).
// Listens to GameEvents to update when the model changes.
// Creates/destroys flower GameObjects to reflect model state.
// Each flower gets a drag handler so it can be dragged to placement lines.
// During AI animations the view freezes so ghost flowers can fly over
// the real flowers; the pending update is applied when the animation ends.
public class DisplayView : MonoBehaviour
{
    #region FIELDS AND PROPERTIES
    [Header("Configuration")]
    [SerializeField] private int factoryIndex = -1;
    [SerializeField] private bool isCentral = false;

    [Header("References")]
    [Tooltip("Container for flower GameObjects. If empty, uses this transform.")]
    [SerializeField] private Transform flowerContainer;
    [SerializeField] private List<FlowerPieceView> currentflowerViews = new List<FlowerPieceView>();

    // freeze/unfreeze for AI animations
    private bool _animationFrozen = false;
    private bool _pendingRebuild = false;
    private IReadOnlyList<FlowerPiece> _pendingflowers = null;
    private bool _pendingShowToken = false;
    private bool _pendingClear = false;

    public int FactoryIndex
    {
        get { return factoryIndex; }
    }
    public bool IsCentral
    {
        get { return isCentral; }
    }
    #endregion

    #region CONSTRUCTOR AND INITIALIZATION
    private void Awake()
    {
        //flower container to store the flowers.
        //If not assigned, use this transform.
        if (flowerContainer == null)
            flowerContainer = transform;

        // Subscribe to events depending on factory or central
        if (isCentral)
        {
            ViewRegistry.RegisterCentral(GetComponent<RectTransform>());
            GameEvents.OnCentralDisplayChanged += HandleCentralChanged;
        }
        else
        {
            ViewRegistry.RegisterFactory(factoryIndex, GetComponent<RectTransform>());
            GameEvents.OnDisplayFilled += HandleFactoryFilled;
            GameEvents.OnFactoryPicked += HandleFactoryPicked;
        }

        GameEvents.OnAIAnimationStart += HandleAIAnimationStart;
        GameEvents.OnAIAnimationEnd   += HandleAIAnimationEnd;
    }

    private void OnDestroy()
    {
        ResetPendingState();

        if (isCentral)
        {
            ViewRegistry.UnregisterCentral();
            GameEvents.OnCentralDisplayChanged -= HandleCentralChanged;
        }
        else
        {
            ViewRegistry.UnregisterFactory(factoryIndex);
            GameEvents.OnDisplayFilled  -= HandleFactoryFilled;
            GameEvents.OnFactoryPicked  -= HandleFactoryPicked;
        }

        GameEvents.OnAIAnimationStart -= HandleAIAnimationStart;
        GameEvents.OnAIAnimationEnd   -= HandleAIAnimationEnd;
    }

    private void OnDisable()
    {
        ResetPendingState();
    }
    #endregion

    #region EVENT HANDLERS
    private void HandleAIAnimationStart(int _index)
    {
        // Apply any pending update before freezing again.
        if (_pendingClear)
        {
            ClearAllflowerViews();
            _pendingClear = false;
        }
        if (_pendingRebuild && _pendingflowers != null)
        {
            RebuildflowerViews(_pendingflowers, _pendingShowToken);
            _pendingRebuild = false;
            _pendingflowers   = null;
        }
        _animationFrozen = true;
    }

    private void HandleAIAnimationEnd()
    {
        _animationFrozen = false;

        if (_pendingClear)
        {
            ClearAllflowerViews();
            _pendingClear = false;
        }

        if (_pendingRebuild && _pendingflowers != null)
        {
            RebuildflowerViews(_pendingflowers, _pendingShowToken);
            _pendingRebuild = false;
            _pendingflowers   = null;
        }
    }

    // Called when a factory gets filled at the start of a round
    private void HandleFactoryFilled(int index, IReadOnlyList<FlowerPiece> flowers)
    {
        if (index != factoryIndex) return;
        Debug.Log($"[DisplayView] Factory {factoryIndex} received {flowers.Count} flowers ? rebuilding views.");
        // Factory fill happens at round start, never during animation
        RebuildflowerViews(flowers, false);
    }

    // Called when someone picks from a factory.
    // Clears the display since the flowers are flying away.
    private void HandleFactoryPicked(int index, FlowerColor color)
    {
        if (index != factoryIndex) return;

        if (_animationFrozen)
        {
            _pendingClear   = true;
            _pendingRebuild = false;
            _pendingflowers   = null;
            return;
        }

        ClearAllflowerViews();
    }

    // Called when the central display changes (flowers added/removed, token changes)
    private void HandleCentralChanged(IReadOnlyList<FlowerPiece> flowers)
    {
        bool showToken = false;
        GameController gc = GameResources.GetGameController();

        if (gc != null && gc.Model != null)
            showToken = gc.Model.CentralDisplay.HasFirstPlayerToken;

        if (_animationFrozen)
        {
            // Cache the newest full central state and replay it once the animation ends.
            // We store the whole rebuild input because multiple central changes can happen during one AI move.
            _pendingRebuild   = true;
            _pendingflowers     = flowers;
            _pendingShowToken = showToken;
            return;
        }

        RebuildflowerViews(flowers, showToken);
    }

    #endregion

    #region HELPER METHODS

    // Rebuilds all flower views from model data, sorted by color.
    // Optionally shows the first player token as the first element.
    private void RebuildflowerViews(IReadOnlyList<FlowerPiece> flowers, bool showFirstPlayerToken)
    {
        ClearAllflowerViews();

        // Token is rendered separately first so it stays visually distinct from normal flower sorting.
        // show the first player token before regular flowers
        if (showFirstPlayerToken)
        {
            CreateFirstPlayerTokenView();
        }

        List<FlowerPiece> sorted = new List<FlowerPiece>(flowers);
        // remove FirstPlayer tokens since we already rendered them above
        sorted.RemoveAll(delegate(FlowerPiece t) { return t.Color == FlowerColor.FirstPlayer; });
        sorted.Sort(delegate(FlowerPiece a, FlowerPiece b) { return a.Color.CompareTo(b.Color); });

        for (int i = 0; i < sorted.Count; i++)
        {
            CreateflowerView(sorted[i], i);
        }
    }

    // Creates the first player token flower.
    // Not draggable since it gets picked automaticaly when taking from central.
    private void CreateFirstPlayerTokenView()
    {
        FlowerSpriteData spriteData = null;
        if (GameResources.Instance != null)
            spriteData = GameResources.Instance.SpriteData;

        GameObject tokenObj = new GameObject("FirstPlayerToken");
        tokenObj.transform.SetParent(flowerContainer);
        tokenObj.transform.localScale = Vector3.one;

        Image img = tokenObj.AddComponent<Image>();
        img.raycastTarget = false;
        tokenObj.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 80);

        if (spriteData != null)
        {
            Sprite tokenSprite = spriteData.GetFirstPlayerTokenSprite();
            if (tokenSprite != null)
            {
                img.sprite = tokenSprite;
                img.color = Color.white;
            }
        }

        FlowerPieceView view = tokenObj.AddComponent<FlowerPieceView>();
        if (spriteData != null)
            view.SetSpriteData(spriteData);
        view.Initialize(FlowerColor.FirstPlayer, this);

        currentflowerViews.Add(view);
    }


    // Creates a flower in the display and adds components for dragging and visuals.
    private void CreateflowerView(FlowerPiece flowerPiece, int index)
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
        {
            flowerObj = Instantiate(flowerPrefab, flowerContainer);
        }
        else
        {
            flowerObj = new GameObject("flower_" + flowerPiece.Color + "_" + index);
            flowerObj.transform.SetParent(flowerContainer);
            flowerObj.transform.localScale = Vector3.one;

            Image img = flowerObj.AddComponent<Image>();
            img.raycastTarget = true;

            flowerObj.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 80);
        }

        //Add the flowerView script to the object
        FlowerPieceView view = flowerObj.GetComponent<FlowerPieceView>();
        if (view == null)
            view = flowerObj.AddComponent<FlowerPieceView>();

        //Set srpite data reference to load the sprites
        if (spriteData != null)
            view.SetSpriteData(spriteData);

        //Initialize view with flower color and reference to this display
        view.Initialize(flowerPiece.Color, this);

        // Add drag handler for drag-and-drop
        if (flowerObj.GetComponent<FlowerDragHandler>() == null)
            flowerObj.AddComponent<FlowerDragHandler>();

        // CanvasGroup needed for drag transparency
        if (flowerObj.GetComponent<CanvasGroup>() == null)
            flowerObj.AddComponent<CanvasGroup>();

        currentflowerViews.Add(view);
    }

    private void ClearAllflowerViews()
    {
        // Detach before destroy so layout groups stop referencing these objects immediately.
        // This avoided one-frame layout glitches while Unity deferred the actual Destroy call.
        for (int i = 0; i < currentflowerViews.Count; i++)
        {
            if (currentflowerViews[i] != null)
            {
                currentflowerViews[i].transform.SetParent(null);
                Destroy(currentflowerViews[i].gameObject);
            }
        }
        currentflowerViews.Clear();

        // Also destroy any children of the container that are not tracked
        // (e.g. pre-existing scene objects or orphans from deferred Destroy).
        if (flowerContainer != null)
        {
            for (int i = flowerContainer.childCount - 1; i >= 0; i--)
                Destroy(flowerContainer.GetChild(i).gameObject);
        }
    }

    private void ResetPendingState()
    {
        _animationFrozen = false;
        _pendingRebuild = false;
        _pendingflowers = null;
        _pendingShowToken = false;
        _pendingClear = false;
    }

    #endregion
}
