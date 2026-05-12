using System.Collections.Generic;
using Unity.InferenceEngine;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.1 (May 2026)    =^..^=    =^..^=
//                    Last Update 10/05/2026
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// Wrapper that lets a trained ML model behave like a normal AI brain.
public class IkebanaAgentBrain : IPlayerAIBrain
{
    private const string ObservationInputName = "obs_0";
    private const string MaskInputName = "action_masks";
    private const string DiscreteActionOutput = "deterministic_discrete_actions";
    private const string DiscreteActionOutputAlt = "discrete_actions";

    private static readonly string[] ScoreOutputNames =
    {
        "action_scores",
        "discrete_action_scores",
        "discrete_action_logits",
        "logits"
    };

    private Worker _worker;
    private Tensor<float> _observationTensor;
    private Tensor<float> _maskTensor;
    private readonly float[] _observationBuffer;
    private readonly int[] _actionToMove = new int[IkebanaActionCodex.ActionCount];
    private readonly RookieAIBrain _fallbackBrain = new RookieAIBrain();

    public string BrainName => "IkebanaAgent (ML)";

    public IkebanaAgentBrain(Object rawModelAsset)
    {
        _observationBuffer = new float[FeatureExtractor.STRATEGIC_OBSERVATION_SIZE];

        ModelAsset modelAsset = rawModelAsset as ModelAsset;
        if (modelAsset == null)
        {
            Debug.LogError("[IkebanaAgentBrain] Assign a trained .onnx ModelAsset.");
            return;
        }

        Model sentisModel = ModelLoader.Load(modelAsset);
        _worker = new Worker(sentisModel, BackendType.CPU);
        _observationTensor = new Tensor<float>(new TensorShape(1, FeatureExtractor.STRATEGIC_OBSERVATION_SIZE));
        _maskTensor = new Tensor<float>(new TensorShape(1, IkebanaActionCodex.ActionCount));
    }

    public GameMove ChooseMove(GameModel model, List<GameMove> validMoves)
    {
        if (validMoves == null || validMoves.Count == 0)
            return default;

        if (_worker == null)
            return _fallbackBrain.ChooseMove(model, validMoves);

        BuildActionLookup(validMoves);
        BuildObservationTensor(model);
        BuildMaskTensor();

        _worker.SetInput(ObservationInputName, _observationTensor);
        _worker.SetInput(MaskInputName, _maskTensor);
        _worker.Schedule();

        int moveIndex;
        if (TryResolveScoredMove(validMoves.Count, out moveIndex) && moveIndex >= 0)
            return validMoves[moveIndex];

        if (TryResolveDeterministicAction(out moveIndex) && moveIndex >= 0)
            return validMoves[moveIndex];

        return _fallbackBrain.ChooseMove(model, validMoves);
    }

    private void BuildActionLookup(List<GameMove> validMoves)
    {
        for (int i = 0; i < _actionToMove.Length; i++)
            _actionToMove[i] = -1;

        for (int i = 0; i < validMoves.Count; i++)
        {
            int actionIndex;
            if (!IkebanaActionCodex.TryEncode(validMoves[i], out actionIndex))
                continue;

            if (_actionToMove[actionIndex] < 0)
                _actionToMove[actionIndex] = i;
        }
    }

    private void BuildObservationTensor(GameModel model)
    {
        int written = FeatureExtractor.WriteStrategicObservation(model, model.CurrentPlayerIndex, _observationBuffer, 0);
        for (int i = 0; i < written; i++)
            _observationTensor[0, i] = _observationBuffer[i];
    }

    private void BuildMaskTensor()
    {
        for (int i = 0; i < IkebanaActionCodex.ActionCount; i++)
        {
            if (_actionToMove[i] >= 0)
                _maskTensor[0, i] = 1f;
            else
                _maskTensor[0, i] = 0f;
        }
    }

    // Some exported models expose action scores under different output names.
    private bool TryResolveScoredMove(int validMoveCount, out int moveIndex)
    {
        moveIndex = -1;
        Tensor scoreTensor = null;

        for (int i = 0; i < ScoreOutputNames.Length; i++)
        {
            try
            {
                scoreTensor = _worker.PeekOutput(ScoreOutputNames[i]);
                if (scoreTensor != null)
                    break;
            }
            catch
            {
            }
        }

        Tensor<float> floatScores = scoreTensor as Tensor<float>;
        if (floatScores == null)
            return false;

        scoreTensor.CompleteAllPendingOperations();

        float bestScore = float.NegativeInfinity;
        for (int actionIndex = 0; actionIndex < IkebanaActionCodex.ActionCount; actionIndex++)
        {
            int mappedMove = _actionToMove[actionIndex];
            if (mappedMove < 0 || mappedMove >= validMoveCount)
                continue;

            float score = floatScores[0, actionIndex];
            if (score > bestScore)
            {
                bestScore = score;
                moveIndex = mappedMove;
            }
        }

        return moveIndex >= 0;
    }

    // Older exports may only expose the final chosen action.
    private bool TryResolveDeterministicAction(out int moveIndex)
    {
        moveIndex = -1;
        Tensor outputTensor = null;

        try
        {
            outputTensor = _worker.PeekOutput(DiscreteActionOutput);
        }
        catch
        {
            try
            {
                outputTensor = _worker.PeekOutput(DiscreteActionOutputAlt);
            }
            catch
            {
            }
        }

        if (outputTensor == null)
            return false;

        outputTensor.CompleteAllPendingOperations();

        int actionIndex = -1;
        Tensor<int> intTensor = outputTensor as Tensor<int>;
        if (intTensor != null)
        {
            actionIndex = intTensor[0, 0];
        }
        else
        {
            Tensor<float> floatTensor = outputTensor as Tensor<float>;
            if (floatTensor != null)
                actionIndex = Mathf.RoundToInt(floatTensor[0, 0]);
        }

        if (actionIndex < 0 || actionIndex >= _actionToMove.Length)
            return false;

        moveIndex = _actionToMove[actionIndex];
        return moveIndex >= 0;
    }
}
