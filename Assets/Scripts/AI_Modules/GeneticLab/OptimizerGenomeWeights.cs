//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


//Same as BasicGAGenome (ScriptableObject).
//Struct for threading.
public struct OptimizerGenomeWeights
{
    public float SimulateScoringWeight;
    public float PenaltyWeight;
    public float TilesPlacedWeight;
    public float LineCompletionWeight;

    public float[] ChaseBonusRowWeights;
    public float[] ChaseBonusColumnWeights;
    public float[] ChaseBonusColorWeights;

    public float TryPushPenaltyWeight;
    public float TryDenyMoveWeight;

    public float AvoidPartialLineWeight;

    public float FirstPlayerTokenWeight;
    public float PrioritizeCentralPlacementWeight;
    public float PrioritizeTopRowsWeight;

    public static OptimizerGenomeWeights FromGenome(BasicGAGenome g)
    {
        return new OptimizerGenomeWeights
        {
            SimulateScoringWeight            = g.SimulateScoringWeight,
            PenaltyWeight                    = g.PenaltyWeight,
            TilesPlacedWeight                = g.TilesPlacedWeight,
            LineCompletionWeight             = g.LineCompletionWeight,
            ChaseBonusRowWeights             = g.ChaseBonusRowWeights != null ? (float[])g.ChaseBonusRowWeights.Clone() : null,
            ChaseBonusColumnWeights          = g.ChaseBonusColumnWeights != null ? (float[])g.ChaseBonusColumnWeights.Clone() : null,
            ChaseBonusColorWeights           = g.ChaseBonusColorWeights != null ? (float[])g.ChaseBonusColorWeights.Clone() : null,
            TryPushPenaltyWeight             = g.TryPushPenaltyWeight,
            TryDenyMoveWeight                = g.TryDenyMoveWeight,
            AvoidPartialLineWeight           = g.AvoidPartialLineWeight,
            FirstPlayerTokenWeight           = g.FirstPlayerTokenWeight,
            PrioritizeCentralPlacementWeight = g.PrioritizeCentralPlacementWeight,
            PrioritizeTopRowsWeight          = g.PrioritizeTopRowsWeight,
        };
    }
}
