using System.Text;
using UnityEngine;

// ==========================================================================
//  MinMaxDebugLogger  -  structured per-turn diagnostics for MinMaxBrain.
//
//  Design constraints
//  ??????????????????
//  • Zero allocations inside the hot search loop.
//    All string building happens AFTER the search, in FlushToUnityLog(),
//    which is called from the main thread after the background thread finishes.
//  • All fields written from the background search thread are plain value
//    types (int / float / bool).  No cross-thread Unity API calls.
//  • Reuses a single StringBuilder and a fixed-size per-depth array so
//    repeated calls do not generate GC pressure.
//
//  How to read the log
//  ???????????????????
//  [MM-TURN]   One line per ChooseMove() call.  Shows player, round, turn,
//              moves available, best move chosen and its eval, final depth,
//              node count, time, and whether the budget was exhausted.
//
//  [MM-ID]     One line per iterative-deepening depth layer.  Shows how the
//              best eval evolves as depth increases and how much time each
//              layer costs.  Watch for eval DROPS between layers – that
//              signals a horizon effect.
//
//  [MM-TREE]   Branching-factor and alpha-beta efficiency per depth level.
//              "visits" = nodes visited at that level.
//              "cuts"   = branches pruned by alpha-beta.
//              "cutPct" = cuts / (cuts + visits) – higher is better.
//              Low cutPct at shallow levels means move ordering is poor.
//
//  [MM-EVAL]   Min / max / mean of the leaf evaluations seen this turn.
//              Large spread ? position is highly uncertain.
//              Mean near 0  ? evaluator sees the position as balanced; the
//                             AI may be choosing almost arbitrarily.
//
//  [MM-WARN]   Anomaly flags written when something looks wrong:
//              FALLBACK    – best-move mapping failed; first move used.
//              TIMEOUT     – search ended on time / node budget.
//              ZERO_MOVES  – MinimalGM produced no moves (adapter bug?).
//              EVAL_FLAT   – all root evals within 0.1 of each other;
//                             evaluator may not differentiate moves.
//              BEST_FIRST  – best move is always index 0 (move ordering
//                             may already be perfect, or evaluator is weak).
//              NEG_EVAL    – chosen move has negative eval (losing position).
// ==========================================================================
public sealed class MinMaxDebugLogger
{
    // ?? per-depth visit / cut counters (written from search thread) ??????
    private const int MAX_DEPTH_SLOTS = 16;

    private readonly int[] _visits = new int[MAX_DEPTH_SLOTS];
    private readonly int[] _cuts   = new int[MAX_DEPTH_SLOTS];

    // ?? leaf-eval statistics ?????????????????????????????????????????????
    private float _evalMin;
    private float _evalMax;
    private double _evalSum;
    private int    _evalCount;

    // ?? per-ID-layer snapshot (written from search thread) ???????????????
    private const int MAX_ID_LAYERS = 16;
    private readonly IDLayerRecord[] _idLayers = new IDLayerRecord[MAX_ID_LAYERS];
    private int _idLayerCount;

    private struct IDLayerRecord
    {
        public int   Depth;
        public float BestEval;
        public int   Nodes;
        public float ElapsedMs;
        public bool  TimedOut;
    }

    // ?? turn-level context (set from main thread before search) ??????????
    private int   _playerIndex;
    private int   _round;
    private int   _turn;
    private int   _rootMoveCount;
    private int   _estimatedRemainingPly;
    private int   _clampedDepth;

    // ?? turn-level result (set from search thread) ????????????????????????
    private int   _chosenMoveIndex;
    private float _chosenEval;
    private int   _finalDepth;
    private int   _totalNodes;
    private float _totalMs;
    private bool  _timedOut;
    private bool  _fallback;
    private bool  _zeroMoves;

    // ?? reusable string builder ???????????????????????????????????????????
    private readonly StringBuilder _sb = new StringBuilder(2048);

    // =========================================================================
    //  API called from MinMaxBrain (main thread)
    // =========================================================================

    /// <summary>Call at the start of every ChooseMove(), before the search thread starts.</summary>
    public void BeginTurn(int playerIndex, int round, int turn,
                          int rootMoveCount, int estimatedRemainingPly, int clampedDepth)
    {
        _playerIndex           = playerIndex;
        _round                 = round;
        _turn                  = turn;
        _rootMoveCount         = rootMoveCount;
        _estimatedRemainingPly = estimatedRemainingPly;
        _clampedDepth          = clampedDepth;

        _chosenMoveIndex = 0;
        _chosenEval      = 0f;
        _finalDepth      = 0;
        _totalNodes      = 0;
        _totalMs         = 0f;
        _timedOut        = false;
        _fallback        = false;
        _zeroMoves       = false;

        _evalMin   =  float.PositiveInfinity;
        _evalMax   =  float.NegativeInfinity;
        _evalSum   =  0.0;
        _evalCount =  0;

        _idLayerCount = 0;

        for (int i = 0; i < MAX_DEPTH_SLOTS; i++)
        {
            _visits[i] = 0;
            _cuts[i]   = 0;
        }
    }

    /// <summary>
    /// Call after the search thread finishes, from the main thread,
    /// to emit all diagnostics to the Unity console.
    /// </summary>
    public void FlushToUnityLog()
    {
        _sb.Length = 0;

        // ?? [MM-TURN] ?????????????????????????????????????????????????????
        _sb.Append("[MM-TURN]")
           .Append(" P=").Append(_playerIndex)
           .Append(" R=").Append(_round)
           .Append(" T=").Append(_turn)
           .Append(" moves=").Append(_rootMoveCount)
           .Append(" plyEst=").Append(_estimatedRemainingPly)
           .Append(" depthCap=").Append(_clampedDepth)
           .Append(" | chosen=").Append(_chosenMoveIndex)
           .Append(" eval=").Append(_chosenEval.ToString("F2"))
           .Append(" depth=").Append(_finalDepth)
           .Append(" nodes=").Append(_totalNodes)
           .Append(" time=").Append(_totalMs.ToString("F1")).Append("ms")
           .Append(" timedOut=").Append(_timedOut);
        Debug.Log(_sb.ToString());

        // ?? [MM-ID] per layer ?????????????????????????????????????????????
        if (_idLayerCount > 0)
        {
            _sb.Length = 0;
            _sb.Append("[MM-ID]");
            float prevEval = float.NegativeInfinity;
            for (int i = 0; i < _idLayerCount; i++)
            {
                IDLayerRecord r = _idLayers[i];
                _sb.Append("  d").Append(r.Depth)
                   .Append(":eval=").Append(r.BestEval.ToString("F2"));
                if (i > 0 && prevEval != float.NegativeInfinity)
                {
                    float delta = r.BestEval - prevEval;
                    _sb.Append("(").Append(delta >= 0 ? "+" : "").Append(delta.ToString("F2")).Append(")");
                }
                _sb.Append(" n=").Append(r.Nodes)
                   .Append(" t=").Append(r.ElapsedMs.ToString("F1")).Append("ms");
                if (r.TimedOut) _sb.Append(" [TIMEOUT]");
                prevEval = r.BestEval;
            }
            Debug.Log(_sb.ToString());
        }

        // ?? [MM-TREE] branching / pruning per depth level ?????????????????
        int maxSlot = 0;
        for (int i = MAX_DEPTH_SLOTS - 1; i >= 0; i--)
            if (_visits[i] > 0 || _cuts[i] > 0) { maxSlot = i; break; }

        if (maxSlot > 0)
        {
            _sb.Length = 0;
            _sb.Append("[MM-TREE]");
            for (int d = 0; d <= maxSlot; d++)
            {
                int v   = _visits[d];
                int c   = _cuts[d];
                int tot = v + c;
                float cutPct = tot > 0 ? 100f * c / tot : 0f;
                _sb.Append("  d").Append(d)
                   .Append(":v=").Append(v)
                   .Append(",c=").Append(c)
                   .Append(",cut%=").Append(cutPct.ToString("F0")).Append("%");
            }
            Debug.Log(_sb.ToString());
        }

        // ?? [MM-EVAL] leaf statistics ?????????????????????????????????????
        if (_evalCount > 0)
        {
            float mean  = (float)(_evalSum / _evalCount);
            float range = _evalMax - _evalMin;
            _sb.Length = 0;
            _sb.Append("[MM-EVAL]")
               .Append(" leafCount=").Append(_evalCount)
               .Append(" min=").Append(_evalMin.ToString("F2"))
               .Append(" max=").Append(_evalMax.ToString("F2"))
               .Append(" mean=").Append(mean.ToString("F2"))
               .Append(" range=").Append(range.ToString("F2"));
            Debug.Log(_sb.ToString());
        }

        // ?? [MM-WARN] anomalies ???????????????????????????????????????????
        _sb.Length = 0;
        bool anyWarn = false;

        if (_zeroMoves)
        {
            _sb.Append("[MM-WARN] ZERO_MOVES – MinimalGM returned 0 moves. Adapter or model bug.");
            anyWarn = true;
        }
        if (_fallback)
        {
            if (anyWarn) _sb.Append('\n');
            _sb.Append("[MM-WARN] FALLBACK – best-move mapping failed; fell back to first valid move.");
            anyWarn = true;
        }
        if (_timedOut)
        {
            if (anyWarn) _sb.Append('\n');
            _sb.Append("[MM-WARN] TIMEOUT – search ended early. Increase time budget or reduce depth.");
            anyWarn = true;
        }
        if (_evalCount > 1)
        {
            float range = _evalMax - _evalMin;
            if (range < 0.1f)
            {
                if (anyWarn) _sb.Append('\n');
                _sb.Append("[MM-WARN] EVAL_FLAT – all leaf evals within ")
                   .Append(range.ToString("F3"))
                   .Append(". Evaluator may not differentiate moves; check ProjectedScoreEvaluator.");
                anyWarn = true;
            }
        }
        if (_chosenMoveIndex == 0 && _rootMoveCount > 1)
        {
            if (anyWarn) _sb.Append('\n');
            _sb.Append("[MM-WARN] BEST_FIRST – best move is index 0. " +
                       "This is fine if it recurs; suspicious if it always happens.");
            anyWarn = true;
        }
        if (_chosenEval < 0f)
        {
            if (anyWarn) _sb.Append('\n');
            _sb.Append("[MM-WARN] NEG_EVAL – chosen move eval=")
               .Append(_chosenEval.ToString("F2"))
               .Append(". AI is in a losing position according to the evaluator.");
            anyWarn = true;
        }

        if (anyWarn)
            Debug.LogWarning(_sb.ToString());
    }

    // =========================================================================
    //  API called from the search thread  (NO Unity API here)
    // =========================================================================

    /// <summary>Record one node visit at a given depth level.</summary>
    public void RecordVisit(int depthIdx)
    {
        if ((uint)depthIdx < (uint)MAX_DEPTH_SLOTS)
            _visits[depthIdx]++;
    }

    /// <summary>Record one alpha-beta cutoff at a given depth level.</summary>
    public void RecordCut(int depthIdx)
    {
        if ((uint)depthIdx < (uint)MAX_DEPTH_SLOTS)
            _cuts[depthIdx]++;
    }

    /// <summary>Record a leaf evaluation score.</summary>
    public void RecordLeafEval(float eval)
    {
        if (eval < _evalMin) _evalMin = eval;
        if (eval > _evalMax) _evalMax = eval;
        _evalSum   += eval;
        _evalCount++;
    }

    /// <summary>Called after each complete ID layer finishes.</summary>
    public void RecordIDLayer(int depth, float bestEval, int nodes, float elapsedMs, bool timedOut)
    {
        if (_idLayerCount < MAX_ID_LAYERS)
        {
            _idLayers[_idLayerCount++] = new IDLayerRecord
            {
                Depth     = depth,
                BestEval  = bestEval,
                Nodes     = nodes,
                ElapsedMs = elapsedMs,
                TimedOut  = timedOut
            };
        }
    }

    /// <summary>Set the final turn-level result, called from the search thread.</summary>
    public void RecordResult(int chosenIndex, float chosenEval, int finalDepth,
                             int totalNodes, float totalMs, bool timedOut)
    {
        _chosenMoveIndex = chosenIndex;
        _chosenEval      = chosenEval;
        _finalDepth      = finalDepth;
        _totalNodes      = totalNodes;
        _totalMs         = totalMs;
        _timedOut        = timedOut;
    }

    public void RecordFallback()   { _fallback  = true; }
    public void RecordZeroMoves()  { _zeroMoves = true; }
}
