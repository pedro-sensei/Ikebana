// Plain serializable data class for JSON export of a genome.
[System.Serializable]
public class GAGenomeData
{
    public float placingWeight;
    public float endGameBonusWeight;
    public float denyOpponentWeight;
    public float pushingPenaltiesWeight;
    public float topRowsWeight;
    public int   earlyRoundsThreshold;
    public float centralPlacementWeight;
    public float avoidPartialLinesWeight;
    public float penaltyPerflower;
    public float firstPlayerTokenBaseBonus;
    public float denyColorBaseBonus;
    public float disruptExistingLineBonus;
    public float denyCompletionBonus;
    public float poisonColorBonus;
    public float oneMoveFillingBonus;
    public float lineCompletionBonus;
    public float fitness;

    public static GAGenomeData FromGenome(GAGenome g)
    {
        return new GAGenomeData
        {
            placingWeight             = g.placingWeight,
            endGameBonusWeight        = g.endGameBonusWeight,
            denyOpponentWeight        = g.denyOpponentWeight,
            pushingPenaltiesWeight    = g.pushingPenaltiesWeight,
            topRowsWeight             = g.topRowsWeight,
            earlyRoundsThreshold      = g.earlyRoundsThreshold,
            centralPlacementWeight    = g.centralPlacementWeight,
            avoidPartialLinesWeight   = g.avoidPartialLinesWeight,
            penaltyPerflower          = g.penaltyPerflower,
            firstPlayerTokenBaseBonus = g.firstPlayerTokenBaseBonus,
            denyColorBaseBonus        = g.denyColorBaseBonus,
            disruptExistingLineBonus  = g.disruptExistingLineBonus,
            denyCompletionBonus       = g.denyCompletionBonus,
            poisonColorBonus          = g.poisonColorBonus,
            oneMoveFillingBonus       = g.oneMoveFillingBonus,
            lineCompletionBonus       = g.lineCompletionBonus,
        };
    }
}
