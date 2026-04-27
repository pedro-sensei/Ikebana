using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//=^..^=   =^..^=   VERSION 1.1.1 (April 2026)    =^..^=    =^..^=
//                    Last Update 27/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// View for the discard pile.
//POPUP REMOVED IN FINAL VERSION (May be restored in the future with new code not buggy)
public class DiscardView : MonoBehaviour
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
     // POPUP REMOVED IN FINAL VERSION

    private void HandleDiscardCountChanged(int newCount)
    {
        if (countText != null)
            countText.text = newCount.ToString();
    }

}
