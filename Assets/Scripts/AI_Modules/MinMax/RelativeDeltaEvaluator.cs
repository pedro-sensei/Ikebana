using Unity.InferenceEngine;
using UnityEngine;

// -----------------------------------------------------------------------
//  RelativeDeltaEvaluator  -  non-zero-sum MinMax evaluator.
//
//  Returns a relative delta from maximizing-player perspective:
//      score(maximizingPlayer) - score(opponent)
//
//  Terminal states use strong win/loss weights plus score margin.
//  Non-terminal states combine weighted score/material/positional deltas.
// -----------------------------------------------------------------------
public class RelativeDeltaEvaluator : IMinMaxEvaluator
{
    private const int MAX_PLAYERS = 4;
    private const int MAX_GRID_SIZE = 10;

    private readonly OptimizerEvaluator _optimizerEvaluator;

    private readonly int[] _savedWallBits = new int[MAX_PLAYERS];
    private readonly int[] _savedScores = new int[MAX_PLAYERS];
    private readonly int[] _savedPenalties = new int[MAX_PLAYERS];
    private readonly bool[] _savedTokens = new bool[MAX_PLAYERS];
    private readonly bool[] _savedCompleteRows = new bool[MAX_PLAYERS];
    private readonly byte[] _savedLineColors = new byte[MAX_PLAYERS * MAX_GRID_SIZE];
    private readonly int[] _savedLineCounts = new int[MAX_PLAYERS * MAX_GRID_SIZE];
    private readonly int[] _savedForbidden = new int[MAX_PLAYERS * MAX_GRID_SIZE];
    private int _savedDiscardCount;
    private int _savedStartingPlayer;
    private bool _savedIsGameOver;

    public float ImmediateSimulationWeight { get; set; }
    public float TerminalDeltaWeight { get; set; }
    public float ExpectedMoveWeight { get; set; }
    public bool UsePhaseAwareImmediateWeight { get; set; }
    public float MinImmediatePhaseFactor { get; set; }

    public RelativeDeltaEvaluator(
        float immediateSimulationWeight = 1f,
        float terminalDeltaWeight = 10000f,
        float expectedMoveWeight = 0.35f,
        bool usePhaseAwareImmediateWeight = true,
        OptimizerEvaluator optimizerEvaluator = null)
    {
        ImmediateSimulationWeight = immediateSimulationWeight;
        TerminalDeltaWeight = terminalDeltaWeight;
        ExpectedMoveWeight = expectedMoveWeight;
        UsePhaseAwareImmediateWeight = usePhaseAwareImmediateWeight;
        MinImmediatePhaseFactor = 0.2f;
        _optimizerEvaluator = optimizerEvaluator ?? new OptimizerEvaluator();
    }

    public float Evaluate(MinimalGM model, int maximizingPlayer)
    {
        float currentScoreDelta = ComputeScoreDelta(model, maximizingPlayer);
        bool terminalReachedInSimulation;
        float immediateScoreDelta = SimulateEndOfRoundScoreDelta(model, maximizingPlayer, out terminalReachedInSimulation);

        bool terminalReached = model.IsGameOver || terminalReachedInSimulation;
        float terminalScoreDelta = currentScoreDelta;
        if (terminalReached)
            terminalScoreDelta = immediateScoreDelta;

        float scoreDeltaImprovement = terminalScoreDelta - currentScoreDelta;

        float expectedMoveValue = _optimizerEvaluator.Evaluate(model, maximizingPlayer);
        float immediatePhaseScale = 1f;
        if (UsePhaseAwareImmediateWeight)
            immediatePhaseScale = ComputeImmediatePhaseScale(model);

        return (immediateScoreDelta * ImmediateSimulationWeight * immediatePhaseScale)
             + (scoreDeltaImprovement * TerminalDeltaWeight)
             + (expectedMoveValue * ExpectedMoveWeight);
    }

    private float SimulateEndOfRoundScoreDelta(MinimalGM model, int maximizingPlayer, out bool terminalReached)
    {
        SaveState(model);
        bool gameEnding = model.ScoreEndOfRoundMinMax();
        terminalReached = gameEnding;
        float scoreDelta;
        if (gameEnding)
            scoreDelta = ComputeScoreDeltaWithEndGameBonuses(model, maximizingPlayer);
        else
            scoreDelta = ComputeScoreDelta(model, maximizingPlayer);
        RestoreState(model);
        return scoreDelta;
    }

    private float ComputeImmediatePhaseScale(MinimalGM model)
    {
        int totalDisplayflowers = model.NumFactories * model.Config.flowersPerFactory;
        if (totalDisplayflowers <= 0) return 1f;

        int remainingflowers = model.CentralCount;
        for (int f = 0; f < model.NumFactories; f++)
            remainingflowers += model.GetFactoryflowerCount(f);

        float progress = 1f - ((float)remainingflowers / totalDisplayflowers);
        if (progress < 0f) progress = 0f;
        if (progress > 1f) progress = 1f;

        float minFactor = MinImmediatePhaseFactor;
        if (minFactor < 0f) minFactor = 0f;
        if (minFactor > 1f) minFactor = 1f;

        return minFactor + ((1f - minFactor) * progress);
    }

    private static float ComputeScoreDeltaWithEndGameBonuses(MinimalGM model, int maximizingPlayer)
    {
        float myTotal = model.Players[maximizingPlayer].Score
                      + ComputeFullEndGameBonus(model, maximizingPlayer);

        float bestOppTotal = float.NegativeInfinity;
        for (int p = 0; p < model.NumPlayers; p++)
        {
            if (p == maximizingPlayer) continue;
            float total = model.Players[p].Score + ComputeFullEndGameBonus(model, p);
            if (total > bestOppTotal) bestOppTotal = total;
        }

        if (bestOppTotal == float.NegativeInfinity) bestOppTotal = 0f;
        return myTotal - bestOppTotal;
    }

    private static int ComputeFullEndGameBonus(MinimalGM model, int playerIndex)
    {
        MinimalPlayer player = model.Players[playerIndex];
        int gridSize = model.Config.GridSize;
        int numColors = model.Config.NumColors;
        int bonus = 0;

        for (int r = 0; r < gridSize; r++)
        {
            bool full = true;
            for (int c = 0; c < gridSize; c++)
            {
                if (!player.IsWallOccupied(r, c))
                {
                    full = false;
                    break;
                }
            }
            if (full) bonus += model.Config.BonusRow;
        }

        for (int c = 0; c < gridSize; c++)
        {
            bool full = true;
            for (int r = 0; r < gridSize; r++)
            {
                if (!player.IsWallOccupied(r, c))
                {
                    full = false;
                    break;
                }
            }
            if (full) bonus += model.Config.BonusColumn;
        }

        for (int color = 0; color < numColors; color++)
        {
            bool allPlaced = true;
            for (int r = 0; r < gridSize; r++)
            {
                int col = model.Config.GetWallColumn(playerIndex, r, color);
                if (!player.IsWallOccupied(r, col))
                {
                    allPlaced = false;
                    break;
                }
            }
            if (allPlaced) bonus += model.Config.BonusColor;
        }

        return bonus;
    }

    private static float ComputeScoreDelta(MinimalGM model, int maximizingPlayer)
    {
        float myScore = model.Players[maximizingPlayer].Score;
        float bestOpponentScore = float.NegativeInfinity;

        for (int p = 0; p < model.NumPlayers; p++)
        {
            if (p == maximizingPlayer) continue;
            float score = model.Players[p].Score;
            if (score > bestOpponentScore) bestOpponentScore = score;
        }

        if (bestOpponentScore == float.NegativeInfinity) bestOpponentScore = 0f;
        return myScore - bestOpponentScore;
    }

    private void SaveState(MinimalGM model)
    {
        int numPlayers = model.NumPlayers;
        int numLines = model.Config.NumPlacementLines;

        _savedDiscardCount = model.DiscardCount;
        _savedStartingPlayer = model.StartingPlayer;
        _savedIsGameOver = model.IsGameOver;

        for (int p = 0; p < numPlayers; p++)
        {
            MinimalPlayer player = model.Players[p];
            _savedWallBits[p] = player.WallBits;
            _savedScores[p] = player.Score;
            _savedPenalties[p] = player.GetPenaltyCount();
            _savedTokens[p] = player.HasFirstPlayerToken;
            _savedCompleteRows[p] = player.HasCompleteRow;

            int offset = p * numLines;
            for (int l = 0; l < numLines; l++)
            {
                _savedLineColors[offset + l] = player.GetLineColor(l);
                _savedLineCounts[offset + l] = player.GetLineCount(l);
                _savedForbidden[offset + l] = player.GetForbidden(l);
            }
        }
    }

    private void RestoreState(MinimalGM model)
    {
        int numPlayers = model.NumPlayers;
        int numLines = model.Config.NumPlacementLines;

        model.ForceDiscardCount(_savedDiscardCount);
        model.ForceStartingPlayer(_savedStartingPlayer);
        model.ForceIsGameOver(_savedIsGameOver);

        for (int p = 0; p < numPlayers; p++)
        {
            MinimalPlayer player = model.Players[p];
            player.Score = _savedScores[p];
            player.HasFirstPlayerToken = _savedTokens[p];
            player.HasCompleteRow = _savedCompleteRows[p];
            player.SetPenaltyCount(_savedPenalties[p]);
            player.RestoreWallBits(_savedWallBits[p]);

            int offset = p * numLines;
            for (int l = 0; l < numLines; l++)
            {
                player.SetLineState(l, _savedLineColors[offset + l], _savedLineCounts[offset + l]);
                player.SetForbiddenBits(l, _savedForbidden[offset + l]);
            }
        }
    }
}


