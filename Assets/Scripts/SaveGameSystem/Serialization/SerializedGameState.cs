using System;
using UnityEngine;
//=^..^=   =^..^=   VERSION 1.0.2 (April 2026)    =^..^=    =^..^=
//                    Last Update 01/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

[CreateAssetMenu(fileName = "Gamestate_SO", menuName = "Scriptable Objects/Gamestate_SO")]
[Serializable]
public class SerializedGameState : ScriptableObject
{
    // GLOBAL STATE
    public bool IsGameOver;
    public int StartingPlayerIndex;
    public int CurrentPlayerIndex;
    public int CurrentRound;
    public int TurnNumber;
    public int CurrentPhase; // cast from RoundPhaseEnum

    // MAIN BOARD
    public int[] Bag;
    public int[] Discard;
    public int[] CentralDisplay;
    public bool CentralHasFirstPlayerToken;
    public int[][] FactoryDisplays;

    // PLAYER BOARDS
    public SerializedPlayer[] Players;
}