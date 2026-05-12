using System;
using System;
using System.Collections.Generic;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=



//Fixed heuristic brain with hardcoded weights.
//Does not implement hatedrafting.
#region FRIENDLY BRAIN
public class FriendlyAIBrain : IPlayerAIBrain, IEvolvableBrain
{
    #region WEIGHTS AND PARAMETERS

    private const int GENE_COUNT = 4;
    private static readonly string[] GeneNames =
    {
        "SimulateScoringWeight",
        "PenaltyWeight",
        "TilesPlacedWeight",
        "LineCompletionWeight"
    };

    public virtual string BrainName => "Friendly";

    private GameModel _model;
    private PlayerModel _me;
    private PlayerModel _opp;
    [SerializeField] private float _simulateScoringWeight;
    [SerializeField] private float _penaltyWeight;
    [SerializeField] private float _tilesPlacedWeight;
    [SerializeField] private float _lineCompletionWeight;
    protected float _bonusRow;
    protected float _bonusColumn;
    protected float _bonusColor;
    protected int   _gridSize;
    protected int   _numColors;
    protected int   _numPlacementLines;
    protected int   _penaltyCapacity;
    protected int[] _penaltyValues;

    // Config snapshot
    protected GameConfigSnapshot _gameConfigSnapshot;
    

    // Loads game constants from a config snapshot.
    protected void LoadConfig(GameConfigSnapshot config)
    {
        _gameConfigSnapshot = config;
        _bonusRow        = config.BonusRow;
        _bonusColumn     = config.BonusColumn;
        _bonusColor      = config.BonusColor;
        _gridSize        = config.GridSize;
        _numColors       = config.NumColors;
        _numPlacementLines = config.NumPlacementLines;
        _penaltyCapacity = config.PenaltyCapacity;
        _penaltyValues   = config.PenaltyValues;
    }


    // Default initializer — reads from the current global GameConfig.

    protected void LoadConfigFromGlobal()
    {
        LoadConfig(GameConfig.CreateSnapshot());
    }

    #endregion
    #region MOVE EVALUATION


    // Evaluates a move 

    protected virtual float EvaluateMove(
        MoveSource source, int factoryIndex, FlowerColor color,
        int targetLineIndex, int flowerCount, bool isPenalty,
        bool centralHasToken)
    {
        if (isPenalty)
            return flowerCount*(_penaltyWeight*-1);

        int lineIdx   = targetLineIndex;
        int capacity  = _model.Players[_model.CurrentPlayerIndex].PlacementLines[lineIdx].Capacity;     
        int lineCount = _model.Players[_model.CurrentPlayerIndex].PlacementLines[lineIdx].Count;
        int spaceLeft = capacity - lineCount;
        bool completesLine = flowerCount >= spaceLeft;
        int tilesPlaced = Mathf.Min(flowerCount, spaceLeft);
        int tilesOverflow = Mathf.Max(0, flowerCount - spaceLeft);

        float score = 0f;
        score += tilesPlaced * _tilesPlacedWeight;
        score -= tilesOverflow * _penaltyWeight; 
        if (completesLine)
            score += _lineCompletionWeight;  //Bonus for completing a line

        score += SimulateLineScoring(color, lineIdx, completesLine) * _simulateScoringWeight;
        return score;
    }

    //Simple scoring simulator based on curreng grid state.
    private float SimulateLineScoring(FlowerColor color, int lineIdx, bool completesLine)
    {
        PlayerModel _me = _model.GetCurrentPlayer();
        PlayerGridModel _grid = _me.Grid;
        if (!completesLine) return 0f;

        int row = lineIdx;
        int col = _grid.GetColumnForColor(row, color);

        //Score for row.
        int h = 1;
        for (int c = col - 1; c >= 0 && _grid.IsOccupied(lineIdx, c); c--) h++;
        for (int c = col + 1; c < _grid.GetSize() && _grid.IsOccupied(lineIdx, c); c++) h++;

        //Score for column.
        int v = 1;
        for (int r = row - 1; r >= 0; r--)
        {
            if (_grid.IsOccupied(r, col))
                v++;
            else if (_me.PlacementLines[r].IsFull &&
                     _me.PlacementLines[r].CurrentColor.HasValue &&
                     _grid.GetColumnForColor(r, _me.PlacementLines[r].CurrentColor.Value) == col)
                v++;
            else
                break;
        }
        for (int r = row + 1; r < _gridSize; r++)
        {
            if (_grid.IsOccupied(r, col))
                v++;
            else
                break;
        }

        if (h == 1 && v == 1) return 1f;
        float pts = 0f;
        if (h > 1) pts += h;
        if (v > 1) pts += v;
        return pts;
    }


    public FriendlyAIBrain() { 
        LoadConfigFromGlobal();
        _simulateScoringWeight = 7.3f;
        _penaltyWeight = 10.5f;
        _tilesPlacedWeight = 12.8f;
        _lineCompletionWeight = 11.18f;

    }
    public FriendlyAIBrain(int seed) { LoadConfigFromGlobal(); }

    public FriendlyAIBrain(BasicGAGenome genome) : this()
    {
        ApplyGenomeWeights(genome);
    }

    public GameMove ChooseMove(GameModel model, List<GameMove> validMoves)
    {
        GameMove bestMove  = validMoves[0];
        float    bestScore = float.MinValue;

        foreach (var move in validMoves)
        {
            float score = EvaluateMove(move, model);
            if (score > bestScore) { bestScore = score; bestMove = move; }
        }
        //Debug.Log($"FriendlyAIBrain evaluated {validMoves.Count} moves. Best score: {bestScore}");
        return bestMove;
    }


    protected void SetContext(GameModel model)
    {
        _model = model;
        _me    = model.GetCurrentPlayer();
        int oppIdx = (_me.PlayerIndex + 1) % model.NumberOfPlayers;
        _opp   = model.Players[oppIdx];
    }

    protected virtual float EvaluateMove(GameMove move, GameModel model)
    {
        SetContext(model);

        return EvaluateMove(
            move.SourceEnum, move.FactoryIndex, move.Color,
            move.TargetLineIndex, move.flowerCount, move.IsPenalty,
            model.CentralDisplay.HasFirstPlayerToken);
    }

    public int GeneCount => GENE_COUNT;

    public void GetGenes(float[] destination)
    {
        if (destination == null || destination.Length < GENE_COUNT) return;

        destination[0] = _simulateScoringWeight;
        destination[1] = _penaltyWeight;
        destination[2] = _tilesPlacedWeight;
        destination[3] = _lineCompletionWeight;
    }

    public void SetGenes(float[] source)
    {
        if (source == null || source.Length < GENE_COUNT) return;

        _simulateScoringWeight = source[0];
        _penaltyWeight = source[1];
        _tilesPlacedWeight = source[2];
        _lineCompletionWeight = source[3];
    }

    public void RandomInitialise(float[] genes, System.Random rng)
    {
        if (genes == null || genes.Length < GENE_COUNT || rng == null) return;

        for (int i = 0; i < GENE_COUNT; i++)
            genes[i] = (float)(rng.NextDouble() * 20.0);
    }

    public string GetGeneName(int index)
    {
        if (index < 0 || index >= GENE_COUNT)
            return "Gene[" + index + "]";

        return GeneNames[index];
    }

    private void ApplyGenomeWeights(BasicGAGenome genome)
    {
        if (genome == null) return;

        _simulateScoringWeight = genome.SimulateScoringWeight;
        _penaltyWeight = genome.PenaltyWeight;
        _tilesPlacedWeight = genome.TilesPlacedWeight;
        _lineCompletionWeight = genome.LineCompletionWeight;
    }

    #endregion
}
#endregion

#region MINIMAL FRIENDLY BRAIN
//Version for minimal GM
public class MinFriendlyBrain : IMinimalAIBrain
{
    // Weights matching FriendlyAIBrain defaults.
    public string BrainName => "Friendly (Minimal)";

    private float _simulateScoringWeight = 7.3f;
    private float _penaltyWeight = 10.5f;
    private float _tilesPlacedWeight = 12.8f;
    private float _lineCompletionWeight = 11.18f;


    private int _gridSize = 5;

    public MinFriendlyBrain() { }

    public MinFriendlyBrain(float[] genes, GameConfigSnapshot config) : this()
    {
        if (config != null)
            _gridSize = config.GridSize;

        if (genes != null && genes.Length >= 4)
        {
            _simulateScoringWeight = genes[0];
            _penaltyWeight = genes[1];
            _tilesPlacedWeight = genes[2];
            _lineCompletionWeight = genes[3];
        }
    }

    public MinFriendlyBrain(BasicGAGenome genome, GameConfigSnapshot config) : this()
    {
        if (config != null)
            _gridSize = config.GridSize;

        if (genome == null)
            return;

        _simulateScoringWeight = genome.SimulateScoringWeight;
        _penaltyWeight = genome.PenaltyWeight;
        _tilesPlacedWeight = genome.TilesPlacedWeight;
        _lineCompletionWeight = genome.LineCompletionWeight;
    }

    public int ChooseMoveIndex(MinimalGM model, GameMove[] moves, int moveCount)
    {
        int   bestIdx   = 0;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < moveCount; i++)
        {
            float s = Evaluate(model, moves[i]);
            if (s > bestScore)
            {
                bestScore = s;
                bestIdx   = i;
            }
        }
        return bestIdx;
    }

    private float Evaluate(MinimalGM model, GameMove move)
    {
        if (move.IsPenalty)
            return move.flowerCount * (-_penaltyWeight);

        MinimalPlayer me = model.Players[model.CurrentPlayer];
        int lineIdx   = move.TargetLineIndex;
        int capacity  = me.GetLineCapacity(lineIdx);
        int lineCount = me.GetLineCount(lineIdx);
        int spaceLeft = capacity - lineCount;
        int placed    = System.Math.Min(move.flowerCount, spaceLeft);
        int overflow  = move.flowerCount - placed;
        bool completes = placed >= spaceLeft;

        float score = 0f;
        score += placed   * _tilesPlacedWeight;
        score -= overflow * _penaltyWeight;
        if (completes)
            score += _lineCompletionWeight;

        score += SimulateLineScoring(model, me, move.Color, lineIdx, completes)
                 * _simulateScoringWeight;
        return score;
    }

    private float SimulateLineScoring(MinimalGM model, MinimalPlayer me,
                                      FlowerColor color, int lineIdx,
                                      bool completesLine)
    {
        if (!completesLine) return 0f;

        int row = lineIdx;
        int col = model.Config.GetWallColumn(model.CurrentPlayer, row, (int)color);

        int h = 1;
        for (int c = col - 1; c >= 0 && me.IsWallOccupied(row, c); c--) h++;
        for (int c = col + 1; c < _gridSize && me.IsWallOccupied(row, c); c++) h++;

        int v = 1;
        for (int r = row - 1; r >= 0; r--)
        {
            if (me.IsWallOccupied(r, col))
                v++;
            else if (me.IsLineFull(r) && model.Config.GetWallColumn(model.CurrentPlayer, r, me.GetLineColor(r)) == col)
                v++;
            else
                break;
        }
        for (int r = row + 1; r < _gridSize; r++)
        {
            if (me.IsWallOccupied(r, col))
                v++;
            else
                break;
        }

        if (h == 1 && v == 1) return 1f;
        float pts = 0f;
        if (h > 1) pts += h;
        if (v > 1) pts += v;
        return pts;
    }
}

#endregion

#region MINIMAL HYBRID ELITE BRAIN
public class MinHybridEliteBrain : IMinimalAIBrain
{
    public string BrainName => "HybridElite (Minimal)";

    private readonly IMinimalAIBrain _friendly;
    private readonly IMinimalAIBrain _optimizer;
    private readonly IMinimalAIBrain _emily;

    public MinHybridEliteBrain(IMinimalAIBrain friendly, IMinimalAIBrain optimizer, IMinimalAIBrain emily)
    {
        _friendly = friendly;
        _optimizer = optimizer;
        _emily = emily;
    }

    public int ChooseMoveIndex(MinimalGM model, GameMove[] moves, int moveCount)
    {
        int roll = UnityEngine.Random.Range(0, 3);

        switch (roll)
        {
            case 0:
                return _friendly != null
                    ? _friendly.ChooseMoveIndex(model, moves, moveCount)
                    : 0;
            case 1:
                return _optimizer != null
                    ? _optimizer.ChooseMoveIndex(model, moves, moveCount)
                    : 0;
            default:
                return _emily != null
                    ? _emily.ChooseMoveIndex(model, moves, moveCount)
                    : 0;
        }
    }
}

#endregion

