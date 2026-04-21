using System;
//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// -----------------------------------------------------------------------
//Adapt to minimal GM model.
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

    public int GenerateMoves(int plyDepth)
    {
        int count = _model.GetValidMovesMinMax(_moveBuffers[plyDepth]);
        SortMovesForPruning(_moveBuffers[plyDepth], count);
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

    private static float QuickMoveScore(ref MinimalMoveRecord moveRecord, MinimalPlayer player)
    {
        if (moveRecord.TargetIsPenalty) return -500f;

        int line = moveRecord.TargetLineIndex;
        int capacity = player.GetLineCapacity(line);
        int currentCount = player.GetLineCount(line);
        int nextCount = Math.Min(currentCount + moveRecord.FlowerCount, capacity);
        float fillRatio = (float)nextCount / capacity;

        float score = fillRatio * 100f;              // 0–100 based on fill achieved
        if (nextCount >= capacity) score += 500f;    // Line-completion bonus
        if (!moveRecord.IsCentralSource) score += 30f; // Prefer factory pick
        return score;
    }

    public bool ExecuteRootMoveAndAdvance(int plyDepth, int moveIndex)
    {
        _savedPlayer[plyDepth] = _model.CurrentPlayer;
        _savedTurn[plyDepth] = _model.TurnNumber;
        _undoRecords[plyDepth] = _rootMoves[moveIndex];
        _model.ExecuteMoveMinMax(ref _undoRecords[plyDepth]);
        return _model.AdvanceTurnMinMax();
    }

    public bool ExecuteMoveAndAdvance(int plyDepth, int moveIndex)
    {
        _savedPlayer[plyDepth] = _model.CurrentPlayer;
        _savedTurn[plyDepth] = _model.TurnNumber;
        _undoRecords[plyDepth] = _moveBuffers[plyDepth][moveIndex];
        _model.ExecuteMoveMinMax(ref _undoRecords[plyDepth]);
        return _model.AdvanceTurnMinMax();
    }

    public void UndoAndRestore(int plyDepth)
    {
        _model.ForceCurrentPlayer(_savedPlayer[plyDepth]);
        _model.ForceTurnNumber(_savedTurn[plyDepth]);
        _model.UndoMoveMinMax(ref _undoRecords[plyDepth]);
    }

    public bool IsTerminal()
    {
        return _model.IsGameOver || _model.AreDisplaysEmpty();
    }

    public int EstimateRemainingMoves()
    {
        int estimatedMoveCount = 0;

        for (int factoryIndex = 0; factoryIndex < _model.NumFactories; factoryIndex++)
        {
            if (_model.GetFactoryflowerCount(factoryIndex) > 0)
                estimatedMoveCount += _model.CountDistinctColorsInFactory(factoryIndex);
        }

        if (_model.CentralCount > 0)
            estimatedMoveCount += _model.CountDistinctColorsInCentral();

        return Math.Max(1, estimatedMoveCount);
    }

    public float Evaluate(int maximizingPlayer)
    {
        return _evaluator.Evaluate(_model, maximizingPlayer);
    }

    // -- Eval-safe state save/restore -------------------------------------

    public void SaveStateForEval()
    {
        int playerCount = _model.NumPlayers;
        int lineCount = _model.Config.NumPlacementLines;

        _evalDiscardCount   = _model.DiscardCount;
        _evalStartingPlayer = _model.StartingPlayer;
        _evalIsGameOver     = _model.IsGameOver;

        for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
        {
            MinimalPlayer player = _model.Players[playerIndex];
            _evalWallBits[playerIndex] = player.WallBits;
            _evalScores[playerIndex] = player.Score;
            _evalPenalties[playerIndex] = player.GetPenaltyCount();
            _evalTokens[playerIndex] = player.HasFirstPlayerToken;
            _evalCompleteRow[playerIndex] = player.HasCompleteRow;

            int offset = playerIndex * lineCount;
            for (int lineIndex = 0; lineIndex < lineCount; lineIndex++)
            {
                _evalLineColors[offset + lineIndex] = player.GetLineColor(lineIndex);
                _evalLineCounts[offset + lineIndex] = player.GetLineCount(lineIndex);
                _evalForbidden[offset + lineIndex] = player.GetForbidden(lineIndex);
            }
        }
    }

    public void RestoreStateAfterEval()
    {
        int playerCount = _model.NumPlayers;
        int lineCount = _model.Config.NumPlacementLines;

        _model.ForceDiscardCount(_evalDiscardCount);
        _model.ForceStartingPlayer(_evalStartingPlayer);
        _model.ForceIsGameOver(_evalIsGameOver);

        for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
        {
            MinimalPlayer player = _model.Players[playerIndex];
            player.Score = _evalScores[playerIndex];
            player.HasFirstPlayerToken = _evalTokens[playerIndex];
            player.HasCompleteRow = _evalCompleteRow[playerIndex];
            player.SetPenaltyCount(_evalPenalties[playerIndex]);
            player.RestoreWallBits(_evalWallBits[playerIndex]);

            int offset = playerIndex * lineCount;
            for (int lineIndex = 0; lineIndex < lineCount; lineIndex++)
            {
                player.SetLineState(lineIndex, _evalLineColors[offset + lineIndex], _evalLineCounts[offset + lineIndex]);
                player.SetForbiddenBits(lineIndex, _evalForbidden[offset + lineIndex]);
            }
        }
    }
}
