using TMPro;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// View for the bag. Shows how many flowers are left.
// Updates when the bag count changes or gets refilled.
public class BagView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI countText;
    
    private void Awake()
    {
        GameEvents.OnBagCountChanged += HandleBagCountChanged;
        GameEvents.OnBagRefilled += HandleBagRefilled;
    }

    private void OnDestroy()
    {
        GameEvents.OnBagCountChanged -= HandleBagCountChanged;
        GameEvents.OnBagRefilled -= HandleBagRefilled;
    }

    private void HandleBagCountChanged(int newCount)
    {
        if (countText != null)
            countText.text = newCount.ToString();
    }

    private void HandleBagRefilled()
    {
        if (GameController.Instance != null && countText != null)
            countText.text = GameController.Instance.Model.Bag.Count.ToString();
    }
}
