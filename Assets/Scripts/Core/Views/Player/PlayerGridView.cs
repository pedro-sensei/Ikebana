using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

public class PlayerGridView : MonoBehaviour
{
    private const string ScoringHelperPrefKey = "EnableScoringHelper";

    [Header("Config")]
    [SerializeField] private int playerIndex = 0;
    [SerializeField] private Transform gridContainer;
    [SerializeField] private Transform scoreOverlayContainer;

    [Header("Popups")]
    [SerializeField] private float scoreDuration = 1.2f;
    [SerializeField] private float fadeDuration  = 0.4f;
    [SerializeField] private int helperFontSize = 24;

    [Header("Bonus Colors")]
    [SerializeField] private Color rowColor = new Color(0.2f, 1f, 0.2f, 1f);
    [SerializeField] private Color colColor = new Color(0.2f, 0.6f, 1f, 1f);
    [SerializeField] private Color bonusColor = new Color(1f, 0.8f, 0.1f, 1f);

    private Image[,] _cells;
    private TMP_Text[,] _scores;
    private bool _initialized;
    private bool _usingPrebuiltScoreOverlay;

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
        StopAllCoroutines();
        KillScoreTweens();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        KillScoreTweens();
    }

    private void HandleEndGameBonusCell(int pIndex, int row, int col, int bonus, BonusType type)
    {
        if (pIndex != playerIndex || _scores == null) return;
        if (row < 0 || col < 0 || row >= _scores.GetLength(0) || col >= _scores.GetLength(1)) return;

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
        RefreshScoringHelperOverlay();
    }

    #region INITIALIZATION
    public void Initialize()
    {
        if (!_initialized) CreateGrid();
    }

    public void Reinitialize()
    {
        StopAllCoroutines();
        KillScoreTweens();

        ClearContainer(gridContainer);
        if (scoreOverlayContainer != null)
        {
            // Prebuilt overlays works now. Keeping this in case.
            // Runtime overlays generated 
            if (_usingPrebuiltScoreOverlay)
                ClearScoringHelperLabels();
            else
                ClearContainer(scoreOverlayContainer);
        }

        _cells = null;
        _scores = null;
        _usingPrebuiltScoreOverlay = false;
        _initialized = false;
        CreateGrid();
    }

    private void ClearContainer(Transform parent)
    {
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private void CreateGrid()
    {
        GameController gc = GameResources.GetGameController();
        if (gc == null || gc.Model == null || gridContainer == null) return;

        if (playerIndex < 0 || playerIndex >= gc.Model.Players.Length) return;

        FlowerSpriteData spriteData = null;
        if (GameResources.Instance != null)
            spriteData = GameResources.Instance.SpriteData;

        int size = GameConfig.GRID_SIZE;

        _cells = new Image[size, size];
        InitializeScoreOverlay(size);

        PlayerGridModel gridModel = gc.Model.Players[playerIndex].Grid;
        bool[] occupation = gridModel.GetOccupationPattern();

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                //Visual cell
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

            }
        }
        _initialized = true;
        RefreshScoringHelperOverlay();
    }

    private void InitializeScoreOverlay(int size)
    {
        if (scoreOverlayContainer == null) return;

        _scores = new TMP_Text[size, size];
        _usingPrebuiltScoreOverlay = TryBindPrebuiltScoreOverlay(size);
        if (_usingPrebuiltScoreOverlay) return;

        ClearContainer(scoreOverlayContainer);
        CreateRuntimeScoreOverlay(size);
    }


    //TODO: Fix all scenes or focus on runtime building but it also had errors.
    private bool TryBindPrebuiltScoreOverlay(int size)
    {
        //Keep code overlays since some errors were found in some scenes.
        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                string slotName = "Slot_" + (r + 1) + "_" + (c + 1);
                Transform slot = scoreOverlayContainer.Find(slotName);
                
                if (slot == null) return false;

                TMP_Text label = slot.GetComponentInChildren<TMP_Text>(true);
                if (label == null) return false;

                label.raycastTarget = false;
                label.text = "";
                label.color = new Color(1f, 1f, 1f, 0f);
                _scores[r, c] = label;
            }
        }
        return true;
    }

    private void CreateRuntimeScoreOverlay(int size)
    {
        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
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
    #endregion

    #region RUNTIME UPDATES
    public void Placeflower(int row, int col, FlowerColor color)
    {
        if (_cells != null && row >= 0 && col >= 0 && row < _cells.GetLength(0) && col < _cells.GetLength(1))
            _cells[row, col].color = Color.white;

        RefreshScoringHelperOverlay();
    }

    public void PlaceflowerWithScore(int row, int col, FlowerColor color, int points)
    {
        Placeflower(row, col, color);
        if (_scores == null || points <= 0) return;
        if (row < 0 || col < 0 || row >= _scores.GetLength(0) || col >= _scores.GetLength(1)) return;

        TMP_Text label = _scores[row, col];
        if (label == null) return;

        label.DOKill(false);
        label.color = Color.white;
        label.fontSize = 24 + Mathf.Min(points, 10);
        StartCoroutine(AnimateScore(label, points));
    }

    private IEnumerator AnimateScore(TMP_Text label, int points)
    {
        if (label == null) yield break;

        label.DOKill(false);
        label.text = "+" + points;
        label.DOFade(1f, fadeDuration * 0.5f).SetEase(Ease.OutQuad);
        yield return new WaitForSeconds(scoreDuration);
        label.DOFade(0f, fadeDuration).SetEase(Ease.InQuad);
        yield return new WaitForSeconds(fadeDuration);
        RefreshScoringHelperOverlay();
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

        RefreshScoringHelperOverlay();
    }

    public void RefreshScoringHelperOverlay()
    {
        if (_scores == null) return;

        bool enabled = PlayerPrefs.GetInt(ScoringHelperPrefKey, 1) == 1;
        if (!enabled)
        {
            // If the helper was turned off in settings, clear everything.
            ClearScoringHelperLabels();
            return;
        }

        GameController gc = GameResources.GetGameController();
        if (gc == null || gc.Model == null || playerIndex < 0 || playerIndex >= gc.Model.Players.Length)
            return;

        GameModel model = gc.Model;
        PlayerModel player = model.Players[playerIndex];
        PlayerGridModel grid = player.Grid;
        int size = GameConfig.GRID_SIZE;

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                TMP_Text label = _scores[r, c];
                if (label == null) continue;

                if (grid.IsOccupied(r, c))
                {
                    // Occupied cells should not show helper values.
                    label.text = "";
                    label.color = new Color(1f, 1f, 1f, 0f);
                    continue;
                }

                // Ask the player model for the projected score 
                int points = player.GetPotentialGridPlacementScore(r, c, includeProjectedFullLines: true);
                label.text = points.ToString();
                label.fontSize = helperFontSize;
                label.color = new Color(1f, 0.92f, 0.01f, 0.65f);
            }
        }
    }

    private void ClearScoringHelperLabels()
    {
        int size = GameConfig.GRID_SIZE;
        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                TMP_Text label = _scores[r, c];
                if (label == null) continue;
                label.text = "";
                label.color = new Color(1f, 1f, 1f, 0f);
            }
        }
    }

    private void KillScoreTweens()
    {
        if (_scores == null) return;

        int rows = _scores.GetLength(0);
        int cols = _scores.GetLength(1);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (_scores[r, c] != null)
                    _scores[r, c].DOKill(false);
            }
        }
    }

    #endregion
}