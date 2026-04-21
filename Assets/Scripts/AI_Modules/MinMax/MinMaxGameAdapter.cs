using System;

// -----------------------------------------------------------------------
//  MinMaxGameAdapter  -  Ikebana/Azul-specific implementation of
//  IMinMaxGameAdapter for the MinMax search.
//
//  Manages all game-specific operations: move generation, execute/undo,
//  turn advancement, terminal detection, and eval-safe state
//  save/restore.  Pre-allocates all buffers up front so the search
//  loop produces zero heap allocations.
//
//  The MinMax algorithm interacts with the game ONLY through the
//  IMinMaxGameAdapter interface.
// -----------------------------------------------------------------------
public class MinMaxGameAdapter : IMinMaxGameAdapter
{
    private readonly int _maxDepth;

    // The MinimalGM model being searched.  Set via SetModel() before each search.
    private MinimalGM _model;

    // The root move buffer (external, supplied by the brain).
    private MinimalMoveRecord[] _rootMoves;

    // Per-depth move generation buffers.
    private readonly MinimalMoveRecord[][] _moveBuffers;

    // Per-depth undo records.
    private readonly MinimalMoveRecord[] _undoRecords;

    // Per-depth saved-player / saved-turn for turn-state restore.
    private readonly int[] _savedPlayer;
    private readonly int[] _savedTurn;

    // Reusable score buffer for move ordering (zero-allocation insertion sort).
    private readonly float[] _moveSortScores = new float[MinimalGM.MAX_MINMAX_MOVES];

    // Eval save/restore buffers - per player (up to 4 players x 10 grid lines).
    private const int MAX_PLAYERS   = 4;
    private const int MAX_GRID_SIZE = 10;

    private readonly int[]  _evalWallBits;
    private readonly int[]  _evalScores;
    private readonly int[]  _evalPenalties;
    private readonly bool[] _evalTokens;
    private readonly bool[] _evalCompleteRow;
    private readonly byte[] _evalLineColors;
    private readonly int[]  _evalLineCounts;
    private readonly int[]  _evalForbidden;

    private int  _evalDiscardCount;
    private int  _evalStartingPlayer;
    private bool _evalIsGameOver;

    // Pluggable evaluator.
    private IMinMaxEvaluator _evaluator;

    public MinMaxGameAdapter(int maxDepth)
    {
        _maxDepth    = Math.Max(1, maxDepth);
        _moveBuffers = new MinimalMoveRecord[_maxDepth][];
        _undoRecords = new MinimalMoveRecord[_maxDepth];
        _savedPlayer = new int[_maxDepth];
        _savedTurn   = new int[_maxDepth];

        for (int d = 0; d < _maxDepth; d++)
            _moveBuffers[d] = new MinimalMoveRecord[MinimalGM.MAX_MINMAX_MOVES];

        int slots        = MAX_PLAYERS * MAX_GRID_SIZE;
        _evalWallBits    = new int [MAX_PLAYERS];
        _evalScores      = new int [MAX_PLAYERS];
        _evalPenalties   = new int [MAX_PLAYERS];
        _evalTokens      = new bool[MAX_PLAYERS];
        _evalCompleteRow = new bool[MAX_PLAYERS];
        _evalLineColors  = new byte[slots];
        _evalLineCounts  = new int [slots];
        _evalForbidden   = new int [slots];
    }

    // -- Configuration (called before each search) -------------------------

    public void SetModel(MinimalGM model) { _model = model; }
    public void SetRootMoves(MinimalMoveRecord[] rootMoves) { _rootMoves = rootMoves; }
    public void SetEvaluator(IMinMaxEvaluator evaluator) { _evaluator = evaluator; }

    // Expose model for evaluator use.
    public MinimalGM Model { get { return _model; } }

    // -- IMinMaxGameAdapter ------------------------------------------------

    public int GetCurrentPlayer() { return _model.CurrentPlayer; }

    public int GenerateMoves(int depthIdx)
    {
        int count = _model.GetValidMovesMinMax(_moveBuffers[depthIdx]);
        SortMovesForPruning(_moveBuffers[depthIdx], count);
        return count;
    }

    // -- Move ordering (best-first for alpha-beta pruning) -----------------
    //
    //  Priority (descending score):
    //   1. Moves that complete a placement line  (+500)
    //   2. Moves that reach >= 50 % fill ratio   (proportional, max +100)
    //   3. Factory source over central source    (+30)  – avoids gifting first-player token
    //   4. Penalty-line placements               (−500) – try last
    //
    //  Uses a pre-allocated score buffer + insertion sort (O(n²) acceptable
    //  because move counts in Azul are typically < 40 per node).

    private void SortMovesForPruning(MinimalMoveRecord[] moves, int count)
    {
        if (count <= 1) return;
        MinimalPlayer player = _model.Players[_model.CurrentPlayer];

        for (int i = 0; i < count; i++)
            _moveSortScores[i] = QuickMoveScore(ref moves[i], player);

        // Insertion sort descending by score.
        for (int i = 1; i < count; i++)
        {
            MinimalMoveRecord key      = moves[i];
            float             keyScore = _moveSortScores[i];
            int j = i - 1;
            while (j >= 0 && _moveSortScores[j] < keyScore)
            {
                moves[j + 1]           = moves[j];
                _moveSortScores[j + 1] = _moveSortScores[j];
                j--;
            }
            moves[j + 1]           = key;
            _moveSortScores[j + 1] = keyScore;
        }
    }

    private static float QuickMoveScore(ref MinimalMoveRecord m, MinimalPlayer player)
    {
        if (m.TargetIsPenalty) return -500f;

        int   line        = m.TargetLineIndex;
        int   capacity    = player.GetLineCapacity(line);
        int   current     = player.GetLineCount(line);
        int   after       = Math.Min(current + m.FlowerCount, capacity);
        float fillRatio   = (float)after / capacity;

        float score = fillRatio * 100f;              // 0–100 based on fill achieved
        if (after >= capacity) score += 500f;        // Line-completion bonus
        if (!m.IsCentralSource)  score += 30f;       // Prefer factory pick
        return score;
    }

    public bool ExecuteRootMoveAndAdvance(int depthIdx, int moveIdx)
    {
        _savedPlayer[depthIdx] = _model.CurrentPlayer;
        _savedTurn[depthIdx]   = _model.TurnNumber;
        _undoRecords[depthIdx] = _rootMoves[moveIdx];
        _model.ExecuteMoveMinMax(ref _undoRecords[depthIdx]);
        return _model.AdvanceTurnMinMax();
    }

    public bool ExecuteMoveAndAdvance(int depthIdx, int moveIdx)
    {
        _savedPlayer[depthIdx] = _model.CurrentPlayer;
        _savedTurn[depthIdx]   = _model.TurnNumber;
        _undoRecords[depthIdx] = _moveBuffers[depthIdx][moveIdx];
        _model.ExecuteMoveMinMax(ref _undoRecords[depthIdx]);
        return _model.AdvanceTurnMinMax();
    }

    public void UndoAndRestore(int depthIdx)
    {
        _model.ForceCurrentPlayer(_savedPlayer[depthIdx]);
        _model.ForceTurnNumber(_savedTurn[depthIdx]);
        _model.UndoMoveMinMax(ref _undoRecords[depthIdx]);
    }

    public bool IsTerminal()
    {
        return _model.IsGameOver || _model.AreDisplaysEmpty();
    }

    public int EstimateRemainingPly()
    {
        int picks = 0;

        for (int f = 0; f < _model.NumFactories; f++)
        {
            if (_model.GetFactoryflowerCount(f) > 0)
                picks += _model.CountDistinctColorsInFactory(f);
        }

        if (_model.CentralCount > 0)
            picks += _model.CountDistinctColorsInCentral();

        return Math.Max(1, picks);
    }

    public float Evaluate(int maximizingPlayer)
    {
        return _evaluator.Evaluate(_model, maximizingPlayer);
    }

    // -- Eval-safe state save/restore -------------------------------------

    public void SaveStateForEval()
    {
        int numPlayers = _model.NumPlayers;
        int numLines   = _model.Config.NumPlacementLines;

        _evalDiscardCount   = _model.DiscardCount;
        _evalStartingPlayer = _model.StartingPlayer;
        _evalIsGameOver     = _model.IsGameOver;

        for (int p = 0; p < numPlayers; p++)
        {
            MinimalPlayer pl    = _model.Players[p];
            _evalWallBits[p]    = pl.WallBits;
            _evalScores[p]      = pl.Score;
            _evalPenalties[p]   = pl.GetPenaltyCount();
            _evalTokens[p]      = pl.HasFirstPlayerToken;
            _evalCompleteRow[p] = pl.HasCompleteRow;

            int off = p * numLines;
            for (int l = 0; l < numLines; l++)
            {
                _evalLineColors[off + l] = pl.GetLineColor(l);
                _evalLineCounts[off + l] = pl.GetLineCount(l);
                _evalForbidden[off + l]  = pl.GetForbidden(l);
            }
        }
    }

    public void RestoreStateAfterEval()
    {
        int numPlayers = _model.NumPlayers;
        int numLines   = _model.Config.NumPlacementLines;

        _model.ForceDiscardCount(_evalDiscardCount);
        _model.ForceStartingPlayer(_evalStartingPlayer);
        _model.ForceIsGameOver(_evalIsGameOver);

        for (int p = 0; p < numPlayers; p++)
        {
            MinimalPlayer pl = _model.Players[p];
            pl.Score               = _evalScores[p];
            pl.HasFirstPlayerToken = _evalTokens[p];
            pl.HasCompleteRow      = _evalCompleteRow[p];
            pl.SetPenaltyCount(_evalPenalties[p]);
            pl.RestoreWallBits(_evalWallBits[p]);

            int off = p * numLines;
            for (int l = 0; l < numLines; l++)
            {
                pl.SetLineState(l, _evalLineColors[off + l], _evalLineCounts[off + l]);
                pl.SetForbiddenBits(l, _evalForbidden[off + l]);
            }
        }
    }
}
