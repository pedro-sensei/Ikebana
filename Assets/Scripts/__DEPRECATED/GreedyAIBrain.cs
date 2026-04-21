using System.Collections.Generic;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.0.2 (April 2026)    =^..^=    =^..^=
//                    Last Update 01/04/2026 

//DEPRECATED : 15/04/2026
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

//TO BE DEPRECATED. NOT WORKING PROPERLY. REPLACED BY OPTIMIZER VERSION.
// Created to unify the heuristic of all greedy brains.

/*
public abstract class GreedyHeuristicBase
{
    #region WEIGHTS AND PARAMETERS

    public float PlacingWeight          = 10f;
    public float EndGameBonusWeight     =  5f;
    public float DenyOpponentWeight     =  5f;
    public float PushingPenaltiesWeight =  5f;
    public float TopRowsWeight          =  2f;
    public int   EarlyRoundsThreshold   =  2;
    public float CentralPlacementWeight =  2f;
    public float AvoidPartialLinesWeight=  5f;
    public float LineCompletionBonus    = 10f;

    public float PenaltyWeight         = 10f;
    public float FirstPlayerTokenBonus  =  5f;

    public float DenyColorBaseBonus       =  1f;
    public float DisruptExistingLineBonus =  5f;
    public float DenyCompletionBonus      = 10f;

    public float PoisonColorBonus         =  3f;
    public float OneMoveFillingBonus      =  5f;

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
    // Called by subclass constructors or SetConfig.

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
        _projectedCentralState = new int[_numColors];
    }


    // Default initializer — reads from the current global GameConfig.

    protected void LoadConfigFromGlobal()
    {
        LoadConfig(GameConfig.CreateSnapshot());
    }

    #endregion
    #region WEIGHT APPLICATION

    protected void ApplyGenomeWeights(GAGenome g)
    {
        PlacingWeight           = g.placingWeight;
        EndGameBonusWeight      = g.endGameBonusWeight;
        DenyOpponentWeight      = g.denyOpponentWeight;
        PushingPenaltiesWeight  = g.pushingPenaltiesWeight;
        TopRowsWeight           = g.topRowsWeight;
        EarlyRoundsThreshold    = g.earlyRoundsThreshold;
        CentralPlacementWeight  = g.centralPlacementWeight;
        AvoidPartialLinesWeight = g.avoidPartialLinesWeight;
        PenaltyWeight          = g.penaltyPerflower;
        FirstPlayerTokenBonus   = g.firstPlayerTokenBaseBonus;
        DenyColorBaseBonus      = g.denyColorBaseBonus;
        DisruptExistingLineBonus= g.disruptExistingLineBonus;
        DenyCompletionBonus     = g.denyCompletionBonus;
        PoisonColorBonus        = g.poisonColorBonus;
        LineCompletionBonus     = g.lineCompletionBonus;
    }

    protected void ApplyWeightsSnapshot(GenomeWeights w)
    {
        PlacingWeight           = w.placingWeight;
        EndGameBonusWeight      = w.endGameBonusWeight;
        DenyOpponentWeight      = w.denyOpponentWeight;
        PushingPenaltiesWeight  = w.pushingPenaltiesWeight;
        TopRowsWeight           = w.topRowsWeight;
        EarlyRoundsThreshold    = w.earlyRoundsThreshold;
        CentralPlacementWeight  = w.centralPlacementWeight;
        AvoidPartialLinesWeight = w.avoidPartialLinesWeight;
        PenaltyWeight          = w.penaltyPerflower;
        FirstPlayerTokenBonus   = w.firstPlayerTokenBaseBonus;
        DenyColorBaseBonus      = w.denyColorBaseBonus;
        DisruptExistingLineBonus= w.disruptExistingLineBonus;
        DenyCompletionBonus     = w.denyCompletionBonus;
        PoisonColorBonus        = w.poisonColorBonus;
        LineCompletionBonus     = w.lineCompletionBonus;
    }

    #endregion
    #region PLAYER-BOARD, OPPONENT AND DISPLAY QUERIES

    //Player board queries — my player
    protected abstract bool  MyWallOccupied(int row, int col);
    protected abstract bool  MyLineFull(int line);
    protected abstract byte  MyLineColor(int line);
    protected abstract int   MyLineCount(int line);
    protected abstract bool  MyLineEmpty(int line);
    protected abstract int   MyLineCapacity(int line);

    protected abstract int   MyPenaltyCount();
    protected abstract int   MyCalculatePenalty();
    protected abstract int   MyGetWallColumn(int row, int colorIndex);

    // Board queries — opponent player
    protected abstract bool  OppCanPlaceInLine(int line, byte color);
    protected abstract byte  OppLineColor(int line);
    protected abstract int   OppLineCount(int line);
    protected abstract int   OppLineCapacity(int line);

    // Display queries
    protected abstract int   GetNumFactories();
    protected abstract int   GetNonEmptyFactoryCount();
    protected abstract int   GetFactoryColorCount(int factoryIndex, int colorIndex);
    protected abstract int   GetFactoryflowerCount(int factoryIndex);
    protected abstract int   GetCentralColorCount(int colorIndex);

    // Game state
    protected abstract int   GetCurrentRound();

    #endregion

    #region MOVE EVALUATION


    // Evaluates a move 

    protected virtual float EvaluateMove(
        MoveSource source, int factoryIndex, FlowerColor color,
        int targetLineIndex, int flowerCount, bool isPenalty,
        bool centralHasToken)
    {
        if (isPenalty)
            return ScorePenaltyMove(flowerCount)*PenaltyWeight;

        int lineIdx   = targetLineIndex;               // TargetLineIndex is 0-based
        int capacity  = MyLineCapacity(lineIdx);       // read real capacity from the model
        int lineCount = MyLineCount(lineIdx);
        int spaceLeft = capacity - lineCount;
        bool completesLine = flowerCount >= spaceLeft;

        float score = 0f;

        score += SimulateLineScoring(color, lineIdx, completesLine) * PlacingWeight;
        score += GetPartialEndGameBonuses(color, lineIdx)           * EndGameBonusWeight;
        score += SimulateDenyingOpponent(color, flowerCount)          * DenyOpponentWeight;
        score += SimulatePushingPenalties(source, factoryIndex, color) * PushingPenaltiesWeight;

        if (source == MoveSource.Central && centralHasToken)
            score += FirstPlayerTokenBonus + MyCalculatePenalty();

        if (GetCurrentRound() <= EarlyRoundsThreshold && lineIdx <= _gridSize / 2)
            score += TopRowsWeight;

        int wallCol    = MyGetWallColumn(lineIdx, (int)color);
        int middle     = _gridSize / 2;
        int distCenter = System.Math.Abs(middle - wallCol);
        score += (middle - distCenter) * CentralPlacementWeight;

        if (lineIdx >= _gridSize / 2 && !completesLine)
        {
            score -= AvoidPartialLinesWeight;
            if (MyLineFull(_gridSize - 1)) score -= AvoidPartialLinesWeight;
            if (MyLineFull(_gridSize - 2)) score -= AvoidPartialLinesWeight;
        }

        if (lineCount == 0 && completesLine)
            score += spaceLeft * OneMoveFillingBonus;

        return score;
    }

    // Penalty scoring value.
    protected float ScorePenaltyMove(int flowerCount)
    {
        float penScore = 0f;
        int penaltyStart = MyPenaltyCount();
        for (int i = 0; i < flowerCount && (penaltyStart + i) < _penaltyCapacity; i++)
        {
            int slot = penaltyStart + i;
            penScore += _penaltyValues[slot];
        }
        return penScore;
    }

    // Scoring Simulation

    protected float SimulateLineScoring(FlowerColor color, int lineIdx, bool completesLine)
    {
        if (!completesLine) return 0f;

        int row = lineIdx;
        int col = MyGetWallColumn(row, (int)color);

        int h = 1;
        for (int c = col - 1; c >= 0       && MyWallOccupied(row, c); c--) h++;
        for (int c = col + 1; c < _gridSize && MyWallOccupied(row, c); c++) h++;

        int v = 1;
        for (int r = row - 1; r >= 0; r--)
        {
            if (MyWallOccupied(r, col))
                v++;
            else if (MyLineFull(r) && MyGetWallColumn(r, MyLineColor(r)) == col)
                v++;
            else
                break;
        }
        for (int r = row + 1; r < _gridSize; r++)
        {
            if (MyWallOccupied(r, col))
                v++;
            else if (MyLineFull(r) && MyGetWallColumn(r, MyLineColor(r)) == col)
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

    //ENDGAME BONUS Partial Scoring.

    protected float GetPartialEndGameBonuses(FlowerColor color, int lineIdx)
    {
        return CheckPartialRow(lineIdx)
             + CheckPartialColumn(color, lineIdx)
             + CheckPartialColor(color);
    }

    private float CheckPartialRow(int row)
    {
        int filled = 0;
        for (int c = 0; c < _gridSize; c++) if (MyWallOccupied(row, c)) filled++;
        int remaining = _gridSize - filled;
        return remaining <= 0 ? 0f : _bonusRow / (remaining * remaining);
    }

    private float CheckPartialColumn(FlowerColor color, int row)
    {
        int col = MyGetWallColumn(row, (int)color);
        int filled = 0;
        for (int r = 0; r < _gridSize; r++) if (MyWallOccupied(r, col)) filled++;
        int remaining = _gridSize - filled;
        return remaining <= 0 ? 0f : _bonusColumn / (remaining * remaining);
    }

    private float CheckPartialColor(FlowerColor color)
    {
        int colorIdx = (int)color;
        int filled = 0;
        for (int r = 0; r < _gridSize; r++)
        {
            int c = MyGetWallColumn(r, colorIdx);
            if (MyWallOccupied(r, c)) filled++;
        }
        int remaining = _gridSize - filled;
        return remaining <= 0 ? 0f : _bonusColor / (remaining * remaining);
    }

    // DENIAL AND OPPONENT DISRUPTION

    protected float SimulateDenyingOpponent(FlowerColor color, int flowerCount)
    {
        float score = 0f;
        byte colorByte = (byte)color;

        for (int line = 0; line < _numPlacementLines; line++)
        {
            if (!OppCanPlaceInLine(line, colorByte)) continue;

            score += DenyColorBaseBonus;

            byte oppColor = OppLineColor(line);
            if (oppColor != 255 && oppColor == colorByte)
            {
                score += DisruptExistingLineBonus;
                int oppSpace = OppLineCapacity(line) - OppLineCount(line);
                if (flowerCount >= oppSpace)
                    score += DenyCompletionBonus;
            }
        }
        return score;
    }

    // PUSHING PENALTIES (LAST-FACTORY POISON)
    private int[] _projectedCentralState;

    protected float SimulatePushingPenalties(MoveSource source, int factoryIndex, FlowerColor moveColor)
    {
        // Only applies when picking from the last non-empty factory
        if (source != MoveSource.Factory) return 0f;
        if (GetNonEmptyFactoryCount() != 1) return 0f;

        byte pickedColor = (byte)moveColor;  
        int distinctColors = 0;
        int poisonCount = 0;

 
        for (int c = 0; c < _numColors; c++)
        {
            _projectedCentralState[c] = GetCentralColorCount(c);
            if ((byte)c != pickedColor)
            _projectedCentralState[c] += GetFactoryColorCount(factoryIndex, c);
        }

        //Count colors
        for (int c = 0; c < _numColors; c++)
        {
            int groupSize = _projectedCentralState[c];
            if (groupSize == 0) continue;

            distinctColors++;

            // Check if the opponent can place this color
            bool canPlace = false;
            byte colorByte = (byte)c;
            for (int line = 0; line < _numPlacementLines; line++)
            {
                if (OppCanPlaceInLine(line, colorByte)) { canPlace = true; break; }
            }

            if (!canPlace)
            {
                poisonCount+= groupSize;
            }
        }

        if (poisonCount == 0) return 0f;

        //TODO: IMPROVE THIS TO BE MORE REALISTIC
        return poisonCount;
    }

    #endregion
}





/// Greedy AI brain for the full GameModel.
/// All heuristic logic in GreedyHeuristicBase 
public class GreedyAIBrain : GreedyHeuristicBase, IPlayerAIBrain, IEvolvableBrain
{
    // Gene layout: 15 scalar weights (matching GAGenome fields)
    private const int GREEDY_GENE_COUNT = 15;

    private static readonly string[] GreedyGeneNames =
    {
        "PlacingWeight",
        "EndGameBonusWeight",
        "DenyOpponentWeight",
        "PushingPenaltiesWeight",
        "TopRowsWeight",
        "CentralPlacementWeight",
        "AvoidPartialLinesWeight",
        "PenaltyWeight",
        "FirstPlayerTokenBonus",
        "DenyColorBaseBonus",
        "DisruptExistingLineBonus",
        "DenyCompletionBonus",
        "PoisonColorBonus",
        "OneMoveFillingBonus",
        "LineCompletionBonus",
    };

    private GameModel    _model;
    private PlayerModel  _me;
    private PlayerModel  _opp;

    public virtual string BrainName => "Greedy AI";

    public GreedyAIBrain() { LoadConfigFromGlobal(); }
    public GreedyAIBrain(int seed) { LoadConfigFromGlobal(); }

    public GameMove ChooseMove(GameModel model, List<GameMove> validMoves)
    {
        GameMove bestMove  = validMoves[0];
        float    bestScore = float.MinValue;

        foreach (var move in validMoves)
        {
            float score = EvaluateMove(move, model);
            if (score > bestScore) { bestScore = score; bestMove = move; }
        }
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

    #region IEvolvableBrain

    public int GeneCount => GREEDY_GENE_COUNT;

    public void GetGenes(float[] dest)
    {
        int i = 0;
        dest[i++] = PlacingWeight;
        dest[i++] = EndGameBonusWeight;
        dest[i++] = DenyOpponentWeight;
        dest[i++] = PushingPenaltiesWeight;
        dest[i++] = TopRowsWeight;
        dest[i++] = CentralPlacementWeight;
        dest[i++] = AvoidPartialLinesWeight;
        dest[i++] = PenaltyWeight;
        dest[i++] = FirstPlayerTokenBonus;
        dest[i++] = DenyColorBaseBonus;
        dest[i++] = DisruptExistingLineBonus;
        dest[i++] = DenyCompletionBonus;
        dest[i++] = PoisonColorBonus;
        dest[i++] = OneMoveFillingBonus;
        dest[i++] = LineCompletionBonus;
    }

    public void SetGenes(float[] src)
    {
        int i = 0;
        PlacingWeight            = src[i++];
        EndGameBonusWeight       = src[i++];
        DenyOpponentWeight       = src[i++];
        PushingPenaltiesWeight   = src[i++];
        TopRowsWeight            = src[i++];
        CentralPlacementWeight   = src[i++];
        AvoidPartialLinesWeight  = src[i++];
        PenaltyWeight            = src[i++];
        FirstPlayerTokenBonus    = src[i++];
        DenyColorBaseBonus       = src[i++];
        DisruptExistingLineBonus = src[i++];
        DenyCompletionBonus      = src[i++];
        PoisonColorBonus         = src[i++];
        OneMoveFillingBonus      = src[i++];
        LineCompletionBonus      = src[i++];
    }

    public void RandomInitialise(float[] genes, System.Random rng)
    {
        for (int i = 0; i < genes.Length; i++)
            genes[i] = (float)(rng.NextDouble() * 20.0);
    }

    public string GetGeneName(int index)
    {
        if (index >= 0 && index < GreedyGeneNames.Length)
            return GreedyGeneNames[index];
        return "Gene[" + index + "]";
    }

    #endregion

    // Data accessors for GameModel
    protected override bool MyWallOccupied(int row, int col)
        => _me.Grid.IsOccupied(row, col);

    protected override bool MyLineFull(int line)
        => _me.PlacementLines[line].IsFull;

    protected override byte MyLineColor(int line)
    {
        var c = _me.PlacementLines[line].CurrentColor;
        return c.HasValue ? (byte)c.Value : (byte)255;
    }

    protected override int MyLineCount(int line)
        => _me.PlacementLines[line].Count;

    protected override bool MyLineEmpty(int line)
        => _me.PlacementLines[line].IsEmpty;

    protected override int MyLineCapacity(int line)
        => _me.PlacementLines[line].Capacity;

    protected override int MyPenaltyCount()
        => _me.PenaltyLine.Count;

    protected override int MyCalculatePenalty()
        => _me.PenaltyLine.CalculatePenalty();

    protected override int MyGetWallColumn(int row, int colorIndex)
        => _me.Grid.GetColumnForColor(row, (FlowerColor)colorIndex);

    protected override bool OppCanPlaceInLine(int line, byte color)
        => _opp.CanPlaceInLine(line, (FlowerColor)color);

    protected override byte OppLineColor(int line)
    {
        var c = _opp.PlacementLines[line].CurrentColor;
        return c.HasValue ? (byte)c.Value : (byte)255;
    }

    protected override int OppLineCount(int line)
        => _opp.PlacementLines[line].Count;

    protected override int OppLineCapacity(int line)
        => _opp.PlacementLines[line].Capacity;

    protected override int GetNumFactories()
        => _model.FactoryDisplays.Length;

    protected override int GetNonEmptyFactoryCount()
    {
        int count = 0;
        for (int i = 0; i < _model.FactoryDisplays.Length; i++)
            if (!_model.FactoryDisplays[i].IsEmpty) count++;
        return count;
    }

    protected override int GetFactoryColorCount(int factoryIndex, int colorIndex)
        => _model.FactoryDisplays[factoryIndex].CountColor((FlowerColor)colorIndex);

    protected override int GetFactoryflowerCount(int factoryIndex)
        => _model.FactoryDisplays[factoryIndex].Count;

    protected override int GetCentralColorCount(int colorIndex)
        => _model.CentralDisplay.CountColor((FlowerColor)colorIndex);

    protected override int GetCurrentRound()
        => _model.CurrentRound;
}
*/