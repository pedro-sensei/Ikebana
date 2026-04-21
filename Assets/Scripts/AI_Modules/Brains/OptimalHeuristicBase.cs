using System.Collections.Generic;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


//Base heuristic for all optimizer brains with adjustable weights.
public abstract class OptimalHeuristicBase
{
    #region WEIGHTS AND PARAMETERS

    public float SimulateScoringWeight;
    public float PenaltyWeight;
    public float TilesPlacedWeight;
    public float LineCompletionWeight;

    public float[] ChaseBonusRowWeights;
    public float[] ChaseBonusColumnWeights;
    public float[] ChaseBonusColorWeights;

    public float TryPushPenaltyWeight = 5f;
    public float TryDenyMoveWeight = 5f;

    public float AvoidPartialLineWeight = 5f;

    public float FirstPlayerTokenWeight = 5f;
    public float PrioritizeCentralPlacementWeight = 2f;
    public float PrioritizeTopRowsWeight = 2f;


    protected float _bonusRow;
    protected float _bonusColumn;
    protected float _bonusColor;
    protected int _gridSize;
    protected int _numColors;
    protected int _numPlacementLines;
    protected int _penaltyCapacity;
    protected int[] _penaltyValues;


    // Config snapshot

    protected GameConfigSnapshot _gameConfigSnapshot;

    // Loads game constants from a config snapshot.
    // Called by subclass constructors or SetConfig.

    protected void LoadConfig(GameConfigSnapshot config)
    {
        _gameConfigSnapshot = config;
        _bonusRow = config.BonusRow;
        _bonusColumn = config.BonusColumn;
        _bonusColor = config.BonusColor;
        _gridSize = config.GridSize;
        _numColors = config.NumColors;
        _numPlacementLines = config.NumPlacementLines;
        _penaltyCapacity = config.PenaltyCapacity;
        _penaltyValues = config.PenaltyValues;
        _projectedCentralState = new int[_numColors];

        // Initialize per-row/column/color chase-bonus arrays if not already set
        if (ChaseBonusRowWeights == null || ChaseBonusRowWeights.Length != _gridSize)
            ChaseBonusRowWeights = new float[_gridSize];
        if (ChaseBonusColumnWeights == null || ChaseBonusColumnWeights.Length != _gridSize)
            ChaseBonusColumnWeights = new float[_gridSize];
        if (ChaseBonusColorWeights == null || ChaseBonusColorWeights.Length != _numColors)
            ChaseBonusColorWeights = new float[_numColors];
    }


    // Default initializer — reads from the current global GameConfig.

    protected void LoadConfigFromGlobal()
    {
        LoadConfig(GameConfig.CreateSnapshot());
    }

    #endregion
    #region WEIGHT APPLICATION

    protected void ApplyGenomeWeights(BasicGAGenome g)
    {
        SimulateScoringWeight = g.SimulateScoringWeight;
        PenaltyWeight = g.PenaltyWeight;
        TilesPlacedWeight = g.TilesPlacedWeight;
        LineCompletionWeight = g.LineCompletionWeight;

        if (g.ChaseBonusRowWeights != null)
            ChaseBonusRowWeights = (float[])g.ChaseBonusRowWeights.Clone();
        if (g.ChaseBonusColumnWeights != null)
            ChaseBonusColumnWeights = (float[])g.ChaseBonusColumnWeights.Clone();
        if (g.ChaseBonusColorWeights != null)
            ChaseBonusColorWeights = (float[])g.ChaseBonusColorWeights.Clone();

        TryPushPenaltyWeight = g.TryPushPenaltyWeight;
        TryDenyMoveWeight = g.TryDenyMoveWeight;

        AvoidPartialLineWeight = g.AvoidPartialLineWeight;

        FirstPlayerTokenWeight = g.FirstPlayerTokenWeight;
        PrioritizeCentralPlacementWeight = g.PrioritizeCentralPlacementWeight;
        PrioritizeTopRowsWeight = g.PrioritizeTopRowsWeight;
    }

    protected void ApplyWeightsSnapshot(OptimizerGenomeWeights w)
    {
        SimulateScoringWeight = w.SimulateScoringWeight;
        PenaltyWeight = w.PenaltyWeight;
        TilesPlacedWeight = w.TilesPlacedWeight;
        LineCompletionWeight = w.LineCompletionWeight;
        TryPushPenaltyWeight = w.TryPushPenaltyWeight;
        TryDenyMoveWeight = w.TryDenyMoveWeight;
        AvoidPartialLineWeight = w.AvoidPartialLineWeight;
        FirstPlayerTokenWeight = w.FirstPlayerTokenWeight;
        PrioritizeCentralPlacementWeight = w.PrioritizeCentralPlacementWeight;
        PrioritizeTopRowsWeight = w.PrioritizeTopRowsWeight;

        if (w.ChaseBonusRowWeights != null)
            ChaseBonusRowWeights = (float[])w.ChaseBonusRowWeights.Clone();
        if (w.ChaseBonusColumnWeights != null)
            ChaseBonusColumnWeights = (float[])w.ChaseBonusColumnWeights.Clone();
        if (w.ChaseBonusColorWeights != null)
            ChaseBonusColorWeights = (float[])w.ChaseBonusColorWeights.Clone();
    }

    #endregion
    #region PLAYER-BOARD, OPPONENT AND DISPLAY QUERIES

    //Player board queries — my player
    protected abstract bool MyWallOccupied(int row, int col);
    protected abstract bool MyLineFull(int line);
    protected abstract byte MyLineColor(int line);
    protected abstract int MyLineCount(int line);
    protected abstract bool MyLineEmpty(int line);
    protected abstract int MyLineCapacity(int line);

    protected abstract int MyPenaltyCount();
    protected abstract int MyCalculatePenalty();
    protected abstract int MyGetWallColumn(int row, int colorIndex);

    // Board queries — opponent player
    protected abstract bool OppCanPlaceInLine(int line, byte color);
    protected abstract byte OppLineColor(int line);
    protected abstract int OppLineCount(int line);
    protected abstract int OppLineCapacity(int line);

    // Display queries
    protected abstract int GetNumFactories();
    protected abstract int GetNonEmptyFactoryCount();
    protected abstract int GetFactoryColorCount(int factoryIndex, int colorIndex);
    protected abstract int GetFactoryflowerCount(int factoryIndex);
    protected abstract int GetCentralColorCount(int colorIndex);

    // Game state
    protected abstract int GetCurrentRound();

    #endregion

    #region MOVE EVALUATION


    // Evaluates a move using OptimalHeuristicBase's own weight set.

    protected virtual float EvaluateMove(
        MoveSource source, int factoryIndex, FlowerColor color,
        int targetLineIndex, int flowerCount, bool isPenalty,
        bool centralHasToken)
    {
        if (isPenalty)
            return ScorePenaltyMove(flowerCount) * PenaltyWeight;

        int lineIdx = targetLineIndex;
        int capacity = MyLineCapacity(lineIdx);
        int lineCount = MyLineCount(lineIdx);
        int spaceLeft = capacity - lineCount;
        bool completesLine = flowerCount >= spaceLeft;
        int tilesPlaced = System.Math.Min(flowerCount, spaceLeft);
        int tilesOverflow = System.Math.Max(0, flowerCount - spaceLeft);

        float score = 0f;

        // Immediate scoring simulation
        score += SimulateLineScoring(color, lineIdx, completesLine) * SimulateScoringWeight;

        // Tiles placed vs overflow
        score += tilesPlaced * TilesPlacedWeight;
        score -= tilesOverflow * PenaltyWeight;

        // Line completion bonus
        if (completesLine)
            score += LineCompletionWeight;

        // Per-row/column/color chase-bonus for end-game bonuses
        score += GetWeightedEndGameBonuses(color, lineIdx);

        // Opponent denial
        score += SimulateDenyingOpponent(color, flowerCount) * TryDenyMoveWeight;

        // Pushing penalties to opponent
        score += SimulatePushingPenalties(source, factoryIndex, color) * TryPushPenaltyWeight;

        // First player token
        if (source == MoveSource.Central && centralHasToken)
            score += FirstPlayerTokenWeight + MyCalculatePenalty();

        // Top rows preference
        if (lineIdx <= _gridSize / 2)
            score += PrioritizeTopRowsWeight;

        // Central column preference
        int wallCol = MyGetWallColumn(lineIdx, (int)color);
        int middle = _gridSize / 2;
        int distCenter = System.Math.Abs(middle - wallCol);
        score += (middle - distCenter) * PrioritizeCentralPlacementWeight;

        // Avoid partial lines in bottom half
        if (lineIdx >= _gridSize / 2 && !completesLine)
        {
            score -= AvoidPartialLineWeight;
            if (MyLineFull(_gridSize - 1)) score -= AvoidPartialLineWeight;
            if (MyLineFull(_gridSize - 2)) score -= AvoidPartialLineWeight;
        }

        return score;
    }

    // Penalty scoring value.
    protected float ScorePenaltyMove(int flowerCount)
    {
        float penScore = 0f;
        int penaltyStart = MyPenaltyCount();
        for (int i = 0; i < flowerCount && (penaltyStart + i) < _penaltyCapacity; i++)
        {
            int slot = penaltyStart + i;
            penScore += _penaltyValues[slot];
        }
        return penScore;
    }

    // Scoring Simulation

    protected float SimulateLineScoring(FlowerColor color, int lineIdx, bool completesLine)
    {
        if (!completesLine) return 0f;

        int row = lineIdx;
        int col = MyGetWallColumn(row, (int)color);

        int h = 1;
        for (int c = col - 1; c >= 0 && MyWallOccupied(row, c); c--) h++;
        for (int c = col + 1; c < _gridSize && MyWallOccupied(row, c); c++) h++;

        int v = 1;
        for (int r = row - 1; r >= 0; r--)
        {
            if (MyWallOccupied(r, col))
                v++;
            else if (MyLineFull(r))
            {
                byte projectedColor = MyLineColor(r);
                if (projectedColor != 255 && MyGetWallColumn(r, projectedColor) == col)
                    v++;
                else
                    break;
            }
            else
                break;
        }
        for (int r = row + 1; r < _gridSize; r++)
        {
            if (MyWallOccupied(r, col))
                v++;
            else
                break;
        }

        if (h == 1 && v == 1) return 1f;
        float pts = 0f;
        if (h > 1) pts += h;
        if (v > 1) pts += v;
        return pts;
    }

    // Weighted end-game bonus using per-row, per-column, and per-color weights.

    protected float GetWeightedEndGameBonuses(FlowerColor color, int lineIdx)
    {
        float bonus = 0f;
        bonus += CheckPartialRow(lineIdx) * GetChaseBonusRowWeight(lineIdx);
        bonus += CheckPartialColumn(color, lineIdx) * GetChaseBonusColumnWeight(color, lineIdx);
        bonus += CheckPartialColor(color) * GetChaseBonusColorWeight(color);
        return bonus;
    }

    protected float GetChaseBonusRowWeight(int row)
    {
        if (ChaseBonusRowWeights != null && row >= 0 && row < ChaseBonusRowWeights.Length)
            return ChaseBonusRowWeights[row];
        return 1f;
    }

    protected float GetChaseBonusColumnWeight(FlowerColor color, int row)
    {
        int col = MyGetWallColumn(row, (int)color);
        if (ChaseBonusColumnWeights != null && col >= 0 && col < ChaseBonusColumnWeights.Length)
            return ChaseBonusColumnWeights[col];
        return 1f;
    }

    protected float GetChaseBonusColorWeight(FlowerColor color)
    {
        int idx = (int)color;
        if (ChaseBonusColorWeights != null && idx >= 0 && idx < ChaseBonusColorWeights.Length)
            return ChaseBonusColorWeights[idx];
        return 1f;
    }

    protected float CheckPartialRow(int row)
    {
        int filled = 0;
        for (int c = 0; c < _gridSize; c++) if (MyWallOccupied(row, c)) filled++;
        int remaining = _gridSize - filled;
        return remaining <= 0 ? 0f : _bonusRow / (remaining * remaining);
    }

    protected float CheckPartialColumn(FlowerColor color, int row)
    {
        int col = MyGetWallColumn(row, (int)color);
        int filled = 0;
        for (int r = 0; r < _gridSize; r++) if (MyWallOccupied(r, col)) filled++;
        int remaining = _gridSize - filled;
        return remaining <= 0 ? 0f : _bonusColumn / (remaining * remaining);
    }

    protected float CheckPartialColor(FlowerColor color)
    {
        int colorIdx = (int)color;
        int filled = 0;
        for (int r = 0; r < _gridSize; r++)
        {
            int c = MyGetWallColumn(r, colorIdx);
            if (MyWallOccupied(r, c)) filled++;
        }
        int remaining = _gridSize - filled;
        return remaining <= 0 ? 0f : _bonusColor / (remaining * remaining);
    }

    // DENIAL AND OPPONENT DISRUPTION

    protected float SimulateDenyingOpponent(FlowerColor color, int flowerCount)
    {
        float score = 0f;
        byte colorByte = (byte)color;

        for (int line = 0; line < _numPlacementLines; line++)
        {
            if (!OppCanPlaceInLine(line, colorByte)) continue;

            score += 1f;

            byte oppColor = OppLineColor(line);
            if (oppColor != 255 && oppColor == colorByte)
            {
                score += 2f;
                int oppSpace = OppLineCapacity(line) - OppLineCount(line);
                if (flowerCount >= oppSpace)
                    score += 3f;
            }
        }
        return score;
    }

    // PUSHING PENALTIES (LAST-FACTORY POISON)
    private int[] _projectedCentralState;

    protected float SimulatePushingPenalties(MoveSource source, int factoryIndex, FlowerColor moveColor)
    {
        // Only applies when picking from the last non-empty factory
        if (source != MoveSource.Factory) return 0f;
        if (GetNonEmptyFactoryCount() != 1) return 0f;

        byte pickedColor = (byte)moveColor;
        int distinctColors = 0;
        int poisonCount = 0;


        for (int c = 0; c < _numColors; c++)
        {
            _projectedCentralState[c] = GetCentralColorCount(c);
            if ((byte)c != pickedColor)
                _projectedCentralState[c] += GetFactoryColorCount(factoryIndex, c);
        }

        //Count colors
        for (int c = 0; c < _numColors; c++)
        {
            int groupSize = _projectedCentralState[c];
            if (groupSize == 0) continue;

            distinctColors++;

            // Check if the opponent can place this color
            bool canPlace = false;
            byte colorByte = (byte)c;
            for (int line = 0; line < _numPlacementLines; line++)
            {
                if (OppCanPlaceInLine(line, colorByte)) { canPlace = true; break; }
            }

            if (!canPlace)
            {
                poisonCount += groupSize;
            }
        }

        if (poisonCount == 0) return 0f;

        //TODO: IMPROVE THIS TO BE MORE REALISTIC
        return poisonCount;
    }



    #endregion
}
