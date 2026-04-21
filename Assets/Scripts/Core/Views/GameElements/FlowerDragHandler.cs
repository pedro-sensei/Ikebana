using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

//=^..^=   =^..^=   VERSION 1.0.2 (April 2026)    =^..^=    =^..^=
//                    Last Update 01/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// Handles drag-and-drop of a flower from a display.
// When dragging starts, all flowers of the same color follow along.
//
// On drop: the destination calls MarkAsPlaced() if the move succeeds.
// If not placed on a valid target, all flowers go back to their original spot.

[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(FlowerPieceView))]
public class FlowerDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Drag Settings")]
    [SerializeField] private float dragAlpha = 0.7f;

    private FlowerPieceView flowerPieceView;
    private CanvasGroup canvasGroup;

    private Transform originalParent;
    private Vector3 originalPosition;
    private int originalSiblingIndex;

    private List<FlowerDragHandler> sameflowerSiblings = new List<FlowerDragHandler>();
    private Dictionary<FlowerDragHandler, Vector3> siblingOffsets = new Dictionary<FlowerDragHandler, Vector3>();

    private RectTransform dragCanvasRect;
    private Camera dragCanvasCamera;

    public bool WasDroppedOnValidTarget { get; private set; }

    public FlowerPieceView FlowerPieceView
    {
        get { return flowerPieceView; }
    }

    private void Awake()
    {
        flowerPieceView = GetComponent<FlowerPieceView>();
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private Canvas GetDragCanvas()
    {
        if (GameResources.Instance != null && GameResources.Instance.DragCanvas != null)
            return GameResources.Instance.DragCanvas;

        Canvas[] canvases = GetComponentsInParent<Canvas>(true);
        if (canvases.Length > 0)
            return canvases[canvases.Length - 1];
        return null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!flowerPieceView.IsDraggable)
        {
            eventData.pointerDrag = null;
            return;
        }

        Canvas dragCanvas = GetDragCanvas();
        WasDroppedOnValidTarget = false;

        dragCanvasRect = dragCanvas.transform as RectTransform;
        dragCanvasCamera = dragCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : dragCanvas.worldCamera;

        originalParent = transform.parent;
        originalPosition = transform.position;
        originalSiblingIndex = transform.GetSiblingIndex();

        sameflowerSiblings = GetSameColorSiblings();
        siblingOffsets = CalculateOffsets(sameflowerSiblings);

        ReparentToCanvas(this, dragCanvas);
        for (int i = 0; i < sameflowerSiblings.Count; i++)
        {
            sameflowerSiblings[i].SaveOriginalState();
            ReparentToCanvas(sameflowerSiblings[i], dragCanvas);
            sameflowerSiblings[i].canvasGroup.blocksRaycasts = false;
        }

        canvasGroup.alpha = dragAlpha;
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector3 worldPoint;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                dragCanvasRect, eventData.position, dragCanvasCamera, out worldPoint))
        {
            transform.position = worldPoint;
        }

        foreach (KeyValuePair<FlowerDragHandler, Vector3> kvp in siblingOffsets)
        {
            if (kvp.Key != null)
            {
                kvp.Key.transform.position = transform.position + kvp.Value;
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        for (int i = 0; i < sameflowerSiblings.Count; i++)
        {
            sameflowerSiblings[i].canvasGroup.alpha = 1f;
            sameflowerSiblings[i].canvasGroup.blocksRaycasts = true;
        }

        if (!WasDroppedOnValidTarget)
        {
            ReturnToOriginalPosition();
            for (int i = 0; i < sameflowerSiblings.Count; i++)
                sameflowerSiblings[i].ReturnToOriginalPosition();
        }

        sameflowerSiblings.Clear();
        siblingOffsets.Clear();
    }

    public void MarkAsPlaced()
    {
        WasDroppedOnValidTarget = true;
    }

    public void SaveOriginalState()
    {
        originalParent = transform.parent;
        originalPosition = transform.position;
        originalSiblingIndex = transform.GetSiblingIndex();
    }

    private void ReturnToOriginalPosition()
    {
        if (originalParent != null)
        {
            transform.SetParent(originalParent);
            transform.SetSiblingIndex(Mathf.Min(originalSiblingIndex, originalParent.childCount));
            transform.position = originalPosition;
        }
    }

    private void ReparentToCanvas(FlowerDragHandler handler, Canvas canvas)
    {
        handler.transform.SetParent(canvas.transform, true);
        handler.transform.SetAsLastSibling();

        Vector3 pos = handler.transform.localPosition;
        pos.z = 0f;
        handler.transform.localPosition = pos;
    }

    private List<FlowerDragHandler> GetSameColorSiblings()
    {
        List<FlowerDragHandler> siblings = new List<FlowerDragHandler>();
        if (originalParent == null) return siblings;

        FlowerColor myColor = flowerPieceView.FlowerColor;
        FlowerDragHandler[] handlers = originalParent.GetComponentsInChildren<FlowerDragHandler>();
        for (int i = 0; i < handlers.Length; i++)
        {
            if (handlers[i] != this && handlers[i].FlowerPieceView.FlowerColor == myColor)
                siblings.Add(handlers[i]);
        }
        return siblings;
    }

    private Dictionary<FlowerDragHandler, Vector3> CalculateOffsets(List<FlowerDragHandler> siblings)
    {
        Dictionary<FlowerDragHandler, Vector3> offsets = new Dictionary<FlowerDragHandler, Vector3>();
        for (int i = 0; i < siblings.Count; i++)
        {
            if (siblings[i] != null)
                offsets[siblings[i]] = siblings[i].transform.position - transform.position;
        }
        return offsets;
    }
}
