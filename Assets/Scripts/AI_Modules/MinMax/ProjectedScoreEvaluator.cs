//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// -----------------------------------------------------------------------
//  ProjectedScoreEvaluator
//
//  Goal: evaluate a state by simulating an immediate end of round and then
//  valuing each player with a simple score model:
//    1) scored points after immediate round scoring
//    2) achievable end-game bonus projection
//    3) unfinished placement-line potential
//
//  Return value: maximizingPlayerTotal - bestOpponentTotal

public class ProjectedScoreEvaluator : IMinMaxEvaluator
{
    private const int ExpectedMaxRounds = 5;

    private readonly MinMaxGameAdapter _adapter;

    public ProjectedScoreEvaluator(MinMaxGameAdapter adapter)
    {
        _adapter = adapter;
    }

    public float Evaluate(MinimalGM model, int maximizingPlayer)
    {
        int roundsLeft = System.Math.Max(1, ExpectedMaxRounds - model.CurrentRound);

        _adapter.SaveStateForEval();
        model.ScoreEndOfRoundMinMax();

        float maximizingPlayerTotal = EvaluatePlayerAfterProjectedScoring(
            model,
            maximizingPlayer,
            roundsLeft);

        float bestOpponentTotal = float.NegativeInfinity;
        for (int playerIndex = 0; playerIndex < model.NumPlayers; playerIndex++)
        {
            if (playerIndex == maximizingPlayer) continue;

            float opponentTotal = EvaluatePlayerAfterProjectedScoring(
                model,
                playerIndex,
                roundsLeft);

            if (opponentTotal > bestOpponentTotal)
                bestOpponentTotal = opponentTotal;
        }

        if (bestOpponentTotal == float.NegativeInfinity)
            bestOpponentTotal = 0f;

        _adapter.RestoreStateAfterEval();
        return maximizingPlayerTotal - bestOpponentTotal;
    }

    private static float EvaluatePlayerAfterProjectedScoring(MinimalGM model, int playerIndex, int roundsLeft)
    {
        MinimalPlayer player = model.Players[playerIndex];

        float total = player.Score;
        total += ComputeAchievableEndGameBonus(model, player, playerIndex, roundsLeft);
        total += ComputePlacementLinePotential(model, player, playerIndex);
        return total;
    }

    private static float ComputeAchievableEndGameBonus(
        MinimalGM model,
        MinimalPlayer player,
        int playerIndex,
        int roundsLeft)
    {
        int gridSize = model.Config.GridSize;
        int colorCount = model.Config.NumColors;
        float bonus = 0f;

        for (int row = 0; row < gridSize; row++)
        {
            int filledCount = 0;
            for (int col = 0; col < gridSize; col++)
                if (player.IsWallOccupied(row, col)) filledCount++;

            bonus += ComputeAchievableBonus(model.Config.BonusRow, filledCount, gridSize, roundsLeft);
        }

        for (int col = 0; col < gridSize; col++)
        {
            int filledCount = 0;
            for (int row = 0; row < gridSize; row++)
                if (player.IsWallOccupied(row, col)) filledCount++;

            bonus += ComputeAchievableBonus(model.Config.BonusColumn, filledCount, gridSize, roundsLeft);
        }

        for (int colorIndex = 0; colorIndex < colorCount; colorIndex++)
        {
            int filledCount = 0;
            for (int row = 0; row < gridSize; row++)
            {
                int col = model.Config.GetWallColumn(playerIndex, row, colorIndex);
                if (player.IsWallOccupied(row, col)) filledCount++;
            }

            bonus += ComputeAchievableBonus(model.Config.BonusColor, filledCount, gridSize, roundsLeft);
        }

        return bonus;
    }

    private static float ComputeAchievableBonus(int fullBonusValue, int filledCount, int targetCount, int roundsLeft)
    {
        if (filledCount <= 0)
            return 0f;

        if (filledCount >= targetCount)
            return fullBonusValue;

        int missingCount = targetCount - filledCount;
        float achievability = (float)roundsLeft / missingCount;
        if (achievability > 1f)
            achievability = 1f;

        float completionRatio = (float)filledCount / targetCount;
        return fullBonusValue * completionRatio * achievability;
    }

    private static float ComputePlacementLinePotential(MinimalGM model, MinimalPlayer player, int playerIndex)
    {
        int lineCount = model.Config.NumPlacementLines;
        float totalPotential = 0f;

        for (int lineIndex = 0; lineIndex < lineCount; lineIndex++)
        {
            int tileCount = player.GetLineCount(lineIndex);
            if (tileCount <= 0)
                continue;

            int capacity = player.GetLineCapacity(lineIndex);
            if (tileCount >= capacity)
                continue;

            byte color = player.GetLineColor(lineIndex);
            if (color == 255)
                continue;

            int targetColumn = model.Config.GetWallColumn(playerIndex, lineIndex, color);
            int projectedWallPoints = model.CalculateWallPointsForEval(player, lineIndex, targetColumn);

            float fillRatio = (float)tileCount / capacity;
            totalPotential += projectedWallPoints * fillRatio;
        }

        return totalPotential;
    }
}
