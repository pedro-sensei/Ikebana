using System.Collections.Generic;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

//TODO: UPDATE TO USE IGameModel AND SUPPORT DIFFERENT GAME MODEL VERSIONS
public static class MoveGenerator
{
    // Returns all valid moves for the current player.
    public static List<GameMove> GetAllValidMoves(GameModel model)
    {
        List<GameMove> moves = new List<GameMove>();

        if (model.IsGameOver || model.CurrentPhase != RoundPhaseEnum.PlacementPhase)
            return moves;

        PlayerModel player = model.GetCurrentPlayer();

        // Moves from factory displays
        for (int f = 0; f < model.FactoryDisplays.Length; f++)
        {
            DisplayModel factory = model.FactoryDisplays[f];
            HashSet<FlowerColor> colors = factory.GetAvailableColors();
            foreach (FlowerColor color in colors)
            {
                int flowerCount = factory.CountColor(color);
                AddMovesForColorAndSource(moves, player, MoveSource.Factory, f, color, flowerCount);
            }
        }

        // Moves from central display
        DisplayModel central = model.CentralDisplay;
        HashSet<FlowerColor> centralColors = central.GetAvailableColors();
        foreach (FlowerColor color in centralColors)
        {
            int flowerCount = central.CountColor(color);
            AddMovesForColorAndSource(moves, player, MoveSource.Central, 0, color, flowerCount);
        }

        return moves;
    }

    private static void AddMovesForColorAndSource(
        List<GameMove> moves,
        PlayerModel player,
        MoveSource source,
        int factoryIndex,
        FlowerColor color,
        int flowerCount)
    {
        // Check each placement line
        for (int line = 0; line < GameConfig.NUM_PLACEMENT_LINES; line++)
        {
            if (player.CanPlaceInLine(line, color))
            {
                moves.Add(new GameMove
                {
                    SourceEnum = source,
                    SourceIsFactory = source == MoveSource.Factory,
                    FactoryIndex = factoryIndex,
                    Color = color,
                    TargetEnum = MoveTarget.PlacementLine,
                    TargetLineIndex = line, 
                    flowerCount = flowerCount
                });
            }
        }

        // Penalty move is always available
        moves.Add(new GameMove
        {
            SourceEnum = source,
            SourceIsFactory = source == MoveSource.Factory,
            FactoryIndex = factoryIndex,
            Color = color,
            TargetEnum = MoveTarget.PenaltyLine,
            IsPenalty = true,
            TargetLineIndex = 0,
            flowerCount = flowerCount
        });
    }

    // Returns only moves that place flowers in a valid line (no penalty-only moves
    // unless no line accepts the color).
    public static List<GameMove> GetNonPenaltyMoves(GameModel model)
    {
        List<GameMove> allMoves = GetAllValidMoves(model);
        List<GameMove> nonPenalty = new List<GameMove>();

        foreach (GameMove move in allMoves)
        {
            if (!move.IsPenalty)
            {
                nonPenalty.Add(move);
            }
        }

        // If there are no non-penalty moves, return all
        if (nonPenalty.Count > 0)
            return nonPenalty;
        return allMoves;
    }
}


