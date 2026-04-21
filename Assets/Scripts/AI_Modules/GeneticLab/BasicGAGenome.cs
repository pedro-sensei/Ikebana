using UnityEngine;
//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


//New Genome for greedy heuristics. For the Optimizer version.

[CreateAssetMenu(fileName = "BasicGAGenome", menuName = "Ikebana/AI/Optimizer Weights")]
public class BasicGAGenome : ScriptableObject
{
    [Header("Core Weights")]
    public float SimulateScoringWeight = 3f;
    public float PenaltyWeight = 3f;
    public float TilesPlacedWeight = 1f;
    public float LineCompletionWeight = 5f;

    [Header("Chasing end game Bonus (per row/column/color)")]
    public float[] ChaseBonusRowWeights;
    public float[] ChaseBonusColumnWeights;
    public float[] ChaseBonusColorWeights;

    [Header("Opponent Interaction")]
    public float TryPushPenaltyWeight = 5f;
    public float TryDenyMoveWeight = 5f;

    [Header("Positional")]
    public float AvoidPartialLineWeight = 5f;
    public float FirstPlayerTokenWeight = 5f;
    public float PrioritizeCentralPlacementWeight = 2f;
    public float PrioritizeTopRowsWeight = 2f;

    public void RandomInitialise()
    {
        SimulateScoringWeight            = Random.Range(0f, 20f);
        PenaltyWeight                    = Random.Range(0f, 20f);
        TilesPlacedWeight                = Random.Range(0f, 20f);
        LineCompletionWeight             = Random.Range(0f, 20f);
        TryPushPenaltyWeight             = Random.Range(0f, 20f);
        TryDenyMoveWeight                = Random.Range(0f, 20f);
        AvoidPartialLineWeight           = Random.Range(0f, 20f);
        FirstPlayerTokenWeight           = Random.Range(0f, 20f);
        PrioritizeCentralPlacementWeight = Random.Range(0f, 10f);
        PrioritizeTopRowsWeight          = Random.Range(0f, 10f);

        if (ChaseBonusRowWeights != null)
            for (int i = 0; i < ChaseBonusRowWeights.Length; i++)
                ChaseBonusRowWeights[i] = Random.Range(0f, 20f);
        if (ChaseBonusColumnWeights != null)
            for (int i = 0; i < ChaseBonusColumnWeights.Length; i++)
                ChaseBonusColumnWeights[i] = Random.Range(0f, 20f);
        if (ChaseBonusColorWeights != null)
            for (int i = 0; i < ChaseBonusColorWeights.Length; i++)
                ChaseBonusColorWeights[i] = Random.Range(0f, 20f);
    }

    public BasicGAGenome Clone()
    {
        BasicGAGenome copy = CreateInstance<BasicGAGenome>();
        copy.CopyFrom(this);
        return copy;
    }

    public void CopyFrom(BasicGAGenome source)
    {
        SimulateScoringWeight            = source.SimulateScoringWeight;
        PenaltyWeight                    = source.PenaltyWeight;
        TilesPlacedWeight                = source.TilesPlacedWeight;
        LineCompletionWeight             = source.LineCompletionWeight;
        TryPushPenaltyWeight             = source.TryPushPenaltyWeight;
        TryDenyMoveWeight                = source.TryDenyMoveWeight;
        AvoidPartialLineWeight           = source.AvoidPartialLineWeight;
        FirstPlayerTokenWeight           = source.FirstPlayerTokenWeight;
        PrioritizeCentralPlacementWeight = source.PrioritizeCentralPlacementWeight;
        PrioritizeTopRowsWeight          = source.PrioritizeTopRowsWeight;

        ChaseBonusRowWeights    = source.ChaseBonusRowWeights != null ? (float[])source.ChaseBonusRowWeights.Clone() : null;
        ChaseBonusColumnWeights = source.ChaseBonusColumnWeights != null ? (float[])source.ChaseBonusColumnWeights.Clone() : null;
        ChaseBonusColorWeights  = source.ChaseBonusColorWeights != null ? (float[])source.ChaseBonusColorWeights.Clone() : null;
    }
}
