using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

/// ScriptableObject that fully describes a single AI opponent.
[CreateAssetMenu(fileName = "NewAIOpponent", menuName = "Ikebana/AI Opponent Data")]
public class AIOpponentData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Display name shown in the UI.")]
    public string OpponentName = "Opponent";

    [Tooltip("Portrait sprite")]
    public Sprite Portrait;

    [Header("Brain")]
    [Tooltip("Which AI brain this opponent uses.")]
    public AIBrainType BrainType = AIBrainType.GoodRandom;

    [Header("Optimizer / Greedy Brain Settings")]
    [Tooltip("Genome weights used by the Optimizer brain. Ignored by other brain types.")]
    public BasicGAGenome OptimizerWeights;

    [Header("Emily Brain Settings")]
    [Tooltip("Genome weights used by Emily early game (rounds 1-2).")]
    public BasicGAGenome EmilyEarlyWeights;

    [Tooltip("Genome weights used by Emily mid game (rounds 3-4).")]
    public BasicGAGenome EmilyMidWeights;

    [Tooltip("Genome weights used by Emily late game (rounds 5+).")]
    public BasicGAGenome EmilyLateWeights;

    [Header("MinMax Brain Settings")]
    [Tooltip("Search depth for MinMax. Higher = stronger but slower. Ignored by other brain types.")]
    [Range(1, 15)]
    public int MinMaxDepth = 5;

    [Tooltip("Time limit in milliseconds for MinMax. Ignored by other brain types.")]
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

    [Header("ML Agent Brain Settings")]
    [Tooltip("Trained .onnx model asset for ML-agent brain types. Ignored by other brain types.")]
    public UnityEngine.Object NeuralNetwork;

    // HELPERS

    public PlayerSlotConfig ToPlayerSlotConfig()
    {
        return new PlayerSlotConfig
        {
            PlayerName        = OpponentName,
            Portrait          = Portrait,
            PlayerType        = PlayerType.AI,
            AIBrain           = BrainType,
            OptimizerWeights  = OptimizerWeights,
            EmilyEarlyWeights = EmilyEarlyWeights,
            EmilyMidWeights   = EmilyMidWeights,
            EmilyLateWeights  = EmilyLateWeights,
            MinMaxDepth       = MinMaxDepth,
            MinMaxTimeLimitMs = MinMaxTimeLimitMs,
            MinMaxEvaluator   = MinMaxEvaluator,
            RelativeImmediateSimulationWeight = RelativeImmediateSimulationWeight,
            RelativeTerminalDeltaWeight = RelativeTerminalDeltaWeight,
            RelativeExpectedMoveWeight = RelativeExpectedMoveWeight,
            RelativeUsePhaseAwareImmediateWeight = RelativeUsePhaseAwareImmediateWeight,
            MLAgentModel      = NeuralNetwork,
        };
    }
}
