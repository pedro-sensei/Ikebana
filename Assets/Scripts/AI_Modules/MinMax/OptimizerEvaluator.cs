//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// -----------------------------------------------------------------------
//  OptimizerEvaluator  -  IMinMaxEvaluator with the Optimizer
//  heuristics from OptimalHeuristicBase.

public class OptimizerEvaluator : OptimalHeuristicBase, IMinMaxEvaluator
{
    #region FIELDS AND PARAMETERS
    private MinimalGM _model;
    private MinimalPlayer _currentPlayer;
    private MinimalPlayer _nextPlayer;
    private int _currentPlayerIndex;
    #endregion

    #region CONSTRUCTORS

    /// Default weights – mirrors MinOptimizerBrain defaults derived from
    /// the last GA training run.
    public OptimizerEvaluator()
    {
        SimulateScoringWeight            = 11.51325f;
        PenaltyWeight                    = 15.74914f;
        TilesPlacedWeight                = 17.34546f;
        LineCompletionWeight             = 13.17323f;
        TryPushPenaltyWeight             = 4.776058f;
        TryDenyMoveWeight                = 2.529862f;
        AvoidPartialLineWeight           = 11.48462f;
        FirstPlayerTokenWeight           = 4.032143f;
        PrioritizeCentralPlacementWeight = 22.29502f;
        PrioritizeTopRowsWeight          = 4.646821f;

        ChaseBonusColorWeights  = new float[] { 10.0f, 10.0f, 10.0f, 10.0f, 10.0f };
        ChaseBonusRowWeights    = new float[] { 10.0f, 13.0f, 13.0f,  6.0f, 12.0f };
        ChaseBonusColumnWeights = new float[] { 16.0f,  6.0f,  6.0f,  6.0f, 16.0f };
    }

    public OptimizerEvaluator(BasicGAGenome genome) : this()
    {
        ApplyGenomeWeights(genome);
    }

    public OptimizerEvaluator(OptimizerGenomeWeights weights) : this()
    {
        ApplyWeightsSnapshot(weights);
    }
    #endregion

    #region IMinMaxEvaluator

    public float Evaluate(MinimalGM model, int maximizingPlayer)
    {
        if (_gameConfigSnapshot == null || _gameConfigSnapshot != model.Config)
            LoadConfig(model.Config);

        _model = model;

        float myValue = ComputePlayerPositionValue(model, maximizingPlayer);

        float bestOpp = float.NegativeInfinity;
        for (int p = 0; p < model.NumPlayers; p++)
        {
            if (p == maximizingPlayer) continue;
            float v = ComputePlayerPositionValue(model, p);
            if (v > bestOpp) bestOpp = v;
        }
        if (bestOpp == float.NegativeInfinity) bestOpp = 0f;

        return myValue - bestOpp;
    }
    #endregion

    #region INTERNAL EVALUATION HELPERS

    /// Computes a heuristic value for playerIndex's current board state.
    private float ComputePlayerPositionValue(MinimalGM model, int playerIndex)
    {
        _currentPlayerIndex = playerIndex;
        _currentPlayer = model.Players[playerIndex];
        _nextPlayer = model.Players[(playerIndex + 1) % model.NumPlayers];

        float value = _currentPlayer.Score;
        value += _currentPlayer.CalculatePenalty(model);

        // Placement-line progress: each non-empty line contributes
        // the same weights the Optimizer uses when evaluating a move.
        for (int line = 0; line < _numPlacementLines; line++)
        {
            int count = _currentPlayer.GetLineCount(line);
            if (count == 0) continue;

            FlowerColor color = (FlowerColor)_currentPlayer.GetLineColor(line);
            int capacity = _currentPlayer.GetLineCapacity(line);
            bool completes = count >= capacity;
            int placed = System.Math.Min(count, capacity);

            // Projected immediate scoring if/when this line completes.
            value += SimulateLineScoring(color, line, completes) * SimulateScoringWeight;

            // Tile-placement credit.
            value += placed * TilesPlacedWeight;

            // Completion bonus.
            if (completes)
                value += LineCompletionWeight;

            // End-game bonus chasing (row / column / color progress).
            value += GetWeightedEndGameBonuses(color, line);
        }

        // Wall-tile progress toward end-game bonuses (already locked in).
        for (int row = 0; row < _gridSize; row++)
            value += CheckPartialRow(row) * GetChaseBonusRowWeight(row);

        for (int colorIdx = 0; colorIdx < _numColors; colorIdx++)
        {
            FlowerColor color = (FlowerColor)colorIdx;
            value += CheckPartialColor(color) * GetChaseBonusColorWeight(color);
            for (int row = 0; row < _gridSize; row++)
                value += CheckPartialColumn(color, row) * GetChaseBonusColumnWeight(color, row);
        }

        return value;
    }
    #endregion

    #region OptimalHeuristicBase OVERRIDES

    protected override bool MyWallOccupied(int row, int col)     => _currentPlayer.IsWallOccupied(row, col);
    protected override bool MyLineFull(int line)                 => _currentPlayer.IsLineFull(line);
    protected override byte MyLineColor(int line)                => _currentPlayer.GetLineColor(line);
    protected override int  MyLineCount(int line)                => _currentPlayer.GetLineCount(line);
    protected override bool MyLineEmpty(int line)                => _currentPlayer.GetLineCount(line) == 0;
    protected override int  MyLineCapacity(int line)             => _currentPlayer.GetLineCapacity(line);
    protected override int  MyPenaltyCount()                     => _currentPlayer.GetPenaltyCount();
    protected override int  MyCalculatePenalty()                 => _currentPlayer.CalculatePenalty(_model);

    protected override int MyGetWallColumn(int row, int colorIndex)
        => _gameConfigSnapshot.GetWallColumn(_currentPlayerIndex, row, colorIndex);

    protected override bool OppCanPlaceInLine(int line, byte color) => _nextPlayer.CanPlaceInLine(line, color);
    protected override byte OppLineColor(int line)                  => _nextPlayer.GetLineColor(line);
    protected override int  OppLineCount(int line)                  => _nextPlayer.GetLineCount(line);
    protected override int  OppLineCapacity(int line)               => _nextPlayer.GetLineCapacity(line);

    protected override int GetNumFactories() => _model.NumFactories;

    protected override int GetNonEmptyFactoryCount()
    {
        int count = 0;
        for (int f = 0; f < _model.NumFactories; f++)
            if (_model.GetFactoryflowerCount(f) > 0) count++;
        return count;
    }

    protected override int GetFactoryColorCount(int factoryIndex, int colorIndex)
        => _model.CountColorInFactory(factoryIndex, (FlowerColor)colorIndex);

    protected override int GetFactoryflowerCount(int factoryIndex)
        => _model.GetFactoryflowerCount(factoryIndex);

    protected override int GetCentralColorCount(int colorIndex)
        => _model.CountColorInCentral((FlowerColor)colorIndex);

    protected override int GetCurrentRound() => _model.CurrentRound;
    #endregion
}
