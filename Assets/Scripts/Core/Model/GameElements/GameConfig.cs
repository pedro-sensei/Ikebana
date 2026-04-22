using System.Collections.Generic;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

public static class GameConfig
{
    #region DEFAULTS

    //DEFAULT.

    private static readonly int[] DEFAULT_PLACEMENT_CAPACITIES = { 1, 2, 3, 4, 5 };
    private static readonly int[] DEFAULT_PENALTY_LINE_VALUES  = { -1, -1, -2, -2, -3, -3, -4 };
    private static readonly int[] DEFAULT_GRID_PATTERN =
    {
        0, 1, 2, 3, 4,
        4, 0, 1, 2, 3,
        3, 4, 0, 1, 2,
        2, 3, 4, 0, 1,
        1, 2, 3, 4, 0
    };

    private static readonly FlowerColor[] DEFAULT_FLOWER_COLORS =
    {
        FlowerColor.Blue, FlowerColor.Yellow, FlowerColor.Red,
        FlowerColor.Pink, FlowerColor.Green
    };
#endregion
    #region PROPERTIES AND FIELDS

    private static GameConfigData data;
    private static bool isInitialized = false;

    public static int NUM_PLAYERS
    {
        get { return isInitialized ? data.NumPlayers : 2; }
    }

    // True when the SO defines a non-standard setup (custom factories, starting flowers, etc.)
    public static bool IS_NON_STANDARD_SETUP
    {
        get { return isInitialized ? data.NonStandardSetups : false; }
    }

    // Flowers of each color placed in the bag at game start
    public static int COUNT_PER_COLOR
    {
        get { return isInitialized ? data.CountPerColor : 20; }
    }

    // Number of distinct flower colors in this variant
    public static int NUM_COLORS
    {
        get { return isInitialized ? data.NumColors : 5; }
    }

    // Total flowers in a new bag (COUNT_PER_COLOR * NUM_COLORS)
    public static int TOTAL_FLOWERS
    {
        get { return COUNT_PER_COLOR * NUM_COLORS; }
    }

    // Ordered list of flower colors used in this variant
    public static FlowerColor[] FLOWER_COLORS
    {
        get
        {
            if (isInitialized && data.FlowerColors != null && data.FlowerColors.Length > 0)
                return data.FlowerColors;
            return DEFAULT_FLOWER_COLORS;
        }
    }

    // Number of factory displays (excludes central). Standard = numPlayers * 2 + 1.
    public static int NUM_DISPLAYS
    {
        get { return isInitialized ? data.NumFactories : GetDisplayCount(NUM_PLAYERS); }
    }

    // Flowers drawn into each factory display at the start of a round
    public static int FLOWERS_PER_DISPLAY
    {
        get { return isInitialized ? data.FlowersPerDisplay : 4; }
    }

    // Derives the standard factory count from the number of players
    public static int GetDisplayCount(int numberOfPlayers)
    {
        return numberOfPlayers * 2 + 1;
    }

    // Wall grid size (NxN)
    public static int GRID_SIZE
    {
        get { return isInitialized ? data.GridSize : 5; }
    }

    ///TODO: ALLOW FOR SPECIAL NON-SQUARE GRIDS IN THE FUTURE
    ///THIS WOULD REQUIRE CHANGING THE GRID STRUCTURE 

    // Number of placement (pattern) lines on each player board
    public static int NUM_PLACEMENT_LINES
    {
        get { return isInitialized ? data.NumPlacementLines : 5; }
    }

    // Capacity of each placement line (index 0 = line 0)
    public static int[] PLACEMENT_LINE_CAPACITIES
    {
        get
        {
            if (isInitialized && data.PlacementLineCapacities != null && data.PlacementLineCapacities.Length > 0)
                return data.PlacementLineCapacities;
            return DEFAULT_PLACEMENT_CAPACITIES;
        }
    }

    // Maximum slots in the penalty (floor) line
    public static int PENALTY_LINE_CAPACITY
    {
        get { return isInitialized ? data.PenaltyLineCapacity : 7; }
    }

    // Negative point value of each penalty slot (index 0 = first slot)
    public static int[] PENALTY_LINE_VALUES
    {
        get
        {
            if (isInitialized && data.PenaltyLineValues != null && data.PenaltyLineValues.Length > 0)
                return data.PenaltyLineValues;
            return DEFAULT_PENALTY_LINE_VALUES;
        }
    }


    // Bonus for completing a full horizontal row on the wall
    public static int COMPLETE_LINE_SCORING_BONUS
    {
        get { return isInitialized ? data.CompleteLineScoringBonus : 2; }
    }

    // Bonus for completing a full vertical column on the wall
    public static int COMPLETE_COLUMN_SCORING_BONUS
    {
        get { return isInitialized ? data.CompleteColumnScoringBonus : 7; }
    }

    // Bonus for placing all 5 flowers of the same color on the wall
    public static int COMPLETE_COLOR_SCORING_BONUS
    {
        get { return isInitialized ? data.CompleteColorScoringBonus : 10; }
    }

    // Bonus for completing a full set of colors (not standard)
    public static int COLORS_SET_SCORING_BONUS
    {
        get { return isInitialized ? data.ColorsSetScoringBonus : 0; }
    }

    // Special Scoring Bonuses (variant rules)
    public static int SPECIAL_SCORING_BONUS_A
    {
        get { return isInitialized ? data.SpecialScoringBonusA : 0; }
    }

    public static int SPECIAL_SCORING_BONUS_B
    {
        get { return isInitialized ? data.SpecialScoringBonusB : 0; }
    }

    public static int SPECIAL_SCORING_BONUS_C
    {
        get { return isInitialized ? data.SpecialScoringBonusC : 0; }
    }

    public static int SPECIAL_SCORING_BONUS_D
    {
        get { return isInitialized ? data.SpecialScoringBonusD : 0; }
    }

    //Non-Standard Setup:  Overrides

    // Extra color flowers to seed in the bag at game start (non-standard only)
    public static KeyValuePair<FlowerColor, int> STARTING_BAG_COLORS
    {
        get
        {
            if (isInitialized) return data.StartingBagColors;
            return new KeyValuePair<FlowerColor, int>(FlowerColor.Red, 0);
        }
    }

    // Extra color flowers to seed in the discard at game start (non-standard only)
    public static KeyValuePair<FlowerColor, int> STARTING_DISCARD_COLORS
    {
        get
        {
            if (isInitialized) return data.StartingDiscardColors;
            return new KeyValuePair<FlowerColor, int>(FlowerColor.Red, 0);
        }
    }

    // Extra color flowers to seed in the central display at game start (non-standard only)
    public static KeyValuePair<FlowerColor, int> STARTING_CENTRAL_COLORS
    {
        get
        {
            if (isInitialized) return data.StartingCentralColors;
            return new KeyValuePair<FlowerColor, int>(FlowerColor.Red, 0);
        }
    }

    // Pre-configured display contents for scripted levels. Empty array for standard play.
    public static DisplayConfig[] DISPLAY_SETUPS
    {
        get
        {
            if (isInitialized && data.DisplaySetups != null)
                return data.DisplaySetups;
            return new DisplayConfig[0];
        }
    }

    // Grid Color Patterns
    //  Encoded as int[] of length GridSize*GridSize.
    //  Index = row * GridSize + col, value = color index for that cell.
    //  Up to 4 patterns (one per player).

    public static int[] GRID_COLOR_PATTERN1
    {
        get
        {
            if (isInitialized && data.GridColorPattern1 != null && data.GridColorPattern1.Length > 0)
                return data.GridColorPattern1;
            return DEFAULT_GRID_PATTERN;
        }
    }

    public static int[] GRID_COLOR_PATTERN2
    {
        get
        {
            if (isInitialized && data.GridColorPattern2 != null && data.GridColorPattern2.Length > 0)
                return data.GridColorPattern2;
            return DEFAULT_GRID_PATTERN;
        }
    }

    public static int[] GRID_COLOR_PATTERN3
    {
        get
        {
            if (isInitialized && data.GridColorPattern3 != null && data.GridColorPattern3.Length > 0)
                return data.GridColorPattern3;
            return DEFAULT_GRID_PATTERN;
        }
    }

    public static int[] GRID_COLOR_PATTERN4
    {
        get
        {
            if (isInitialized && data.GridColorPattern4 != null && data.GridColorPattern4.Length > 0)
                return data.GridColorPattern4;
            return DEFAULT_GRID_PATTERN;
        }
    }

    #endregion
    #region CONSTRUCTION & INITIALIZATION

    /// Loads configuration from a GameConfigData ScriptableObject
    /// Call this once before creating the GameModel.
 
    public static void Initialize(GameConfigData configData, int playerCount)
    {
        data = configData;
        isInitialized = data != null;

        if (isInitialized)
        {
            data.NumPlayers   = playerCount;
            data.NumFactories = GetDisplayCount(playerCount);
        }
    }

    /// Used by simulators / genetic algorithms that already have
    /// NumPlayers set correctly inside the SO.
    public static void Initialize(GameConfigData configData)
    {
        data = configData;
        isInitialized = data != null;
    }

    #endregion
    #region HELPER METHODS

    public static void ResetToDefaults()
    {
        data = null;
        isInitialized = false;
    }

    public static int[] GetGridPattern(int playerIndex)
    {
        switch (playerIndex % 4)
        {
            case 0: return GRID_COLOR_PATTERN1;
            case 1: return GRID_COLOR_PATTERN2;
            case 2: return GRID_COLOR_PATTERN3;
            case 3: return GRID_COLOR_PATTERN4;
            default: return GRID_COLOR_PATTERN1;
        }
    }

    public static GameConfigSnapshot CreateSnapshot()
    {
        int size = GRID_SIZE;
        int[][,] wallPatterns = new int[4][,];
        for (int p = 0; p < 4; p++)
        {
            int[] flat = GetGridPattern(p);
            int[,] grid = new int[size, size];
            for (int r = 0; r < size; r++)
                for (int c = 0; c < size; c++)
                    grid[r, c] = flat[r * size + c];
            wallPatterns[p] = grid;
        }

        return new GameConfigSnapshot(
            numColors:              NUM_COLORS,
            flowersPerColor:          COUNT_PER_COLOR,
            flowersPerFactory:        FLOWERS_PER_DISPLAY,
            numFactories:           NUM_DISPLAYS,
            gridSize:               size,
            numPlacementLines:      NUM_PLACEMENT_LINES,
            placementLineCapacities:(int[])PLACEMENT_LINE_CAPACITIES.Clone(),
            penaltyCapacity:        PENALTY_LINE_CAPACITY,
            penaltyValues:          (int[])PENALTY_LINE_VALUES.Clone(),
            bonusRow:               COMPLETE_LINE_SCORING_BONUS,
            bonusColumn:            COMPLETE_COLUMN_SCORING_BONUS,
            bonusColor:             COMPLETE_COLOR_SCORING_BONUS,
            wallPatterns:           wallPatterns
        );
    }
    #endregion
}

#region GAME CONFIG SNAPSHOT

public class GameConfigSnapshot
{
    // flower / Bag
    public readonly int NumColors;
    public readonly int flowersPerColor;
    public readonly int Totalflowers;

    // Displays
    public readonly int flowersPerFactory;
    public readonly int NumFactories;

    // Board
    public readonly int GridSize;
    public readonly int NumPlacementLines;
    public readonly int[] PlacementLineCapacities;

    // Penalty
    public readonly int PenaltyCapacity;
    public readonly int[] PenaltyValues;

    // Scoring bonuses
    public readonly int BonusRow;
    public readonly int BonusColumn;
    public readonly int BonusColor;

    // Wall pattern per player 
    public readonly int[][,] WallPatterns;

    public GameConfigSnapshot(
        int numColors, int flowersPerColor, int flowersPerFactory, int numFactories,
        int gridSize, int numPlacementLines, int[] placementLineCapacities,
        int penaltyCapacity, int[] penaltyValues,
        int bonusRow, int bonusColumn, int bonusColor,
        int[][,] wallPatterns)
    {
        NumColors = numColors;
        this.flowersPerColor = flowersPerColor;
        Totalflowers = numColors * flowersPerColor;
        this.flowersPerFactory = flowersPerFactory;
        NumFactories = numFactories;
        GridSize = gridSize;
        NumPlacementLines = numPlacementLines;
        PlacementLineCapacities = placementLineCapacities;
        PenaltyCapacity = penaltyCapacity;
        PenaltyValues = penaltyValues;
        BonusRow = bonusRow;
        BonusColumn = bonusColumn;
        BonusColor = bonusColor;
        WallPatterns = wallPatterns;
    }
    public int GetWallColumn(int playerIndex, int row, int colorIndex)
    {
        int[,] pattern = WallPatterns[playerIndex % WallPatterns.Length];
        for (int c = 0; c < GridSize; c++)
        {
            if (pattern[row, c] == colorIndex)
                return c;
        }
        return 0;
    }

    // Returns penalty value at the given slot index, or 0 if out of range
    public int GetPenaltyValue(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < PenaltyValues.Length)
            return PenaltyValues[slotIndex];
        return 0;
    }
}

#endregion