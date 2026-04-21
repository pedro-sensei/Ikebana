using System;
using System.Collections.Generic;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.0.1 (March 2026)    =^..^=    =^..^=
//                    Last Update 22/03/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

/// <summary>
/// Standard player grid model for Ikebana. It has a fixed pattern of colors and tracks which cells are occupied.
/// The standard Ikebana pattern defines the fixed positions of each color per row.
/// Each row has the 5 colors in a rotated order.
/// When a flower is placed, points are scored based on adjacency.
/// Each player can have a different pattern (loaded from GameConfig by player index).
/// </summary>
[Serializable]
public class PlayerGridModel
{
    #region FIELDS AND PROPERTIES
    // grid[row, col] = true if occupied
    private bool[,] _occupied;

    // colorAt[row, col] = color assigned to that cell (fixed pattern per player)
    private FlowerColor[,] _pattern;

    public int GetColorArray { get; internal set; }
    #endregion
    #region CONSTRUCTORS AND INITIALIZATION

    /// <summary>
    /// Initializes an empty grid with the pattern assigned to the given player index.
    /// Player 0 uses GRID_COLOR_PATTERN1, player 1 uses PATTERN2, etc.
    /// </summary>
    public PlayerGridModel(int playerIndex)
    {
        int size = GameConfig.GRID_SIZE;
        _occupied = new bool[size, size];
        _pattern = BuildPattern(playerIndex);
    }

    /// <summary>
    /// Constructor to initialize the grid from a saved state represented as an array of color codes.
    /// -1 for empty cells. Uses the pattern for the given player index.
    /// </summary>
    public PlayerGridModel(int playerIndex, int[] colorCodeGridArray)
    {
        int size = GameConfig.GRID_SIZE;
        _occupied = new bool[size, size];
        _pattern = BuildPattern(playerIndex);

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                int index = r * size + c;
                _occupied[r, c] = colorCodeGridArray[index] != -1;
            }
        }
    }

    /// <summary>
    /// Constructor with custom pattern and occupation state, useful for cloning or advanced initialization.
    /// </summary>
    public PlayerGridModel(FlowerColor[,] customPattern, bool[,] occupationState)
    {
        int size = GameConfig.GRID_SIZE;
        _pattern = new FlowerColor[size, size];
        _occupied = new bool[size, size];
        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                _pattern[r, c] = customPattern[r, c];
                _occupied[r, c] = occupationState[r, c];
            }
        }
    }

    /// <summary>
    /// Converts the flat int[] pattern from GameConfig into a FlowerColor[,] grid.
    /// </summary>
    private static FlowerColor[,] BuildPattern(int playerIndex)
    {
        int size = GameConfig.GRID_SIZE;
        int[] flat = GameConfig.GetGridPattern(playerIndex);
        FlowerColor[,] result = new FlowerColor[size, size];

        for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
                result[r, c] = (FlowerColor)flat[r * size + c];

        return result;
    }

    #endregion
    #region GAME LOGIC
    

    public event Action<GridPlacementResult> OnflowerPlacedInGrid;


    /// <summary>
    /// Gets the column where a color should be placed in a given row
    /// </summary>
    public int GetColumnForColor(int row, FlowerColor color)
    {
        for (int c = 0; c < GameConfig.GRID_SIZE; c++)
        {
            if (_pattern[row, c] == color)
                return c;
        }
        return -1; // No debería pasar
    }

    /// <summary>
    /// Checks if a color is already placed in a row
    /// </summary>
    public bool IsColorPlacedInRow(int row, FlowerColor color)
    {
        int col = GetColumnForColor(row, color);
        return col >= 0 && _occupied[row, col];
    }

    /// <summary>
    /// Places a flower in the grid (scoring phase) and calculates the points
    /// </summary>
    public GridPlacementResult Placeflower(int row, FlowerColor color)
    {
        int col = GetColumnForColor(row, color);
        _occupied[row, col] = true;

        int points = CalculatePoints(row, col);

        GridPlacementResult result = new GridPlacementResult
        {
            Row = row,
            Column = col,
            Color = color,
            PointsScored = points
        };

        //NOTIFY that a flower has been placed in the grid, so the UI can update and show animations
        //Includes the scoring result for that placement,
        OnflowerPlacedInGrid?.Invoke(result);
        return result;
    }

    /// <summary>
    /// Calculates the points for placing a flower based on horizontal and vertical adjacency.
    /// </summary>
    private int CalculatePoints(int row, int col)
    {
        int horizontalCount = 1;
        int verticalCount = 1;

        // Count horizontal adjacent flowers to the left
        for (int c = col - 1; c >= 0; c--)
        {
            if (_occupied[row, c]) horizontalCount++;
            else break;
        }
        // Count horizontal adjacent flowers to the right
        for (int c = col + 1; c < GameConfig.GRID_SIZE; c++)
        {
            if (_occupied[row, c]) horizontalCount++;
            else break;
        }

        //  Count vertical adjacent flowers upwards
        for (int r = row - 1; r >= 0; r--)
        {
            if (_occupied[r, col]) verticalCount++;
            else break;
        }
        // Count vertical adjacent flowers downwards
        for (int r = row + 1; r < GameConfig.GRID_SIZE; r++)
        {
            if (_occupied[r, col]) verticalCount++;
            else break;
        }

        // If only counts 1 in both directions, it's worth 1 point
        if (horizontalCount == 1 && verticalCount == 1)
            return 1;

        int points = 0;
        if (horizontalCount > 1) points += horizontalCount;
        if (verticalCount > 1) points += verticalCount;

        return points;
    }

   

    public bool IsOccupied(int row, int col) => _occupied[row, col];
    public FlowerColor GetColorAt(int row, int col) => _pattern[row, col];

    /// <summary>
    /// Checks if any row in the grid is complete (end game condition)
    /// </summary>
    public bool HasCompleteRow()
    {
        for (int r = 0; r < GameConfig.GRID_SIZE; r++)
        {
            bool complete = true;
            for (int c = 0; c < GameConfig.GRID_SIZE; c++)
            {
                if (!_occupied[r, c]) { complete = false; break; }
            }
            if (complete) return true;
        }
        return false;
    }


    //Added for AI decision making:
    public int GetSize() => GameConfig.GRID_SIZE;
    public int GetNumflowersInColumn(int col)
    {
        int count = 0;
        for (int r = 0; r < GameConfig.GRID_SIZE; r++)
        {
            if (_occupied[r, col]) count++;
        }
        return count;
    }

    public int GetNumflowersInRow(int row)
    {
        int count = 0;
        for (int c = 0; c < GameConfig.GRID_SIZE; c++)
        {
            if (_occupied[row, c]) count++;
        }
        return count;
    }
    public int GetColumnForColorInRow(int row, FlowerColor color)
    {
        for (int c = 0; c < GameConfig.GRID_SIZE; c++)
        {
            if (_pattern[row, c] == color)
                return c;
        }
        return -1; // No debería pasar
    }

    public int GetNumflowersForColorInGrid(FlowerColor color)
    {
        int count = 0;
        for (int r = 0; r < GameConfig.GRID_SIZE; r++)
        {
            for (int c = 0; c < GameConfig.GRID_SIZE; c++)
            {
                if (_pattern[r, c] == color && _occupied[r, c])
                    count++;
            }
        }
        return count;
    }

    public bool[] GetOccupationPattern()
    {
        bool[] occupationPattern = new bool[GameConfig.GRID_SIZE * GameConfig.GRID_SIZE];
        for (int r = 0; r < GameConfig.GRID_SIZE; r++)
        {
            for (int c = 0; c < GameConfig.GRID_SIZE; c++)
            {
                occupationPattern[r * GameConfig.GRID_SIZE + c] = _occupied[r, c];
            }
        }
        return occupationPattern;
    }

    internal PlayerGridModel Clone()
    {
        return new PlayerGridModel(_pattern, _occupied);
    }

    internal int GetCompletedRows()
    {
        int completedRows = 0;
        for (int r = 0; r < GameConfig.GRID_SIZE; r++)
        {
            bool complete = true;
            for (int c = 0; c < GameConfig.GRID_SIZE; c++)
            {
                if (!_occupied[r, c]) { complete = false; break; }
            }
            if (complete) completedRows++; // Increment the number of completed rows
        }
        return completedRows;
    }

    #endregion
    #region HELPER METHODS
    internal void Clear()
    {
        _occupied = new bool[GameConfig.GRID_SIZE, GameConfig.GRID_SIZE];
    }

    #endregion
}
