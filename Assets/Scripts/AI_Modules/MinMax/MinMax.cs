using System.Diagnostics;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


//  MinMax  with Alpha-beta and iterative deepening.
//
//  Score:
//    positive = good for the maximizing player
//    negative = good for the minimizing player


public class MinMax
{
    private readonly int _searchDepthLimit;
    private readonly IMinMaxGameAdapter _gameAdapter;

    private long _deadlineTimestamp;
    private bool _hasTimedOut;
    private int _visitedNodeCount;

    private const int MaxNodeBudget = 5_000_000;

    //Debug, null default.
    public MinMaxDebugLogger Logger { get; set; }

    public MinMax(int maxDepth, IMinMaxGameAdapter adapter)
    {
        if (maxDepth < 1) maxDepth = 1;
        _searchDepthLimit = maxDepth;
        _gameAdapter = adapter;
    }

    
    public SearchResult FindBestMove(
        int rootMoveCount,
        int depth,
        int maximizingPlayer,
        float timeLimitMs)
    {
        int searchDepth = depth;
        if (searchDepth < 1) searchDepth = 1;
        if (searchDepth > _searchDepthLimit) searchDepth = _searchDepthLimit;

        _visitedNodeCount = 0;
        _hasTimedOut = false;
        _deadlineTimestamp = Stopwatch.GetTimestamp()
                           + (long)(timeLimitMs * Stopwatch.Frequency / 1000.0);
        long startTicks = Stopwatch.GetTimestamp();

        SearchResult searchResult = new SearchResult
        {
            BestIndex = 0,
            BestEval = float.NegativeInfinity,
            DepthSearched = searchDepth
        };

        if (rootMoveCount == 0)
        {
            searchResult.BestIndex = -1;
            return searchResult;
        }

        if (rootMoveCount == 1)
        {
            searchResult.BestIndex = 0;
            searchResult.BestEval = 0f;
            return searchResult;
        }

        float alpha = float.NegativeInfinity;
        float beta = float.PositiveInfinity;

        for (int rootMoveIndex = 0; rootMoveIndex < rootMoveCount; rootMoveIndex++)
        {
            if (IsOutOfSearchBudget())
            {
                _hasTimedOut = true;
                break;
            }

            if (_hasTimedOut) break;

            bool roundContinues = _gameAdapter.ExecuteRootMoveAndAdvance(0, rootMoveIndex);

            float moveEvaluation;
            if (!roundContinues || searchDepth <= 1)
            {
                _visitedNodeCount++;
                moveEvaluation = _gameAdapter.Evaluate(maximizingPlayer);
                Logger?.RecordLeafEval(moveEvaluation);

                if ((_visitedNodeCount & 0x3FF) == 0 && Stopwatch.GetTimestamp() >= _deadlineTimestamp)
                    _hasTimedOut = true;
            }
            else
            {
                bool nextTurnIsMaximizing = _gameAdapter.GetCurrentPlayer() == maximizingPlayer;
                moveEvaluation = AlphaBeta(
                    searchDepth - 1,
                    1,
                    alpha,
                    beta,
                    nextTurnIsMaximizing,
                    maximizingPlayer);
            }

            _gameAdapter.UndoAndRestore(0);

            if (moveEvaluation > searchResult.BestEval)
            {
                searchResult.BestEval = moveEvaluation;
                searchResult.BestIndex = rootMoveIndex;
            }
            if (moveEvaluation > alpha) alpha = moveEvaluation;
            if (alpha >= beta) break;
        }

        long endTicks = Stopwatch.GetTimestamp();
        searchResult.NodesEvaluated = _visitedNodeCount;
        searchResult.TimedOut = _hasTimedOut;
        searchResult.ElapsedMs = (float)(endTicks - startTicks) * 1000f / Stopwatch.Frequency;
        return searchResult;
    }

    private float AlphaBeta(
        int remainingDepth,
        int plyDepth,
        float alpha, float beta,
        bool isMaximizingTurn,
        int maximizingPlayer)
    {
        if (IsOutOfSearchBudget())
        {
            _hasTimedOut = true;
            _visitedNodeCount++;
            return _gameAdapter.Evaluate(maximizingPlayer);
        }

        if (remainingDepth <= 0 || _gameAdapter.IsTerminal())
        {
            _visitedNodeCount++;
            return _gameAdapter.Evaluate(maximizingPlayer);
        }

        int moveCount = _gameAdapter.GenerateMoves(plyDepth);
        if (moveCount == 0)
        {
            _visitedNodeCount++;
            return _gameAdapter.Evaluate(maximizingPlayer);
        }

        float bestEvaluation = isMaximizingTurn ? float.NegativeInfinity : float.PositiveInfinity;

        for (int moveIndex = 0; moveIndex < moveCount; moveIndex++)
        {
            if (_hasTimedOut) break;

            Logger?.RecordVisit(plyDepth);
            bool roundContinues = _gameAdapter.ExecuteMoveAndAdvance(plyDepth, moveIndex);

            float moveEvaluation;
            if (!roundContinues || remainingDepth <= 1)
            {
                _visitedNodeCount++;
                moveEvaluation = _gameAdapter.Evaluate(maximizingPlayer);
                Logger?.RecordLeafEval(moveEvaluation);
                if ((_visitedNodeCount & 0xFFF) == 0 && Stopwatch.GetTimestamp() >= _deadlineTimestamp)
                    _hasTimedOut = true;
            }
            else
            {
                bool nextTurnIsMaximizing = _gameAdapter.GetCurrentPlayer() == maximizingPlayer;
                moveEvaluation = AlphaBeta(
                    remainingDepth - 1,
                    plyDepth + 1,
                    alpha,
                    beta,
                    nextTurnIsMaximizing,
                    maximizingPlayer);
            }

            _gameAdapter.UndoAndRestore(plyDepth);

            if (isMaximizingTurn)
            {
                if (moveEvaluation > bestEvaluation) bestEvaluation = moveEvaluation;
                if (moveEvaluation > alpha) alpha = moveEvaluation;
            }
            else
            {
                if (moveEvaluation < bestEvaluation) bestEvaluation = moveEvaluation;
                if (moveEvaluation < beta) beta = moveEvaluation;
            }

            if (beta <= alpha)
            {
                Logger?.RecordCut(plyDepth);
                break;
            }
        }

        return bestEvaluation;
    }

    private bool IsOutOfSearchBudget()
    {
        return _visitedNodeCount >= MaxNodeBudget || Stopwatch.GetTimestamp() >= _deadlineTimestamp;
    }
}


public struct SearchResult
{
    public int BestIndex;
    public float BestEval;
    public int NodesEvaluated;
    public int DepthSearched;
    public bool TimedOut;
    public float ElapsedMs;
}
