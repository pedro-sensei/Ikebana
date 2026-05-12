using Unity.MLAgents;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using System.Collections.Generic;

//=^..^=   =^..^=   VERSION 1.1.1 (May 2026)    =^..^=    =^..^=
//                    Last Update 10/05/2026
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// Main ML-Agents player used for training and self-play.
// It can play against scripted opponents or share one game with another agent.
[RequireComponent(typeof(BehaviorParameters))]
public class IkebanaAgent : Agent
{
    private const int NumPlayers = 2;
    private const int MaxMoves = 200;

    private static readonly List<IkebanaAgent> RegisteredAgents = new List<IkebanaAgent>(NumPlayers);
    private static readonly Dictionary<IkebanaAgent, bool> SelfPlayReady = new Dictionary<IkebanaAgent, bool>(NumPlayers);
    private static readonly Dictionary<IkebanaAgent, int> SelfPlayAssignments = new Dictionary<IkebanaAgent, int>(NumPlayers);

    private static MinimalGM SharedModel;
    private static bool SharedEpisodeActive;
    private static bool SharedEpisodeEnding;

    [Header("Game Settings")]
    [SerializeField] private int randomSeed = 0;

    [Header("Reward Tuning")]
    [SerializeField] private float rewardWin = 1.0f;
    [SerializeField] private float rewardPerTilePlaced = 0.05f;
    [SerializeField] private float penaltyPerFloorTile = -0.2f;
    [SerializeField] private float rewardPerScorePoint = 0.02f;

    [Header("Opponent Curriculum")]
    [SerializeField] private BasicGAGenome friendlyGenome;
    [SerializeField] private BasicGAGenome optimizerGenome;
    [SerializeField] private BasicGAGenome emilyEarlyGenome;
    [SerializeField] private BasicGAGenome emilyMidGenome;
    [SerializeField] private BasicGAGenome emilyLateGenome;
    [SerializeField] private int winRateWindowSize = 400;

    public const int ObservationSize = FeatureExtractor.STRATEGIC_OBSERVATION_SIZE;

    private MinimalGM _model;
    private GameConfigSnapshot _config;
    private GameState _snapshot;
    private readonly GameMove[] _moveBuffer = new GameMove[MaxMoves];
    private readonly int[] _actionToMove = new int[IkebanaActionCodex.ActionCount];
    private float[] _observationBuffer;
    private int _moveCount;
    private int _playerIndex;

    private MinGoodRandomBrain _brainRandom;
    private MinRookieBrain _brainRookie;
    private MinFriendlyBrain _brainFriendly;
    private MinOptimizerBrain _brainOptimizer;
    private MinEmilyBrain _brainEmily;
    private MinHybridEliteBrain _brainHybridElite;

    private float _difficulty;
    private float _selfplayPercentage;
    private float _threshGoodRandom;
    private float _threshRookie;
    private float _threshOptimizer;
    private bool _useSelfPlayThisEpisode;

    private int[] _winHistory;
    private int _winIdx;
    private int _winCount;
    private int _winSum;

    private BehaviorParameters _behaviorParameters;
    private int _teamId;

    public override void Initialize()
    {
        _behaviorParameters = GetComponent<BehaviorParameters>();
        _teamId = 0;
        if (_behaviorParameters != null)
            _teamId = _behaviorParameters.TeamId;

        RegisterAgent(this);

        _model = new MinimalGM(NumPlayers, randomSeed);
        _config = _model.Config;
        _snapshot = GameState.Create(NumPlayers, _config.NumFactories, _config.flowersPerFactory);
        _observationBuffer = new float[ObservationSize];
        _playerIndex = Random.Range(0, _model.NumPlayers);

        _brainRandom = new MinGoodRandomBrain();
        _brainRookie = new MinRookieBrain();

        if (friendlyGenome != null)
            _brainFriendly = new MinFriendlyBrain(friendlyGenome, _config);
        else
            _brainFriendly = new MinFriendlyBrain();

        if (optimizerGenome != null)
            _brainOptimizer = new MinOptimizerBrain(optimizerGenome);
        else
            _brainOptimizer = new MinOptimizerBrain();

        _brainEmily = new MinEmilyBrain(emilyEarlyGenome, emilyMidGenome, emilyLateGenome, _config);
        _brainHybridElite = new MinHybridEliteBrain(_brainFriendly, _brainOptimizer, _brainEmily);

        _winHistory = new int[winRateWindowSize];
        UpdateCurriculumThresholds();
    }

    private void OnDestroy()
    {
        UnregisterAgent(this);
    }

    public override void OnEpisodeBegin()
    {
        _difficulty = Academy.Instance.EnvironmentParameters.GetWithDefault("opponent_difficulty", 0f);
        _selfplayPercentage = Academy.Instance.EnvironmentParameters.GetWithDefault("selfplay_percentage", 0f);
        UpdateCurriculumThresholds();

        _useSelfPlayThisEpisode = CanUseSharedSelfPlay() && Random.value < _selfplayPercentage;

        if (UseSelfPlay())
        {
            BeginSharedEpisode();
            return;
        }

        _model.ResetForNewGame();
        _playerIndex = Random.Range(0, _model.NumPlayers);
        _model.SimStartRound();
        _moveCount = 0;
        if (!UseSelfPlay() && _model.CurrentPlayer != _playerIndex)
            PlayOpponentTurns();

        RequestDecisionIfReady();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (UseSelfPlay())
        {
            int selfPlayIndex;
            if (!TryGetSelfPlayPlayerIndex(this, out selfPlayIndex) || SharedModel == null)
            {
                for (int i = 0; i < ObservationSize; i++)
                    sensor.AddObservation(0f);
                return;
            }

            SharedModel.FillSnapshot(ref _snapshot);
            FeatureExtractor.WriteStrategicObservation(SharedModel, _snapshot, selfPlayIndex, _observationBuffer, 0);
        }
        else
        {
            _model.FillSnapshot(ref _snapshot);
            FeatureExtractor.WriteStrategicObservation(_model, _snapshot, _playerIndex, _observationBuffer, 0);
        }

        for (int i = 0; i < _observationBuffer.Length; i++)
            sensor.AddObservation(_observationBuffer[i]);
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (UseSelfPlay())
        {
            int selfPlayIndex;
            if (SharedModel == null || !TryGetSelfPlayPlayerIndex(this, out selfPlayIndex) || SharedModel.CurrentPlayer != selfPlayIndex)
            {
                for (int i = 0; i < IkebanaActionCodex.ActionCount; i++)
                    actionMask.SetActionEnabled(0, i, false);
                return;
            }

            RefreshLegalMoves(SharedModel);
            for (int i = 0; i < IkebanaActionCodex.ActionCount; i++)
                actionMask.SetActionEnabled(0, i, _actionToMove[i] >= 0);
            return;
        }

        RefreshLegalMoves();

        for (int i = 0; i < IkebanaActionCodex.ActionCount; i++)
            actionMask.SetActionEnabled(0, i, _actionToMove[i] >= 0);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (UseSelfPlay())
        {
            OnSharedActionReceived(actions);
            return;
        }

        RefreshLegalMoves();

        if (_model.CurrentPlayer != _playerIndex)
        {
            if (!UseSelfPlay())
                PlayOpponentTurns();

            if (_model.IsGameOver)
            {
                bool won = CheckWinCondition();
                AddMatchResultReward(won);
                RecordWinLoss(won);
                EndEpisode();
                return;
            }

            RequestDecisionIfReady();
            return;
        }

        if (_moveCount == 0)
        {
            if (_model.IsGameOver)
            {
                bool won = CheckWinCondition();
                AddMatchResultReward(won);
                RecordWinLoss(won);
                EndEpisode();
            }
            return;
        }

        int actionIndex = actions.DiscreteActions[0];
        int moveIndex = -1;
        if (actionIndex >= 0 && actionIndex < _actionToMove.Length)
            moveIndex = _actionToMove[actionIndex];

        if (moveIndex < 0 || moveIndex >= _moveCount)
            moveIndex = _brainRookie.ChooseMoveIndex(_model, _moveBuffer, _moveCount);

        int previousScore = _model.Players[_playerIndex].Score;
        GameMove move = _moveBuffer[moveIndex];
        ApplyImmediateReward(move);

        _model.ExecuteMove(move);
        _model.SimEndTurn();

        if (!UseSelfPlay())
            PlayOpponentTurns();

        int scoreDelta = _model.Players[_playerIndex].Score - previousScore;
        if (scoreDelta != 0)
            AddReward(scoreDelta * rewardPerScorePoint);

        if (_model.IsGameOver)
        {
            bool won = CheckWinCondition();
            AddMatchResultReward(won);
            RecordWinLoss(won);
            EndEpisode();
            return;
        }

        RequestDecisionIfReady();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (UseSelfPlay() && SharedModel != null)
            RefreshLegalMoves(SharedModel);
        else
            RefreshLegalMoves();

        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = 0;

        if (_moveCount == 0)
            return;

        int moveIndex = _brainRookie.ChooseMoveIndex(_model, _moveBuffer, _moveCount);
        int actionIndex;
        if (IkebanaActionCodex.TryEncode(_moveBuffer[moveIndex], out actionIndex))
            discreteActions[0] = actionIndex;
    }

    private void ApplyImmediateReward(GameMove move)
    {
        if (!move.IsPenalty)
        {
            MinimalPlayer player = _model.Players[_playerIndex];
            int lineCapacity = player.GetLineCapacity(move.TargetLineIndex);
            int currentCount = player.GetLineCount(move.TargetLineIndex);
            int space = lineCapacity - currentCount;
            int placed = System.Math.Min(move.flowerCount, space);
            int overflow = move.flowerCount - placed;

            AddReward(placed * rewardPerTilePlaced);
            AddReward(overflow * penaltyPerFloorTile);
        }
        else
        {
            AddReward(move.flowerCount * penaltyPerFloorTile);
        }
    }

    private void OnSharedActionReceived(ActionBuffers actions)
    {
        int selfPlayIndex;
        if (SharedModel == null || !TryGetSelfPlayPlayerIndex(this, out selfPlayIndex))
            return;

        RefreshLegalMoves(SharedModel);

        if (SharedModel.CurrentPlayer != selfPlayIndex)
        {
            RequestDecisionIfReady();
            return;
        }

        if (_moveCount == 0)
        {
            if (SharedModel.IsGameOver)
                FinishSharedEpisode();
            return;
        }

        int actionIndex = actions.DiscreteActions[0];
        int moveIndex = -1;
        if (actionIndex >= 0 && actionIndex < _actionToMove.Length)
            moveIndex = _actionToMove[actionIndex];

        if (moveIndex < 0 || moveIndex >= _moveCount)
            moveIndex = _brainRookie.ChooseMoveIndex(SharedModel, _moveBuffer, _moveCount);

        int[] previousScores = new int[NumPlayers];
        for (int i = 0; i < NumPlayers; i++)
            previousScores[i] = SharedModel.Players[i].Score;

        GameMove move = _moveBuffer[moveIndex];
        ApplyImmediateReward(move);

        SharedModel.ExecuteMove(move);
        SharedModel.SimEndTurn();

        foreach (IkebanaAgent agent in GetOrderedSelfPlayAgents())
        {
            int assignedIndex;
            if (!TryGetSelfPlayPlayerIndex(agent, out assignedIndex))
                continue;

            int scoreDelta = SharedModel.Players[assignedIndex].Score - previousScores[assignedIndex];
            if (scoreDelta != 0)
                agent.AddReward(scoreDelta * agent.rewardPerScorePoint);
        }

        if (SharedModel.IsGameOver)
        {
            FinishSharedEpisode();
            return;
        }

        RequestDecisionIfReady();
    }

    private void RefreshLegalMoves()
    {
        RefreshLegalMoves(_model);
    }

    private void RefreshLegalMoves(MinimalGM model)
    {
        _moveCount = model.GetValidMoves(_moveBuffer);
        IkebanaActionCodex.BuildActionLookup(_moveBuffer, _moveCount, _actionToMove);
    }

    // Runs the scripted opponent until it becomes this agent's turn again.
    private void PlayOpponentTurns()
    {
        int safety = 256;
        while (!UseSelfPlay() && !_model.IsGameOver && _model.CurrentPlayer != _playerIndex && safety-- > 0)
        {
            int moveCount = _model.GetValidMoves(_moveBuffer);
            if (moveCount == 0)
                break;

            IMinimalAIBrain brain = PickOpponentBrain();
            int moveIndex = brain.ChooseMoveIndex(_model, _moveBuffer, moveCount);
            _model.ExecuteMove(_moveBuffer[moveIndex]);
            _model.SimEndTurn();
        }
    }

    private IMinimalAIBrain PickOpponentBrain()
    {
        float roll = Random.value;

        if (roll < _threshGoodRandom)
            return _brainRandom;

        if (roll < _threshRookie)
            return _brainRookie;

        if (roll < _threshOptimizer)
            return _brainOptimizer;

        return _brainHybridElite;
    }

    private bool UseSelfPlay()
    {
        return Academy.Instance.IsCommunicatorOn && _useSelfPlayThisEpisode;
    }

    private void RequestDecisionIfReady()
    {
        if (UseSelfPlay())
        {
            RequestSharedDecisionIfReady();
            return;
        }

        if (_model == null || _model.IsGameOver)
            return;

        if (_model.CurrentPlayer == _playerIndex)
            RequestDecision();
    }

    private void BeginSharedEpisode()
    {
        MarkSelfPlayReady(this, true);

        List<IkebanaAgent> agents = GetOrderedSelfPlayAgents();
        if (agents.Count < NumPlayers)
            return;

        bool allReady = true;
        for (int i = 0; i < NumPlayers; i++)
        {
            bool isReady;
            if (!SelfPlayReady.TryGetValue(agents[i], out isReady) || !isReady)
            {
                allReady = false;
                break;
            }
        }

        if (!allReady)
            return;

        if (SharedModel == null)
        {
            SharedModel = new MinimalGM(NumPlayers, randomSeed);
        }

        SharedModel.ResetForNewGame();
        SharedModel.SimStartRound();

        int offset = Random.Range(0, NumPlayers);
        for (int i = 0; i < NumPlayers; i++)
        {
            SelfPlayAssignments[agents[i]] = (i + offset) % NumPlayers;
            agents[i]._moveCount = 0;
            MarkSelfPlayReady(agents[i], false);
        }

        SharedEpisodeEnding = false;
        SharedEpisodeActive = true;
        RequestSharedDecisionIfReady();
    }

    private static void FinishSharedEpisode()
    {
        if (SharedEpisodeEnding || !SharedEpisodeActive || SharedModel == null)
            return;

        SharedEpisodeEnding = true;
        SharedEpisodeActive = false;

        List<IkebanaAgent> agents = GetOrderedSelfPlayAgents();
        for (int i = 0; i < agents.Count && i < NumPlayers; i++)
        {
            int playerIndex;
            if (!TryGetSelfPlayPlayerIndex(agents[i], out playerIndex))
                continue;

            bool won = CheckWinCondition(SharedModel, playerIndex);
            agents[i].AddMatchResultReward(won);
            agents[i].RecordWinLoss(won);
        }

        for (int i = 0; i < agents.Count && i < NumPlayers; i++)
            agents[i].EndEpisode();
    }

    private static void RequestSharedDecisionIfReady()
    {
        if (!SharedEpisodeActive || SharedEpisodeEnding || SharedModel == null || SharedModel.IsGameOver)
            return;

        int currentPlayer = SharedModel.CurrentPlayer;
        List<IkebanaAgent> agents = GetOrderedSelfPlayAgents();
        for (int i = 0; i < agents.Count && i < NumPlayers; i++)
        {
            int playerIndex;
            if (TryGetSelfPlayPlayerIndex(agents[i], out playerIndex) && playerIndex == currentPlayer)
            {
                agents[i].RequestDecision();
                return;
            }
        }
    }

    private bool CanUseSharedSelfPlay()
    {
        return Academy.Instance != null && Academy.Instance.IsCommunicatorOn && GetOrderedSelfPlayAgents().Count >= NumPlayers;
    }

    private static void RegisterAgent(IkebanaAgent agent)
    {
        if (agent == null || RegisteredAgents.Contains(agent))
            return;

        RegisteredAgents.Add(agent);
        SelfPlayReady[agent] = false;
    }

    private static void UnregisterAgent(IkebanaAgent agent)
    {
        RegisteredAgents.Remove(agent);
        SelfPlayReady.Remove(agent);
        SelfPlayAssignments.Remove(agent);

        if (RegisteredAgents.Count < NumPlayers)
        {
            SharedEpisodeActive = false;
            SharedEpisodeEnding = false;
        }
    }

    private static void MarkSelfPlayReady(IkebanaAgent agent, bool ready)
    {
        if (agent == null)
            return;

        SelfPlayReady[agent] = ready;
    }

    private static bool TryGetSelfPlayPlayerIndex(IkebanaAgent agent, out int playerIndex)
    {
        return SelfPlayAssignments.TryGetValue(agent, out playerIndex);
    }

    private static List<IkebanaAgent> GetOrderedSelfPlayAgents()
    {
        List<IkebanaAgent> agents = new List<IkebanaAgent>(RegisteredAgents.Count);
        for (int i = 0; i < RegisteredAgents.Count; i++)
        {
            IkebanaAgent agent = RegisteredAgents[i];
            if (agent != null && agent.isActiveAndEnabled)
                agents.Add(agent);
        }

        agents.Sort((a, b) => a._teamId.CompareTo(b._teamId));
        return agents;
    }

    private void UpdateCurriculumThresholds()
    {
        float d = Mathf.Clamp01(_difficulty);
        float wG;
        float wR;
        float wO;
        float wE;

        if (d < 0.25f)
        {
            wG = 1f;
            wR = 0f;
            wO = 0f;
            wE = 0f;
        }
        else if (d < 0.50f)
        {
            float t = (d - 0.25f) / 0.25f;
            wG = 1f - t;
            wR = t;
            wO = 0f;
            wE = 0f;
        }
        else if (d < 0.75f)
        {
            float t = (d - 0.50f) / 0.25f;
            wG = 0f;
            wR = 1f - 0.4f * t;
            wO = 0.4f + 0.6f * t;
            wE = 0f;
        }
        else
        {
            float t = (d - 0.75f) / 0.25f;
            wG = 0f;
            wR = 0.6f * (1f - t);
            wO = 1f - 0.4f * t;
            wE = 0.4f * t;
        }

        float total = wG + wR + wO + wE;
        if (total <= 0f)
        {
            _threshGoodRandom = 1f;
            _threshRookie = 1f;
            _threshOptimizer = 1f;
            return;
        }

        _threshGoodRandom = wG / total;
        _threshRookie = (wG + wR) / total;
        _threshOptimizer = (wG + wR + wO) / total;
    }

    private bool CheckWinCondition()
    {
        return CheckWinCondition(_model, _playerIndex);
    }

    private void AddMatchResultReward(bool won)
    {
        if (won)
            AddReward(rewardWin);
        else
            AddReward(-rewardWin);
    }

    private static bool CheckWinCondition(MinimalGM model, int playerIndex)
    {
        int myScore = model.Players[playerIndex].Score;
        for (int i = 0; i < NumPlayers; i++)
        {
            if (i == playerIndex)
                continue;

            if (model.Players[i].Score >= myScore)
                return false;
        }

        return true;
    }

    private void RecordWinLoss(bool won)
    {
        int value = 0;
        if (won)
            value = 1;

        if (_winCount >= _winHistory.Length)
            _winSum -= _winHistory[_winIdx];
        else
            _winCount++;

        _winHistory[_winIdx] = value;
        _winSum += value;
        _winIdx = (_winIdx + 1) % _winHistory.Length;

        float winRate = 0f;
        if (_winCount > 0)
            winRate = _winSum / (float)_winCount;

        Academy.Instance.StatsRecorder.Add("Curriculum/WinRate", winRate, StatAggregationMethod.MostRecent);
        Academy.Instance.StatsRecorder.Add("Curriculum/Difficulty", _difficulty, StatAggregationMethod.MostRecent);
    }
}
