using System;

//=^..^=   =^..^=   VERSION 1.0.1 (March 2026)    =^..^=    =^..^=
//                    Last Update 22/03/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=
//TO BE DEPRECATED.

/*
// Minimal greedy brain. All heuristic logic lives in GreedyHeuristicBase.
// This class only provides data accessors and a thread safe version.
public class MinimalGreedyBrain : GreedyHeuristicBase, IMinimalAIBrain
{
    private MinimalGM     _model;
    private MinimalPlayer _me;
    private MinimalPlayer _opp;

    private readonly System.Random _rng;

    public virtual string BrainName
    {
        get { return "Greedy (Minimal)"; }
    }

    public MinimalGreedyBrain(int seed = 0)
    {
        if (seed == 0)
            _rng = new System.Random();
        else
            _rng = new System.Random(seed);
        // Config is loaded lazily from the model in SetMinimalContext
    }

    public MinimalGreedyBrain(GAGenome genome, int seed = 0) : this(seed)
    {
        ApplyGenomeWeights(genome);
    }

    public MinimalGreedyBrain(GenomeWeights w, int seed = 0) : this(seed)
    {
        ApplyWeightsSnapshot(w);
    }

    // ---- IMinimalAIBrain ----

    public int ChooseMoveIndex(MinimalGM model, GameMove[] moves, int moveCount)
    {
        int   bestIdx   = 0;
        float bestScore = float.MinValue;

        for (int i = 0; i < moveCount; i++)
        {
            float s = EvaluateMinimalMove(model, moves[i]);
            if (s > bestScore) { bestScore = s; bestIdx = i; }
        }
        return bestIdx;
    }

    // Sets up context for the abstract accessors and delegates to EvaluateMove
    protected void SetMinimalContext(MinimalGM model)
    {
        _model = model;
        // Load config from the model's snapshot on first use or if it changed
        if (_gameConfigSnapshot == null || _gameConfigSnapshot != model.Config)
            LoadConfig(model.Config);
        int myIdx  = model.CurrentPlayer;
        int oppIdx = (myIdx + 1) % model.NumPlayers;
        _me  = model.Players[myIdx];
        _opp = model.Players[oppIdx];
    }

    protected virtual float EvaluateMinimalMove(MinimalGM model, GameMove move)
    {
        SetMinimalContext(model);

        return EvaluateMove(
            move.SourceEnum, move.FactoryIndex, move.Color,
            move.TargetLineIndex, move.flowerCount, move.IsPenalty,
            model.CentralHasToken);
    }

    // ---- Data accessors for MinimalGM / MinimalPlayer ----

    protected override bool MyWallOccupied(int row, int col)
    {
        return _me.IsWallOccupied(row, col);
    }

    protected override bool MyLineFull(int line)
    {
        return _me.IsLineFull(line);
    }

    protected override byte MyLineColor(int line)
    {
        return _me.GetLineColor(line);
    }

    protected override int MyLineCount(int line)
    {
        return _me.GetLineCount(line);
    }

    protected override bool MyLineEmpty(int line)
    {
        return _me.GetLineCount(line) == 0;
    }

    protected override int MyLineCapacity(int line)
    {
        return _me.GetLineCapacity(line);
    }

    protected override int MyPenaltyCount()
    {
        return _me.GetPenaltyCount();
    }

    protected override int MyCalculatePenalty()
    {
        return _me.CalculatePenalty(_model);
    }

    protected override int MyGetWallColumn(int row, int colorIndex)
    {
        return _gameConfigSnapshot.GetWallColumn(_model.CurrentPlayer, row, colorIndex);
    }

    protected override bool OppCanPlaceInLine(int line, byte color)
    {
        return _opp.CanPlaceInLine(line, color);
    }

    protected override byte OppLineColor(int line)
    {
        return _opp.GetLineColor(line);
    }

    protected override int OppLineCount(int line)
    {
        return _opp.GetLineCount(line);
    }

    protected override int OppLineCapacity(int line)
    {
        return _opp.GetLineCapacity(line);
    }

    protected override int GetNumFactories()
    {
        return _model.NumFactories;
    }

    protected override int GetNonEmptyFactoryCount()
    {
        int count = 0;
        for (int f = 0; f < _model.NumFactories; f++)
        {
            if (_model.GetFactoryflowerCount(f) > 0) count++;
        }
        return count;
    }

    protected override int GetFactoryColorCount(int factoryIndex, int colorIndex)
    {
        return _model.CountColorInFactory(factoryIndex, (FlowerColor)colorIndex);
    }

    protected override int GetFactoryflowerCount(int factoryIndex)
    {
        return _model.GetFactoryflowerCount(factoryIndex);
    }

    protected override int GetCentralColorCount(int colorIndex)
    {
        return _model.CountColorInCentral((FlowerColor)colorIndex);
    }

    protected override int GetCurrentRound()
    {
        return _model.CurrentRound;
    }
}

// -----------------------------------------------------------------------
// MinimalGreedyBrainEML - round-gated variant for MinimalGM
// -----------------------------------------------------------------------

// Minimal-path EML greedy brain. Round-gated heuristics for end-game bonuses,
// first-player token, top-rows preference, and central placement preference.
// Thread-safe since all state is instance-local.
public class MinimalGreedyBrainEML : MinimalGreedyBrain
{
    public override string BrainName
    {
        get { return "Greedy EML (Minimal)"; }
    }

    public MinimalGreedyBrainEML(GAGenome genome, int seed = 0) : base(genome, seed) { }
    public MinimalGreedyBrainEML(GenomeWeights w, int seed = 0) : base(w, seed) { }

    protected override float EvaluateMinimalMove(MinimalGM model, GameMove move)
    {
        SetMinimalContext(model);

        if (move.IsPenalty)
            return ScorePenaltyMove(move.flowerCount) * PenaltyWeight;

        int lineIdx   = move.TargetLineIndex;
        int capacity  = MyLineCapacity(lineIdx);
        int lineCount = MyLineCount(lineIdx);
        int spaceLeft = capacity - lineCount;
        bool completesLine = move.flowerCount >= spaceLeft;

        float score = 0f;

        // Immediate scoring - always active
        score += SimulateLineScoring(move.Color, lineIdx, completesLine) * PlacingWeight;

        // End-game bonuses only from mid-game onwards (round >= 3)
        if (GetCurrentRound() >= 3)
            score += GetPartialEndGameBonuses(move.Color, lineIdx) * EndGameBonusWeight;

        // Opponent interaction - always active
        score += SimulatePushingPenalties(move.SourceEnum, move.FactoryIndex, move.Color) * PushingPenaltiesWeight;
        score += SimulateDenyingOpponent(move.Color, move.flowerCount)   * DenyOpponentWeight;

        // First-player token bonus only before late game (round < 5)
        if (GetCurrentRound() < 5)
            if (move.SourceEnum == MoveSource.Central && model.CentralHasToken)
                score += FirstPlayerTokenBonus + MyCalculatePenalty();

        // Top rows preference only in early game (round < 3)
        if (GetCurrentRound() < 3 && lineIdx <= _gridSize / 2)
            score += TopRowsWeight;

        // Central column preference only through early/mid (round <= 3)
        if (GetCurrentRound() <= 3)
        {
            int wallCol    = MyGetWallColumn(lineIdx, (int)move.Color);
            int middle     = _gridSize / 2;
            int distCenter = System.Math.Abs(middle - wallCol);
            score += (middle - distCenter) * CentralPlacementWeight;
        }

        // Avoid partial bottom rows - always active
        if (lineIdx >= _gridSize / 2 && !completesLine)
        {
            score -= AvoidPartialLinesWeight;
            if (MyLineFull(_gridSize - 1)) score -= AvoidPartialLinesWeight;
            if (MyLineFull(_gridSize - 2)) score -= AvoidPartialLinesWeight;
        }

        // One-shot completion bonus - always active
        if (lineCount == 0 && completesLine)
            score += spaceLeft * OneMoveFillingBonus;

        return score;
    }
}
*/