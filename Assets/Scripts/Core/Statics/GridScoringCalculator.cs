using System;

public static class GridScoringCalculator
{
    public static int CalculatePlacementPoints(GameModel model, int playerIndex, int row, int col,
                                               bool includeProjectedFullLines = false)
    {
        if (model == null || model.Players == null || playerIndex < 0 || playerIndex >= model.Players.Length)
            return 0;

        return CalculatePlacementPoints(model.Players[playerIndex], row, col, includeProjectedFullLines);
    }

    public static int CalculatePlacementPoints(PlayerModel player, int row, int col,
                                               bool includeProjectedFullLines = false)
    {
        if (player == null) return 0;

        PlayerGridModel grid = player.Grid;
        int size = GameConfig.GRID_SIZE;

        if (row < 0 || row >= size || col < 0 || col >= size)
            return 0;

        if (grid.IsOccupied(row, col))
            return 0;

        return CalculatePlacementPoints(
            size,
            delegate (int r, int c)
            {
                if (grid.IsOccupied(r, c)) return true;
                if (!includeProjectedFullLines) return false;
                return IsProjectedOccupiedFromFullLine(player, grid, row, r, c);
            },
            row,
            col);
    }

    public static int CalculatePlacementPoints(int gridSize, Func<int, int, bool> isOccupied, int row, int col)
    {
        int horizontalCount = 1;
        int verticalCount = 1;

        for (int c = col - 1; c >= 0; c--)
        {
            if (isOccupied(row, c)) horizontalCount++;
            else break;
        }

        for (int c = col + 1; c < gridSize; c++)
        {
            if (isOccupied(row, c)) horizontalCount++;
            else break;
        }

        for (int r = row - 1; r >= 0; r--)
        {
            if (isOccupied(r, col)) verticalCount++;
            else break;
        }

        for (int r = row + 1; r < gridSize; r++)
        {
            if (isOccupied(r, col)) verticalCount++;
            else break;
        }

        if (horizontalCount == 1 && verticalCount == 1)
            return 1;

        int points = 0;
        if (horizontalCount > 1) points += horizontalCount;
        if (verticalCount > 1) points += verticalCount;
        return points;
    }

    private static bool IsProjectedOccupiedFromFullLine(PlayerModel player, PlayerGridModel grid,
                                                        int evalRow, int row, int col)
    {
        // End-of-round scoring places full lines from top to bottom.
        // For a potential placement at evalRow, only rows above evalRow can be
        // considered already projected on the wall.
        if (row >= evalRow) return false;

        PlacementLineModel[] lines = player.PlacementLines;
        if (lines == null) return false;
        if (row < 0 || row >= lines.Length) return false;

        PlacementLineModel placement = lines[row];
        if (placement == null || !placement.IsFull || !placement.CurrentColor.HasValue)
            return false;

        int projectedCol = grid.GetColumnForColor(row, placement.CurrentColor.Value);
        if (projectedCol != col) return false;

        return !grid.IsOccupied(row, col);
    }
}
