using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;


//=^..^=   =^..^=   VERSION 1.0.2 (April 2026)    =^..^=    =^..^=
//                    Last Update 01/042026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// MinMax algorithm with alpha-beta pruning operating on ModelRedux.
//
//Model redux was added due to poor performance.

// Implements time limit control, unity was freezing.
//
// Thread-safe as long as each caller owns its own instance.
public class MinMaxRedux
{
    //Buffers

    private readonly int         _maxDepth;
    private readonly MoveRedux[][] _moveBuffers;
    private readonly MoveRecord[]  _undoRecords;

    // Sized to MAX_PLAYERS since we don't know game config
    private readonly int[]  _evalWalls;
    private readonly int[]  _evalScores;
    private readonly int[]  _evalPenalties;
    private readonly bool[] _evalTokens;

    private readonly int[]  _evalLineColors;
    private readonly int[]  _evalLineCounts;
    private readonly int[]  _evalForbidden;

    // Separate buffer for saving the discard array during evaluation
    private readonly int[]  _evalSavedDiscard;

    //Time limits

    private long   _deadlineTicks;
    private bool   _timeUp;

    // Search stats for reporting/debugging

    private int _nodesEvaluated;
    
    //Grid size 10 for future variants.
    public MinMaxRedux(int maxDepth, int maxGridSize = 10)
    {
        _maxDepth   = System.Math.Max(1, maxDepth);
        _moveBuffers = new MoveRedux[_maxDepth][];
        _undoRecords = new MoveRecord[_maxDepth];

        int safeNumColors = maxGridSize*2;
        for (int d = 0; d < _maxDepth; d++)
        {
            _moveBuffers[d] = new MoveRedux[ModelRedux.MAX_MOVES];
            _undoRecords[d] = MoveRecord.Create(safeNumColors);
        }

        _evalWalls      = new int[ModelRedux.MAX_PLAYERS];
        _evalScores     = new int[ModelRedux.MAX_PLAYERS];
        _evalPenalties  = new int[ModelRedux.MAX_PLAYERS];
        _evalTokens     = new bool[ModelRedux.MAX_PLAYERS];
        _evalLineColors = new int[ModelRedux.MAX_PLAYERS * maxGridSize];
        _evalLineCounts = new int[ModelRedux.MAX_PLAYERS * maxGridSize];
        _evalForbidden  = new int[ModelRedux.MAX_PLAYERS * maxGridSize];
        _evalSavedDiscard = new int[safeNumColors];
    }


    public struct SearchResult
    {
        public int   BestIndex;
        public int   BestEval;
        public int   NodesEvaluated;
        public int   DepthSearched;
        public bool  TimedOut;
        public float ElapsedMs;
    }

    // Finds the best move index from the given move array.
  
    public SearchResult FindBestMove(
        ref ModelRedux state,
        MoveRedux[] rootMoves, int rootMoveCount,
        int depth, int maximizingPlayer,
        float timeLimitMs)
    {
        depth = System.Math.Min(depth, _maxDepth);
        _nodesEvaluated = 0;
        _timeUp = false;

        long budgetTicks = (long)(timeLimitMs * Stopwatch.Frequency / 1000.0);
        _deadlineTicks = Stopwatch.GetTimestamp() + budgetTicks;

        long startTicks = Stopwatch.GetTimestamp();

        SearchResult result = new SearchResult
        {
            BestIndex    = 0,
            BestEval     = int.MinValue,
            DepthSearched = depth
        };

        if (rootMoveCount == 0) { result.BestIndex = -1; return result; }
        if (rootMoveCount == 1) { result.BestIndex = 0; result.BestEval = 0; return result; }

        int alpha = int.MinValue;
        int beta  = int.MaxValue;

        for (int i = 0; i < rootMoveCount; i++)
        {
            if (_timeUp) break;

            state.ExecuteMove(rootMoves[i], ref _undoRecords[0]);
            bool roundContinues = state.AdvanceTurn();

            int eval;
            if (!roundContinues || depth <= 1)
            {
                _nodesEvaluated++;
                eval = Evaluate(ref state, maximizingPlayer);
            }
            else
            {
                bool nextIsMax = state.CurrentPlayer == maximizingPlayer;
                eval = AlphaBeta(ref state, depth - 1, 1, alpha, beta,
                                 nextIsMax, maximizingPlayer);
            }

            // Undo advance + move
            state.TurnNumber    = _undoRecords[0].PrevTurnNumber + 1;
            state.CurrentPlayer = (_undoRecords[0].PrevCurrentPlayer + 1) % state.NumPlayers;
            state.UndoMove(ref _undoRecords[0]);

            if (eval > result.BestEval)
            {
                result.BestEval  = eval;
                result.BestIndex = i;
            }
            if (eval > alpha) alpha = eval;
        }

        long endTicks = Stopwatch.GetTimestamp();
        result.NodesEvaluated = _nodesEvaluated;
        result.TimedOut       = _timeUp;
        result.ElapsedMs      = (float)(endTicks - startTicks) * 1000f / Stopwatch.Frequency;

        return result;
    }

    private int AlphaBeta(
        ref ModelRedux state, int depth, int depthIdx,
        int alpha, int beta,
        bool maximizing, int maximizingPlayer)
    {
        if (Stopwatch.GetTimestamp() >= _deadlineTicks)
        {
            _timeUp = true;
            _nodesEvaluated++;
            return Evaluate(ref state, maximizingPlayer);
        }

        if (depth == 0 || state.IsGameOver || state.AreDisplaysEmpty())
        {
            _nodesEvaluated++;
            return Evaluate(ref state, maximizingPlayer);
        }

        MoveRedux[] moves = _moveBuffers[depthIdx];
        int moveCount = state.GetValidMoves(moves);

        if (moveCount == 0)
        {
            _nodesEvaluated++;
            return Evaluate(ref state, maximizingPlayer);
        }

        // Remember turn state before the loop since AdvanceTurn modifies it
        int savedPlayer = state.CurrentPlayer;
        int savedTurn   = state.TurnNumber;

        if (maximizing)
        {
            int maxEval = int.MinValue;
            for (int i = 0; i < moveCount; i++)
            {
                if (_timeUp) break;

                state.ExecuteMove(moves[i], ref _undoRecords[depthIdx]);
                bool roundContinues = state.AdvanceTurn();

                int eval;
                if (!roundContinues || depth <= 1)
                {
                    _nodesEvaluated++;
                    eval = Evaluate(ref state, maximizingPlayer);
                }
                else
                {
                    bool nextIsMax = state.CurrentPlayer == maximizingPlayer;
                    eval = AlphaBeta(ref state, depth - 1, depthIdx + 1,
                                     alpha, beta, nextIsMax, maximizingPlayer);
                }

                // Undo AdvanceTurn
                state.CurrentPlayer = savedPlayer;
                state.TurnNumber    = savedTurn;
                state.UndoMove(ref _undoRecords[depthIdx]);

                if (eval > maxEval) maxEval = eval;
                if (eval > alpha)   alpha = eval;
                if (beta <= alpha)  break;
            }
            return maxEval;
        }
        else
        {
            int minEval = int.MaxValue;
            for (int i = 0; i < moveCount; i++)
            {
                if (_timeUp) break;

                state.ExecuteMove(moves[i], ref _undoRecords[depthIdx]);
                bool roundContinues = state.AdvanceTurn();

                int eval;
                if (!roundContinues || depth <= 1)
                {
                    _nodesEvaluated++;
                    eval = Evaluate(ref state, maximizingPlayer);
                }
                else
                {
                    bool nextIsMax = state.CurrentPlayer == maximizingPlayer;
                    eval = AlphaBeta(ref state, depth - 1, depthIdx + 1,
                                     alpha, beta, nextIsMax, maximizingPlayer);
                }

                state.CurrentPlayer = savedPlayer;
                state.TurnNumber    = savedTurn;
                state.UndoMove(ref _undoRecords[depthIdx]);

                if (eval < minEval) minEval = eval;
                if (eval < beta)    beta = eval;
                if (beta <= alpha)  break;
            }
            return minEval;
        }
    }

    // Evaluates the board from the maximizing player's point of view.
    // Simulates end-of-round scoring (wall placement + penalty) 

    // Returns (myTotal - bestOpponentTotal)
    private int Evaluate(ref ModelRedux state, int playerIndex)
    {
        int numPlayers = state.NumPlayers;
        int numLines   = state.NumPlacementLines;
        int numColors  = state.NumColors;

        // Save discard into dedicated buffer
        for (int c = 0; c < numColors; c++)
            _evalSavedDiscard[c] = state.Discard[c];

        int savedStarting = state.StartingPlayer;
        bool savedGameOver = state.IsGameOver;

        for (int p = 0; p < numPlayers; p++)
        {
            ref PlayerRedux pl = ref state.Players[p];
            _evalWalls[p]     = pl.WallBits;
            _evalScores[p]    = pl.Score;
            _evalPenalties[p] = pl.PenaltyCount;
            _evalTokens[p]    = pl.HasFirstPlayerToken;

            int off = p * numLines;
            for (int i = 0; i < numLines; i++)
            {
                _evalLineColors[off + i] = pl.LineColors[i];
                _evalLineCounts[off + i] = pl.LineCounts[i];
                _evalForbidden[off + i]  = pl.Forbidden[i];
            }
        }

        // Run the scoring simulation
        state.ScoreEndOfRound();

        // Read scores and estimate bonuses on the scored wall
        float myScore   = state.Players[playerIndex].Score;
        float myBonus   = EstimateEndGameBonus(ref state, ref state.Players[playerIndex]);
        float myPartial = EstimatePartialLines(ref state, ref state.Players[playerIndex]);

        float bestOppTotal = float.MinValue;
        for (int p = 0; p < numPlayers; p++)
        {
            if (p == playerIndex) continue;
            float s  = state.Players[p].Score;
            float b  = EstimateEndGameBonus(ref state, ref state.Players[p]);
            float pv = EstimatePartialLines(ref state, ref state.Players[p]);
            float total = s + b + pv;
            if (total > bestOppTotal) bestOppTotal = total;
        }

        // Restore discard
        for (int c = 0; c < numColors; c++)
            state.Discard[c] = _evalSavedDiscard[c];

        state.StartingPlayer = savedStarting;
        state.IsGameOver     = savedGameOver;

        for (int p = 0; p < numPlayers; p++)
        {
            ref PlayerRedux pl = ref state.Players[p];
            pl.WallBits          = _evalWalls[p];
            pl.Score             = _evalScores[p];
            pl.PenaltyCount      = _evalPenalties[p];
            pl.HasFirstPlayerToken = _evalTokens[p];

            int off = p * numLines;
            for (int i = 0; i < numLines; i++)
            {
                pl.LineColors[i] = _evalLineColors[off + i];
                pl.LineCounts[i] = _evalLineCounts[off + i];
                pl.Forbidden[i]  = _evalForbidden[off + i];
            }
        }

        float eval = (myScore + myBonus + myPartial) - bestOppTotal;
        return (int)System.Math.Round(eval);
    }

    //End-game bonus

    private static float EstimateEndGameBonus(ref ModelRedux state, ref PlayerRedux player)
    {
        int gridSize  = state.GridSize;
        int numColors = state.NumColors;
        float bonus = 0f;

        // Row completion
        for (int r = 0; r < gridSize; r++)
        {
            int filled = 0;
            for (int c = 0; c < gridSize; c++)
                if (player.IsWallOccupied(r, c, gridSize)) filled++;
            if (filled == gridSize)
                bonus += state.BonusRow;
            else if (filled > 0)
                bonus += (float)state.BonusRow * filled * filled / (gridSize * gridSize);
        }

        // Column completion
        for (int c = 0; c < gridSize; c++)
        {
            int filled = 0;
            for (int r = 0; r < gridSize; r++)
                if (player.IsWallOccupied(r, c, gridSize)) filled++;
            if (filled == gridSize)
                bonus += state.BonusColumn;
            else if (filled > 0)
                bonus += (float)state.BonusColumn * filled * filled / (gridSize * gridSize);
        }

        // Color completion
        for (int color = 0; color < numColors; color++)
        {
            int filled = 0;
            for (int r = 0; r < gridSize; r++)
            {
                int col = state.GetGridColumn(r, color);
                if (player.IsWallOccupied(r, col, gridSize)) filled++;
            }
            if (filled == gridSize)
                bonus += state.BonusColor;
            else if (filled > 0)
                bonus += (float)state.BonusColor * filled * filled / (gridSize * gridSize);
        }
        //TODO: ADD MORE IN VARIANTS 
        return bonus;
    }

    private static float EstimatePartialLines(ref ModelRedux state, ref PlayerRedux player)
    {
        int numLines  = state.NumPlacementLines;
        int numColors = state.NumColors;
        int gridSize  = state.GridSize;
        float value = 0f;
        for (int line = 0; line < numLines; line++)
        {
            int count = player.LineCounts[line];
            if (count == 0) continue;
            int capacity = state.PlacementLineCapacities[line];
            if (count >= capacity) continue;

            int color = player.LineColors[line];
            if (color < 0 || color >= numColors) continue;

            int col = state.GetGridColumn(line, color);
            int wallPts = state.CalculateWallPoints(ref player, line, col);
            float progress = (float)count / capacity;
            value += wallPts * progress;
        }
        return value;
    }
}

// MinMax AI brain backed by ModelRedux + MinMaxRedux.
// Converts GameModel to ModelRedux, runs search, maps result back to GameMove.

public class MinMaxReduxBrain : IPlayerAIBrain
{
    private readonly int   _maxDepth;
    private float _timeLimitMs = 10000f;
    private readonly bool  _useIterativeDeepening;
    private readonly bool  _enableDebugLog;
    private readonly MinMaxRedux _minMaxAlgo;

    public string BrainName
    {
        get { return "MinMax Redux (d=" + _maxDepth + ")"; }
    }

    public MinMaxReduxBrain(int maxDepth = 15, float timeLimitMs = 10000f,
                            bool useIterativeDeepening = true, bool enableDebugLog = true)
    {
        _maxDepth             = Mathf.Max(1, maxDepth);
        _timeLimitMs          = timeLimitMs;
        _useIterativeDeepening = useIterativeDeepening;
        _enableDebugLog       = enableDebugLog;
        _minMaxAlgo             = new MinMaxRedux(_maxDepth);
    }

    public void SetTimeLimitMs(float ms)
    {
        _timeLimitMs = ms;
    }

    public GameMove ChooseMove(GameModel model, List<GameMove> validMoves)
    {
        if (validMoves.Count <= 1) return validMoves[0];

        // Convert GameModel to ModelRedux
        ModelRedux redux = GameModelToRedux.Convert(model);

        // Generate moves in Redux space
        MoveRedux[] reduxMoves = new MoveRedux[ModelRedux.MAX_MOVES];
        int moveCount = redux.GetValidMoves(reduxMoves);

        if (moveCount == 0)
        {
            Debug.LogWarning("[MinMaxReduxBrain] Redux generated 0 moves; falling back.");
            return validMoves[0];
        }

        int maximizingPlayer = redux.CurrentPlayer;
        int bestIdx = 0;

        if (_useIterativeDeepening)
        {
            bestIdx = IterativeDeepening(ref redux, reduxMoves, moveCount, maximizingPlayer);
        }
        else
        {
            MinMaxRedux.SearchResult result = _minMaxAlgo.FindBestMove(
                ref redux, reduxMoves, moveCount,
                _maxDepth, maximizingPlayer, _timeLimitMs);
            bestIdx = result.BestIndex;

            if (_enableDebugLog)
                LogResult(model, result, _maxDepth);
        }

        if (bestIdx < 0 || bestIdx >= moveCount) bestIdx = 0;

        MoveRedux chosen = reduxMoves[bestIdx];
        return MapReduxMoveToGameMove(chosen, validMoves);
    }

    private int IterativeDeepening(ref ModelRedux redux, MoveRedux[] moves, int moveCount, int maxPlayer)
    {
        Stopwatch sw = Stopwatch.StartNew();
        int bestIdx = 0;
        int bestEval = int.MinValue;
        int lastCompletedDepth = 0;

        for (int d = 1; d <= _maxDepth; d++)
        {
            float remaining = _timeLimitMs - (float)sw.Elapsed.TotalMilliseconds;
            if (remaining <= 0) break;

            MinMaxRedux.SearchResult result = _minMaxAlgo.FindBestMove(
                ref redux, moves, moveCount, d, maxPlayer, remaining);

            // Only accept this iteration if it completed (not timed out),
            if (!result.TimedOut || d == 1)
            {
                bestIdx  = result.BestIndex;
                bestEval = result.BestEval;
                lastCompletedDepth = d;
            }

            if (_enableDebugLog)
            {
                Debug.Log("[MinMaxRedux] ID depth=" + d + " eval=" + result.BestEval +
                          " nodes=" + result.NodesEvaluated + " time=" + result.ElapsedMs.ToString("F1") + "ms" +
                          " timedOut=" + result.TimedOut);
            }

            if (result.TimedOut) break;
        }

        if (_enableDebugLog)
        {
            Debug.Log("[MinMaxRedux] Iterative deepening finished: " +
                      "depth=" + lastCompletedDepth + "/" + _maxDepth +
                      " eval=" + bestEval + " index=" + bestIdx +
                      " totalTime=" + sw.Elapsed.TotalMilliseconds.ToString("F1") + "ms");
        }

        return bestIdx;
    }

    private void LogResult(GameModel model, MinMaxRedux.SearchResult result, int depth)
    {
        Debug.Log("[MinMaxRedux] Player=" + model.CurrentPlayerIndex +
                  " Round=" + model.CurrentRound + " Turn=" + model.TurnNumber +
                  " Depth=" + depth + " Nodes=" + result.NodesEvaluated +
                  " Eval=" + result.BestEval + " Time=" + result.ElapsedMs.ToString("F1") + "ms" +
                  " TimedOut=" + result.TimedOut);
    }

 
    // Maps a MoveRedux back to a GameMove.
    private static GameMove MapReduxMoveToGameMove(MoveRedux redux, List<GameMove> validMoves)
    {

        FlowerColor expectedColor = (FlowerColor)redux.ColorIndex;
        MoveSource expectedSource = redux.IsCentralSource ? MoveSource.Central : MoveSource.Factory;
        int expectedFactory = redux.FactoryIndex;
        MoveTarget expectedTarget = redux.TargetIsPenalty ? MoveTarget.PenaltyLine : MoveTarget.PlacementLine;
        int expectedLine = redux.TargetLineIndex;
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

        Debug.LogWarning("[MinMaxReduxBrain] Could not map " + redux + " to GameMove; using first valid move.");
        return validMoves[0];
    }
}


// Converts a live GameModel into a ModelRedux for MinMax search.
public static class GameModelToRedux
{
    public static ModelRedux Convert(GameModel model)
    {
        int numPlayers = model.NumberOfPlayers;
        ModelRedux redux = ModelRedux.Create(numPlayers);

        int gridSize  = redux.GridSize;
        int numColors = redux.NumColors;

        redux.CurrentRound   = model.CurrentRound;
        redux.CurrentPlayer  = model.CurrentPlayerIndex;
        redux.TurnNumber     = model.TurnNumber;
        redux.StartingPlayer = model.StartingPlayerIndex;
        redux.IsGameOver     = model.IsGameOver;

        // Bag (count per color)
        foreach (FlowerPiece flower in model.Bag.flowers)
        {
            int c = (int)flower.Color;
            if (c >= 0 && c < numColors) redux.Bag[c]++;
        }

        // Discard
        foreach (FlowerPiece flower in model.Discard.flowers)
        {
            int c = (int)flower.Color;
            if (c >= 0 && c < numColors) redux.Discard[c]++;
        }

        // Central display
        foreach (FlowerPiece flower in model.CentralDisplay.flowers)
        {
            int c = (int)flower.Color;
            if (c >= 0 && c < numColors) redux.Central[c]++;
        }
        redux.CentralHasToken = model.CentralDisplay.HasFirstPlayerToken;

        // Displays (0-based in redux)
        int numFactories = model.FactoryDisplays.Length;
        for (int f = 0; f < numFactories && f < redux.NumDisplays; f++)
        {
            DisplayModel factory = model.FactoryDisplays[f];
            foreach (FlowerPiece flower in factory.flowers)
            {
                int c = (int)flower.Color;
                if (c >= 0 && c < numColors) redux.Displays[f * numColors + c]++;
            }
        }

        // Players
        for (int p = 0; p < numPlayers; p++)
        {
            PlayerModel src = model.Players[p];
            ref PlayerRedux dst = ref redux.Players[p];

            dst.Score              = src.Score;
            dst.HasFirstPlayerToken = src.HasFirstPlayerToken;

            // Wall bits
            for (int r = 0; r < gridSize; r++)
                for (int c = 0; c < gridSize; c++)
                    if (src.Grid.IsOccupied(r, c))
                        dst.SetWallOccupied(r, c, true, gridSize);

            // Placement lines
            int numLines = src.PlacementLines.Length;
            for (int l = 0; l < numLines && l < redux.NumPlacementLines; l++)
            {
                PlacementLineModel line = src.PlacementLines[l];
                dst.LineCounts[l] = line.Count;
                if (line.Count > 0 && line.CurrentColor.HasValue)
                    dst.LineColors[l] = (int)line.CurrentColor.Value;
                else
                    dst.LineColors[l] = -1;
            }

            // Forbidden colors per row
            for (int r = 0; r < gridSize; r++)
            {
                for (int c = 0; c < gridSize; c++)
                {
                    if (src.Grid.IsOccupied(r, c))
                    {
                        int colorIdx = (int)src.Grid.GetColorAt(r, c);
                        if (colorIdx >= 0 && colorIdx < numColors)
                            dst.SetForbidden(r, colorIdx);
                    }
                }
            }

            // Penalty count
            dst.PenaltyCount = src.PenaltyLine.Count;
            if (src.HasFirstPlayerToken && dst.PenaltyCount < redux.PenaltyCapacity)
                dst.PenaltyCount++; // token takes one penalty slot
        }

        return redux;
    }
}
