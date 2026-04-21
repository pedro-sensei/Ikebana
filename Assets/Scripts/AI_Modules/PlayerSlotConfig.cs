using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

/// <summary>
/// Configuration for a single player slot.
/// </summary>
[System.Serializable]
public class PlayerSlotConfig
{
    public string PlayerName;
    public Sprite Portrait; //For visuals
    public Color PlayerColor = Color.white; //Persistent player identity color
    public PlayerType PlayerType = PlayerType.Human; //Human by default
    public AIBrainType AIBrain = AIBrainType.Random; //Random by default

   // [Tooltip("Weights asset for GreedyAdjustable brain type. Ignored by other brain types.")]
    //public GAGenome GreedyWeights; //Only for GreedyAdjustable brain type, ignored by others

    [Tooltip("Weights asset for Optimizer brain type. Ignored by other brain types.")]
    public BasicGAGenome OptimizerWeights;

    [Tooltip("Weights asset for Emily early game (rounds 1-2). Ignored by other brain types.")]
    public BasicGAGenome EmilyEarlyWeights;

    [Tooltip("Weights asset for Emily mid game (rounds 3-4). Ignored by other brain types.")]
    public BasicGAGenome EmilyMidWeights;

    [Tooltip("Weights asset for Emily late game (rounds 5+). Ignored by other brain types.")]
    public BasicGAGenome EmilyLateWeights;
   
    [Tooltip("Search depth for MinMax brain. Higher = stronger but slower. Ignored by other brain types.")]
    [Range(1, 15)] 
    public int MinMaxDepth = 3;

    [Tooltip("Time limit in milliseconds for MinMax brain. Ignored by other brain types.")]
    [Range(100, 30000)]
    public float MinMaxTimeLimitMs = 5000f;

    [Tooltip("Evaluator used by MinMax brain.")]
    public MinMaxEvaluatorType MinMaxEvaluator = MinMaxEvaluatorType.ProjectedScore;

    [Tooltip("RelativeDelta: immediate simulated end-of-round score delta multiplier.")]
    public float RelativeImmediateSimulationWeight = 1f;

    [Tooltip("RelativeDelta: terminal score-delta improvement multiplier.")]
    public float RelativeTerminalDeltaWeight = 10000f;

    [Tooltip("RelativeDelta: optimizer expected move contribution multiplier.")]
    public float RelativeExpectedMoveWeight = 0.35f;

    [Tooltip("RelativeDelta: scales immediate simulation by round progress (lower early, higher late).")]
    public bool RelativeUsePhaseAwareImmediateWeight = true;

    [Tooltip("Trained .onnx model asset for the MLAgent brain type. Ignored by other brain types.")]
    public UnityEngine.Object MLAgentModel;
}
