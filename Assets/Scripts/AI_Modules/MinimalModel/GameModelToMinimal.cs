

//=^..^=   =^..^=   VERSION 1.0.3 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

//Adapter from one game model to the other (Minimal)
public static class GameModelToMinimal
{
    public static MinimalGM Convert(GameModel source, MinimalGM existing = null)
    {
        int numPlayers = source.NumberOfPlayers;
        GameConfigSnapshot cfg = GameConfig.CreateSnapshot();
        MinimalGM gm = existing ?? new MinimalGM(numPlayers, cfg);
        gm.SetGameState(BuildSnapshot(source, cfg));
        return gm;
    }

    private static GameState BuildSnapshot(GameModel source, GameConfigSnapshot config)
    {
        int numPlayers = source.NumberOfPlayers;
        int numFactories = source.FactoryDisplays.Length;
        int numColors = config.NumColors;
        int gridSize = config.GridSize;

        GameState snap = GameState.Create(numPlayers, numFactories, config.flowersPerFactory);

        snap.BagCount = 0;
        foreach (FlowerPiece f in source.Bag.flowers)
            if (snap.BagCount < snap.Bag.Length)
                snap.Bag[snap.BagCount++] = (byte)(int)f.Color;

        snap.DiscardCount = 0;
        foreach (FlowerPiece f in source.Discard.flowers)
            if (snap.DiscardCount < snap.Discard.Length)
                snap.Discard[snap.DiscardCount++] = (byte)(int)f.Color;

        for (int fi = 0; fi < numFactories; fi++)
        {
            snap.FactoryCount[fi] = 0;
            foreach (FlowerPiece f in source.FactoryDisplays[fi].flowers)
                if (snap.FactoryCount[fi] < snap.Factories[fi].Length)
                    snap.Factories[fi][snap.FactoryCount[fi]++] = (byte)(int)f.Color;
        }

        snap.CentralCount = 0;
        foreach (FlowerPiece f in source.CentralDisplay.flowers)
            if (snap.CentralCount < snap.Central.Length)
                snap.Central[snap.CentralCount++] = (byte)(int)f.Color;
        snap.CentralHasToken = source.CentralDisplay.HasFirstPlayerToken;

        snap.CurrentRound = source.CurrentRound;
        snap.CurrentPlayer = source.CurrentPlayerIndex;
        snap.TurnNumber = source.TurnNumber;
        snap.StartingPlayer = source.StartingPlayerIndex;
        snap.IsGameOver = source.IsGameOver;
        snap.Phase = source.CurrentPhase;

        for (int p = 0; p < numPlayers; p++)
        {
            PlayerModel src = source.Players[p];
            PlayerState ps = snap.PlayerSnapshots[p];

            ps.Score = src.Score;
            ps.HasFirstPlayerToken = src.HasFirstPlayerToken;
            ps.PenaltyCount = 0;
            ps.WallBits = 0;

            for (int r = 0; r < gridSize; r++)
                for (int c = 0; c < gridSize; c++)
                    if (src.Grid.IsOccupied(r, c))
                        ps.WallBits |= 1 << (r * gridSize + c);

            for (int l = 0; l < src.PlacementLines.Length && l < gridSize; l++)
            {
                PlacementLineModel line = src.PlacementLines[l];
                ps.LineCount[l] = line.Count;
                ps.LineColor[l] = (line.Count > 0 && line.CurrentColor.HasValue)
                    ? (byte)(int)line.CurrentColor.Value
                    : (byte)255;
            }

            for (int r = 0; r < gridSize; r++)
                for (int c2 = 0; c2 < gridSize; c2++)
                    if (src.Grid.IsOccupied(r, c2))
                    {
                        int ci = (int)src.Grid.GetColorAt(r, c2);
                        if (ci >= 0 && ci < numColors)
                            ps.Forbidden[r] |= 1 << ci;
                    }

            ps.PenaltyCount = 0;
            foreach (FlowerPiece pf in src.PenaltyLine.Flowers)
            {
                if (ps.PenaltyCount >= ps.Penalty.Length) break;
                ps.Penalty[ps.PenaltyCount++] = (pf.Color == FlowerColor.FirstPlayer)
                    ? (byte)255
                    : (byte)(int)pf.Color;
            }

            snap.PlayerSnapshots[p] = ps;
        }

        return snap;
    }
}