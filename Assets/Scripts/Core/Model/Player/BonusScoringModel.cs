using UnityEngine;

//=^..^=   =^..^=   VERSION 1.0.1 (March 2026)    =^..^=    =^..^=
//                    Last Update 22/03/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// Calculates bonus points at end of game.
// Checks completed rows, columns and colors.

public struct EndGameBonusBreakdown
{
    public int RowPoints;
    public int ColumnPoints;
    public int ColorPoints;

    public int TotalBonus => RowPoints + ColumnPoints + ColorPoints;
}

public static class BonusScoringModel
{

    //TODO: Separate each bonus type so they can be configured individualy in game config
    //BONUSES:
    // Line --> value
    // Column --> value
    // Color --> value
    // Set --> value
    // PatternA --> value
    // PatternB --> value

    public static int CalculateEndGameBonus(PlayerGridModel grid)
    {
        return CalculateEndGameBonusBreakdown(grid).TotalBonus;
    }

    public static EndGameBonusBreakdown CalculateEndGameBonusBreakdown(PlayerGridModel grid)
    {
        EndGameBonusBreakdown breakdown = new EndGameBonusBreakdown();
        int size = GameConfig.GRID_SIZE;

        // Complete rows
        for (int r = 0; r < size; r++)
        {
            bool complete = true;
            for (int c = 0; c < size; c++)
            {
                if (!grid.IsOccupied(r, c)) { complete = false; break; }
            }
            if (complete) breakdown.RowPoints += GameConfig.COMPLETE_LINE_SCORING_BONUS;
            //Debug.Log("Row " + r + " complete: " + complete);
        }

        // Complete columns
        for (int c = 0; c < size; c++)
        {
            bool complete = true;
            for (int r = 0; r < size; r++)
            {
                if (!grid.IsOccupied(r, c)) { complete = false; break; }
            }
            if (complete) breakdown.ColumnPoints += GameConfig.COMPLETE_COLUMN_SCORING_BONUS;
            //Debug.Log("Column " + c + " complete: " + complete);
        }

        // Complete colors (all 5 of the same color placed on the grid)
        for (int color = 0; color < GameConfig.NUM_COLORS; color++)
        {
            FlowerColor flowerColor = (FlowerColor)color;
            bool allPlaced = true;
            for (int r = 0; r < size; r++)
            {
                if (!grid.IsColorPlacedInRow(r, flowerColor)) { allPlaced = false; break; }
            }
            if (allPlaced) breakdown.ColorPoints += GameConfig.COMPLETE_COLOR_SCORING_BONUS;
            //Debug.Log("Color " + flowerColor + " complete: " + allPlaced);
        }

        //TODO: CONTINUE WITH OTHER BONUS CALCULATIONS (SETS, PATTERNS, ETC) WHEN THEY ARE DEFINED

        return breakdown;
    }
}
