using UnityEngine;


//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


// ScriptableObject that carries game setup from the New Game menu
// into the gameplay scene. 

[CreateAssetMenu(fileName = "GameSetupData", menuName = "Ikebana/Game Setup Data")]
public class GameSetupData : ScriptableObject
{
    #region SNAPSHOT
    private class Snapshot
    {
        public int PlayerCount;
        public PlayerSlotConfig[] PlayerSlots;
        public bool IsLoadingFromSave;
        public string SavedGameJson;
    }

    private static Snapshot s_runtimeSnapshot;
    #endregion

    #region FIELDS AND PROPERTIES
    [Header("Player Count")]
    [Range(2, 4)]
    public int PlayerCount = 2;

    [Header("Player Slots")]
    public PlayerSlotConfig[] PlayerSlots;

    [Header("Scene Names")]
    [Tooltip("Scene for 2-player game")]
    public string Scene2Players = "2_PLAYER";
    [Tooltip("Scene for 3-player game")]
    public string Scene3Players = "3_PLAYER";
    [Tooltip("Scene for 4-player game")]
    public string Scene4Players = "4_PLAYER";
    [Header("Load Game")]
    [Tooltip("When true the game scene will load from SavedGameState.")]
    public bool IsLoadingFromSave = false;
    public string SavedGameJson;
    #endregion

    #region METHODS
    public void SaveRuntimeSnapshot()
    {
        var snap = new Snapshot
        {
            PlayerCount       = PlayerCount,
            IsLoadingFromSave = IsLoadingFromSave,
            SavedGameJson     = SavedGameJson
        };

        if (PlayerSlots != null)
        {
            snap.PlayerSlots = new PlayerSlotConfig[PlayerSlots.Length];
            for (int i = 0; i < PlayerSlots.Length; i++)
            {
                var src = PlayerSlots[i];
                snap.PlayerSlots[i] = new PlayerSlotConfig
                {
                    PlayerName        = src.PlayerName,
                    Portrait          = src.Portrait,
                    PlayerColor       = src.PlayerColor,
                    PlayerType        = src.PlayerType,
                    AIBrain           = src.AIBrain,
                    OptimizerWeights  = src.OptimizerWeights,
                    EmilyEarlyWeights = src.EmilyEarlyWeights,
                    EmilyMidWeights   = src.EmilyMidWeights,
                    EmilyLateWeights  = src.EmilyLateWeights,
                    MinMaxDepth       = src.MinMaxDepth,
                    MinMaxTimeLimitMs = src.MinMaxTimeLimitMs,
                    MinMaxEvaluator   = src.MinMaxEvaluator,
                    RelativeImmediateSimulationWeight = src.RelativeImmediateSimulationWeight,
                    RelativeTerminalDeltaWeight = src.RelativeTerminalDeltaWeight,
                    RelativeExpectedMoveWeight = src.RelativeExpectedMoveWeight,
                    RelativeUsePhaseAwareImmediateWeight = src.RelativeUsePhaseAwareImmediateWeight,
                    MLAgentModel      = src.MLAgentModel,
                };
            }
        }

        s_runtimeSnapshot = snap;
        Debug.Log($"[GameSetupData] Snapshot saved: {snap.PlayerCount} players.");
    }

    public bool RestoreFromSnapshot()
    {
        if (s_runtimeSnapshot == null ||
            s_runtimeSnapshot.PlayerSlots == null ||
            s_runtimeSnapshot.PlayerSlots.Length == 0)
            return false;

        PlayerCount        = s_runtimeSnapshot.PlayerCount;
        PlayerSlots        = s_runtimeSnapshot.PlayerSlots;
        IsLoadingFromSave  = s_runtimeSnapshot.IsLoadingFromSave;
        SavedGameJson      = s_runtimeSnapshot.SavedGameJson;
        Debug.Log("[GameSetupData] Restored setup from runtime snapshot.");
        return true;
    }

    // Returns the scene name that matches
    public string GetSceneForPlayerCount()
    {
        switch (PlayerCount)
        {
            case 3:  return "3_PLAYER";
            case 4:  return "4_PLAYER";
            default: return "2_PLAYER";
        }
    }

    // Resets the setup to default values (2 human players, no save).
    public void ResetToDefaults()
    {
        PlayerCount = 2;
        IsLoadingFromSave = false;
        SavedGameJson = null;

        PlayerSlots = new PlayerSlotConfig[2];
        for (int i = 0; i < 2; i++)
        {
            PlayerSlots[i] = new PlayerSlotConfig
            {
                PlayerName = "Player " + (i + 1),
                PlayerColor = Color.white,
                PlayerType = PlayerType.Human,
                AIBrain = AIBrainType.Random
            };
        }
    }

    // Resizes PlayerSlots to match PlayerCount

    public void ResizeSlots()
    {
        PlayerSlotConfig[] prev = PlayerSlots;
        PlayerSlots = new PlayerSlotConfig[PlayerCount];

        for (int i = 0; i < PlayerCount; i++)
        {
            if (prev != null && i < prev.Length)
            {
                PlayerSlots[i] = prev[i];
            }
            else
            {
                PlayerSlots[i] = new PlayerSlotConfig
                {
                    PlayerName = "Player " + (i + 1),
                    PlayerColor = Color.white,
                    PlayerType = PlayerType.Human,
                    AIBrain = AIBrainType.Random
                };
            }
        }
    }
    #endregion
}