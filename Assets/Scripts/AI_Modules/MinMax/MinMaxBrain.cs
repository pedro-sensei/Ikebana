
// -----------------------------------------------------------------------
//  MinMaxBrain  -  IPlayerAIBrain adapter.
//  Wires up MinMax + MinMaxGameAdapter + IMinMaxEvaluator and converts
//  between the live GameModel world and the MinimalGM search model.
//
//  Default evaluator: ProjectedScoreEvaluator (pure game-rule derived).
//  The evaluator can be swapped via SetEvaluator() for any
//  IMinMaxEvaluator (neural net, different heuristic, etc.).
// -----------------------------------------------------------------------
using System.Diagnostics;

public class MinMaxBrain : IPlayerAIBrain
{
    private readonly int _maxDepth;
    private float _timeLimitMs;
    private readonly bool _useIterativeDeepening;
    private readonly bool _enableDebugLog;

    private readonly MinMaxGameAdapter _adapter;
    private readonly MinMax _minMaxAlgo;
    private readonly MinMaxDebugLogger _logger;
    private bool _timedOutLastSearch;

    private readonly MinimalMoveRecord[] _rootMoves =
        new MinimalMoveRecord[MinimalGM.MAX_MINMAX_MOVES];

    private MinimalGM _searchModel;

    public string BrainName { get { return "MinMax (d=" + _maxDepth + ")"; } }

    public MinMaxBrain(int maxDepth = 15, float timeLimitMs = 10000f,
                       bool useIterativeDeepening = true, bool enableDebugLog = false)
    {
        _maxDepth = UnityEngine.Mathf.Max(1, maxDepth);
        _timeLimitMs = timeLimitMs;
        _useIterativeDeepening = useIterativeDeepening;
        _enableDebugLog = enableDebugLog;

        _adapter = new MinMaxGameAdapter(_maxDepth);
        IMinMaxEvaluator evaluator = new ProjectedScoreEvaluator(_adapter);
        _adapter.SetEvaluator(evaluator);
        _minMaxAlgo = new MinMax(_maxDepth, _adapter);

        if (_enableDebugLog)
        {
            _logger = new MinMaxDebugLogger();
            _minMaxAlgo.Logger = _logger;
        }
    }

    public void SetEvaluator(IMinMaxEvaluator evaluator)
    {
        _adapter.SetEvaluator(evaluator);
    }

    public void SetTimeLimitMs(float ms)
    {
        _timeLimitMs = UnityEngine.Mathf.Max(100f, ms);
    }

    public GameMove ChooseMove(GameModel model, System.Collections.Generic.List<GameMove> validMoves)
    {
        if (validMoves.Count <= 1) return validMoves[0];

        _searchModel = GameModelToMinimal.Convert(model, _searchModel);

        int moveCount = _searchModel.GetValidMovesMinMax(_rootMoves);
        if (moveCount == 0)
        {
            UnityEngine.Debug.LogWarning("[MinMaxBrain] MinimalGM generated 0 moves; falling back.");
            _logger?.RecordZeroMoves();
            _logger?.FlushToUnityLog();
            return validMoves[0];
        }

        // Configure adapter for this search
        _adapter.SetModel(_searchModel);
        _adapter.SetRootMoves(_rootMoves);

        int maxPlayer = _searchModel.CurrentPlayer;
        int estimatedPly = _adapter.EstimateRemainingPly();
        int clampedDepth = System.Math.Min(_maxDepth, estimatedPly);

        if (_logger != null)
            _logger.BeginTurn(model.CurrentPlayerIndex, model.CurrentRound,
                              model.TurnNumber, moveCount, estimatedPly, clampedDepth);

        int bestIdx = _useIterativeDeepening
            ? IterativeDeepening(moveCount, maxPlayer)
            : FlatSearch(moveCount, maxPlayer, model);

        if (bestIdx < 0 || bestIdx >= moveCount) bestIdx = 0;
        GameMove chosen = MapMinimalMoveToGameMove(_rootMoves[bestIdx], validMoves);

        _logger?.FlushToUnityLog();
        return chosen;
    }

    private int FlatSearch(int moveCount, int maxPlayer, GameModel srcModel)
    {
        SearchResult result = _minMaxAlgo.FindBestMove(
            moveCount, _maxDepth, maxPlayer, _timeLimitMs);
        if (_enableDebugLog) LogResult(srcModel, result, _maxDepth);
        _logger?.RecordResult(result.BestIndex, result.BestEval, result.DepthSearched,
                              result.NodesEvaluated, result.ElapsedMs, result.TimedOut);
        return result.BestIndex;
    }

    private int IterativeDeepening(int moveCount, int maxPlayer)
    {
        Stopwatch sw = Stopwatch.StartNew();
        int bestIdx = 0;
        float bestEval = float.NegativeInfinity;
        int lastCompletedDepth = 0;
        _timedOutLastSearch = false;

        for (int d = 1; d <= _maxDepth; d++)
        {
            float remaining = _timeLimitMs - (float)sw.Elapsed.TotalMilliseconds;
            if (remaining <= 0) break;

            SearchResult result = _minMaxAlgo.FindBestMove(
                moveCount, d, maxPlayer, remaining);

            if (!result.TimedOut || d == 1)
            {
                bestIdx = result.BestIndex;
                bestEval = result.BestEval;
                lastCompletedDepth = d;
            }

            _logger?.RecordIDLayer(d, result.BestEval, result.NodesEvaluated,
                                   result.ElapsedMs, result.TimedOut);

            if (_enableDebugLog)
                UnityEngine.Debug.Log("[MinMax] ID depth=" + d + " eval=" + result.BestEval +
                          " nodes=" + result.NodesEvaluated +
                          " time=" + result.ElapsedMs.ToString("F1") + "ms" +
                          " timedOut=" + result.TimedOut);

            if (result.TimedOut)
            {
                _timedOutLastSearch = true;
                if (_enableDebugLog)
                    UnityEngine.Debug.Log("[MinMax] ID timed out at depth " + d +
                              "; using best from depth " + lastCompletedDepth);
                break;
            }
        }

        float totalMs = (float)sw.Elapsed.TotalMilliseconds;
        _logger?.RecordResult(bestIdx, bestEval, lastCompletedDepth,
                              0, totalMs, _timedOutLastSearch);

        if (_enableDebugLog)
            UnityEngine.Debug.Log("[MinMax] ID finished: depth=" + lastCompletedDepth + "/" + _maxDepth +
                      " eval=" + bestEval + " index=" + bestIdx +
                      " totalTime=" + totalMs.ToString("F1") + "ms");

        return bestIdx;
    }

    private void LogResult(GameModel model, SearchResult result, int depth)
    {
        UnityEngine.Debug.Log("[MinMax] Player=" + model.CurrentPlayerIndex +
                  " Round=" + model.CurrentRound + " Turn=" + model.TurnNumber +
                  " Depth=" + depth + " Nodes=" + result.NodesEvaluated +
                  " Eval=" + result.BestEval + " Time=" + result.ElapsedMs.ToString("F1") + "ms" +
                  " TimedOut=" + result.TimedOut);
    }

    private GameMove MapMinimalMoveToGameMove(
        MinimalMoveRecord rec,
        System.Collections.Generic.List<GameMove> validMoves)
    {
        FlowerColor expectedColor = (FlowerColor)rec.ColorIndex;
        MoveSource expectedSource = rec.IsCentralSource ? MoveSource.Central : MoveSource.Factory;
        MoveTarget expectedTarget = rec.TargetIsPenalty ? MoveTarget.PenaltyLine : MoveTarget.PlacementLine;
        int expectedLine = rec.TargetLineIndex;
        int expectedFactory = rec.FactoryIndex;

        foreach (GameMove gm in validMoves)
        {
            if (gm.Color != expectedColor) continue;
            if (gm.SourceEnum != expectedSource) continue;
            if (gm.TargetEnum != expectedTarget) continue;
            if (!gm.IsPenalty && gm.TargetLineIndex != expectedLine) continue;
            if (expectedSource == MoveSource.Factory && gm.FactoryIndex != expectedFactory) continue;
            return gm;
        }
        foreach (GameMove gm in validMoves)
        {
            if (gm.Color == expectedColor && gm.TargetEnum == expectedTarget &&
                (gm.IsPenalty || gm.TargetLineIndex == expectedLine))
                return gm;
        }

        UnityEngine.Debug.LogWarning("[MinMaxBrain] Could not map MinimalMoveRecord; using first.");
        _logger?.RecordFallback();
        return validMoves[0];
    }
}