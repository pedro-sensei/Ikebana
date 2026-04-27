using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EndGameMenuController : MonoBehaviour
{
    [System.Serializable]
    public struct PlayerStatsSlotUI
    {
        public GameObject Root;
        public Image PortraitImage;
        public TextMeshProUGUI Ranking;
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI ScoreText;
        public TextMeshProUGUI BonusRowsText;
        public TextMeshProUGUI BonusColumnsText;
        public TextMeshProUGUI BonusColorsText;
        public TextMeshProUGUI PlacementPointsText;
        public TextMeshProUGUI TotalPenaltyText;
    }

    [System.Serializable]
    public struct ColorCountSlotUI
    {
        public FlowerColor Color;
        public Image BackgroundImage;
        public TextMeshProUGUI CountText;
    }

    [Header("Data")]
    [SerializeField] private GameSetupData setupData;
    [SerializeField] private FlowerSpriteData flowerSpriteData;

    [Header("Navigation")]
    [SerializeField] private MainMenuController mainMenuController;

    [Header("Player Slots")]
    [SerializeField] private PlayerStatsSlotUI[] playerSlots;

    [Header("General Statistics")]
    [SerializeField] private TextMeshProUGUI roundsPlayedText;
    [SerializeField] private TextMeshProUGUI turnsPlayedText;
    [SerializeField] private ColorCountSlotUI[] colorCountSlots;

    [Header("Buttons")]
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button playAgainButton;
    [SerializeField] private Button exitGameButton;

    private List<int> sortedPlayers; 

    private void Awake()
    {
        ResolveResources();

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        if (playAgainButton != null)
            playAgainButton.onClick.AddListener(OnPlayAgainClicked);
        if (exitGameButton != null)
            exitGameButton.onClick.AddListener(OnExitGameClicked);
    }

    private void OnEnable()
    {
        ResolveResources();
        Refresh();
    }

    private void ResolveResources()
    {
        GameResources resources = GameResources.Instance;
        if (resources == null) return;

        if (setupData == null) setupData = resources.GameSetupData;
        if (flowerSpriteData == null) flowerSpriteData = resources.SpriteData;
        if (mainMenuController == null) mainMenuController = resources.MainMenuControllerRef;
    }

    public void Refresh()
    {
        GameController gameController = GameResources.GetGameController();
        if (gameController == null || gameController.Model == null)
        {
            ClearView();
            return;
        }

        GameModel model = gameController.Model;
        sortedPlayers = BuildSortedPlayers(model);

        for (int i = 0; i < playerSlots.Length; i++)
        {
            bool active = i < sortedPlayers.Count;
            if (playerSlots[i].Root != null)
                playerSlots[i].Root.SetActive(active);

            if (!active)
                continue;

            int playerIndex = sortedPlayers[i];
            PopulatePlayerSlot(playerSlots[i], model.Players[playerIndex], gameController.GetPortrait(playerIndex), i);
        
        }

        if (roundsPlayedText != null)
            roundsPlayedText.text = model.TotalRoundsPlayed.ToString();

        if (turnsPlayedText != null)
            turnsPlayedText.text = model.TotalTurnsPlayed.ToString();

        RefreshColorCountSlots(model);
    }



    private List<int> BuildSortedPlayers(GameModel model)
    {
        List<int> result = new List<int>(model.NumberOfPlayers);
        for (int i = 0; i < model.NumberOfPlayers; i++)
            result.Add(i);

        // Sort by score first, then completed rows as the tie-breaker, then player index for stable ordering.
        result.Sort(delegate (int left, int right)
        {
            int scoreCompare = model.Players[right].Score.CompareTo(model.Players[left].Score);
            if (scoreCompare != 0) return scoreCompare;

            //For ties
            int rowsCompare = model.Players[right].CompletedRows.CompareTo(model.Players[left].CompletedRows);
            if (rowsCompare != 0) return rowsCompare;

            return left.CompareTo(right);
        });
        Debug.Log("Player ranking order: " + string.Join(", ", result));
        return result;
    }

    private void PopulatePlayerSlot(PlayerStatsSlotUI slot, PlayerModel player, Sprite portrait, int rankingIndex)
    {
        EndGameBonusBreakdown bonus = player.EndGameBonusBreakdown;
        int placementPoints = player.TotalPlacementPoints;

        if (slot.PortraitImage != null)
            slot.PortraitImage.sprite = portrait;
        if (slot.NameText != null)
            slot.NameText.text = player.PlayerName;
        if (slot.Ranking != null)
            slot.Ranking.text = GetPlayerRanking(rankingIndex);
        if (slot.ScoreText != null)
            slot.ScoreText.text = player.Score.ToString();
        if (slot.BonusRowsText != null)
            slot.BonusRowsText.text = bonus.RowPoints.ToString();
        if (slot.BonusColumnsText != null)
            slot.BonusColumnsText.text = bonus.ColumnPoints.ToString();
        if (slot.BonusColorsText != null)
            slot.BonusColorsText.text = bonus.ColorPoints.ToString();
        if (slot.PlacementPointsText != null)
            slot.PlacementPointsText.text = placementPoints.ToString();
        if (slot.TotalPenaltyText != null)
            slot.TotalPenaltyText.text = player.TotalPenaltyPoints.ToString();
    }

    private string GetPlayerRanking(int rankingIndex)
    {
        int placement = rankingIndex + 1;
        
        switch (placement)
        {
            case 1:
                return placement + "st";
            case 2:
                return placement + "nd";
            case 3:
                return placement + "rd";
            default:
                return placement + "th";
        }
    }

    private void RefreshColorCountSlots(GameModel model)
    {
        if (colorCountSlots == null)
            return;

        for (int i = 0; i < colorCountSlots.Length; i++)
        {
            ColorCountSlotUI slot = colorCountSlots[i];
            if (slot.CountText != null)
                slot.CountText.text = model.GetFactoryFillColorCount(slot.Color).ToString();

            if (slot.BackgroundImage != null)
                slot.BackgroundImage.sprite = flowerSpriteData != null ? flowerSpriteData.GetSprite(slot.Color) : null;
        }
    }

    private void OnMainMenuClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("START");
    }

    private void OnPlayAgainClicked()
    {
        Time.timeScale = 1f;

        if (setupData != null)
        {
            // Clear load-mode flags before replaying, otherwise the new match could accidentally boot from the old save snapshot.
            setupData.IsLoadingFromSave = false;
            setupData.SavedGameJson = null;
            setupData.SaveRuntimeSnapshot();
            SceneManager.LoadScene(setupData.GetSceneForPlayerCount());
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnExitGameClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ClearView()
    {
        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (playerSlots[i].Root != null)
                playerSlots[i].Root.SetActive(false);
        }

        if (roundsPlayedText != null)
            roundsPlayedText.text = "0";
        if (turnsPlayedText != null)
            turnsPlayedText.text = "0";

        if (colorCountSlots != null)
        {
            for (int i = 0; i < colorCountSlots.Length; i++)
            {
                if (colorCountSlots[i].CountText != null)
                    colorCountSlots[i].CountText.text = "0";

                if (colorCountSlots[i].BackgroundImage != null)
                    colorCountSlots[i].BackgroundImage.sprite = flowerSpriteData != null ? flowerSpriteData.GetSprite(colorCountSlots[i].Color) : null;
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        GameResources resources = GameResources.Instance;
        bool hasSharedSetupData = resources != null && resources.GameSetupData != null;
        bool hasSharedMainMenu = resources != null && resources.MainMenuControllerRef != null;
        bool hasSharedSpriteData = resources != null && resources.SpriteData != null;

        if (setupData == null && !hasSharedSetupData)
            Debug.LogWarning("[EndGameMenuController] GameSetupData reference is missing.", this);

        if (flowerSpriteData == null && !hasSharedSpriteData)
            Debug.LogWarning("[EndGameMenuController] FlowerSpriteData reference is missing.", this);

        if (mainMenuController == null && !hasSharedMainMenu)
            Debug.LogWarning("[EndGameMenuController] MainMenuController reference is missing.", this);

        if (playerSlots == null || playerSlots.Length == 0)
            Debug.LogWarning("[EndGameMenuController] Player slots are not assigned.", this);

        if (colorCountSlots == null || colorCountSlots.Length == 0)
            Debug.LogWarning("[EndGameMenuController] Color count slots are not assigned.", this);
    }
#endif
}