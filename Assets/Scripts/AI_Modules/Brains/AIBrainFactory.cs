using UnityEngine;
using UnityEngine;




//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// Factory that creates IPlayerAIBrain instances
// see AIBrainType enum
public static class AIBrainFactory
{
    public static IMinimalAIBrain CreateMinimalBrain(
        AIBrainType brainType,
        int minMaxDepth = 3,
        float minMaxTimeLimitMs = 5000f,
        MinMaxEvaluatorType minMaxEvaluator = MinMaxEvaluatorType.ProjectedScore,
        BasicGAGenome optimizerWeights = null,
        float relativeImmediateSimulationWeight = 1f,
        float relativeTerminalDeltaWeight = 10000f,
        float relativeExpectedMoveWeight = 0.35f,
        bool relativeUsePhaseAwareImmediateWeight = true)
    {
        switch (brainType)
        {
            case AIBrainType.Random:
                return new MinimalRandomBrain();

            case AIBrainType.GoodRandom:
                return new MinGoodRandomBrain();

            case AIBrainType.Rookie:
                return new MinRookieBrain();

            case AIBrainType.Optimizer:
            case AIBrainType.Friendly:
            case AIBrainType.Emily:
                return optimizerWeights != null
                    ? new MinOptimizerBrain(optimizerWeights)
                    : new MinOptimizerBrain();

            case AIBrainType.MinMax:
            {
                var brain = new MinMaxBrain(minMaxDepth, minMaxTimeLimitMs);
                switch (minMaxEvaluator)
                {
                    case MinMaxEvaluatorType.RelativeDelta:
                        brain.SetEvaluator(new RelativeDeltaEvaluator(
                            immediateSimulationWeight: relativeImmediateSimulationWeight,
                            terminalDeltaWeight: relativeTerminalDeltaWeight,
                            expectedMoveWeight: relativeExpectedMoveWeight,
                            usePhaseAwareImmediateWeight: relativeUsePhaseAwareImmediateWeight));
                        break;
                    case MinMaxEvaluatorType.Optimizer:
                        brain.SetEvaluator(optimizerWeights != null
                            ? new OptimizerEvaluator(optimizerWeights)
                            : new OptimizerEvaluator());
                        break;
                    case MinMaxEvaluatorType.ProjectedScore:
                    default:
                        break;
                }
                return brain;
            }

            case AIBrainType.MinMaxOptimizer:
            {
                var brain = new MinMaxBrain(minMaxDepth, minMaxTimeLimitMs);
                brain.SetEvaluator(optimizerWeights != null
                    ? new OptimizerEvaluator(optimizerWeights)
                    : new OptimizerEvaluator());
                return brain;
            }

            default:
                Debug.LogWarning("Minimal simulator does not support " + brainType + "; using Rookie minimal brain.");
                return new MinRookieBrain();
        }
    }

    public static IPlayerAIBrain CreateBrain(
        AIBrainType brainType, 
        int minMaxDepth = 3,
        float minMaxTimeLimitMs = 5000f,
        MinMaxEvaluatorType minMaxEvaluator = MinMaxEvaluatorType.ProjectedScore,
        BasicGAGenome optimizerWeights = null,
        UnityEngine.Object mlAgentModel = null,
        int numPlayers = 2,
        BasicGAGenome emilyEarlyWeights = null,
        BasicGAGenome emilyMidWeights = null,
        BasicGAGenome emilyLateWeights = null,
        float relativeImmediateSimulationWeight = 1f,
        float relativeTerminalDeltaWeight = 10000f,
        float relativeExpectedMoveWeight = 0.35f,
        bool relativeUsePhaseAwareImmediateWeight = true,
        MLValueSearchPolicy mlValueSearchPolicy = MLValueSearchPolicy.MCTS,
        int mlValueMctsIterations = 128,
        int mlValueMctsNewRoundSamples = 4)
    {
        switch (brainType)
        {
            case AIBrainType.Random:
                return new RandomAIBrain();
            
            case AIBrainType.GoodRandom:
                return new GoodRandomAIBrain();

            case AIBrainType.Rookie:
                return new RookieAIBrain();

            case AIBrainType.Friendly:
                return optimizerWeights != null
                    ? new FriendlyAIBrain(optimizerWeights)
                    : new FriendlyAIBrain();

            case AIBrainType.MinMax:
            {
                var brain = new MinMaxBrain(minMaxDepth, minMaxTimeLimitMs);
                switch (minMaxEvaluator)
                {
                    case MinMaxEvaluatorType.RelativeDelta:
                        brain.SetEvaluator(new RelativeDeltaEvaluator(
                            immediateSimulationWeight: relativeImmediateSimulationWeight,
                            terminalDeltaWeight: relativeTerminalDeltaWeight,
                            expectedMoveWeight: relativeExpectedMoveWeight,
                            usePhaseAwareImmediateWeight: relativeUsePhaseAwareImmediateWeight));
                        break;
                    case MinMaxEvaluatorType.Optimizer:
                        brain.SetEvaluator(optimizerWeights != null
                            ? new OptimizerEvaluator(optimizerWeights)
                            : new OptimizerEvaluator());
                        break;
                    case MinMaxEvaluatorType.ProjectedScore:
                    default:
                        break;
                }
                return brain;
            }

            case AIBrainType.MinMaxOptimizer:
            {
                var brain = new MinMaxBrain(minMaxDepth, minMaxTimeLimitMs);
                var evaluator = optimizerWeights != null
                    ? new OptimizerEvaluator(optimizerWeights)
                    : new OptimizerEvaluator();
                brain.SetEvaluator(evaluator);
                return brain;
            }

            case AIBrainType.Optimizer:
                if (optimizerWeights != null)
                    return new OptimizerAIBrain(optimizerWeights);
                Debug.LogWarning("Optimizer needs a BasicGAGenome");
                return new OptimizerAIBrain();

            case AIBrainType.Emily:
                return new EmilyAIBrain(emilyEarlyWeights, emilyMidWeights, emilyLateWeights);

            case AIBrainType.IkebanaAgent:
            {
                UnityEngine.Object model = mlAgentModel != null
                    ? mlAgentModel
                    : Resources.Load<UnityEngine.Object>("IkebanaAgent");

                if (model != null)
                    return new IkebanaAgentBrain(model);

                Debug.LogWarning("IkebanaAgent brain requires a ModelAsset (.onnx). Falling back to Rookie.");
                return new RookieAIBrain();
            }

            case AIBrainType.MLMCTSValue:
            {
                UnityEngine.Object model = mlAgentModel != null
                    ? mlAgentModel
                    : Resources.Load<UnityEngine.Object>("MLMCTSAgent");

                if (model != null)
                {
                    return new MLMCTSBrain(
                        model,
                        mlValueSearchPolicy,
                        minMaxDepth,
                        mlValueMctsIterations,
                        mlValueMctsNewRoundSamples);
                }

                Debug.LogWarning("MLMCTSValue brain requires a ModelAsset (.onnx). Falling back to Rookie.");
                return new RookieAIBrain();
            }

            //case AIBrainType.IkebanaAgentV2:
            //    if (mlAgentModel != null)
            //    {
            //        return new IkebanaAgentV2Brain(mlAgentModel);
            //    }
            //    Debug.LogWarning("IkebanaAgentV2 brain requires a ModelAsset (.onnx)");
            //    return new RookieAIBrain();

            default:
                Debug.LogWarning("Using Random by default");
                return new GoodRandomAIBrain();
        }
    }
}
