using System;
//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

//A serializable version of the game state, Only basic data.
[System.Serializable]
public class SerializedGameState
{
    // GLOBAL STATE
    public bool IsGameOver;
    public int StartingPlayerIndex;
    public int CurrentPlayerIndex;
    public int CurrentRound;
    public int TurnNumber;
    public int TotalTurnsPlayed;
    public int CurrentPhase; // cast from RoundPhaseEnum

    // MAIN BOARD
    public int[] Bag;
    public int[] Discard;
    public int[] CentralDisplay;
    public bool CentralHasFirstPlayerToken;
    public int[][] FactoryDisplays;
    public int[] FactoryFillColorCounts;

    // PLAYER BOARDS
    public SerializedPlayer[] Players;
}