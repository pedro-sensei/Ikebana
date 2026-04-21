// -----------------------------------------------------------------------
//  ProjectedScoreEvaluator  -  evaluates a game state by projecting the
//  score as if the round ended immediately.
//
//  1. Save the full model state (via the adapter).
//  2. Run end-of-round scoring (places completed lines on the wall,
//     applies penalty, etc.).
//  3. For each player compute:
//       ProjectedScore = RoundScore (after scoring)
//                      + End-game bonuses (full) or discounted partial bonuses
//                        scaled by achievability relative to rounds remaining
//                        (games rarely exceed 5 rounds, so partial sets that
//                        require more tiles than rounds left are discounted).
//                      + Partial placement-line credit with a shaped weight:
//                        lines filled >= 50 % of capacity earn positive credit;
//                        lines filled < 50 % capacity incur a small penalty
//                        (filling a line barely is usually a bad move).
//                        When trailing by > 4 pts the under-half penalty is
//                        softened so gap-closing scoring moves stay attractive.
//  4. Restore the model state.
//  5. Return:  myProjectedScore - bestOpponentProjectedScore
//
//  This evaluator requires a MinMaxGameAdapter to handle the save/restore
//  of model state around the destructive ScoreEndOfRoundMinMax call.
//  It is completely independent from the MinMax algorithm.
// -----------------------------------------------------------------------
public class ProjectedScoreEvaluator : IMinMaxEvaluator
{
    private readonly MinMaxGameAdapter _adapter;

    public ProjectedScoreEvaluator(MinMaxGameAdapter adapter)
    {
        _adapter = adapter;
    }

// -----------------------------------------------------------------------
//  RelativeDeltaEvaluator  -  non-zero-sum MinMax evaluator.
// -----------------------------------------------------------------------
public class RelativeDeltaEvaluator : IMinMaxEvaluator
{
    private const float TerminalWeight = 10000f;
    private const float ScoreWeight = 4.0f;
    private const float MaterialWeight = 1.5f;
    private const float PositionalWeight = 1.0f;

    public float Evaluate(MinimalGM model, int maximizingPlayer)
    {
        int opponent = GetOpponentIndex(model, maximizingPlayer);

        MinimalPlayer me = model.Players[maximizingPlayer];
        MinimalPlayer opp = model.Players[opponent];

        float scoreDiff = me.Score - opp.Score;

        bool terminal = model.IsGameOver || model.AreDisplaysEmpty();
        if (terminal)
        {
            if (scoreDiff > 0f) return TerminalWeight + scoreDiff;
            if (scoreDiff < 0f) return -TerminalWeight + scoreDiff;
            return scoreDiff;
        }

        float materialDiff = ComputeMaterial(model, me) - ComputeMaterial(model, opp);
        float positionalDiff = ComputePositional(model, me, maximizingPlayer)
                             - ComputePositional(model, opp, opponent);

        return (ScoreWeight * scoreDiff)
             + (MaterialWeight * materialDiff)
             + (PositionalWeight * positionalDiff);
    }

    private static int GetOpponentIndex(MinimalGM model, int maximizingPlayer)
    {
        if (model.NumPlayers <= 1) return maximizingPlayer;
        if (model.NumPlayers == 2) return 1 - maximizingPlayer;
        return (maximizingPlayer + 1) % model.NumPlayers;
    }

    private static float ComputeMaterial(MinimalGM model, MinimalPlayer p)
    {
        int wallTiles = CountBits(p.WallBits);

        int lineTiles = 0;
        int numLines = model.Config.NumPlacementLines;
        for (int l = 0; l < numLines; l++)
            lineTiles += p.GetLineCount(l);

        int penaltyTiles = p.GetPenaltyCount();
        return wallTiles + lineTiles - penaltyTiles;
    }

    private static float ComputePositional(MinimalGM model, MinimalPlayer p, int playerIndex)
    {
        int size = model.Config.GridSize;

        float centerControl = 0f;
        float center = (size - 1) * 0.5f;
        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                if (!p.IsWallOccupied(r, c)) continue;
                float dist = System.Math.Abs(r - center) + System.Math.Abs(c - center);
                centerControl += (size - dist) * 0.1f;
            }
        }

        float mobility = 0f;
        int numLines = model.Config.NumPlacementLines;
        int numColors = model.Config.NumColors;
        for (int l = 0; l < numLines; l++)
        {
            for (byte color = 0; color < numColors; color++)
            {
                if (p.CanPlaceInLine(l, color))
                {
                    mobility += 1f;
                    break;
                }
            }
        }

        float lineProgress = 0f;
        for (int l = 0; l < numLines; l++)
        {
            int cap = p.GetLineCapacity(l);
            if (cap <= 0) continue;
            lineProgress += (float)p.GetLineCount(l) / cap;
        }

        float centerColumnBias = 0f;
        for (int l = 0; l < numLines; l++)
        {
            int count = p.GetLineCount(l);
            if (count <= 0) continue;
            byte color = p.GetLineColor(l);
            if (color == 255) continue;

            int col = model.Config.GetWallColumn(playerIndex, l, color);
            float dist = System.Math.Abs(col - center);
            centerColumnBias += (size - dist) * 0.05f;
        }

        return centerControl + lineProgress + (0.2f * mobility) + centerColumnBias;
    }

    private static int CountBits(int v)
    {
        int count = 0;
        while (v != 0)
        {
            v &= v - 1;
            count++;
        }
        return count;
    }
}

    // Expected maximum game length used when discounting partial end-game bonuses.
    private const int EXPECTED_MAX_ROUNDS = 5;

    // Bonus per adjacent wall tile for a placement line targeting a connected cell.
    // Incentivises building connected wall structures rather than isolated tiles.
    private const float ADJACENCY_POTENTIAL_FACTOR = 1.5f;

    // Upside value of holding the first-player token (going first next round).
    // The floor-line penalty for the token is already deducted by ComputePenaltyProjection.
    private const float FIRST_PLAYER_TOKEN_BONUS = 4.0f;

    // Scales the penalty applied when the center pool is dense relative to
    // the player's remaining floor-line capacity (flood-risk heuristic).
    private const float CENTER_FLOOD_RISK_FACTOR = 1.2f;

    public float Evaluate(MinimalGM model, int maximizingPlayer)
    {
        // Sample pre-scoring scores to detect a trailing situation.
        // When the maximizing player is behind by more than 4 points the
        // under-half placement penalty is softened, keeping gap-closing
        // scoring moves attractive relative to clean-board play.
        float myPreScore      = model.Players[maximizingPlayer].Score;
        float bestOppPreScore = 0f;
        int   numPlayers      = model.NumPlayers;
        for (int p = 0; p < numPlayers; p++)
        {
            if (p == maximizingPlayer) continue;
            float s = model.Players[p].Score;
            if (s > bestOppPreScore) bestOppPreScore = s;
        }
        // partialPenaltyScale: 1.0 = normal, 0.25 = trailing (reduce under-half penalty).
        float trailingGap         = bestOppPreScore - myPreScore;
        float partialPenaltyScale = trailingGap > 4f ? 0.25f : 1.0f;

        // Rounds remaining signal for end-game bonus discounting.
        int roundsLeft = System.Math.Max(1, EXPECTED_MAX_ROUNDS - model.CurrentRound);

        _adapter.SaveStateForEval();

        model.ScoreEndOfRoundMinMax();

        float myTotal = PlayerTotal(model, model.Players[maximizingPlayer], maximizingPlayer, roundsLeft, partialPenaltyScale);

        float bestOppTotal = float.NegativeInfinity;
        for (int p = 0; p < numPlayers; p++)
        {
            if (p == maximizingPlayer) continue;
            float t = PlayerTotal(model, model.Players[p], p, roundsLeft, 1.0f);
            if (t > bestOppTotal) bestOppTotal = t;
        }
        if (bestOppTotal == float.NegativeInfinity) bestOppTotal = 0f;

        _adapter.RestoreStateAfterEval();

        return myTotal - bestOppTotal;
    }

    // -- Per-player total -------------------------------------------------

    private static float PlayerTotal(MinimalGM model, MinimalPlayer player, int playerIndex,
                                     int roundsLeft, float partialPenaltyScale)
    {
        return player.Score
             + ComputeEndGameBonus(model, player, playerIndex, roundsLeft)
             + ComputePartialLines(model, player, playerIndex, partialPenaltyScale)
             + ComputeAdjacencyPotential(model, player, playerIndex)
             + ComputeFirstPlayerBonus(player)
             - ComputePenaltyProjection(model, player)
             - ComputeCenterFloodRisk(model, player);
    }

    // -- Full end-game bonuses for completed sets -------------------------
    // Completed sets earn their full bonus.
    // Partial sets use a linear gradient scaled by an achievability factor:
    //   achievability = clamp(roundsLeft / missing, 0, 1)
    // A set requiring more tiles than rounds likely remain is discounted
    // toward zero, since games rarely last more than EXPECTED_MAX_ROUNDS.

    private static float ComputeEndGameBonus(MinimalGM model, MinimalPlayer player,
                                             int playerIndex, int roundsLeft)
    {
        int   gridSize  = model.Config.GridSize;
        int   numColors = model.Config.NumColors;
        float bonus     = 0f;

        // Row bonuses
        for (int r = 0; r < gridSize; r++)
        {
            int filled = 0;
            for (int c = 0; c < gridSize; c++)
                if (player.IsWallOccupied(r, c)) filled++;

            if (filled == gridSize)
            {
                bonus += model.Config.BonusRow;
            }
            else if (filled > 0)
            {
                int   missing       = gridSize - filled;
                float achievability = System.Math.Min(1f, (float)roundsLeft / missing);
                bonus += model.Config.BonusRow * ((float)filled / gridSize) * achievability;
            }
        }

        // Column bonuses
        for (int c = 0; c < gridSize; c++)
        {
            int filled = 0;
            for (int r = 0; r < gridSize; r++)
                if (player.IsWallOccupied(r, c)) filled++;

            if (filled == gridSize)
            {
                bonus += model.Config.BonusColumn;
            }
            else if (filled > 0)
            {
                int   missing       = gridSize - filled;
                float achievability = System.Math.Min(1f, (float)roundsLeft / missing);
                bonus += model.Config.BonusColumn * ((float)filled / gridSize) * achievability;
            }
        }

        // Color bonuses
        for (int color = 0; color < numColors; color++)
        {
            int filled = 0;
            for (int r = 0; r < gridSize; r++)
            {
                int col = model.Config.GetWallColumn(playerIndex, r, color);
                if (player.IsWallOccupied(r, col)) filled++;
            }

            if (filled == gridSize)
            {
                bonus += model.Config.BonusColor;
            }
            else if (filled > 0)
            {
                int   missing       = gridSize - filled;
                float achievability = System.Math.Min(1f, (float)roundsLeft / missing);
                bonus += model.Config.BonusColor * ((float)filled / gridSize) * achievability;
            }
        }

        return bonus;
    }

    // -- Partial placement lines ------------------------------------------
    // For each incomplete placement line, project the wall points it would
    // earn if completed, using a shaped fill weight:
    //
    //   fillRatio >= 0.5  →  weight = fillRatio           (positive credit)
    //   fillRatio <  0.5  →  weight = fillRatio - 0.5     (negative penalty)
    //
    // A barely-started line (under half capacity) costs opportunity (blocks
    // the color slot, unlikely to complete this round) and is penalised.
    // When the player is trailing by > 4 pts the under-half penalty is
    // scaled down (partialPenaltyScale = 0.25) so that gap-closing scoring
    // moves remain more attractive than purely clean-board alternatives.

    private static float ComputePartialLines(MinimalGM model, MinimalPlayer player,
                                             int playerIndex, float partialPenaltyScale)
    {
        int   numLines = model.Config.NumPlacementLines;
        float value    = 0f;

        for (int line = 0; line < numLines; line++)
        {
            int count = player.GetLineCount(line);
            if (count == 0) continue;

            int capacity = player.GetLineCapacity(line);
            if (count >= capacity) continue;

            byte color = player.GetLineColor(line);
            if (color == 255) continue;

            int col     = model.Config.GetWallColumn(playerIndex, line, (int)color);
            int wallPts = model.CalculateWallPointsForEval(player, line, col);

            float fillRatio = (float)count / capacity;

            float weight;
            if (fillRatio >= 0.5f)
            {
                // Non-linear boost for near-complete lines:
                // a line at 80 %+ capacity scores 2× more than a 50 % line,
                // rewarding the MinMax agent for committing to completions.
                weight = fillRatio * fillRatio * 2f;
            }
            else
            {
                weight = (fillRatio - 0.5f) * partialPenaltyScale;
            }

            value += wallPts * weight;
        }

        return value;
    }

    // -- Penalty projection -----------------------------------------------
    // Penalise the tiles already sitting on the penalty line that have not
    // yet been scored.  ScoreEndOfRoundMinMax deducts them, but between
    // scoring phases the evaluator must account for them so the search
    // actively avoids accumulating penalty tokens.

    private static float ComputePenaltyProjection(MinimalGM model, MinimalPlayer player)
    {
        int penaltyCount = player.GetPenaltyCount();
        if (penaltyCount <= 0) return 0f;
        float total = 0f;
        for (int i = 0; i < penaltyCount; i++)
            total += model.PenaltyValue(i);
        return total;
    }

    // -- Wall adjacency potential -----------------------------------------
    //
    //  For each non-empty placement line whose target wall cell is adjacent
    //  to at least one already-placed tile, award a bonus proportional to
    //  adjacency count × fill progress.  This steers MinMax toward building
    //  connected wall structures, which yield exponentially higher scores
    //  as rows / columns approach completion.

    private static float ComputeAdjacencyPotential(MinimalGM model, MinimalPlayer player, int playerIndex)
    {
        int   numLines = model.Config.NumPlacementLines;
        int   gridSize = model.Config.GridSize;
        float bonus    = 0f;

        for (int line = 0; line < numLines; line++)
        {
            int count = player.GetLineCount(line);
            if (count == 0) continue;

            byte color = player.GetLineColor(line);
            if (color == 255) continue;

            int col = model.Config.GetWallColumn(playerIndex, line, (int)color);

            int adjacent = 0;
            if (col  > 0          && player.IsWallOccupied(line, col - 1)) adjacent++;
            if (col  < gridSize-1 && player.IsWallOccupied(line, col + 1)) adjacent++;
            if (line > 0          && player.IsWallOccupied(line - 1, col)) adjacent++;
            if (line < gridSize-1 && player.IsWallOccupied(line + 1, col)) adjacent++;

            if (adjacent > 0)
            {
                float fillRatio = (float)count / player.GetLineCapacity(line);
                bonus += adjacent * fillRatio * ADJACENCY_POTENTIAL_FACTOR;
            }
        }
        return bonus;
    }

    // -- First-player token -----------------------------------------------
    //
    //  The player holding the first-player token will pick first next round,
    //  granting more choice and board control.  The floor-line cost of the
    //  token itself is already captured by ComputePenaltyProjection; this
    //  term adds back the strategic upside of turn-order priority.

    private static float ComputeFirstPlayerBonus(MinimalPlayer player)
    {
        return player.HasFirstPlayerToken ? FIRST_PLAYER_TOKEN_BONUS : 0f;
    }

    // -- Center flood risk ------------------------------------------------
    //
    //  A dense center pool late in a round is dangerous: someone must take
    //  those tiles.  If the player already has tiles on the floor line (few
    //  open slots remain), additional overflow from the center is costly.
    //  Deduct a risk term proportional to (center count − 2) / open slots.
    //
    //  The threshold of 2 avoids penalising normal center accumulation;
    //  risk only activates once the center becomes genuinely crowded.

    private static float ComputeCenterFloodRisk(MinimalGM model, MinimalPlayer player)
    {
        int centralCount = model.CentralCount;
        if (centralCount <= 2) return 0f;

        int penaltyCapacity = model.Config.PenaltyCapacity;
        int openSlots       = penaltyCapacity - player.GetPenaltyCount();
        if (openSlots <= 0) return 0f;   // Floor already full; no marginal risk.

        float densityRatio = (float)(centralCount - 2) / openSlots;
        return System.Math.Min(densityRatio, 3.0f) * CENTER_FLOOD_RISK_FACTOR;
    }
}
