using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//=^..^=   =^..^=   VERSION 1.0.1 (March 2026)    =^..^=    =^..^=
//                    Last Update 22/03/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// View for the discard pile.
// Shows a discard icon with a flower count. When you hover it shows a panel
// with all discarded flowers grouped by color.
public class DiscardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private GameObject hoverPopUp;
    [SerializeField] private Transform hoverPopUpTransform;

    private bool isHovering = false;
    private RectTransform panelRect;
    private RectTransform iconRect;

    private void Awake()
    {
        GameEvents.OnDiscardCountChanged += HandleDiscardCountChanged;

        iconRect = GetComponent<RectTransform>();

        if (hoverPopUp != null)
        {
            hoverPopUp.SetActive(false);
            panelRect = hoverPopUp.GetComponent<RectTransform>();
        }
    }

    private void OnDestroy()
    {
        GameEvents.OnDiscardCountChanged -= HandleDiscardCountChanged;
    }

    private void Update()
    {
        if (!isHovering) return;

        // check if cursor is still over the icon or the panel
        if (!IsPointerOverRect(iconRect) && (panelRect == null || !IsPointerOverRect(panelRect)))
        {
            isHovering = false;
            if (hoverPopUp != null)
                hoverPopUp.SetActive(false);
        }
    }

    private bool IsPointerOverRect(RectTransform rect)
    {
        if (rect == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition);
    }

    private void HandleDiscardCountChanged(int newCount)
    {
        if (countText != null)
            countText.text = newCount.ToString();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverPopUp == null || GameController.Instance == null) return;

        isHovering = true;
        hoverPopUp.SetActive(true);
        RebuildHoverContent();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        if (hoverPopUp != null)
            hoverPopUp.SetActive(false);
    }

    // Builds the hover panel content showing flower counts by color
    private void RebuildHoverContent()
    {
        if (hoverPopUpTransform == null) return;

        // Clear old content
        for (int i = hoverPopUpTransform.childCount - 1; i >= 0; i--)
            Destroy(hoverPopUpTransform.GetChild(i).gameObject);

        DiscardModel discard = GameController.Instance.Model.Discard;

        // count per color
        Dictionary<FlowerColor, int> colorCounts = new Dictionary<FlowerColor, int>();
        for (int i = 0; i < discard.flowers.Count; i++)
        {
            FlowerPiece flower = discard.flowers[i];
            if (colorCounts.ContainsKey(flower.Color))
                colorCounts[flower.Color]++;
            else
                colorCounts[flower.Color] = 1;
        }

        foreach (KeyValuePair<FlowerColor, int> kvp in colorCounts)
            CreateColorEntry(kvp.Key, kvp.Value);
    }

    private void CreateColorEntry(FlowerColor color, int count)
    {
        FlowerSpriteData spriteData = null;
        if (GameResources.Instance != null)
            spriteData = GameResources.Instance.SpriteData;

        GameObject entryObj = new GameObject("Entry_" + color);
        entryObj.transform.SetParent(hoverPopUpTransform);
        entryObj.transform.localScale = Vector3.one;

        // horizontal layout
        HorizontalLayoutGroup layout = entryObj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 0;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        RectTransform entryRect = entryObj.GetComponent<RectTransform>();

        // Color swatch
        GameObject swatchObj = new GameObject("Swatch");
        swatchObj.transform.SetParent(entryObj.transform);
        swatchObj.transform.localScale = Vector3.one;
        Image swatchImg = swatchObj.AddComponent<Image>();
        if (spriteData != null)
        {
            Sprite sprite = spriteData.GetSprite(color);
            if (sprite != null) swatchImg.sprite = sprite;
        }
        LayoutElement swatchLayout = swatchObj.AddComponent<LayoutElement>();
        swatchLayout.preferredWidth = 50;
        swatchLayout.preferredHeight = 50;

        // Count text
        GameObject textObj = new GameObject("Count");
        textObj.transform.SetParent(entryObj.transform);
        textObj.transform.localScale = Vector3.one;
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "x" + count;
        text.fontSize = 24;
        LayoutElement textLayout = textObj.AddComponent<LayoutElement>();
        textLayout.preferredWidth = 50;
        textLayout.preferredHeight = 50;
    }
}
