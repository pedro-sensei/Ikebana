using System;
using System;
using System.Collections.Generic;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.0.1 (March 2026)    =^..^=    =^..^=
//                    Last Update 22/03/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=
public class PlayerModel
{
    #region FIELDS AND PROPERTIES
    public readonly int PlayerIndex;
    public readonly string PlayerName;

    private PlacementLineModel[] _placementLines;
    private PenaltyLineModel _penaltyLine;
    private PlayerGridModel _grid;
    private int _score;
    private bool _hasFirstPlayerToken;
    private int _totalPlacementPoints;
    private int _lastRoundPlacementPoints;
    private int _lastRoundPenaltyPoints;
    private int _totalPenaltyPoints;
    private List<int> _penaltyPointsByRound;
    private EndGameBonusBreakdown _endGameBonusBreakdown;


    public PlacementLineModel[] PlacementLines => _placementLines;
    public PenaltyLineModel PenaltyLine => _penaltyLine;
    public PlayerGridModel Grid => _grid;
    public bool HasCompleteRow { get; internal set; }

    public int Score => _score;
    public bool HasFirstPlayerToken => _hasFirstPlayerToken;

    public int CompletedRows => _grid.GetCompletedRows();
    public int TotalPlacementPoints => _totalPlacementPoints;
    public int LastRoundPlacementPoints => _lastRoundPlacementPoints;
    public int LastRoundPenaltyPoints => _lastRoundPenaltyPoints;
    public int TotalPenaltyPoints => _totalPenaltyPoints;
    public IReadOnlyList<int> PenaltyPointsByRound => _penaltyPointsByRound;
    public EndGameBonusBreakdown EndGameBonusBreakdown => _endGameBonusBreakdown;
    #endregion
    #region EVENTS
    public event Action<int, int> OnScoreChanged;
    #endregion
    #region CONSTRUCTORS AND INITIALIZATION
    public PlayerModel(int playerIndex, string playerName)
    {
        PlayerIndex = playerIndex;
        PlayerName = playerName;
        _score = 0;
        _hasFirstPlayerToken = false;
        _totalPlacementPoints = 0;
        _lastRoundPlacementPoints = 0;
        _lastRoundPenaltyPoints = 0;
        _totalPenaltyPoints = 0;
        _penaltyPointsByRound = new List<int>();
        _endGameBonusBreakdown = new EndGameBonusBreakdown();

        // Create placement lines with their respective capacities
        _placementLines = new PlacementLineModel[GameConfig.NUM_PLACEMENT_LINES];
        for (int i = 0; i < GameConfig.NUM_PLACEMENT_LINES; i++)
        {
            _placementLines[i] = new PlacementLineModel(i, GameConfig.PLACEMENT_LINE_CAPACITIES[i]);
        }

        _penaltyLine = new PenaltyLineModel();
        _grid = new PlayerGridModel(playerIndex);

        // Derive allowed colors per row from the grid pattern and apply to placement lines
        InitAllowedColorsFromGrid();
    }

  
    //Constructor for loading from saved state
    public PlayerModel(int index, SerializedPlayer player)
    {
        PlayerIndex = index;
        PlayerName = player.Name;
        _score = player.Score;
        _grid = new PlayerGridModel(index, player.Grid);
        HasCompleteRow = false; // This can be calculated from the grid if needed
        _hasFirstPlayerToken = player.HasFirstPlayerToken;
        _totalPlacementPoints = player.TotalPlacementPoints;
        _lastRoundPlacementPoints = 0;
        _lastRoundPenaltyPoints = 0;
        _totalPenaltyPoints = player.TotalPenaltyPoints;
        _penaltyPointsByRound = player.PenaltyPointsByRound != null
            ? new List<int>(player.PenaltyPointsByRound)
            : new List<int>();
        _endGameBonusBreakdown = new EndGameBonusBreakdown
        {
            RowPoints = player.EndGameBonusRows,
            ColumnPoints = player.EndGameBonusColumns,
            ColorPoints = player.EndGameBonusColors
        };
        _placementLines = new PlacementLineModel[GameConfig.NUM_PLACEMENT_LINES];
        for (int i = 0; i < GameConfig.NUM_PLACEMENT_LINES; i++)
        {
            _placementLines[i] = new PlacementLineModel(i, GameConfig.PLACEMENT_LINE_CAPACITIES[i]);
        }

        _penaltyLine = new PenaltyLineModel();

        for (int i = 0; i < GameConfig.NUM_PLACEMENT_LINES; i++)
        {
            List<FlowerPiece> flowerList = new List<FlowerPiece>();
            foreach (int colorCode in player.PlacementLines[i])
            {
                flowerList.Add(new FlowerPiece((FlowerColor)colorCode));
            }
            PlaceflowersInLine(i, flowerList);
        }

        if (player.PenaltyLine != null)
        {
            for (int i = 0; i < player.PenaltyLine.Length; i++)
            {
                FlowerColor color = (FlowerColor)player.PenaltyLine[i];
                if (color == FlowerColor.FirstPlayer)
                {
                    if (!_penaltyLine.HasFirstPlayerToken)
                        _penaltyLine.AddFirstPlayerToken();
                }
                else
                {
                    _penaltyLine.AddFlower(new FlowerPiece(color));
                }
            }
        }

        if (player.HasFirstPlayerToken && !_penaltyLine.HasFirstPlayerToken)
            _penaltyLine.AddFirstPlayerToken();

        InitAllowedColorsFromGrid();
    }

    #endregion
    #region GAME LOGIC
    //
    // Tries to place the given flowers in the specified placement line.
    // If the line index is invalid, all flowers are sent to the penalty line.
    // Returns PlaceResult with overflow info. Excessflowers contains flowers
    // that didn't fit in penalty and should go to discard.
    //
    public PlaceResult PlaceflowersInLine(int lineIndex, List<FlowerPiece> flowers)
    {

        if (lineIndex < 0 || lineIndex >= _placementLines.Length)
        {
            Debug.Log($"[Player:{this.PlayerName}. INVALID LINE. Sending all flowers to penalty line.");
            List<FlowerPiece> excess = _penaltyLine.AddFlowers(flowers);
            PlaceResult invalidResult = new PlaceResult
            {
                NumberOfFlowersPlaced = 0,
                OverflowFlowers = flowers,
                Excessflowers = excess
            };
            return invalidResult;
        }

        PlaceResult result = _placementLines[lineIndex].PlaceFlowers(flowers);

        // Send overflow flowers to penalty line
        if (result.OverflowFlowers.Count > 0)
        {
            List<FlowerPiece> excess = _penaltyLine.AddFlowers(result.OverflowFlowers);
            result.Excessflowers = excess;
        }
        else
        {
            result.Excessflowers = new List<FlowerPiece>();
        }

        return result;
    }

    /// <summary>
    /// Add the first player token to the player's penalty line. 
    /// This should only be called once per round.
    /// 
    /// </summary>
    public void ReceiveFirstPlayerToken()
    {
        _hasFirstPlayerToken = true;
        _penaltyLine.AddFirstPlayerToken();
    }

    /// <summary>
    /// Checks if a flower of the given color can be placed in the specified placement line.
    /// </summary>
    public bool CanPlaceInLine(int lineIndex, FlowerColor color)
    {
        if (lineIndex < 0 || lineIndex >= _placementLines.Length)
            return false;

        // Checks if the color is already placed in the grid row corresponding to this placement line
        if (_grid.IsColorPlacedInRow(lineIndex, color))
            return false;

        return _placementLines[lineIndex].CanAcceptColor(color);
    }

    /// <summary>
    /// Scoring phase at the end of the round:
    /// </summary>
    /// 
    //TODO: REVIEW
    public List<GridPlacementResult> ScoreRound(DiscardModel discard)
    {
        List<GridPlacementResult> results = new List<GridPlacementResult>();
        _lastRoundPlacementPoints = 0;
        _lastRoundPenaltyPoints = 0;

        for (int i = 0; i < _placementLines.Length; i++)
        {
            if (_placementLines[i].IsFull)
            {
                var (flowerForGrid, flowersToDiscard) = _placementLines[i].ScoreLine();

                if (flowerForGrid != null)
                {
                    GridPlacementResult gridResult = _grid.Placeflower(i, flowerForGrid.Color);
                    _score += gridResult.PointsScored;
                    _totalPlacementPoints += gridResult.PointsScored;
                    _lastRoundPlacementPoints += gridResult.PointsScored;
                    OnScoreChanged?.Invoke(PlayerIndex, _score);
                    results.Add(gridResult);
                    discard.AddRange(flowersToDiscard);
                    _placementLines[i].AddForbiddenColor(flowerForGrid.Color);
                }
            }
        }

        // Check end-game condition after all flowers are placed this round
        HasCompleteRow = _grid.HasCompleteRow();

        // Apply penalty
        int penalty = _penaltyLine.CalculatePenalty();
        _lastRoundPenaltyPoints = penalty;
        _totalPenaltyPoints += penalty;
        _penaltyPointsByRound.Add(penalty);
        _score += penalty; // penalty is negative
        if (_score < 0) _score = 0;

        // Clear penalty line
        var (penaltyflowers, returnToken) = _penaltyLine.Clear();
        discard.AddRange(penaltyflowers);

        if (returnToken)
        {
            _hasFirstPlayerToken = true;
        }
        else
        {
            _hasFirstPlayerToken = false;
        }

        OnScoreChanged?.Invoke(PlayerIndex, _score);
        return results;
    }

    public int ApplyEndGameBonus()
    {
        _endGameBonusBreakdown = BonusScoringModel.CalculateEndGameBonusBreakdown(_grid);
        int bonus = _endGameBonusBreakdown.TotalBonus;
        _score += bonus;

        //NOTIFY that the score has changed so the UI can update
        OnScoreChanged?.Invoke(PlayerIndex, _score);
        //Debug.Log($"End game bonus applied: {bonus} points");
        return bonus;
    }


    // Raises the OnScoreChanged event with the current player index and score.
    public void UpdateScore()
    {
        OnScoreChanged?.Invoke(PlayerIndex, _score);

    }
    public void ResetForNewGame()
    {
        _score = 0;
        _hasFirstPlayerToken = false;
        HasCompleteRow = false;
        _totalPlacementPoints = 0;
        _lastRoundPlacementPoints = 0;
        _lastRoundPenaltyPoints = 0;
        _totalPenaltyPoints = 0;
        _penaltyPointsByRound.Clear();
        _endGameBonusBreakdown = new EndGameBonusBreakdown();
        for (int i = 0; i < _placementLines.Length; i++)
        {
            _placementLines[i].Clear();
        }
        _penaltyLine.Clear();
        _grid.Clear();
    }
    #endregion
    #region HELPER METHODS


    // Scans the grid pattern for each row and sets the allowed colors on the
    // corresponding placement line. Colors not present in a grid row cannot be
    // placed in that row's placement line, regardless of forbidden-color state.

    private void InitAllowedColorsFromGrid()
    {
        int size = GameConfig.GRID_SIZE;
        for (int row = 0; row < _placementLines.Length && row < size; row++)
        {
            HashSet<FlowerColor> allowed = new HashSet<FlowerColor>();
            for (int col = 0; col < size; col++)
            {
                allowed.Add(_grid.GetColorAt(row, col));
            }
            _placementLines[row].SetAllowedColors(allowed);
        }
    }

    public void setGrid (PlayerGridModel grid)
    {
        _grid = grid;
    }

    public void setPlacementLines(PlacementLineModel[] placementLines)
    {
        _placementLines = placementLines;
    }

    public void setPenaltyLine(PenaltyLineModel penaltyLine)
    {
        _penaltyLine = penaltyLine;
    }

    public int GetPotentialGridPlacementScore(int row, int col, bool includeProjectedFullLines = true)
    {
        return GridScoringCalculator.CalculatePlacementPoints(this, row, col, includeProjectedFullLines);
    }

    public int GetPotentialGridPlacementScore(int row, FlowerColor color, bool includeProjectedFullLines = true)
    {
        int col = _grid.GetColumnForColor(row, color);
        if (col < 0) return 0;
        return GetPotentialGridPlacementScore(row, col, includeProjectedFullLines);
    }

    internal PlayerState SaveSnapshot()
    {
        var snap = PlayerState.Create(GameConfig.GRID_SIZE, GameConfig.PENALTY_LINE_CAPACITY);
        
        int  wallBits = 0;

        for (int row = 0; row < Grid.GetSize(); row++)
        {
            for (int col = 0; col < Grid.GetSize(); col++)
            {
                // Si la celda es true, encendemos el bit
                if (Grid.IsOccupied(row, col))
                {
                    int index = row * Grid.GetSize() + col;
                    wallBits |= (1 << index);
                }
            }
        }

        snap.WallBits = wallBits;
        snap.PenaltyCount = PenaltyLine.Count;
        snap.Score = Score;
        snap.HasFirstPlayerToken = HasFirstPlayerToken;
        snap.HasCompleteRow = HasCompleteRow;
        snap.TotalPlacementPoints = _totalPlacementPoints;
        snap.LastRoundPlacementPoints = _lastRoundPlacementPoints;
        snap.LastRoundPenaltyPoints = _lastRoundPenaltyPoints;
        snap.TotalPenaltyPoints = _totalPenaltyPoints;
        snap.EndGameBonusRows = _endGameBonusBreakdown.RowPoints;
        snap.EndGameBonusColumns = _endGameBonusBreakdown.ColumnPoints;
        snap.EndGameBonusColors = _endGameBonusBreakdown.ColorPoints;
        snap.PenaltyRoundsCount = _penaltyPointsByRound != null ? _penaltyPointsByRound.Count : 0;

        if (_penaltyPointsByRound != null && snap.PenaltyPointsByRound != null)
        {
            int penaltyRoundsCount = Mathf.Min(_penaltyPointsByRound.Count, snap.PenaltyPointsByRound.Length);
            for (int i = 0; i < penaltyRoundsCount; i++)
                snap.PenaltyPointsByRound[i] = _penaltyPointsByRound[i];
        }
        
            
            
            
        byte[] _lineColor = new byte[GameConfig.GRID_SIZE];
        for (int i = 0; i < GameConfig.GRID_SIZE; i++)
        {
            _lineColor[i] = (byte)PlacementLines[i].CurrentColor;
        }
        Array.Copy(_lineColor, snap.LineColor, GameConfig.GRID_SIZE);
        
        int[] _lineCount = new int[GameConfig.GRID_SIZE];
        for (int i = 0; i < GameConfig.GRID_SIZE; i++)
        {
            _lineCount[i] = PlacementLines[i].Count;
        }
        Array.Copy(_lineCount, snap.LineCount, GameConfig.GRID_SIZE);


        int[] _forbidden = new int[GameConfig.GRID_SIZE];
        for (int i = 0; i < GameConfig.GRID_SIZE; i++)
        {
            foreach (var color in PlacementLines[i].ForbiddenColors)
            {
                int colorIndex = (int)color;
                _forbidden[i]|= (1 << colorIndex);
            }
        }
        Array.Copy(_forbidden, snap.Forbidden, GameConfig.GRID_SIZE);

        byte[] _penalty = new byte[GameConfig.PENALTY_LINE_CAPACITY];
        for (int i = 0; i < PenaltyLine.Flowers.Count; i++)
        {
            FlowerPiece flower = PenaltyLine.Flowers[i];
            _penalty[i] = (byte)flower.Color;
        }
        Array.Copy(_penalty, snap.Penalty, GameConfig.PENALTY_LINE_CAPACITY);
        
            
        return snap;
    }

    internal void RestoreSnapshot(ref PlayerState playerState)
    {
        _score = playerState.Score;
        _hasFirstPlayerToken = playerState.HasFirstPlayerToken;
        HasCompleteRow = playerState.HasCompleteRow;
        _totalPlacementPoints = playerState.TotalPlacementPoints;
        _lastRoundPlacementPoints = playerState.LastRoundPlacementPoints;
        _lastRoundPenaltyPoints = playerState.LastRoundPenaltyPoints;
        _totalPenaltyPoints = playerState.TotalPenaltyPoints;
        _endGameBonusBreakdown = new EndGameBonusBreakdown
        {
            RowPoints = playerState.EndGameBonusRows,
            ColumnPoints = playerState.EndGameBonusColumns,
            ColorPoints = playerState.EndGameBonusColors
        };

        _penaltyPointsByRound = new List<int>();
        if (playerState.PenaltyPointsByRound != null)
        {
            int roundsCount = Mathf.Min(playerState.PenaltyRoundsCount, playerState.PenaltyPointsByRound.Length);
            for (int i = 0; i < roundsCount; i++)
                _penaltyPointsByRound.Add(playerState.PenaltyPointsByRound[i]);
        }

        int size = GameConfig.GRID_SIZE;
        PlayerGridModel patternGrid = new PlayerGridModel(PlayerIndex);
        int[] gridArray = new int[size * size];
        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                int index = row * size + col;
                bool occupied = (playerState.WallBits & (1 << index)) != 0;
                gridArray[index] = occupied ? (int)patternGrid.GetColorAt(row, col) : -1;
            }
        }
        _grid = new PlayerGridModel(PlayerIndex, gridArray);

        _placementLines = new PlacementLineModel[GameConfig.NUM_PLACEMENT_LINES];
        for (int i = 0; i < GameConfig.NUM_PLACEMENT_LINES; i++)
            _placementLines[i] = new PlacementLineModel(i, GameConfig.PLACEMENT_LINE_CAPACITIES[i]);

        InitAllowedColorsFromGrid();

        for (int i = 0; i < GameConfig.NUM_PLACEMENT_LINES; i++)
        {
            FlowerColor savedColor = (FlowerColor)playerState.LineColor[i];
            int savedCount = playerState.LineCount[i];
            if (savedCount > 0)
            {
                List<FlowerPiece> flowers = new List<FlowerPiece>(savedCount);
                for (int f = 0; f < savedCount; f++)
                    flowers.Add(new FlowerPiece(savedColor));

                _placementLines[i].PlaceFlowers(flowers);
            }

            for (int colorIndex = 0; colorIndex < GameConfig.NUM_COLORS; colorIndex++)
            {
                if ((playerState.Forbidden[i] & (1 << colorIndex)) != 0)
                    _placementLines[i].AddForbiddenColor((FlowerColor)colorIndex);
            }
        }

        _penaltyLine = new PenaltyLineModel();
        if (playerState.Penalty != null)
        {
            for (int i = 0; i < playerState.PenaltyCount && i < playerState.Penalty.Length; i++)
            {
                FlowerColor color = (FlowerColor)playerState.Penalty[i];
                if (color == FlowerColor.FirstPlayer)
                {
                    if (!_penaltyLine.HasFirstPlayerToken)
                        _penaltyLine.AddFirstPlayerToken();
                }
                else
                {
                    _penaltyLine.AddFlower(new FlowerPiece(color));
                }
            }
        }

        if (_hasFirstPlayerToken && !_penaltyLine.HasFirstPlayerToken)
            _penaltyLine.AddFirstPlayerToken();
    }
    #endregion
}