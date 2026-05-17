
//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


// -----------------------------------------------------------------------
//  MinMaxBrain  -  IPlayerAIBrain adapter.
//  Wires up MinMax + MinMaxGameAdapter + IMinMaxEvaluator and converts
//  between the live GameModel world and the MinimalGM search model.
//
//  Default evaluator: ProjectedScoreEvaluator 
using System.Diagnostics;
using System.Collections.Generic;

public class MinMaxBrain : IPlayerAIBrain, IMinimalAIBrain
{
    #region FIELDS AND PARAMETERS
    public string BrainName { get { return "MinMax (d=" + _maxSearchDepth + ")"; } }

    private readonly int _maxSearchDepth;
    private float _searchTimeLimitMs;
    private readonly bool _usesIterativeDeepening;
    private readonly bool _writesDebugLogs;

    private readonly MinMaxGameAdapter _searchAdapter;
    private readonly MinMax _searchEngine;
    private readonly MinMaxDebugLogger _debugLogger;
    private bool _lastSearchTimedOut;

    private readonly MinimalMoveRecord[] _rootMoveBuffer =
        new MinimalMoveRecord[MinimalGM.MAX_MINMAX_MOVES];

    private MinimalGM _reusableMinimalModel;
    #endregion

    #region CONSTRUCTORS

    public MinMaxBrain(int maxDepth = 15, float timeLimitMs = 10000f,
                       bool useIterativeDeepening = true, bool enableDebugLog = false)
    {
        _maxSearchDepth = UnityEngine.Mathf.Max(1, maxDepth);
        _searchTimeLimitMs = timeLimitMs;
        _usesIterativeDeepening = useIterativeDeepening;
        _writesDebugLogs = enableDebugLog;

        _searchAdapter = new MinMaxGameAdapter(_maxSearchDepth);
        IMinMaxEvaluator evaluator = new ProjectedScoreEvaluator(_searchAdapter);
        _searchAdapter.SetEvaluator(evaluator);
        _searchEngine = new MinMax(_maxSearchDepth, _searchAdapter);

        if (_writesDebugLogs)
        {
            _debugLogger = new MinMaxDebugLogger();
            _searchEngine.Logger = _debugLogger;
        }
    }
    #endregion

    #region PUBLIC API

    public void SetEvaluator(IMinMaxEvaluator evaluator)
    {
        _searchAdapter.SetEvaluator(evaluator);
    }

    public void SetTimeLimitMs(float ms)
    {
        _searchTimeLimitMs = UnityEngine.Mathf.Max(1f, ms);
    }

    public GameMove ChooseMove(GameModel model, List<GameMove> validMoves)
    {
        if (validMoves.Count <= 1) return validMoves[0];

        _reusableMinimalModel = GameModelToMinimal.Convert(model, _reusableMinimalModel);

        int moveCount = _reusableMinimalModel.GetValidMovesMinMax(_rootMoveBuffer);
        if (moveCount == 0)
        {
            UnityEngine.Debug.LogWarning("[MinMaxBrain] MinimalGM generated 0 moves; falling back.");
            if (_debugLogger != null)
            {
                _debugLogger.RecordZeroMoves();
                _debugLogger.FlushToUnityLog();
            }
            return validMoves[0];
        }

        _searchAdapter.SetModel(_reusableMinimalModel);
        _searchAdapter.SetRootMoves(_rootMoveBuffer);
        _searchAdapter.SortRootMoves(moveCount);

        int maximizingPlayerIndex = _reusableMinimalModel.CurrentPlayer;
        int estimatedRemainingMoves = _searchAdapter.EstimateRemainingMoves();
        int depthToSearch = System.Math.Min(_maxSearchDepth, estimatedRemainingMoves);

        if (_debugLogger != null)
        {
            _debugLogger.BeginTurn(
                model.CurrentPlayerIndex,
                model.CurrentRound,
                model.TurnNumber,
                moveCount,
                estimatedRemainingMoves,
                depthToSearch);
        }

        int bestMoveIndex;
        if (_usesIterativeDeepening)
            bestMoveIndex = RunIterativeDeepeningSearch(moveCount, maximizingPlayerIndex, depthToSearch);
        else
            bestMoveIndex = RunSingleDepthSearch(moveCount, maximizingPlayerIndex, model, depthToSearch);

        if (bestMoveIndex < 0 || bestMoveIndex >= moveCount)
            bestMoveIndex = 0;

        GameMove chosenMove = MapMinimalMoveToGameMove(_rootMoveBuffer[bestMoveIndex], validMoves);

        if (_debugLogger != null)
            _debugLogger.FlushToUnityLog();

        return chosenMove;
    }

    public int ChooseMoveIndex(MinimalGM model, GameMove[] moves, int moveCount)
    {
        if (moveCount <= 1)
            return 0;

        int rootMoveCount = model.GetValidMovesMinMax(_rootMoveBuffer);
        if (rootMoveCount == 0)
            return 0;

        _searchAdapter.SetModel(model);
        _searchAdapter.SetRootMoves(_rootMoveBuffer);
        _searchAdapter.SortRootMoves(rootMoveCount);

        int maximizingPlayerIndex = model.CurrentPlayer;
        int estimatedRemainingMoves = _searchAdapter.EstimateRemainingMoves();
        int depthToSearch = System.Math.Min(_maxSearchDepth, estimatedRemainingMoves);

        int bestMoveIndex = _usesIterativeDeepening
            ? RunIterativeDeepeningSearch(rootMoveCount, maximizingPlayerIndex, depthToSearch)
            : RunSingleDepthSearch(rootMoveCount, maximizingPlayerIndex, null, depthToSearch);

        if (bestMoveIndex < 0 || bestMoveIndex >= rootMoveCount)
            bestMoveIndex = 0;

        return MapMinimalMoveToGameMoveIndex(_rootMoveBuffer[bestMoveIndex], moves, moveCount);
    }
    #endregion

    #region INTERNAL HELPERS

    private int RunSingleDepthSearch(int moveCount, int maximizingPlayerIndex, GameModel sourceModel, int depthToSearch)
    {
        SearchResult result = _searchEngine.FindBestMove(
            moveCount,
            depthToSearch,
            maximizingPlayerIndex,
            _searchTimeLimitMs);

        if (_writesDebugLogs)
            LogSearchResult(sourceModel, result, depthToSearch);

        if (_debugLogger != null)
        {
            _debugLogger.RecordResult(
                result.BestIndex,
                result.BestEval,
                result.DepthSearched,
                result.NodesEvaluated,
                result.ElapsedMs,
                result.TimedOut);
        }

        return result.BestIndex;
    }

    private int RunIterativeDeepeningSearch(int moveCount, int maximizingPlayerIndex, int depthToSearch)
    {
        Stopwatch timer = Stopwatch.StartNew();
        int bestMoveIndex = 0;
        float bestMoveEvaluation = float.NegativeInfinity;
        int deepestCompletedLayer = 0;
        _lastSearchTimedOut = false;

        for (int currentDepth = 1; currentDepth <= depthToSearch; currentDepth++)
        {
            float remainingTimeMs = _searchTimeLimitMs - (float)timer.Elapsed.TotalMilliseconds;
            if (remainingTimeMs <= 0) break;

            SearchResult result = _searchEngine.FindBestMove(
                moveCount,
                currentDepth,
                maximizingPlayerIndex,
                remainingTimeMs);

            if (!result.TimedOut || currentDepth == 1)
            {
                bestMoveIndex = result.BestIndex;
                bestMoveEvaluation = result.BestEval;
                deepestCompletedLayer = currentDepth;
                PromoteRootMoveToFront(bestMoveIndex);
                bestMoveIndex = 0;
            }

            if (_debugLogger != null)
            {
                _debugLogger.RecordIDLayer(
                    currentDepth,
                    result.BestEval,
                    result.NodesEvaluated,
                    result.ElapsedMs,
                    result.TimedOut);
            }

            if (_writesDebugLogs)
            {
                UnityEngine.Debug.Log("[MinMax] ID depth=" + currentDepth + " eval=" + result.BestEval +
                                      " nodes=" + result.NodesEvaluated +
                                      " time=" + result.ElapsedMs.ToString("F1") + "ms" +
                                      " timedOut=" + result.TimedOut);
            }

            if (result.TimedOut)
            {
                _lastSearchTimedOut = true;
                if (_writesDebugLogs)
                {
                    UnityEngine.Debug.Log("[MinMax] ID timed out at depth " + currentDepth +
                                          "; using best from depth " + deepestCompletedLayer);
                }
                break;
            }
        }

        float totalSearchTimeMs = (float)timer.Elapsed.TotalMilliseconds;
        if (_debugLogger != null)
        {
            _debugLogger.RecordResult(
                bestMoveIndex,
                bestMoveEvaluation,
                deepestCompletedLayer,
                0,
                totalSearchTimeMs,
                _lastSearchTimedOut);
        }

        if (_writesDebugLogs)
        {
            UnityEngine.Debug.Log("[MinMax] ID finished: depth=" + deepestCompletedLayer + "/" + depthToSearch +
                                  " eval=" + bestMoveEvaluation + " index=" + bestMoveIndex +
                                  " totalTime=" + totalSearchTimeMs.ToString("F1") + "ms");
        }

        return bestMoveIndex;
    }

    private void PromoteRootMoveToFront(int moveIndex)
    {
        if (moveIndex <= 0) return;

        MinimalMoveRecord bestMove = _rootMoveBuffer[moveIndex];
        for (int i = moveIndex; i > 0; i--)
            _rootMoveBuffer[i] = _rootMoveBuffer[i - 1];
        _rootMoveBuffer[0] = bestMove;
    }

    private void LogSearchResult(GameModel model, SearchResult result, int depth)
    {
        if (model == null)
            return;

        UnityEngine.Debug.Log("[MinMax] Player=" + model.CurrentPlayerIndex +
                  " Round=" + model.CurrentRound + " Turn=" + model.TurnNumber +
                  " Depth=" + depth + " Nodes=" + result.NodesEvaluated +
                  " Eval=" + result.BestEval + " Time=" + result.ElapsedMs.ToString("F1") + "ms" +
                  " TimedOut=" + result.TimedOut);
    }

    private GameMove MapMinimalMoveToGameMove(
        MinimalMoveRecord moveRecord,
        List<GameMove> validMoves)
    {
        FlowerColor expectedColor = (FlowerColor)moveRecord.ColorIndex;

        MoveSource expectedSource = MoveSource.Factory;
        if (moveRecord.IsCentralSource)
            expectedSource = MoveSource.Central;

        MoveTarget expectedTarget = MoveTarget.PlacementLine;
        if (moveRecord.TargetIsPenalty)
            expectedTarget = MoveTarget.PenaltyLine;

        int expectedLine = moveRecord.TargetLineIndex;
        int expectedFactory = moveRecord.FactoryIndex;

        foreach (GameMove candidateMove in validMoves)
        {
            if (candidateMove.Color != expectedColor) continue;
            if (candidateMove.SourceEnum != expectedSource) continue;
            if (candidateMove.TargetEnum != expectedTarget) continue;
            if (!candidateMove.IsPenalty && candidateMove.TargetLineIndex != expectedLine) continue;
            if (expectedSource == MoveSource.Factory && candidateMove.FactoryIndex != expectedFactory) continue;
            return candidateMove;
        }

        foreach (GameMove candidateMove in validMoves)
        {
            bool hasSameColor = candidateMove.Color == expectedColor;
            bool hasSameTargetType = candidateMove.TargetEnum == expectedTarget;
            bool hasCompatibleLine = candidateMove.IsPenalty || candidateMove.TargetLineIndex == expectedLine;

            if (hasSameColor && hasSameTargetType && hasCompatibleLine)
                return candidateMove;
        }

        UnityEngine.Debug.LogWarning("[MinMaxBrain] Could not map MinimalMoveRecord; using first.");
        if (_debugLogger != null)
            _debugLogger.RecordFallback();

        return validMoves[0];
    }

    private int MapMinimalMoveToGameMoveIndex(
        MinimalMoveRecord moveRecord,
        GameMove[] validMoves,
        int moveCount)
    {
        FlowerColor expectedColor = (FlowerColor)moveRecord.ColorIndex;

        MoveSource expectedSource = MoveSource.Factory;
        if (moveRecord.IsCentralSource)
            expectedSource = MoveSource.Central;

        MoveTarget expectedTarget = MoveTarget.PlacementLine;
        if (moveRecord.TargetIsPenalty)
            expectedTarget = MoveTarget.PenaltyLine;

        int expectedLine = moveRecord.TargetLineIndex;
        int expectedFactory = moveRecord.FactoryIndex;

        for (int i = 0; i < moveCount; i++)
        {
            GameMove candidateMove = validMoves[i];
            if (candidateMove.Color != expectedColor) continue;
            if (candidateMove.SourceEnum != expectedSource) continue;
            if (candidateMove.TargetEnum != expectedTarget) continue;
            if (!candidateMove.IsPenalty && candidateMove.TargetLineIndex != expectedLine) continue;
            if (expectedSource == MoveSource.Factory && candidateMove.FactoryIndex != expectedFactory) continue;
            return i;
        }

        for (int i = 0; i < moveCount; i++)
        {
            GameMove candidateMove = validMoves[i];
            bool hasSameColor = candidateMove.Color == expectedColor;
            bool hasSameTargetType = candidateMove.TargetEnum == expectedTarget;
            bool hasCompatibleLine = candidateMove.IsPenalty || candidateMove.TargetLineIndex == expectedLine;

            if (hasSameColor && hasSameTargetType && hasCompatibleLine)
                return i;
        }

        if (_debugLogger != null)
            _debugLogger.RecordFallback();

        return 0;
    }
    #endregion
}