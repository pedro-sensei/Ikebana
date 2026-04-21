using System.Diagnostics;

//=^..^=   =^..^=   VERSION 1.0.3 (April 2026)    =^..^=    =^..^=
//                    Last Update 16/04/2026
//=^..^=    =^..^=  By Pedro Sanchez Vazquez      =^..^=    =^..^=

//  MinMax  -  Alpha-beta search with iterative deepening.
//
//  Score:
//    positive = good for the maximizing player
//    negative = good for the minimizing player


public class MinMax
{
    private readonly int _maxDepth;
    private readonly IMinMaxGameAdapter _adapter;

    private long _timerTicks;
    private bool _timeUp;
    private int  _nodesEvaluated;

    private const int MAX_NODES = 5_000_000;

    //Debug, null default.
    public MinMaxDebugLogger Logger { get; set; }

    public MinMax(int maxDepth, IMinMaxGameAdapter adapter)
    {
        if (maxDepth < 1) maxDepth = 1;
        _maxDepth = maxDepth;
        _adapter  = adapter;
    }

    
    public SearchResult FindBestMove(
        int rootMoveCount,
        int depth,
        int maximizingPlayer,
        float timeLimitMs)
    {
        if (depth < 1) depth = 1;
        if (depth > _maxDepth) depth = _maxDepth;

        _nodesEvaluated = 0;
        _timeUp         = false;
        //Start timer
        _timerTicks  = Stopwatch.GetTimestamp()
                        + (long)(timeLimitMs * Stopwatch.Frequency / 1000.0);
        long startTicks = Stopwatch.GetTimestamp();

        SearchResult result = new SearchResult
        {
            BestIndex = 0, BestEval = float.NegativeInfinity, DepthSearched = depth
        };

        if (rootMoveCount == 0) { result.BestIndex = -1; return result; }
        if (rootMoveCount == 1) { result.BestIndex =  0; result.BestEval = 0f; return result; }

        float alpha = float.NegativeInfinity;
        float beta  = float.PositiveInfinity;

        for (int i = 0; i < rootMoveCount; i++)
        {
            if (_nodesEvaluated >= MAX_NODES || Stopwatch.GetTimestamp() >= _timerTicks)
            {
                _timeUp = true;
                break;
            }

            if (_timeUp) break;

            bool roundContinues = _adapter.ExecuteRootMoveAndAdvance(0, i);

            float eval;
            if (!roundContinues || depth <= 1)
            {
                _nodesEvaluated++;
                // Evaluate leaf node
                eval = _adapter.Evaluate(maximizingPlayer);
                Logger?.RecordLeafEval(eval);

                if ((_nodesEvaluated & 0x3FF) == 0 && Stopwatch.GetTimestamp() >= _timerTicks)
                    _timeUp = true;
            }
            else
            {
                bool nextIsMax = _adapter.GetCurrentPlayer() == maximizingPlayer;
                // Recurse
                eval = AlphaBeta(depth - 1, 1,
                                 alpha, beta, nextIsMax, maximizingPlayer);
            }

            //Undo move and restore state for next iteration
            _adapter.UndoAndRestore(0);

            if (eval > result.BestEval)
            {
                result.BestEval  = eval;
                result.BestIndex = i;
            }
            if (eval > alpha) alpha = eval;
            if (alpha >= beta) break;
        }

        long endTicks = Stopwatch.GetTimestamp();
        result.NodesEvaluated = _nodesEvaluated;
        result.TimedOut       = _timeUp;
        result.ElapsedMs      = (float)(endTicks - startTicks) * 1000f / Stopwatch.Frequency;
        return result;
    }

    // -- Alpha-Beta -------------------------------------------------------
    private float AlphaBeta(
        int depth, int depthIdx,
        float alpha, float beta,
        bool maximizing, int maximizingPlayer)
    {
        // Time / node-budget check
        if (_nodesEvaluated >= MAX_NODES || Stopwatch.GetTimestamp() >= _timerTicks)
        {
            _timeUp = true;
            _nodesEvaluated++;
            return _adapter.Evaluate(maximizingPlayer);
        }

        // Terminal
        if (depth <= 0 || _adapter.IsTerminal())
        {
            _nodesEvaluated++;
            return _adapter.Evaluate(maximizingPlayer);
        }

        // Generate moves
        int moveCount = _adapter.GenerateMoves(depthIdx);
        if (moveCount == 0)
        {
            _nodesEvaluated++;
            return _adapter.Evaluate(maximizingPlayer);
        }

        float best = maximizing ? float.NegativeInfinity : float.PositiveInfinity;

        for (int i = 0; i < moveCount; i++)
        {
            if (_timeUp) break;

            Logger?.RecordVisit(depthIdx);
            bool cont = _adapter.ExecuteMoveAndAdvance(depthIdx, i);

            float eval;
            if (!cont || depth <= 1)
            {
                _nodesEvaluated++;
                eval = _adapter.Evaluate(maximizingPlayer);
                Logger?.RecordLeafEval(eval);
                if ((_nodesEvaluated & 0xFFF) == 0 && Stopwatch.GetTimestamp() >= _timerTicks)
                    _timeUp = true;
            }
            else
            {
                bool nextIsMax = _adapter.GetCurrentPlayer() == maximizingPlayer;
                eval = AlphaBeta(depth - 1, depthIdx + 1,
                                 alpha, beta, nextIsMax, maximizingPlayer);
            }

            _adapter.UndoAndRestore(depthIdx);

            if (maximizing)
            {
                if (eval > best)  best  = eval;
                if (eval > alpha) alpha = eval;
            }
            else
            {
                if (eval < best) best = eval;
                if (eval < beta) beta = eval;
            }

            if (beta <= alpha)
            {
                Logger?.RecordCut(depthIdx);
                break;
            }
        }

        return best;
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
