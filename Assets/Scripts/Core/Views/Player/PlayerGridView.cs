using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//=^..^=   =^..^=   VERSION 1.0.2 (April 2026)    =^..^=    =^..^=
//                    Last Update 01/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


//  Player grid view. Shows the wall flowers and score popups when flowers are placed.
public class PlayerGridView : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private int playerIndex = 0;
    [SerializeField] private Transform gridContainer;
    [SerializeField] private Transform scoreOverlayContainer;

    [Header("Popups")]
    [SerializeField] private float scoreDuration = 1.2f;
    [SerializeField] private float fadeDuration  = 0.4f;

    [Header("Bonus Colors")]
    [SerializeField] private Color rowColor = new Color(0.2f, 1f, 0.2f, 1f);
    [SerializeField] private Color colColor = new Color(0.2f, 0.6f, 1f, 1f);
    [SerializeField] private Color bonusColor = new Color(1f, 0.8f, 0.1f, 1f);

    private Image[,] _cells;
    private TMP_Text[,] _scores;
    private bool _initialized;

    public void SetPlayerIndex(int index)
    {
        playerIndex = index;
    }

    private void Awake()
    {
        if (gridContainer == null)
            gridContainer = transform;

        GameEvents.OnEndGameBonusCellScored += HandleEndGameBonusCell;
    }

    private void OnDestroy()
    {
        GameEvents.OnEndGameBonusCellScored -= HandleEndGameBonusCell;
    }

    private void HandleEndGameBonusCell(int pIndex, int row, int col, int bonus, BonusType type)
    {
        if (pIndex != playerIndex || _scores == null) return;

        // pick the right color depending on the bonus type
        Color color;
        if (type == BonusType.Row)
            color = rowColor;
        else if (type == BonusType.Column)
            color = colColor;
        else
            color = bonusColor;

        StartCoroutine(ShowBonusPopup(_scores[row, col], bonus, color));
    }

    private IEnumerator ShowBonusPopup(TMP_Text label, int bonus, Color color)
    {
        label.text = "+" + bonus;
        Color startColor = color;
        startColor.a = 0;
        label.color = startColor;

        label.DOFade(1f, fadeDuration * 0.5f).SetEase(Ease.OutQuad);
        yield return new WaitForSeconds(scoreDuration);

        label.DOFade(0f, fadeDuration).SetEase(Ease.InQuad);
        yield return new WaitForSeconds(fadeDuration);
        label.text = "";
    }

    #region INITIALIZATION
    public void Initialize()
    {
        if (!_initialized) CreateGrid();
    }

    public void Reinitialize()
    {
        ClearContainer(gridContainer);
        if (scoreOverlayContainer != null)
            ClearContainer(scoreOverlayContainer);

        _cells = null;
        _scores = null;
        _initialized = false;
        CreateGrid();
    }

    private void ClearContainer(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private void CreateGrid()
    {
        GameController gc = GameController.Instance;
        if (GameResources.Instance != null && GameResources.Instance.GameController != null)
            gc = GameResources.Instance.GameController;

        FlowerSpriteData spriteData = null;
        if (GameResources.Instance != null)
            spriteData = GameResources.Instance.SpriteData;

        int size = GameConfig.GRID_SIZE;

        _cells = new Image[size, size];
        if (scoreOverlayContainer != null)
            _scores = new TMP_Text[size, size];

        PlayerGridModel gridModel = gc.Model.Players[playerIndex].Grid;
        bool[] occupation = gridModel.GetOccupationPattern();

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                //-- Visual cell
                GameObject cellObj = new GameObject("Cell_" + r + "_" + c, typeof(RectTransform), typeof(Image));
                cellObj.transform.SetParent(gridContainer, false);

                Image img = cellObj.GetComponent<Image>();
                img.raycastTarget = false;
                img.rectTransform.sizeDelta = new Vector2(60, 60);

                if (spriteData != null)
                    img.sprite = spriteData.GetSprite(gridModel.GetColorAt(r, c));

                bool isFull = occupation[r * size + c];
                img.color = isFull ? Color.white : new Color(1, 1, 1, 0.2f);
                _cells[r, c] = img;

                //-- Score label overlay
                if (scoreOverlayContainer != null)
                {
                    GameObject scoreObj = new GameObject("Score_" + r + "_" + c, typeof(RectTransform));
                    scoreObj.transform.SetParent(scoreOverlayContainer, false);
                    scoreObj.GetComponent<RectTransform>().sizeDelta = new Vector2(60, 60);

                    GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                    labelObj.transform.SetParent(scoreObj.transform, false);

                    TextMeshProUGUI tmp = labelObj.GetComponent<TextMeshProUGUI>();
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.fontSize = 20;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.color = new Color(1, 0.92f, 0.01f, 0);
                    tmp.raycastTarget = false;

                    _scores[r, c] = tmp;
                }
            }
        }
        _initialized = true;
    }
    #endregion

    #region RUNTIME UPDATES
    public void Placeflower(int row, int col, FlowerColor color)
    {
        if (_cells != null)
            _cells[row, col].color = Color.white;
    }

    public void PlaceflowerWithScore(int row, int col, FlowerColor color, int points)
    {
        Placeflower(row, col, color);
        if (_scores == null || points <= 0) return;

        TMP_Text label = _scores[row, col];
        label.color = Color.white;
        label.fontSize = 24 + Mathf.Min(points, 10);
        StartCoroutine(AnimateScore(label, points));
    }

    private IEnumerator AnimateScore(TMP_Text label, int points)
    {
        label.text = "+" + points;
        label.DOFade(1f, fadeDuration * 0.5f).SetEase(Ease.OutQuad);
        yield return new WaitForSeconds(scoreDuration);
        label.DOFade(0f, fadeDuration).SetEase(Ease.InQuad);
        yield return new WaitForSeconds(fadeDuration);
        label.text = "";
    }

    public void LoadFromOccupationPattern(bool[] pattern)
    {
        if (_cells == null) return;
        int size = GameConfig.GRID_SIZE;
        for (int i = 0; i < pattern.Length; i++)
        {
            int r = i / size;
            int c = i % size;
            if (r < size)
                _cells[r, c].color = pattern[i] ? Color.white : new Color(1, 1, 1, 0.2f);
        }
    }
    #endregion
}