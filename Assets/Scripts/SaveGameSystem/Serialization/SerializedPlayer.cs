using UnityEngine;
//=^..^=   =^..^=   VERSION 1.0.2 (April 2026)    =^..^=    =^..^=
//                    Last Update 01/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=
[System.Serializable]

[CreateAssetMenu(fileName = "Gamestate_SO", menuName = "Scriptable Objects/SerializedPlayer")]
public class SerializedPlayer :ScriptableObject
{
    public string Name;
    public int Score;
    public int PlayerIndex;
    public bool HasFirstPlayerToken;

    // Portrait index (into PortraitDatabase) so it can be restored on load
    public int PortraitIndex;

    // AI configuration
    public bool IsAI;
    public int AIBrainTypeIndex;

    // Grid is serialized as a flat array of integers,
    // where each integer represents the color of the flower (or -1 for empty)
    public int[] Grid;

    // Placement lines are serialized as a 2D array of integers,
    // where each sub-array represents a placement line and contains the colors of the flowers in that line
    public int[][] PlacementLines;

    // Penalty line is serialized as an array of integers
    public int[] PenaltyLine;
}