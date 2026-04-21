using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


// SO containing all game configuration values.
//Used to define variants and player counts.
//Loaded by GameManager.

[CreateAssetMenu(fileName = "GameConfigData", menuName = "Ikebana/Game Config")]
public class GameConfigData : ScriptableObject
{

    [Header("General")]
    [Tooltip("Enable for non-standard setups (e.g. different factory displays, starting bag colors, etc.)")]
    public bool NonStandardSetups = false;

    [Header("Game Settings")]
    [Tooltip("Number of players in the game")]
    public int NumPlayers = 2;
    
    [Header("Bag")]
    [Tooltip("Number of flowers per color in the bag")]
    public int CountPerColor = 20;
    [Tooltip("Starting color sets in bag for special setups")]
    public KeyValuePair<FlowerColor, int> StartingBagColors;

    [Header("Discard")]
    [Tooltip("Starting color sets in discard for special setups")]
    public KeyValuePair<FlowerColor, int> StartingDiscardColors;

    [Header("Central")]
    [Tooltip("Starting color sets in central for special setups")]
    public KeyValuePair<FlowerColor, int> StartingCentralColors;

    [Header("Flowers")]
    [Tooltip("Number of flower colors")]
    public int NumColors = 5;
    [Tooltip("List of flower colors used in the game (index = color code)")]
    public FlowerColor[] FlowerColors;

    [Header("Displays")]
    [Tooltip("Number of factory displays in the game")]
    public int NumFactories = 5;

    [Tooltip("Flowers drawn into each factory display per round")]
    public int FlowersPerDisplay = 4;

    [Tooltip("List of factories for special setups")]
    public DisplayConfig[] DisplaySetups;

    [Header("Player Grid")]
    [Tooltip("Grid size (NxN)")]
    public int GridSize = 5;

    [Tooltip("Grid Color Pattern coded as ints (To get coordinates do row * GridSize + column, value = color for that row)")]
    public int[] GridColorPattern1;
    [Tooltip("Grid Color Pattern coded as ints (To get coordinates do row * GridSize + column, value = color for that row)")]
    public int[] GridColorPattern2;
    [Tooltip("Grid Color Pattern coded as ints (To get coordinates do row * GridSize + column, value = color for that row)")]
    public int[] GridColorPattern3;
    [Tooltip("Grid Color Pattern coded as ints (To get coordinates do row * GridSize + column, value = color for that row)")]
    public int[] GridColorPattern4;

    [Header("Placement Lines")]
    [Tooltip("Number of placement lines on the player board")]
    public int NumPlacementLines = 5;

    [Tooltip("Capacity of each placement line (index 0 = line 0)")]
    public int[] PlacementLineCapacities = { 1, 2, 3, 4, 5 };

    [Header("Penalty Line")]
    [Tooltip("Maximum slots in the penalty line")]
    public int PenaltyLineCapacity = 7;

    [Tooltip("Penalty points per slot (should be negative). Index = slot position.")]
    public int[] PenaltyLineValues = { -1, -1, -2, -2, -3, -3, -4 };

    [Header("End Game Scoring Bonuses")]
    [Tooltip("Bonus for completing a full horizontal line on the grid")]
    public int CompleteLineScoringBonus = 2;

    [Tooltip("Bonus for completing a full vertical column on the grid")]
    public int CompleteColumnScoringBonus = 7;

    [Tooltip("Bonus for placing all 5 flowers of the same color on the grid")]
    public int CompleteColorScoringBonus = 10;

    [Tooltip("Bonus for completing a full set of colors (all 5 colors placed at least once) //NOT IMPLEMENTED YET")]
    public int ColorsSetScoringBonus = 0;

    [Header("Special Scoring Bonuses")]
    [Tooltip("Special bonus A (define usage in game variant rules) //NOT IMPLEMENTED YET")]
    public int SpecialScoringBonusA = 0;

    [Tooltip("Special bonus B (define usage in game variant rules)//NOT IMPLEMENTED YET")]
    public int SpecialScoringBonusB = 0;

    [Tooltip("Special bonus C (define usage in game variant rules)//NOT IMPLEMENTED YET")]
    public int SpecialScoringBonusC = 0;

    [Tooltip("Special bonus D (define usage in game variant rules)//NOT IMPLEMENTED YET")]
    public int SpecialScoringBonusD = 0;

    

}

public struct DisplayConfig
{
    public int DisplayIndex;
    public FlowerColor[] flowers; 
}