using System;
//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


//Minimal representation of game state.
public struct GameState
{
    public byte[] Bag;
    public int BagCount;
    public byte[] Discard;
    public int DiscardCount;
    public byte[][] Factories;
    public int[] FactoryCount;
    public byte[] Central;
    public int CentralCount;
    public bool CentralHasToken;
    public int CurrentRound;
    public int CurrentPlayer;
    public int TurnNumber;
    public int StartingPlayer;
    public bool IsGameOver;
    public RoundPhaseEnum Phase;
    public PlayerState[] PlayerSnapshots;

    public static GameState Create(int numPlayers, int numFactories, int flowersPerFactory)
    {
        int totalflowers = 200; // safe upper bound
        var snap = new GameState
        {
            Bag = new byte[totalflowers],
            Discard = new byte[totalflowers],
            Factories = new byte[numFactories][],
            FactoryCount = new int[numFactories],
            Central = new byte[totalflowers],
            PlayerSnapshots = new PlayerState[numPlayers]
        };
        for (int f = 0; f < numFactories; f++)
            snap.Factories[f] = new byte[flowersPerFactory];
        for (int p = 0; p < numPlayers; p++)
            snap.PlayerSnapshots[p] = PlayerState.Create();
        return snap;
    }
}

//Minimal representation of player state.
public struct PlayerState
{
    public int WallBits;
    public int PenaltyCount;
    public int Score;
    public bool HasFirstPlayerToken;
    public bool HasCompleteRow;
    public byte[] LineColor;
    public int[] LineCount;
    public int[] Forbidden;
    public byte[] Penalty;

    public static PlayerState Create(int gridSize = 5, int penaltyCapacity = 7)
    {
        return new PlayerState
        {
            LineColor = new byte[gridSize],
            LineCount = new int[gridSize],
            Forbidden = new int[gridSize],
            Penalty = new byte[penaltyCapacity]
        };
    }
}
