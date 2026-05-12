using System;
using System.Collections.Generic;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

public enum MLValueSearchPolicy
{
    MinMax,
    MCTS
}

[RequireComponent(typeof(BehaviorParameters))]
public class MLMCTSAgent : Agent
{
    private const int NumPlayers = 2;
    private const int MaxMoves = 200;

    [Header("Game Settings")]
    [SerializeField] private int randomSeed = 0;

    [Header("Value Search")]
    [SerializeField] private MLValueSearchPolicy searchPolicy = MLValueSearchPolicy.MCTS;
    [SerializeField] [Range(1, 20)] private int searchDepth = 6;
    [SerializeField] [Range(1, 2048)] private int mctsIterations = 128;
    [SerializeField] [Range(1, 16)] private int mctsNewRoundSamples = 4;

    [Header("Training Rewards")]
    [SerializeField] private float scoreDeltaNorm = 100f;
    [SerializeField] private float valueErrorPenalty = 0.25f;
    [SerializeField] private float closeValueReward = 0.02f;
    [SerializeField] private float rewardWin = 1f;

    public const int ObservationSize = FeatureExtractor.STRATEGIC_OBSERVATION_SIZE;

    private MinimalGM _model;
    private GameConfigSnapshot _config;
    private GameState _snapshot;
    private readonly GameMove[] _moveBuffer = new GameMove[MaxMoves];
    private readonly MinimalMoveRecord[] _rootMoveBuffer = new MinimalMoveRecord[MinimalGM.MAX_MINMAX_MOVES];
    private float[] _observationBuffer;
    private int _playerIndex;
    private MinRookieBrain _opponentBrain;
    private MLValueSearchEngine _searchEngine;

    public override void Initialize()
    {
        _model = new MinimalGM(NumPlayers, randomSeed);
        _config = _model.Config;
        _snapshot = GameState.Create(NumPlayers, _config.NumFactories, _config.flowersPerFactory);
        _observationBuffer = new float[ObservationSize];
        _opponentBrain = new MinRookieBrain();
        _searchEngine = new MLValueSearchEngine(searchDepth, mctsIterations, mctsNewRoundSamples);
    }

    public override void OnEpisodeBegin()
    {
        _model.ResetForNewGame();
        _model.SimStartRound();
        _playerIndex = UnityEngine.Random.Range(0, _model.NumPlayers);

        if (_model.CurrentPlayer != _playerIndex)
            PlayOpponentTurns();

        RequestDecisionIfReady();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        _model.FillSnapshot(ref _snapshot);
        FeatureExtractor.WriteStrategicObservation(_model, _snapshot, _playerIndex, _observationBuffer, 0);

        for (int i = 0; i < _observationBuffer.Length; i++)
            sensor.AddObservation(_observationBuffer[i]);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_model == null || _model.IsGameOver)
            return;

        if (_model.CurrentPlayer != _playerIndex)
        {
            PlayOpponentTurns();
            RequestDecisionIfReady();
            return;
        }

        int moveCount = _model.GetValidMoves(_moveBuffer);
        int rootMoveCount = _model.GetValidMovesMinMax(_rootMoveBuffer);
        if (moveCount <= 0 || rootMoveCount <= 0)
        {
            FinishIfGameOver();
            return;
        }

        float predictedValue = 0f;
        if (actions.ContinuousActions.Length > 0)
            predictedValue = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);

        MLValueSearchResult searchResult = _searchEngine.FindBestMove(
            _model,
            _playerIndex,
            _rootMoveBuffer,
            rootMoveCount,
            searchPolicy,
            EvaluateScoreDeltaNormalized);

        float targetValue = Mathf.Clamp(searchResult.Value, -1f, 1f);
        float error = Mathf.Abs(predictedValue - targetValue);
        AddReward(-error * valueErrorPenalty);
        if (error <= 0.10f)
            AddReward(closeValueReward);

        int chosenIndex = searchResult.BestMoveIndex;
        if (chosenIndex < 0 || chosenIndex >= moveCount)
            chosenIndex = _opponentBrain.ChooseMoveIndex(_model, _moveBuffer, moveCount);

        _model.ExecuteMove(_moveBuffer[chosenIndex]);
        _model.SimEndTurn();

        PlayOpponentTurns();
        FinishIfGameOver();
        RequestDecisionIfReady();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        if (continuousActions.Length > 0)
            continuousActions[0] = EvaluateScoreDeltaNormalized(_model, _playerIndex);
    }

    private float EvaluateScoreDeltaNormalized(MinimalGM model, int evaluatingPlayer)
    {
        return Mathf.Clamp(MLValueSearchEngine.ComputeScoreDelta(model, evaluatingPlayer) / Mathf.Max(1f, scoreDeltaNorm), -1f, 1f);
    }

    private void PlayOpponentTurns()
    {
        int safety = 256;
        while (!_model.IsGameOver && _model.CurrentPlayer != _playerIndex && safety-- > 0)
        {
            int moveCount = _model.GetValidMoves(_moveBuffer);
            if (moveCount <= 0)
                break;

            int moveIndex = _opponentBrain.ChooseMoveIndex(_model, _moveBuffer, moveCount);
            _model.ExecuteMove(_moveBuffer[moveIndex]);
            _model.SimEndTurn();
        }
    }

    private void RequestDecisionIfReady()
    {
        if (_model != null && !_model.IsGameOver && _model.CurrentPlayer == _playerIndex)
            RequestDecision();
    }

    private void FinishIfGameOver()
    {
        if (!_model.IsGameOver)
            return;

        bool won = MLValueSearchEngine.IsWinningPlayer(_model, _playerIndex);
        AddReward(won ? rewardWin : -rewardWin);
        EndEpisode();
    }
}

public sealed class MLMCTSBrain : IPlayerAIBrain
{
    private const string ObservationInputName = "obs_0";

    private static readonly string[] ValueOutputNames =
    {
        "continuous_actions",
        "deterministic_continuous_actions",
        "action",
        "output_0"
    };

    private readonly float[] _observationBuffer = new float[FeatureExtractor.STRATEGIC_OBSERVATION_SIZE];
    private readonly MinimalMoveRecord[] _rootMoveBuffer = new MinimalMoveRecord[MinimalGM.MAX_MINMAX_MOVES];
    private readonly RookieAIBrain _fallbackBrain = new RookieAIBrain();
    private readonly MLValueSearchEngine _searchEngine;
    private readonly MLValueSearchPolicy _searchPolicy;

    private Worker _worker;
    private Tensor<float> _observationTensor;
    private MinimalGM _rootModel;
    private GameState _valueSnapshot;
    private bool _hasSnapshot;

    public string BrainName { get { return "MLValue " + _searchPolicy; } }

    public MLMCTSBrain(
        UnityEngine.Object rawModelAsset,
        MLValueSearchPolicy searchPolicy = MLValueSearchPolicy.MCTS,
        int searchDepth = 6,
        int mctsIterations = 128,
        int mctsNewRoundSamples = 4)
    {
        _searchPolicy = searchPolicy;
        _searchEngine = new MLValueSearchEngine(searchDepth, mctsIterations, mctsNewRoundSamples);

        ModelAsset modelAsset = rawModelAsset as ModelAsset;
        if (modelAsset == null)
        {
            Debug.LogError("[MLMCTSBrain] Assign a trained .onnx ModelAsset.");
            return;
        }

        Model sentisModel = ModelLoader.Load(modelAsset);
        _worker = new Worker(sentisModel, BackendType.CPU);
        _observationTensor = new Tensor<float>(new TensorShape(1, FeatureExtractor.STRATEGIC_OBSERVATION_SIZE));
    }

    public GameMove ChooseMove(GameModel model, List<GameMove> validMoves)
    {
        if (validMoves == null || validMoves.Count == 0)
            return default(GameMove);

        if (validMoves.Count == 1 || _worker == null)
            return _fallbackBrain.ChooseMove(model, validMoves);

        _rootModel = GameModelToMinimal.Convert(model, _rootModel);
        int rootMoveCount = _rootModel.GetValidMovesMinMax(_rootMoveBuffer);
        if (rootMoveCount <= 0)
            return _fallbackBrain.ChooseMove(model, validMoves);

        EnsureSnapshot(_rootModel);
        MLValueSearchResult result = _searchEngine.FindBestMove(
            _rootModel,
            _rootModel.CurrentPlayer,
            _rootMoveBuffer,
            rootMoveCount,
            _searchPolicy,
            EvaluateWithModel);

        if (result.BestMoveIndex < 0 || result.BestMoveIndex >= rootMoveCount)
            return _fallbackBrain.ChooseMove(model, validMoves);

        return MapMinimalMoveToGameMove(_rootMoveBuffer[result.BestMoveIndex], validMoves);
    }

    private void EnsureSnapshot(MinimalGM model)
    {
        if (_hasSnapshot)
            return;

        _valueSnapshot = GameState.Create(model.NumPlayers, model.Config.NumFactories, model.Config.flowersPerFactory);
        _hasSnapshot = true;
    }

    private float EvaluateWithModel(MinimalGM model, int evaluatingPlayer)
    {
        EnsureSnapshot(model);
        model.FillSnapshot(ref _valueSnapshot);
        int written = FeatureExtractor.WriteStrategicObservation(model, _valueSnapshot, evaluatingPlayer, _observationBuffer, 0);
        for (int i = 0; i < written; i++)
            _observationTensor[0, i] = _observationBuffer[i];

        _worker.SetInput(ObservationInputName, _observationTensor);
        _worker.Schedule();

        return ReadValueOutput();
    }

    private float ReadValueOutput()
    {
        Tensor outputTensor = null;
        for (int i = 0; i < ValueOutputNames.Length; i++)
        {
            try
            {
                outputTensor = _worker.PeekOutput(ValueOutputNames[i]);
                if (outputTensor != null)
                    break;
            }
            catch
            {
            }
        }

        Tensor<float> floatTensor = outputTensor as Tensor<float>;
        if (floatTensor == null)
            return 0f;

        outputTensor.CompleteAllPendingOperations();
        return Mathf.Clamp(floatTensor[0, 0], -1f, 1f);
    }

    private static GameMove MapMinimalMoveToGameMove(MinimalMoveRecord moveRecord, List<GameMove> validMoves)
    {
        FlowerColor expectedColor = (FlowerColor)moveRecord.ColorIndex;
        MoveSource expectedSource = moveRecord.IsCentralSource ? MoveSource.Central : MoveSource.Factory;
        MoveTarget expectedTarget = moveRecord.TargetIsPenalty ? MoveTarget.PenaltyLine : MoveTarget.PlacementLine;

        for (int i = 0; i < validMoves.Count; i++)
        {
            GameMove candidateMove = validMoves[i];
            if (candidateMove.Color != expectedColor) continue;
            if (candidateMove.SourceEnum != expectedSource) continue;
            if (candidateMove.TargetEnum != expectedTarget) continue;
            if (!candidateMove.IsPenalty && candidateMove.TargetLineIndex != moveRecord.TargetLineIndex) continue;
            if (expectedSource == MoveSource.Factory && candidateMove.FactoryIndex != moveRecord.FactoryIndex) continue;
            return candidateMove;
        }

        return validMoves[0];
    }
}

public delegate float MLValueLeafEvaluator(MinimalGM model, int evaluatingPlayer);

public struct MLValueSearchResult
{
    public int BestMoveIndex;
    public float Value;
}

public sealed class MLValueSearchEngine
{
    private readonly MinimalMoveRecord[] _moveBuffer = new MinimalMoveRecord[MinimalGM.MAX_MINMAX_MOVES];
    private readonly MinimalMoveRecord[] _rolloutMoveBuffer = new MinimalMoveRecord[MinimalGM.MAX_MINMAX_MOVES];
    private readonly System.Random _rng = new System.Random();

    private int _maxDepth;
    private int _mctsIterations;
    private int _mctsNewRoundSamples;

    public MLValueSearchEngine(int maxDepth, int mctsIterations, int mctsNewRoundSamples)
    {
        _maxDepth = Math.Max(1, maxDepth);
        _mctsIterations = Math.Max(1, mctsIterations);
        _mctsNewRoundSamples = Math.Max(1, mctsNewRoundSamples);
    }

    public MLValueSearchResult FindBestMove(
        MinimalGM model,
        int evaluatingPlayer,
        MinimalMoveRecord[] rootMoves,
        int rootMoveCount,
        MLValueSearchPolicy policy,
        MLValueLeafEvaluator leafEvaluator)
    {
        if (policy == MLValueSearchPolicy.MinMax)
            return FindBestMoveMinMax(model, evaluatingPlayer, rootMoves, rootMoveCount, leafEvaluator);

        return FindBestMoveMCTS(model, evaluatingPlayer, rootMoves, rootMoveCount, leafEvaluator);
    }

    private MLValueSearchResult FindBestMoveMinMax(
        MinimalGM model,
        int evaluatingPlayer,
        MinimalMoveRecord[] rootMoves,
        int rootMoveCount,
        MLValueLeafEvaluator leafEvaluator)
    {
        MLValueSearchResult result = new MLValueSearchResult { BestMoveIndex = -1, Value = float.NegativeInfinity };
        GameState rootState = model.SaveSnapshot();

        for (int i = 0; i < rootMoveCount; i++)
        {
            model.SetGameState(rootState);
            MinimalMoveRecord move = rootMoves[i];
            bool stopAtEndRound;
            ApplyMoveForMinMax(model, ref move, out stopAtEndRound);

            float value = stopAtEndRound || model.IsGameOver || _maxDepth <= 1
                ? leafEvaluator(model, evaluatingPlayer)
                : AlphaBeta(model, evaluatingPlayer, _maxDepth - 1, float.NegativeInfinity, float.PositiveInfinity, leafEvaluator);

            if (value > result.Value)
            {
                result.Value = value;
                result.BestMoveIndex = i;
            }
        }

        model.SetGameState(rootState);
        return result;
    }

    private float AlphaBeta(
        MinimalGM model,
        int evaluatingPlayer,
        int depth,
        float alpha,
        float beta,
        MLValueLeafEvaluator leafEvaluator)
    {
        if (depth <= 0 || model.IsGameOver || model.AreDisplaysEmpty())
            return leafEvaluator(model, evaluatingPlayer);

        int moveCount = model.GetValidMovesMinMax(_moveBuffer);
        if (moveCount <= 0)
            return leafEvaluator(model, evaluatingPlayer);

        bool maximizing = model.CurrentPlayer == evaluatingPlayer;
        float bestValue = maximizing ? float.NegativeInfinity : float.PositiveInfinity;
        GameState state = model.SaveSnapshot();

        for (int i = 0; i < moveCount; i++)
        {
            model.SetGameState(state);
            MinimalMoveRecord move = _moveBuffer[i];
            bool stopAtEndRound;
            ApplyMoveForMinMax(model, ref move, out stopAtEndRound);

            float value = stopAtEndRound || model.IsGameOver
                ? leafEvaluator(model, evaluatingPlayer)
                : AlphaBeta(model, evaluatingPlayer, depth - 1, alpha, beta, leafEvaluator);

            if (maximizing)
            {
                if (value > bestValue) bestValue = value;
                if (value > alpha) alpha = value;
            }
            else
            {
                if (value < bestValue) bestValue = value;
                if (value < beta) beta = value;
            }

            if (beta <= alpha)
                break;
        }

        model.SetGameState(state);
        return bestValue;
    }

    private MLValueSearchResult FindBestMoveMCTS(
        MinimalGM model,
        int evaluatingPlayer,
        MinimalMoveRecord[] rootMoves,
        int rootMoveCount,
        MLValueLeafEvaluator leafEvaluator)
    {
        MLValueSearchResult result = new MLValueSearchResult { BestMoveIndex = -1, Value = float.NegativeInfinity };
        GameState rootState = model.SaveSnapshot();
        int samplesPerMove = Math.Max(1, _mctsIterations) * Math.Max(1, _mctsNewRoundSamples);

        for (int i = 0; i < rootMoveCount; i++)
        {
            float total = 0f;
            for (int sample = 0; sample < samplesPerMove; sample++)
            {
                model.SetGameState(rootState);
                MinimalMoveRecord move = rootMoves[i];
                ApplyMoveForMCTS(model, ref move);
                total += RunRandomRollout(model, evaluatingPlayer, 1, leafEvaluator);
            }

            float average = total / samplesPerMove;
            if (average > result.Value)
            {
                result.Value = average;
                result.BestMoveIndex = i;
            }
        }

        model.SetGameState(rootState);
        return result;
    }

    private float RunRandomRollout(MinimalGM model, int evaluatingPlayer, int depth, MLValueLeafEvaluator leafEvaluator)
    {
        while (!model.IsGameOver && depth < _maxDepth)
        {
            int moveCount = model.GetValidMovesMinMax(_rolloutMoveBuffer);
            if (moveCount <= 0)
                break;

            int moveIndex = _rng.Next(moveCount);
            MinimalMoveRecord move = _rolloutMoveBuffer[moveIndex];
            ApplyMoveForMCTS(model, ref move);
            depth++;
        }

        return leafEvaluator(model, evaluatingPlayer);
    }

    private static void ApplyMoveForMinMax(MinimalGM model, ref MinimalMoveRecord move, out bool stopAtEndRound)
    {
        model.ExecuteMoveMinMax(ref move);
        stopAtEndRound = model.AreDisplaysEmpty();
        if (stopAtEndRound)
        {
            bool gameEnding = model.ScoreEndOfRoundMinMax();
            if (gameEnding)
                model.SimEndGame();
        }
        else
        {
            model.AdvanceTurnMinMax();
        }
    }

    private static void ApplyMoveForMCTS(MinimalGM model, ref MinimalMoveRecord move)
    {
        model.ExecuteMoveMinMax(ref move);
        if (model.AreDisplaysEmpty())
            model.SimEndRound();
        else
            model.AdvanceTurnMinMax();
    }

    public static float ComputeScoreDelta(MinimalGM model, int evaluatingPlayer)
    {
        float score = model.Players[evaluatingPlayer].Score;
        float bestOpponent = float.NegativeInfinity;
        for (int p = 0; p < model.NumPlayers; p++)
        {
            if (p == evaluatingPlayer) continue;
            if (model.Players[p].Score > bestOpponent)
                bestOpponent = model.Players[p].Score;
        }

        if (bestOpponent == float.NegativeInfinity)
            bestOpponent = 0f;

        return score - bestOpponent;
    }

    public static bool IsWinningPlayer(MinimalGM model, int playerIndex)
    {
        int score = model.Players[playerIndex].Score;
        for (int p = 0; p < model.NumPlayers; p++)
        {
            if (p == playerIndex) continue;
            if (model.Players[p].Score >= score)
                return false;
        }

        return true;
    }
}
