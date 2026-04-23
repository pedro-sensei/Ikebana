using UnityEngine;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=
[System.Serializable]

public class SerializedPlayer
{
    public string Name;
    public int Score;
    public int PlayerIndex;
    public bool HasFirstPlayerToken;

    // Portrait index 
    public int PortraitIndex;

    // Player highlight color
    public float PlayerColorR;
    public float PlayerColorG;
    public float PlayerColorB;
    public float PlayerColorA = 1f;

    // AI setup
    public bool IsAI;
    public int AIBrainTypeIndex;

    // Grid as array of integers,
    // each represents the color of the flower
    // (or -1 for empty)
    public int[] Grid;

    // Placement lines as a 2D array of integers,
    // each represents a placement line
    // contains the colors of the flowers in that line
    public int[][] PlacementLines;

    // Penalty as an array of integers
    public int[] PenaltyLine;

    // Statistics
    public int TotalPlacementPoints;
    public int TotalPenaltyPoints;
    public int[] PenaltyPointsByRound;
    public int EndGameBonusRows;
    public int EndGameBonusColumns;
    public int EndGameBonusColors;
}