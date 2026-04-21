using UnityEngine;

//=^..^=   =^..^=   VERSION 1.0.2 (April 2026)    =^..^=    =^..^=
//                    Last Update 01/04/2026 

// DEPRECATED 15/04/2026.
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

/// TO BE DEPRECATED.
/// 

// ScriptableObject containing all heuristic weights 
// for the Greedy AI.
// Create multiple assets to test different strategies.

//[CreateAssetMenu(fileName = "GAGenome", menuName = "Ikebana/AI/Greedy Weights")]
/*public class GAGenome : ScriptableObject
{
    #region FIELDS AND PROPERTIES
    [Header("Short Term Planning")]
    [Tooltip("Multiplier for simulated immediate scoring")]
    public float placingWeight = 10f;
    [Tooltip("Bonus for one move filling")]
    public float oneMoveFillingBonus = 10f;
    [Tooltip("Bonus for completing a line")]
    public float lineCompletionBonus = 10f;

    [Header("Long Term Planning")]
    [Tooltip("Multiplier for partial end-game bonus contribution")]
    public float endGameBonusWeight = 5f;
    [Tooltip("Bonus for targeting top-half rows in early rounds")]
    public float topRowsWeight = 2f;
    [Tooltip("Rounds considered 'early game' for top-row preference")]
    public int earlyRoundsThreshold = 2;
    [Tooltip("Multiplier for center-column proximity bonus")]
    public float centralPlacementWeight = 2f;
    [Tooltip("Score per flower sent to penalty (applied as negative)")]
    public float penaltyPerflower = 10f;

    [Header("Opponent Denial")]
    [Tooltip("Multiplier for opponent denial score")]
    public float denyOpponentWeight = 5f;
    [Tooltip("Multiplier for pushing-opponent-to-penalty score")]
    public float pushingPenaltiesWeight = 5f;
    [Header("Opponent Denial (detail)")]
    [Tooltip("Small bonus per opponent line that could accept this color")]
    public float denyColorBaseBonus = 1f;
    [Tooltip("Bonus when opponent already has this color in a line")]
    public float disruptExistingLineBonus = 5f;
    [Tooltip("Bonus when taking enough flowers to deny opponent completing their line")]
    public float denyCompletionBonus = 10f;
    [Tooltip("Bonus count per poison color in the remaining pool")]
    public float poisonColorBonus = 3f;

    [Header("Penalty Avoidance")]
    [Tooltip("Penalty for leaving partial lines in the bottom half")]
    public float avoidPartialLinesWeight = 5f;

    [Header("First Player Token")]
    [Tooltip("Base bonus for picking from central when first player token is present")]
    public float firstPlayerTokenBaseBonus = 5f;



    //[Tooltip("Bonus when opponent is forced to take the last move")]
    //public float opponentLastMoveBonus = 5f;

    //[Tooltip("Bonus when the single last move is poison for opponent")]
    //public float forcedPoisonLastMoveBonus = 15f;

    //[Tooltip("Penalty per color we can't place that remains in the pool")]
    //public float selfPoisonPenalty = 2f;

    //[Tooltip("Extra bonus per confirmed poison color remaining")]
    //public float poisonRemainingBonus = 2f;
    #endregion
    #region CONSTRUCTOR AND INITIALIZATION
    public void RandomInitialise()
    {
        placingWeight              = Random.Range(0f, 20f);
        endGameBonusWeight         = Random.Range(0f, 20f);
        denyOpponentWeight         = Random.Range(0f, 20f);
        pushingPenaltiesWeight     = Random.Range(0f, 20f);
        topRowsWeight              = Random.Range(0f, 10f);
        //earlyRoundsThreshold       = Random.Range(1, 4);
        centralPlacementWeight     = Random.Range(0f, 10f);
        avoidPartialLinesWeight    = Random.Range(0f, 20f);
        penaltyPerflower             = Random.Range(0f, 30f);
        firstPlayerTokenBaseBonus  = Random.Range(0f, 20f);
        denyColorBaseBonus         = Random.Range(0f, 10f);
        disruptExistingLineBonus   = Random.Range(0f, 20f);
        denyCompletionBonus        = Random.Range(0f, 30f);
        poisonColorBonus           = Random.Range(0f, 10f);
        oneMoveFillingBonus        = Random.Range(0f, 20f);
        //lineCompletionBonus        = Random.Range(0f, 20f);
        //opponentLastMoveBonus      = Random.Range(0f, 15f);
        //forcedPoisonLastMoveBonus  = Random.Range(0f, 30f);
        //selfPoisonPenalty          = Random.Range(0f, 10f);
        //poisonRemainingBonus       = Random.Range(0f, 10f);
    }

    #endregion
    #region AUXILIARY METHODS
 
    public GAGenome Clone()
    {
        GAGenome copy = CreateInstance<GAGenome>();
        copy.CopyFrom(this);
        return copy;
    }

    public void CopyFrom(GAGenome source)
    {
        placingWeight             = source.placingWeight;
        endGameBonusWeight        = source.endGameBonusWeight;
        denyOpponentWeight        = source.denyOpponentWeight;
        pushingPenaltiesWeight    = source.pushingPenaltiesWeight;
        topRowsWeight             = source.topRowsWeight;
        earlyRoundsThreshold      = source.earlyRoundsThreshold;
        centralPlacementWeight    = source.centralPlacementWeight;
        avoidPartialLinesWeight   = source.avoidPartialLinesWeight;
        penaltyPerflower            = source.penaltyPerflower;
        firstPlayerTokenBaseBonus = source.firstPlayerTokenBaseBonus;
        denyColorBaseBonus        = source.denyColorBaseBonus;
        disruptExistingLineBonus  = source.disruptExistingLineBonus;
        denyCompletionBonus       = source.denyCompletionBonus;
        poisonColorBonus          = source.poisonColorBonus;
        oneMoveFillingBonus       = source.oneMoveFillingBonus;
        lineCompletionBonus       = source.lineCompletionBonus;
    }

    #endregion
}
*/