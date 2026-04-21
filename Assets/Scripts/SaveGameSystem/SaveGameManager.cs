using System.Collections.Generic;
using System.IO;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


// Manages saving and loading the game state to JSON files.
// Save files are stored in Application.persistentDataPath/Saves/.

public class SaveGameManager : MonoBehaviour
{
    public static SaveGameManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("GameSetupData SO carry data into the game scene.")]
    [SerializeField] private GameSetupData setupData;

    [Tooltip("Portrait repository")]
    [SerializeField] private PortraitRepository portraitDatabase;

    private const string SAVE_FOLDER = "Saves";
    private const string FILE_EXT = ".json";

    private string SaveDirectory
    {
        get { return Path.Combine(Application.persistentDataPath, SAVE_FOLDER); }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

       
        if (transform.parent != null)
            transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        if (!Directory.Exists(SaveDirectory))
            Directory.CreateDirectory(SaveDirectory);
    }


    public void SaveCurrentGame(string slotName = null)
    {
        GameController gc = GameController.Instance;
        if (gc == null || gc.Model == null)
        {
            Debug.LogWarning("[SaveGameManager] No active game to save.");
            return;
        }

        SerializedGameState state = SerializeModel(gc.Model, gc.PlayerSlots);
        string json = JsonUtility.ToJson(state, true);

        if (string.IsNullOrEmpty(slotName))
            slotName = "Save_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

        string path = Path.Combine(SaveDirectory, slotName + FILE_EXT);
        File.WriteAllText(path, json);

        Debug.Log("[SaveGameManager] Game saved to: " + path);
    }


    // Returns the list of available save file names.

    public string[] GetSaveFileList()
    {
        if (!Directory.Exists(SaveDirectory)) return new string[0];

        string[] files = Directory.GetFiles(SaveDirectory, "*" + FILE_EXT);
        string[] names = new string[files.Length];
        for (int i = 0; i < files.Length; i++)
            names[i] = Path.GetFileNameWithoutExtension(files[i]);
        return names;
    }

    // Loads a save file by name and writes the data into GAmeSetupData SO
    // so the game scene can reconstruct state on Awake.

    public bool LoadGame(string slotName)
    {
        string path = Path.Combine(SaveDirectory, slotName + FILE_EXT);
        if (!File.Exists(path))
        {
            Debug.LogWarning("[SaveGameManager] Save file not found: " + path);
            return false;
        }

        string json = File.ReadAllText(path);
        SerializedGameState state = JsonUtility.FromJson<SerializedGameState>(json);

        if (state == null || state.Players == null || state.Players.Length == 0)
        {
            Debug.LogWarning("[SaveGameManager] Invalid save data.");
            return false;
        }

        // Write into setupData so the game scene picks it up
        setupData.PlayerCount = state.Players.Length;
        setupData.IsLoadingFromSave = true;
        setupData.SavedGameJson = json;

        // Rebuild PlayerSlots from saved player data
        setupData.PlayerSlots = new PlayerSlotConfig[state.Players.Length];
        for (int i = 0; i < state.Players.Length; i++)
        {
            SerializedPlayer sp = state.Players[i];
            PlayerSlotConfig cfg = new PlayerSlotConfig();
            cfg.PlayerName = sp.Name;
            cfg.PlayerType = sp.IsAI ? PlayerType.AI : PlayerType.Human;
            cfg.AIBrain = (AIBrainType)sp.AIBrainTypeIndex;
            bool hasSavedColor = sp.PlayerColorA > 0f ||
                                 sp.PlayerColorR != 0f || sp.PlayerColorG != 0f || sp.PlayerColorB != 0f;
            if (hasSavedColor)
                cfg.PlayerColor = new Color(sp.PlayerColorR, sp.PlayerColorG, sp.PlayerColorB,
                                            sp.PlayerColorA <= 0f ? 1f : sp.PlayerColorA);

            if (portraitDatabase != null)
                cfg.Portrait = portraitDatabase.GetPortrait(sp.PortraitIndex);

            setupData.PlayerSlots[i] = cfg;
        }

        setupData.SaveRuntimeSnapshot();
        Debug.Log("[SaveGameManager] Loaded save: " + slotName);
        return true;
    }

    
    // Deletes a save file by name. 
    // Returns true if the file existed and was deleted.
    public bool DeleteSave(string slotName)
    {
        string path = Path.Combine(SaveDirectory, slotName + FILE_EXT);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        Debug.Log("[SaveGameManager] Deleted save: " + path);
        return true;
    }

   
    // Converts a GameModel into SerializedGameState
    private SerializedGameState SerializeModel(GameModel model, PlayerSlotConfig[] slots)
    {
        SerializedGameState state = new SerializedGameState();

        // Global
        state.IsGameOver = model.IsGameOver;
        state.StartingPlayerIndex = model.StartingPlayerIndex;
        state.CurrentPlayerIndex = model.CurrentPlayerIndex;
        state.CurrentRound = model.CurrentRound;
        state.TurnNumber = model.TurnNumber;
        state.CurrentPhase = (int)model.CurrentPhase;

        // Bag
        state.Bag = SerializeFlowerList(model.Bag.GetAllFlowers());

        // Discard
        state.Discard = SerializeFlowerList(model.Discard.GetAllFlowers());

        // Central display
        state.CentralDisplay = SerializeFlowerList(model.CentralDisplay.flowers);
        state.CentralHasFirstPlayerToken = model.CentralDisplay.HasFirstPlayerToken;

        // Displays
        state.FactoryDisplays = new int[model.FactoryDisplays.Length][];
        for (int f = 0; f < model.FactoryDisplays.Length; f++)
            state.FactoryDisplays[f] = SerializeFlowerList(model.FactoryDisplays[f].flowers);

        // Players
        state.Players = new SerializedPlayer[model.NumberOfPlayers];
        for (int p = 0; p < model.NumberOfPlayers; p++)
        {
            PlayerModel pm = model.Players[p];
            SerializedPlayer sp = new SerializedPlayer();
            sp.Name = pm.PlayerName;
            sp.Score = pm.Score;
            sp.PlayerIndex = pm.PlayerIndex;
            sp.HasFirstPlayerToken = pm.HasFirstPlayerToken;

            // AI settings from the slot config
            if (slots != null && p < slots.Length)
            {
                sp.IsAI = slots[p].PlayerType == PlayerType.AI;
                sp.AIBrainTypeIndex = (int)slots[p].AIBrain;
                sp.PortraitIndex = FindPortraitIndex(slots[p].Portrait);
                sp.PlayerColorR = slots[p].PlayerColor.r;
                sp.PlayerColorG = slots[p].PlayerColor.g;
                sp.PlayerColorB = slots[p].PlayerColor.b;
                sp.PlayerColorA = slots[p].PlayerColor.a;
            }

            // Grid (flat array, -1 for empty)
            PlayerGridModel grid = pm.Grid;
            int size = GameConfig.GRID_SIZE;
            sp.Grid = new int[size * size];
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    if (grid.IsOccupied(r, c))
                        sp.Grid[r * size + c] = (int)grid.GetColorAt(r, c);
                    else
                        sp.Grid[r * size + c] = -1;
                }
            }

            // Placement lines
            sp.PlacementLines = new int[pm.PlacementLines.Length][];
            for (int l = 0; l < pm.PlacementLines.Length; l++)
            {
                PlacementLineModel line = pm.PlacementLines[l];
                sp.PlacementLines[l] = new int[line.Count];
                for (int t = 0; t < line.Count; t++)
                {
                    FlowerPiece flower = line.GetFlowerAt(t);
                    sp.PlacementLines[l][t] = (int)flower.Color;
                }
            }

            // Penalty line
            sp.PenaltyLine = new int[pm.PenaltyLine.Count];
            for (int t = 0; t < pm.PenaltyLine.Count; t++)
            {
                FlowerPiece flower = pm.PenaltyLine.GetFlowerAt(t);
                sp.PenaltyLine[t] = (int)flower.Color;
            }

            state.Players[p] = sp;
        }

        return state;
    }

    private int[] SerializeFlowerList(IReadOnlyList<FlowerPiece> flowers)
    {
        if (flowers == null) return new int[0];
        int[] arr = new int[flowers.Count];
        for (int i = 0; i < flowers.Count; i++)
            arr[i] = (int)flowers[i].Color;
        return arr;
    }

    private int FindPortraitIndex(Sprite portrait)
    {
        if (portrait == null || portraitDatabase == null) return 0;
        for (int i = 0; i < portraitDatabase.Count; i++)
        {
            if (portraitDatabase.GetPortrait(i) == portrait)
                return i;
        }
        return 0;
    }
}
