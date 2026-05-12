using System;
using System;

//=^..^=   =^..^=   VERSION 1.1.1 (May 2026)    =^..^=    =^..^=
//                    Last Update 10/05/2026
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// Turns live game data into numbers that agents can learn from.
// Most methods are small building blocks so different agents can reuse them.

public static class FeatureExtractor
{
    // -----------------------------------------------------------------------
    // Game config and general info

    public static int NumPlayers(GameModel m) => m.NumberOfPlayers;

    public static int NumFactories(GameModel m) => m.FactoryDisplays.Length;

    public static int NumSources(GameModel m) => m.FactoryDisplays.Length + 1;

    public static int NumColors() => GameConfig.NUM_COLORS;

    public static int GridSize() => GameConfig.GRID_SIZE;

    public static int NumLines() => GameConfig.NUM_PLACEMENT_LINES;

    public static int LineCapacity(int line) => GameConfig.PLACEMENT_LINE_CAPACITIES[line];

    public static int PenaltyCap() => GameConfig.PENALTY_LINE_CAPACITY;

    public static int TotalFlowers() => GameConfig.TOTAL_FLOWERS;

    public static int FlowersPerColor() => GameConfig.COUNT_PER_COLOR;

    public static int FlowersPerDisplay() => GameConfig.FLOWERS_PER_DISPLAY;

    // -----------------------------------------------------------------------
    // Round and turn info

    public static int CurrentRound(GameModel m) => m.CurrentRound;

    public static int TurnNumber(GameModel m) => m.TurnNumber;

    public static int CurrentPlayerIndex(GameModel m) => m.CurrentPlayerIndex;

    public static int StartingPlayerIndex(GameModel m) => m.StartingPlayerIndex;

    public static float RoundNorm(GameModel m) => m.CurrentRound / 10f;

    public static bool IsGameOver(GameModel m) => m.IsGameOver;

    public static RoundPhaseEnum Phase(GameModel m) => m.CurrentPhase;

    // -----------------------------------------------------------------------
    // Score helpers

    // Current scores for every player by player index
    public static int[] Scores(GameModel m)
    {
        int n = m.NumberOfPlayers;
        int[] s = new int[n];
        for (int i = 0; i < n; i++) s[i] = m.Players[i].Score;
        return s;
    }

    public static int Score(GameModel m, int p) => m.Players[p].Score;

    // Positive values mean the other player is ahead.
    public static int[] ScoreDiffs(GameModel m, int selfIdx)
    {
        int self = m.Players[selfIdx].Score;
        int n = m.NumberOfPlayers;
        int[] d = new int[n];
        for (int i = 0; i < n; i++) d[i] = m.Players[i].Score - self;
        return d;
    }

    public static float ScoreNorm(GameModel m, int p, float maxScore = 240f)
        => m.Players[p].Score / maxScore;

    public static float ScoreDiffNorm(GameModel m, int selfIdx, int oppIdx, float maxScore = 240f)
        => (m.Players[selfIdx].Score - m.Players[oppIdx].Score) / maxScore;

    // Rank 0 means best score so far.
    public static int[] ScoreRanks(GameModel m)
    {
        int n = m.NumberOfPlayers;
        int[] scores = Scores(m);
        int[] ranks = new int[n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                if (scores[j] > scores[i]) ranks[i]++;
        return ranks;
    }

    // -----------------------------------------------------------------------
    // Bag and discard state

    public static int[] BagColorCounts(GameModel m)
    {
        int nc = GameConfig.NUM_COLORS;
        int[] counts = new int[nc];
        var bag = m.Bag;
        foreach (var f in bag.flowers)
            if ((int)f.Color < nc) counts[(int)f.Color]++;
        return counts;
    }

    public static int[] DiscardColorCounts(GameModel m)
    {
        int nc = GameConfig.NUM_COLORS;
        int[] counts = new int[nc];
        foreach (var f in m.Discard.flowers)
            if ((int)f.Color < nc) counts[(int)f.Color]++;
        return counts;
    }

    public static float[] BagColorRatios(GameModel m)
    {
        int[] c = BagColorCounts(m);
        int total = TotalFlowers();
        float[] r = new float[c.Length];
        for (int i = 0; i < c.Length; i++) r[i] = c[i] / (float)total;
        return r;
    }

    // Probability of drawing each color from the current bag contents.
    public static float[] BagColorChance(GameModel m)
    {
        int[] c = BagColorCounts(m);
        int total = BagCount(m);
        float[] r = new float[c.Length];
        for (int i = 0; i < c.Length; i++) r[i] = c[i] / (float)total;
        return r;
    }

    public static int BagCount(GameModel m) => m.Bag.Count;

    public static int DiscardCount(GameModel m) => m.Discard.Count;


    
    // -----------------------------------------------------------------------
    // Displays: factories + central

    public static int[] AvailableColorCounts(GameModel m)
    {
        int nc = GameConfig.NUM_COLORS;
        int[] counts = new int[nc];
        for (int f = 0; f < m.FactoryDisplays.Length; f++)
            foreach (var fl in m.FactoryDisplays[f].flowers)
                if ((int)fl.Color < nc) counts[(int)fl.Color]++;
        foreach (var fl in m.CentralDisplay.flowers)
            if ((int)fl.Color < nc) counts[(int)fl.Color]++;
        return counts;
    }

    public static float[] AvailableColorRatios(GameModel m)
    {
        int[] c = AvailableColorCounts(m);
        int total = 0;
        for (int i = 0; i < c.Length; i++) total += c[i];
        float[] r = new float[c.Length];
        if (total > 0)
            for (int i = 0; i < c.Length; i++) r[i] = c[i] / (float)total;
        return r;
    }

    // For each color, tells how full one factory is with that color.
    public static float[] FactoryColorDensity(GameModel m, int factoryIdx)
    {
        int nc = GameConfig.NUM_COLORS;
        float fpf = GameConfig.FLOWERS_PER_DISPLAY;
        float[] d = new float[nc];
        var fac = m.FactoryDisplays[factoryIdx];
        foreach (var fl in fac.flowers)
            if ((int)fl.Color < nc) d[(int)fl.Color] += 1f / fpf;
        return d;
    }

    // Same idea as FactoryColorDensity, but normalized against the largest
    // central pile we could ever see in a round.
    public static float[] CentralColorDensity(GameModel m)
    {
        int maxCentral = NumFactories(m) * FlowersPerDisplay();
        int nc = GameConfig.NUM_COLORS;
        float[] d = new float[nc];
        foreach (var fl in m.CentralDisplay.flowers)
            if ((int)fl.Color < nc) d[(int)fl.Color] += 1f / maxCentral;
        return d;
    }

    
    public static bool CentralHasToken(GameModel m) => m.CentralDisplay.HasFirstPlayerToken;

    public static bool AllSourcesEmpty(GameModel m)
    {
        for (int f = 0; f < m.FactoryDisplays.Length; f++)
            if (m.FactoryDisplays[f].Count > 0) return false;
        return m.CentralDisplay.Count == 0;
    }

    //Approximate how close the end of round is.
    public static int NonEmptyFactoryCount(GameModel m)
    {
        int c = 0;
        for (int f = 0; f < m.FactoryDisplays.Length; f++)
            if (m.FactoryDisplays[f].Count > 0) c++;
        return c;
    }

    // -----------------------------------------------------------------------
    // Player board

    // Pattern lines

    public static float[] LineFillRatios(GameModel m, int p)
    {
        var lines = m.Players[p].PlacementLines;
        float[] r = new float[lines.Length];
        for (int i = 0; i < lines.Length; i++)
            r[i] = lines[i].Count / (float)lines[i].Capacity;
        return r;
    }

    public static int[] LineSpaceRemaining(GameModel m, int p)
    {
        var lines = m.Players[p].PlacementLines;
        int[] s = new int[lines.Length];
        for (int i = 0; i < lines.Length; i++)
            s[i] = lines[i].Capacity - lines[i].Count;
        return s;
    }

    public static int[] LineColors(GameModel m, int p)
    {
        var lines = m.Players[p].PlacementLines;
        int[] c = new int[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].CurrentColor.HasValue)
                c[i] = (int)lines[i].CurrentColor.Value;
            else
                c[i] = -1;
        }

        return c;
    }

    // Packs each line into three values:
    // line color, how full it is, and how much space is left.
    public static float[] LineFeatures(GameModel m, int p)
    {
        int nc = GameConfig.NUM_COLORS;
        var lines = m.Players[p].PlacementLines;
        float[] f = new float[lines.Length * 3];
        for (int i = 0; i < lines.Length; i++)
        {
            int cap = lines[i].Capacity;
            int cnt = lines[i].Count;

            if (lines[i].CurrentColor.HasValue)
                f[i * 3] = ((int)lines[i].CurrentColor.Value + 1f) / nc;
            else
                f[i * 3] = 0f;

            f[i * 3 + 1] = cnt / (float)cap;
            f[i * 3 + 2] = (cap - cnt) / (float)cap;
        }
        return f;
    }

    // One-hot version of line colors. Empty lines stay all zeros.
    public static float[] LineColorsOneHot(GameModel m, int p)
    {
        int nc = GameConfig.NUM_COLORS;
        var lines = m.Players[p].PlacementLines;
        float[] oh = new float[lines.Length * nc];
        for (int i = 0; i < lines.Length; i++)
            if (lines[i].CurrentColor.HasValue)
                oh[i * nc + (int)lines[i].CurrentColor.Value] = 1f;
        return oh;
    }

    // For every line/color pair, 1 means the move is legal for that player.
    public static float[] LineAcceptMatrix(GameModel m, int p)
    {
        int nc = GameConfig.NUM_COLORS;
        var player = m.Players[p];
        float[] mat = new float[player.PlacementLines.Length * nc];
        for (int l = 0; l < player.PlacementLines.Length; l++)
            for (int c = 0; c < nc; c++)
            {
                if (player.CanPlaceInLine(l, (FlowerColor)c))
                    mat[l * nc + c] = 1f;
                else
                    mat[l * nc + c] = 0f;
            }

        return mat;
    }

    // Wall

    // Row-major wall occupancy.
    public static float[] WallBinary(GameModel m, int p)
    {
        int gs = GameConfig.GRID_SIZE;
        var grid = m.Players[p].Grid;
        float[] w = new float[gs * gs];
        for (int r = 0; r < gs; r++)
            for (int c = 0; c < gs; c++)
            {
                if (grid.IsOccupied(r, c))
                    w[r * gs + c] = 1f;
                else
                    w[r * gs + c] = 0f;
            }

        return w;
    }

    // Splits the wall pattern into one mask per color.
    public static float[] WallColorMasks(GameModel m, int p)
    {
        int gs = GameConfig.GRID_SIZE;
        int nc = GameConfig.NUM_COLORS;
        var grid = m.Players[p].Grid;
        float[] masks = new float[nc * gs * gs];
        for (int r = 0; r < gs; r++)
            for (int c = 0; c < gs; c++)
            {
                int colorIdx = (int)grid.GetColorAt(r, c);
                if (colorIdx < nc)
                    masks[colorIdx * gs * gs + r * gs + c] = 1f;
            }
        return masks;
    }

    // Keeps only the colors of cells that are already occupied.
    public static float[] WallOccupiedColors(GameModel m, int p)
    {
        int gs = GameConfig.GRID_SIZE;
        int nc = GameConfig.NUM_COLORS;
        var grid = m.Players[p].Grid;
        float[] wc = new float[gs * gs];
        for (int r = 0; r < gs; r++)
            for (int c = 0; c < gs; c++)
            {
                if (grid.IsOccupied(r, c))
                    wc[r * gs + c] = ((int)grid.GetColorAt(r, c) + 1f) / nc;
                else
                    wc[r * gs + c] = 0f;
            }

        return wc;
    }

    // Forbidden colors

    // Each row tells which colors are blocked for each placement line.
    public static float[] ForbiddenMatrix(GameModel m, int p)
    {
        int nc = GameConfig.NUM_COLORS;
        var lines = m.Players[p].PlacementLines;
        float[] fb = new float[lines.Length * nc];
        for (int l = 0; l < lines.Length; l++)
            foreach (var color in lines[l].ForbiddenColors)
                if ((int)color < nc) fb[l * nc + (int)color] = 1f;
        return fb;
    }

    // Penalty line

    public static int PenaltyCount(GameModel m, int p) => m.Players[p].PenaltyLine.Count;

    public static float PenaltyRatio(GameModel m, int p)
        => m.Players[p].PenaltyLine.Count / (float)GameConfig.PENALTY_LINE_CAPACITY;

    public static int ProjectedPenalty(GameModel m, int p)
        => m.Players[p].PenaltyLine.CalculatePenalty();

    // How much worse the penalty gets if extra tiles spill over right now.
    public static int MarginalPenalty(GameModel m, int p, int extra)
    {
        int cur = m.Players[p].PenaltyLine.Count;
        int cap = GameConfig.PENALTY_LINE_CAPACITY;
        int[] vals = GameConfig.PENALTY_LINE_VALUES;
        int penalty = 0;
        for (int i = 0; i < extra && (cur + i) < cap && (cur + i) < vals.Length; i++)
            penalty += vals[cur + i];
        return penalty;
    }

    public static bool HasFirstPlayerToken(GameModel m, int p)
        => m.Players[p].HasFirstPlayerToken;

    // -----------------------------------------------------------------------
    // Wall analysis

    public static int[] RowCounts(GameModel m, int p)
    {
        int gs = GameConfig.GRID_SIZE;
        var grid = m.Players[p].Grid;
        int[] rc = new int[gs];
        for (int r = 0; r < gs; r++) rc[r] = grid.GetNumflowersInRow(r);
        return rc;
    }

    public static int[] ColCounts(GameModel m, int p)
    {
        int gs = GameConfig.GRID_SIZE;
        var grid = m.Players[p].Grid;
        int[] cc = new int[gs];
        for (int c = 0; c < gs; c++) cc[c] = grid.GetNumflowersInColumn(c);
        return cc;
    }

    public static int[] ColorCounts(GameModel m, int p)
    {
        int nc = GameConfig.NUM_COLORS;
        var grid = m.Players[p].Grid;
        int[] cc = new int[nc];
        for (int c = 0; c < nc; c++)
            cc[c] = grid.GetNumflowersForColorInGrid((FlowerColor)c);
        return cc;
    }

    public static float[] RowCompletionRatios(GameModel m, int p)
    {
        int gs = GameConfig.GRID_SIZE;
        int[] rc = RowCounts(m, p);
        float[] r = new float[gs];
        for (int i = 0; i < gs; i++) r[i] = rc[i] / (float)gs;
        return r;
    }

    public static float[] ColCompletionRatios(GameModel m, int p)
    {
        int gs = GameConfig.GRID_SIZE;
        int[] cc = ColCounts(m, p);
        float[] r = new float[gs];
        for (int i = 0; i < gs; i++) r[i] = cc[i] / (float)gs;
        return r;
    }

    public static float[] ColorCompletionRatios(GameModel m, int p)
    {
        int gs = GameConfig.GRID_SIZE;
        int[] cc = ColorCounts(m, p);
        float[] r = new float[cc.Length];
        for (int i = 0; i < cc.Length; i++) r[i] = cc[i] / (float)gs;
        return r;
    }

    public static int CompletedRowCount(GameModel m, int p)
        => m.Players[p].Grid.GetCompletedRows();

    public static int CompletedColCount(GameModel m, int p)
    {
        int gs = GameConfig.GRID_SIZE;
        var grid = m.Players[p].Grid;
        int count = 0;
        for (int c = 0; c < gs; c++)
        {
            bool full = true;
            for (int r = 0; r < gs; r++)
                if (!grid.IsOccupied(r, c)) { full = false; break; }
            if (full) count++;
        }
        return count;
    }

    public static int CompletedColorCount(GameModel m, int p)
    {
        int nc = GameConfig.NUM_COLORS;
        int gs = GameConfig.GRID_SIZE;
        var grid = m.Players[p].Grid;
        int count = 0;
        for (int c = 0; c < nc; c++)
            if (grid.GetNumflowersForColorInGrid((FlowerColor)c) == gs) count++;
        return count;
    }

    public static int TotalWallTiles(GameModel m, int p)
    {
        int gs = GameConfig.GRID_SIZE;
        var grid = m.Players[p].Grid;
        int total = 0;
        for (int r = 0; r < gs; r++)
            for (int c = 0; c < gs; c++)
                if (grid.IsOccupied(r, c)) total++;
        return total;
    }

    // -----------------------------------------------------------------------
    // Scoring projections

    // Tests the score of a hypothetical wall placement without changing the board.
    public static int SimulatePlacementScore(GameModel m, int p, int row, FlowerColor color)
    {
        var grid = m.Players[p].Grid;
        int gs = GameConfig.GRID_SIZE;
        int col = grid.GetColumnForColor(row, color);
        if (col < 0 || grid.IsOccupied(row, col)) return 0;

        int h = 1, v = 1;
        for (int c = col - 1; c >= 0 && grid.IsOccupied(row, c); c--) h++;
        for (int c = col + 1; c < gs && grid.IsOccupied(row, c); c++) h++;
        for (int r = row - 1; r >= 0 && grid.IsOccupied(r, col); r--) v++;
        for (int r = row + 1; r < gs && grid.IsOccupied(r, col); r++) v++;

        if (h == 1 && v == 1) return 1;
        int pts = 0;
        if (h > 1) pts += h;
        if (v > 1) pts += v;
        return pts;
    }

    // Only full lines can score at round end, so the rest stay at zero.
    public static float[] ProjectedLineScoring(GameModel m, int p)
    {
        var lines = m.Players[p].PlacementLines;
        float[] pts = new float[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].IsFull && lines[i].CurrentColor.HasValue)
                pts[i] = SimulatePlacementScore(m, p, i, lines[i].CurrentColor.Value);
        }
        return pts;
    }

    // Breaks current end-game bonus into row, column, color and total parts.
    public static (int row, int col, int color, int total) CurrentEndGameBonus(GameModel m, int p)
    {
        int gs = GameConfig.GRID_SIZE;
        int nc = GameConfig.NUM_COLORS;
        var grid = m.Players[p].Grid;

        int rowBonus = 0;
        for (int r = 0; r < gs; r++)
        {
            bool complete = true;
            for (int c = 0; c < gs; c++)
                if (!grid.IsOccupied(r, c)) { complete = false; break; }
            if (complete) rowBonus += GameConfig.COMPLETE_LINE_SCORING_BONUS;
        }

        int colBonus = 0;
        for (int c = 0; c < gs; c++)
        {
            bool complete = true;
            for (int r = 0; r < gs; r++)
                if (!grid.IsOccupied(r, c)) { complete = false; break; }
            if (complete) colBonus += GameConfig.COMPLETE_COLUMN_SCORING_BONUS;
        }

        int colorBonus = 0;
        for (int i = 0; i < nc; i++)
            if (grid.GetNumflowersForColorInGrid((FlowerColor)i) == gs)
                colorBonus += GameConfig.COMPLETE_COLOR_SCORING_BONUS;

        return (rowBonus, colBonus, colorBonus, rowBonus + colBonus + colorBonus);
    }

    // Higher values mean a row, column or color set is closer to being completed.
    public static float[] EndGameBonusPotential(GameModel m, int p)
    {
        int gs = GameConfig.GRID_SIZE;
        int nc = GameConfig.NUM_COLORS;
        var grid = m.Players[p].Grid;

        float[] pot = new float[gs + gs + nc];

        for (int r = 0; r < gs; r++)
        {
            int filled = grid.GetNumflowersInRow(r);
            int remaining = gs - filled;

            if (remaining > 0)
                pot[r] = GameConfig.COMPLETE_LINE_SCORING_BONUS / (float)(remaining * remaining);
            else
                pot[r] = 0f;
        }

        for (int c = 0; c < gs; c++)
        {
            int filled = grid.GetNumflowersInColumn(c);
            int remaining = gs - filled;

            if (remaining > 0)
                pot[gs + c] = GameConfig.COMPLETE_COLUMN_SCORING_BONUS / (float)(remaining * remaining);
            else
                pot[gs + c] = 0f;
        }

        for (int i = 0; i < nc; i++)
        {
            int filled = grid.GetNumflowersForColorInGrid((FlowerColor)i);
            int remaining = gs - filled;

            if (remaining > 0)
                pot[gs + gs + i] = GameConfig.COMPLETE_COLOR_SCORING_BONUS / (float)(remaining * remaining);
            else
                pot[gs + gs + i] = 0f;
        }

        return pot;
    }

    // Gives each empty wall cell a rough value based on immediate score plus
    // long-term bonus progress.
    public static float[] WallCellScoreMap(GameModel m, int p)
    {
        int gs = GameConfig.GRID_SIZE;
        var grid = m.Players[p].Grid;
        float[] map = new float[gs * gs];
        float[] bonusPot = EndGameBonusPotential(m, p);

        for (int r = 0; r < gs; r++)
        {
            for (int c = 0; c < gs; c++)
            {
                if (grid.IsOccupied(r, c)) continue;

                FlowerColor color = grid.GetColorAt(r, c);
                int adj = SimulatePlacementScore(m, p, r, color);
                float rowPot = bonusPot[r];
                float colPot = bonusPot[gs + c];
                float colorPot = bonusPot[gs + gs + (int)color];

                map[r * gs + c] = adj + rowPot + colPot + colorPot;
            }
        }
        return map;
    }

    // -----------------------------------------------------------------------
    // Move evaluation for action masking and ranking

    public const int MOVE_FEATURE_COUNT = 14;

    public static float[] MoveFeatures(GameModel m, GameMove move, int selfIdx)
    {
        float[] f = new float[MOVE_FEATURE_COUNT];

        int gs = GameConfig.GRID_SIZE;
        int nc = GameConfig.NUM_COLORS;
        var player = m.Players[selfIdx];
        int oppIdx = (selfIdx + 1) % m.NumberOfPlayers;
        var opp = m.Players[oppIdx];

        float maxPickable = GameConfig.FLOWERS_PER_DISPLAY;
        float maxAdj = 2*gs;  // theoretical max adjacency score
        int penCap = GameConfig.PENALTY_LINE_CAPACITY;

        f[0] = move.flowerCount / maxPickable;

        if (!move.IsPenalty)
        {
            int cap = player.PlacementLines[move.TargetLineIndex].Capacity;
            int cur = player.PlacementLines[move.TargetLineIndex].Count;
            int space = cap - cur;
            int toLine = Math.Min(move.flowerCount, space);
            int toPenalty = Math.Max(0, move.flowerCount - space);
            bool completes = move.flowerCount >= space;

            f[1] = toLine / (float)cap;
            f[2] = toPenalty / (float)penCap;

            if (completes)
                f[3] = 1f;
            else
                f[3] = 0f;

            if (completes)
                f[4] = SimulatePlacementScore(m, selfIdx, move.TargetLineIndex, move.Color) / maxAdj;

            f[5] = -MarginalPenalty(m, selfIdx, toPenalty) / 14f; // max penalty = 14

            float[] pot = EndGameBonusPotential(m, selfIdx);
            f[6] = pot[move.TargetLineIndex];
            int wallCol = player.Grid.GetColumnForColor(move.TargetLineIndex, move.Color);
            if (wallCol >= 0 && wallCol < gs) f[7] = pot[gs + wallCol];
            if ((int)move.Color < nc) f[8] = pot[gs + gs + (int)move.Color];
        }
        else
        {
            f[2] = move.flowerCount / (float)penCap;
            f[5] = -MarginalPenalty(m, selfIdx, move.flowerCount) / 14f;
            f[11] = 1f;
        }

        if (!move.SourceIsFactory && m.CentralDisplay.HasFirstPlayerToken)
            f[9] = 1f;
        else
            f[9] = 0f;

        if (move.SourceIsFactory)
            f[10] = 0f;
        else
            f[10] = 1f;

        // Denial is a simple heuristic: if the opponent wanted this color,
        // taking it gets a better score.
        // It does not account for utility of the color for the opponent.
        float denial = 0f;
        for (int l = 0; l < opp.PlacementLines.Length; l++)
        {
            if (!opp.CanPlaceInLine(l, move.Color)) continue;
            denial += 1f;
            var oppLine = opp.PlacementLines[l];
            if (oppLine.CurrentColor.HasValue && oppLine.CurrentColor.Value == move.Color)
            {
                denial += 2f;
                int oppSpace = oppLine.Capacity - oppLine.Count;
                if (move.flowerCount >= oppSpace) denial += 3f;
            }
        }
        f[12] = denial / 6f; // normalise

        // Rough blended value for ranking moves quickly.
        f[13] = f[4] * maxAdj + f[6] + f[7] + f[8] + f[5] * 14f;
        f[13] = Clamp01(f[13] / 20f); //normalisation

        return f;
    }

    // -----------------------------------------------------------------------
    // Small encoding helpers

    // One-hot encode an integer value.
    public static float[] OneHot(int value, int size)
    {
        float[] oh = new float[size];
        if (value >= 0 && value < size) oh[value] = 1f;
        return oh;
    }

    // Unpack an int bit-field into a 0/1 float array.
    public static float[] UnpackBits(int bitfield, int bits)
    {
        float[] arr = new float[bits];
        for (int i = 0; i < bits; i++)
        {
            if ((bitfield & (1 << i)) != 0)
                arr[i] = 1f;
            else
                arr[i] = 0f;
        }

        return arr;
    }

    // Keeps any float inside the 0 to 1 range
    public static float Clamp01(float v)
    {
        if (v < 0f)
            return 0f;

        if (v > 1f)
            return 1f;

        return v;
    }

    // Turns an int into a 0 to 1 value using the expected maximum.
    public static float Norm(int value, int max)
    {
        if (max <= 0)
            return 0f;

        float normalizedValue = value / (float)max;
        return Math.Min(normalizedValue, 1f);
    }

    // Same as the int overload, but used when the source value is already a float.
    public static float Norm(float value, float max)
    {
        if (max <= 0f)
            return 0f;

        float normalizedValue = value / max;
        return Math.Min(normalizedValue, 1f);
    }

    // -----------------------------------------------------------------------
    // Observation builders

    // Baseline observation 
    public static int WriteFullObservation(GameModel m, int selfIdx, float[] dest, int offset = 0)
    {
        int idx = offset;
        int oppIdx = (selfIdx + 1) % m.NumberOfPlayers;
        // Self pattern lines: numLines x 3
        float[] lf = LineFeatures(m, selfIdx);
        Array.Copy(lf, 0, dest, idx, lf.Length); idx += lf.Length;

        // Self wall: gridSize * gridSize
        float[] wb = WallBinary(m, selfIdx);
        Array.Copy(wb, 0, dest, idx, wb.Length); idx += wb.Length;

        // Forbidden: numLines x numColors
        float[] fb = ForbiddenMatrix(m, selfIdx);
        Array.Copy(fb, 0, dest, idx, fb.Length); idx += fb.Length;

        // Opponent wall: gridSize * gridSize
        float[] ow = WallBinary(m, oppIdx);
        Array.Copy(ow, 0, dest, idx, ow.Length); idx += ow.Length;

        // Opponent penalty + score: 2
        dest[idx++] = PenaltyRatio(m, oppIdx);
        dest[idx++] = ScoreNorm(m, oppIdx);

        // Global: round, token, scoreDiff: 3
        dest[idx++] = RoundNorm(m);
        if (CentralHasToken(m))
            dest[idx++] = 1f;
        else
            dest[idx++] = 0f;

        dest[idx++] = ScoreDiffNorm(m, selfIdx, oppIdx);

        return idx - offset;
    }

    // Adds look-ahead style features on top of the baseline observation.
    public static int WriteExtendedObservation(GameModel m, int selfIdx, float[] dest, int offset = 0)
    {
        int idx = offset;
        int oppIdx = (selfIdx + 1) % m.NumberOfPlayers;
        int gs = GameConfig.GRID_SIZE;
        int nc = GameConfig.NUM_COLORS;

        idx += WriteFullObservation(m, selfIdx, dest, idx);

        float[] pls = ProjectedLineScoring(m, selfIdx);
        for (int i = 0; i < pls.Length; i++) dest[idx++] = pls[i] / 10f;

        float[] pot = EndGameBonusPotential(m, selfIdx);
        Array.Copy(pot, 0, dest, idx, pot.Length); idx += pot.Length;

        float[] csm = WallCellScoreMap(m, selfIdx);
        for (int i = 0; i < csm.Length; i++) dest[idx++] = Clamp01(csm[i] / 20f);

        float[] opls = ProjectedLineScoring(m, oppIdx);
        for (int i = 0; i < opls.Length; i++) dest[idx++] = opls[i] / 10f;

        float[] opot = EndGameBonusPotential(m, oppIdx);
        Array.Copy(opot, 0, dest, idx, opot.Length); idx += opot.Length;

        float[] lam = LineAcceptMatrix(m, selfIdx);
        Array.Copy(lam, 0, dest, idx, lam.Length); idx += lam.Length;

        float[] bcr = BagColorRatios(m);
        Array.Copy(bcr, 0, dest, idx, bcr.Length); idx += bcr.Length;

        return idx - offset;
    }

    public const int STRATEGIC_MAX_PLAYERS = 4;
    public const int STRATEGIC_MAX_FACTORIES = 9;
    public const int STRATEGIC_MAX_SOURCES = STRATEGIC_MAX_FACTORIES + 1;
    public const int STRATEGIC_MAX_COLORS = 5;
    public const int STRATEGIC_MAX_LINES = 5;
    public const int STRATEGIC_GLOBAL_FEATURES = 35;
    public const int STRATEGIC_SOURCE_FEATURES = STRATEGIC_MAX_SOURCES * STRATEGIC_MAX_COLORS;
    public const int STRATEGIC_PLAYER_FEATURES = 114;
    public const int STRATEGIC_OBSERVATION_SIZE =
        STRATEGIC_GLOBAL_FEATURES +
        STRATEGIC_SOURCE_FEATURES +
        (STRATEGIC_MAX_PLAYERS * STRATEGIC_PLAYER_FEATURES);

    private const float STRATEGIC_SCORE_NORM = 200f;
    private const float STRATEGIC_SCORE_MAP_NORM = 20f;
    private const float STRATEGIC_MAX_CENTRAL_TILES = 40f;

    // Main observation format used by the current ML agents.
    public static int WriteStrategicObservation(GameModel m, int selfIdx, float[] dest, int offset = 0)
    {
        int idx = offset;
        int playerCount = m.NumberOfPlayers;
        int selfScore = m.Players[selfIdx].Score;

        dest[idx++] = playerCount / (float)STRATEGIC_MAX_PLAYERS;
        dest[idx++] = RoundNorm(m);
        dest[idx++] = Norm(m.TurnNumber, 40);
        dest[idx++] = NonEmptyFactoryCount(m) / (float)STRATEGIC_MAX_FACTORIES;
        dest[idx++] = EstimateTurnsRemaining(m) / 20f;
        if (CentralHasToken(m))
            dest[idx++] = 1f;
        else
            dest[idx++] = 0f;

        float[] bagRatios = BagColorRatios(m);
        for (int c = 0; c < STRATEGIC_MAX_COLORS; c++)
        {
            if (c < bagRatios.Length)
                dest[idx++] = bagRatios[c];
            else
                dest[idx++] = 0f;
        }

        float[] discardRatios = BuildDiscardRatios(m);
        for (int c = 0; c < STRATEGIC_MAX_COLORS; c++)
        {
            if (c < discardRatios.Length)
                dest[idx++] = discardRatios[c];
            else
                dest[idx++] = 0f;
        }

        float[] availableRatios = AvailableColorRatios(m);
        for (int c = 0; c < STRATEGIC_MAX_COLORS; c++)
        {
            if (c < availableRatios.Length)
                dest[idx++] = availableRatios[c];
            else
                dest[idx++] = 0f;
        }

        for (int f = 0; f < STRATEGIC_MAX_FACTORIES; f++)
        {
            if (f < m.FactoryDisplays.Length)
                dest[idx++] = m.FactoryDisplays[f].Count / (float)GameConfig.FLOWERS_PER_DISPLAY;
            else
                dest[idx++] = 0f;
        }

        dest[idx++] = m.CentralDisplay.Count / STRATEGIC_MAX_CENTRAL_TILES;

        for (int p = 0; p < STRATEGIC_MAX_PLAYERS; p++)
        {
            if (p == m.StartingPlayerIndex)
                dest[idx++] = 1f;
            else
                dest[idx++] = 0f;
        }

        for (int s = 0; s < STRATEGIC_MAX_SOURCES; s++)
        {
            if (s < m.FactoryDisplays.Length)
            {
                for (int c = 0; c < STRATEGIC_MAX_COLORS; c++)
                    dest[idx++] = m.FactoryDisplays[s].CountColor((FlowerColor)c)
                        / (float)GameConfig.FLOWERS_PER_DISPLAY;
            }
            else if (s == STRATEGIC_MAX_FACTORIES)
            {
                for (int c = 0; c < STRATEGIC_MAX_COLORS; c++)
                    dest[idx++] = m.CentralDisplay.CountColor((FlowerColor)c)
                        / STRATEGIC_MAX_CENTRAL_TILES;
            }
            else
            {
                for (int c = 0; c < STRATEGIC_MAX_COLORS; c++)
                    dest[idx++] = 0f;
            }
        }

        for (int slot = 0; slot < STRATEGIC_MAX_PLAYERS; slot++)
        {
            if (slot < playerCount)
            {
                int playerIndex = (selfIdx + slot) % playerCount;
                WriteStrategicPlayerBlock(m, playerIndex, selfScore, dest, ref idx);
            }
            else
            {
                WriteZeroBlock(dest, ref idx, STRATEGIC_PLAYER_FEATURES);
            }
        }

        return idx - offset;
    }

    // Same strategic layout, but built from the lightweight snapshot model.
    public static int WriteStrategicObservation(MinimalGM m, GameState snapshot, int selfIdx, float[] dest, int offset = 0)
    {
        int idx = offset;
        int playerCount = m.NumPlayers;
        int selfScore = snapshot.PlayerSnapshots[selfIdx].Score;

        dest[idx++] = playerCount / (float)STRATEGIC_MAX_PLAYERS;
        dest[idx++] = Norm(snapshot.CurrentRound, 10);
        dest[idx++] = Norm(snapshot.TurnNumber, 40);
        dest[idx++] = CountFullFactories(snapshot, m.Config) / (float)STRATEGIC_MAX_FACTORIES;
        dest[idx++] = EstimateTurnsRemaining(snapshot, m.Config) / 20f;
        if (snapshot.CentralHasToken)
            dest[idx++] = 1f;
        else
            dest[idx++] = 0f;

        int[] bagCounts = CountColors(snapshot.Bag, snapshot.BagCount, m.Config.NumColors);
        for (int c = 0; c < STRATEGIC_MAX_COLORS; c++)
        {
            if (c < bagCounts.Length)
                dest[idx++] = bagCounts[c] / (float)m.Config.Totalflowers;
            else
                dest[idx++] = 0f;
        }

        int[] discardCounts = CountColors(snapshot.Discard, snapshot.DiscardCount, m.Config.NumColors);
        for (int c = 0; c < STRATEGIC_MAX_COLORS; c++)
        {
            if (c < discardCounts.Length)
                dest[idx++] = discardCounts[c] / (float)m.Config.Totalflowers;
            else
                dest[idx++] = 0f;
        }

        int[] availableCounts = CountAvailableColors(snapshot, m.Config);
        int availableTotal = 0;
        for (int c = 0; c < availableCounts.Length; c++) availableTotal += availableCounts[c];
        for (int c = 0; c < STRATEGIC_MAX_COLORS; c++)
        {
            if (c < availableCounts.Length && availableTotal > 0)
                dest[idx++] = availableCounts[c] / (float)availableTotal;
            else
                dest[idx++] = 0f;
        }

        for (int f = 0; f < STRATEGIC_MAX_FACTORIES; f++)
        {
            if (f < m.NumFactories)
                dest[idx++] = snapshot.FactoryCount[f] / (float)m.Config.flowersPerFactory;
            else
                dest[idx++] = 0f;
        }

        dest[idx++] = snapshot.CentralCount / STRATEGIC_MAX_CENTRAL_TILES;

        for (int p = 0; p < STRATEGIC_MAX_PLAYERS; p++)
        {
            if (p == snapshot.StartingPlayer)
                dest[idx++] = 1f;
            else
                dest[idx++] = 0f;
        }

        for (int s = 0; s < STRATEGIC_MAX_SOURCES; s++)
        {
            if (s < m.NumFactories)
            {
                for (int c = 0; c < STRATEGIC_MAX_COLORS; c++)
                    dest[idx++] = CountColor(snapshot.Factories[s], snapshot.FactoryCount[s], c)
                        / (float)m.Config.flowersPerFactory;
            }
            else if (s == STRATEGIC_MAX_FACTORIES)
            {
                for (int c = 0; c < STRATEGIC_MAX_COLORS; c++)
                    dest[idx++] = CountColor(snapshot.Central, snapshot.CentralCount, c)
                        / STRATEGIC_MAX_CENTRAL_TILES;
            }
            else
            {
                for (int c = 0; c < STRATEGIC_MAX_COLORS; c++)
                    dest[idx++] = 0f;
            }
        }

        for (int slot = 0; slot < STRATEGIC_MAX_PLAYERS; slot++)
        {
            if (slot < playerCount)
            {
                int playerIndex = (selfIdx + slot) % playerCount;
                WriteStrategicPlayerBlock(m, snapshot, playerIndex, selfScore, dest, ref idx);
            }
            else
            {
                WriteZeroBlock(dest, ref idx, STRATEGIC_PLAYER_FEATURES);
            }
        }

        return idx - offset;
    }

    public static int EstimateTurnsRemaining(GameModel m)
    {
        return NonEmptyFactoryCount(m) + m.CentralDisplay.GetAvailableColors().Count;
    }

    public static int EstimateTurnsRemaining(GameState snapshot, GameConfigSnapshot config)
    {
        int turns = 0;
        for (int f = 0; f < config.NumFactories; f++)
            if (snapshot.FactoryCount[f] > 0) turns++;

        int mask = 0;
        for (int i = 0; i < snapshot.CentralCount; i++)
            mask |= 1 << snapshot.Central[i];

        return turns + CountBits(mask);
    }

    private static float[] BuildDiscardRatios(GameModel m)
    {
        int[] counts = DiscardColorCounts(m);
        float[] ratios = new float[counts.Length];
        float total = TotalFlowers();
        for (int i = 0; i < counts.Length; i++)
            ratios[i] = counts[i] / total;
        return ratios;
    }

    // Writes one player's block inside the strategic observation.
    private static void WriteStrategicPlayerBlock(GameModel m, int playerIndex, int selfScore, float[] dest, ref int idx)
    {
        PlayerModel player = m.Players[playerIndex];

        dest[idx++] = player.Score / STRATEGIC_SCORE_NORM;
        dest[idx++] = (player.Score - selfScore) / STRATEGIC_SCORE_NORM;
        dest[idx++] = player.PenaltyLine.Count / (float)GameConfig.PENALTY_LINE_CAPACITY;
        if (player.HasFirstPlayerToken)
            dest[idx++] = 1f;
        else
            dest[idx++] = 0f;

        float[] wall = WallBinary(m, playerIndex);
        Array.Copy(wall, 0, dest, idx, wall.Length);
        idx += wall.Length;

        float[] fillRatios = LineFillRatios(m, playerIndex);
        Array.Copy(fillRatios, 0, dest, idx, fillRatios.Length);
        idx += fillRatios.Length;

        for (int line = 0; line < STRATEGIC_MAX_LINES; line++)
        {
            if (line < player.PlacementLines.Length)
            {
                PlacementLineModel placementLine = player.PlacementLines[line];
                dest[idx++] = (placementLine.Capacity - placementLine.Count) / (float)placementLine.Capacity;
            }
            else
            {
                dest[idx++] = 0f;
            }
        }

        float[] lineColors = LineColorsOneHot(m, playerIndex);
        Array.Copy(lineColors, 0, dest, idx, lineColors.Length);
        idx += lineColors.Length;

        float[] lineAccept = LineAcceptMatrix(m, playerIndex);
        Array.Copy(lineAccept, 0, dest, idx, lineAccept.Length);
        idx += lineAccept.Length;

        float[] scoreMap = WallCellScoreMap(m, playerIndex);
        for (int i = 0; i < scoreMap.Length; i++)
            dest[idx++] = Clamp01(scoreMap[i] / STRATEGIC_SCORE_MAP_NORM);
    }

    // Snapshot version of the same player block.
    private static void WriteStrategicPlayerBlock(MinimalGM m, GameState snapshot, int playerIndex, int selfScore, float[] dest, ref int idx)
    {
        PlayerState player = snapshot.PlayerSnapshots[playerIndex];

        dest[idx++] = player.Score / STRATEGIC_SCORE_NORM;
        dest[idx++] = (player.Score - selfScore) / STRATEGIC_SCORE_NORM;
        dest[idx++] = player.PenaltyCount / (float)m.Config.PenaltyCapacity;
        if (player.HasFirstPlayerToken)
            dest[idx++] = 1f;
        else
            dest[idx++] = 0f;

        WriteWallBits(player.WallBits, m.Config.GridSize, dest, ref idx);

        for (int line = 0; line < STRATEGIC_MAX_LINES; line++)
        {
            if (line < m.Config.NumPlacementLines)
                dest[idx++] = player.LineCount[line] / (float)m.Config.PlacementLineCapacities[line];
            else
                dest[idx++] = 0f;
        }

        for (int line = 0; line < STRATEGIC_MAX_LINES; line++)
        {
            if (line < m.Config.NumPlacementLines)
            {
                int capacity = m.Config.PlacementLineCapacities[line];
                dest[idx++] = (capacity - player.LineCount[line]) / (float)capacity;
            }
            else
            {
                dest[idx++] = 0f;
            }
        }

        for (int line = 0; line < STRATEGIC_MAX_LINES; line++)
        {
            for (int color = 0; color < STRATEGIC_MAX_COLORS; color++)
            {
                if (line < m.Config.NumPlacementLines && player.LineColor[line] == color)
                    dest[idx++] = 1f;
                else
                    dest[idx++] = 0f;
            }
        }

        for (int line = 0; line < STRATEGIC_MAX_LINES; line++)
        {
            for (int color = 0; color < STRATEGIC_MAX_COLORS; color++)
            {
                if (line < m.Config.NumPlacementLines && color < m.Config.NumColors && m.Players[playerIndex].CanPlaceInLine(line, (byte)color))
                    dest[idx++] = 1f;
                else
                    dest[idx++] = 0f;
            }
        }

        float[] scoreMap = BuildMinimalWallScoreMap(m.Config, player, playerIndex);
        Array.Copy(scoreMap, 0, dest, idx, scoreMap.Length);
        idx += scoreMap.Length;
    }

    private static void WriteWallBits(int wallBits, int gridSize, float[] dest, ref int idx)
    {
        for (int r = 0; r < gridSize; r++)
            for (int c = 0; c < gridSize; c++)
            {
                if ((wallBits & (1 << (r * gridSize + c))) != 0)
                    dest[idx++] = 1f;
                else
                    dest[idx++] = 0f;
            }
    }

    private static void WriteZeroBlock(float[] dest, ref int idx, int count)
    {
        Array.Clear(dest, idx, count);
        idx += count;
    }

    private static int CountFullFactories(GameState snapshot, GameConfigSnapshot config)
    {
        int count = 0;
        for (int f = 0; f < config.NumFactories; f++)
            if (snapshot.FactoryCount[f] >= config.flowersPerFactory) count++;
        return count;
    }

    private static int[] CountColors(byte[] flowers, int count, int numColors)
    {
        int[] counts = new int[numColors];
        for (int i = 0; i < count; i++)
        {
            int color = flowers[i];
            if (color >= 0 && color < numColors) counts[color]++;
        }
        return counts;
    }

    private static int[] CountAvailableColors(GameState snapshot, GameConfigSnapshot config)
    {
        int[] counts = new int[config.NumColors];
        for (int f = 0; f < config.NumFactories; f++)
            for (int i = 0; i < snapshot.FactoryCount[f]; i++)
            {
                int color = snapshot.Factories[f][i];
                if (color >= 0 && color < counts.Length) counts[color]++;
            }

        for (int i = 0; i < snapshot.CentralCount; i++)
        {
            int color = snapshot.Central[i];
            if (color >= 0 && color < counts.Length) counts[color]++;
        }

        return counts;
    }

    private static int CountColor(byte[] flowers, int count, int color)
    {
        int result = 0;
        for (int i = 0; i < count; i++)
            if (flowers[i] == color) result++;
        return result;
    }

    private static int CountBits(int value)
    {
        int count = 0;
        while (value != 0)
        {
            count++;
            value &= value - 1;
        }
        return count;
    }

    private static float[] BuildMinimalWallScoreMap(GameConfigSnapshot config, PlayerState playerState, int playerIndex)
    {
        int gs = config.GridSize;
        int[,] pattern = config.WallPatterns[playerIndex % config.WallPatterns.Length];
        int[] rowCounts = new int[gs];
        int[] colCounts = new int[gs];
        int[] colorCounts = new int[config.NumColors];

        for (int r = 0; r < gs; r++)
        {
            for (int c = 0; c < gs; c++)
            {
                if ((playerState.WallBits & (1 << (r * gs + c))) == 0) continue;

                rowCounts[r]++;
                colCounts[c]++;
                int color = pattern[r, c];
                if (color >= 0 && color < colorCounts.Length) colorCounts[color]++;
            }
        }

        float[] map = new float[gs * gs];
        for (int r = 0; r < gs; r++)
        {
            for (int c = 0; c < gs; c++)
            {
                int flat = r * gs + c;
                if ((playerState.WallBits & (1 << flat)) != 0)
                {
                    map[flat] = 0f;
                    continue;
                }

                int color = pattern[r, c];
                float rowPotential = ComputeBonusPotential(config.BonusRow, gs - rowCounts[r]);
                float colPotential = ComputeBonusPotential(config.BonusColumn, gs - colCounts[c]);
                float colorPotential = 0f;
                if (color >= 0 && color < colorCounts.Length)
                    colorPotential = ComputeBonusPotential(config.BonusColor, gs - colorCounts[color]);

                int adjacency = SimulatePlacementScore(playerState.WallBits, gs, r, c);
                map[flat] = Clamp01((adjacency + rowPotential + colPotential + colorPotential) / STRATEGIC_SCORE_MAP_NORM);
            }
        }

        return map;
    }

    private static float ComputeBonusPotential(int bonusValue, int remaining)
    {
        if (remaining <= 0) return 0f;
        return bonusValue / (float)(remaining * remaining);
    }

    private static int SimulatePlacementScore(int wallBits, int gridSize, int row, int col)
    {
        int h = 1;
        int v = 1;

        for (int c = col - 1; c >= 0 && IsOccupied(wallBits, gridSize, row, c); c--) h++;
        for (int c = col + 1; c < gridSize && IsOccupied(wallBits, gridSize, row, c); c++) h++;
        for (int r = row - 1; r >= 0 && IsOccupied(wallBits, gridSize, r, col); r--) v++;
        for (int r = row + 1; r < gridSize && IsOccupied(wallBits, gridSize, r, col); r++) v++;

        if (h == 1 && v == 1) return 1;

        int points = 0;
        if (h > 1) points += h;
        if (v > 1) points += v;
        return points;
    }

    private static bool IsOccupied(int wallBits, int gridSize, int row, int col)
    {
        return (wallBits & (1 << (row * gridSize + col))) != 0;
    }
}
