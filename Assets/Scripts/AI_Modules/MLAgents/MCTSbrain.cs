using System;
using System;
using System.Collections.Generic;

public interface IMCTSMoveEvaluator
{
    int ChooseMoveIndex(MinimalGM model, MinimalMoveRecord[] moves, int moveCount, int evaluatingPlayer);
}

public sealed class RandomMCTSMoveEvaluator : IMCTSMoveEvaluator
{
    private readonly System.Random _rng;

    public RandomMCTSMoveEvaluator(int seed = 0)
    {
        _rng = seed == 0 ? new System.Random() : new System.Random(seed);
    }

    public int ChooseMoveIndex(MinimalGM model, MinimalMoveRecord[] moves, int moveCount, int evaluatingPlayer)
    {
        if (moveCount <= 0)
            return -1;

        return _rng.Next(moveCount);
    }
}

public class MCTSBrain : IPlayerAIBrain
{
    private const float DefaultExplorationConstant = 1.41421356f;
    private const int DefaultSimulationIterations = 256;

    private readonly MinimalMoveRecord[] _rootMoveBuffer =
        new MinimalMoveRecord[MinimalGM.MAX_MINMAX_MOVES];

    private readonly MinimalMoveRecord[] _rolloutMoveBuffer =
        new MinimalMoveRecord[MinimalGM.MAX_MINMAX_MOVES];

    private MinimalGM _rootModel;
    private MinimalGM _simulationModel;
    private int _maxSearchDepth;
    private int _simulationIterations;
    private float _explorationConstant;
    private IMCTSMoveEvaluator _moveEvaluator;

    public string BrainName
    {
        get { return "MCTS (d=" + _maxSearchDepth + ", n=" + _simulationIterations + ")"; }
    }

    public MCTSBrain(
        int maxDepth = 8,
        int simulationIterations = DefaultSimulationIterations,
        IMCTSMoveEvaluator moveEvaluator = null,
        float explorationConstant = DefaultExplorationConstant)
    {
        _maxSearchDepth = Math.Max(1, maxDepth);
        _simulationIterations = Math.Max(1, simulationIterations);
        _explorationConstant = explorationConstant <= 0f
            ? DefaultExplorationConstant
            : explorationConstant;
        _moveEvaluator = moveEvaluator ?? new RandomMCTSMoveEvaluator();
    }

    public void SetEvaluator(IMCTSMoveEvaluator evaluator)
    {
        _moveEvaluator = evaluator ?? new RandomMCTSMoveEvaluator();
    }

    public void SetDepth(int depth)
    {
        _maxSearchDepth = Math.Max(1, depth);
    }

    public void SetSimulationIterations(int iterations)
    {
        _simulationIterations = Math.Max(1, iterations);
    }

    public GameMove ChooseMove(GameModel model, List<GameMove> validMoves)
    {
        if (validMoves == null || validMoves.Count == 0)
            return default(GameMove);

        if (validMoves.Count == 1)
            return validMoves[0];

        _rootModel = GameModelToMinimal.Convert(model, _rootModel);
        EnsureSimulationModel();

        int rootMoveCount = _rootModel.GetValidMovesMinMax(_rootMoveBuffer);
        if (rootMoveCount <= 0)
            return validMoves[0];

        GameState rootState = _rootModel.SaveSnapshot();
        MCTSNode rootNode = new MCTSNode(null, rootState, default(MinimalMoveRecord), 0, false);
        InitializeMoves(rootNode, _rootMoveBuffer, rootMoveCount);

        int bestRootChildIndex = SearchBestRootChild(rootNode, model.CurrentPlayerIndex);
        if (bestRootChildIndex < 0 || bestRootChildIndex >= rootNode.Children.Count)
            return MapMinimalMoveToGameMove(_rootMoveBuffer[0], validMoves);

        return MapMinimalMoveToGameMove(rootNode.Children[bestRootChildIndex].MoveFromParent, validMoves);
    }

    private void EnsureSimulationModel()
    {
        if (_simulationModel == null || _simulationModel.NumPlayers != _rootModel.NumPlayers)
            _simulationModel = new MinimalGM(_rootModel.NumPlayers, _rootModel.Config);
    }

    private int SearchBestRootChild(MCTSNode rootNode, int evaluatingPlayer)
    {
        int iterations = Math.Max(_simulationIterations, rootNode.UnexpandedMoveCount);

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            _simulationModel.SetGameState(rootNode.State);

            MCTSNode node = rootNode;
            while (!node.IsTerminal
                   && node.Depth < _maxSearchDepth
                   && node.MovesInitialized
                   && node.UnexpandedMoveCount == 0
                   && node.Children.Count > 0)
            {
                node = SelectChildByUcb(node);
                _simulationModel.SetGameState(node.State);
            }

            if (!node.IsTerminal && node.Depth < _maxSearchDepth)
            {
                EnsureNodeMovesInitialized(node);

                if (!node.IsTerminal && node.UnexpandedMoveCount > 0)
                {
                    int chosenMoveIndex = ChooseSimulationMoveIndex(
                        _simulationModel,
                        node.UnexpandedMoves,
                        node.UnexpandedMoveCount,
                        evaluatingPlayer);

                    MinimalMoveRecord chosenMove = node.UnexpandedMoves[chosenMoveIndex];
                    RemoveMoveAt(node.UnexpandedMoves, ref node.UnexpandedMoveCount, chosenMoveIndex);

                    _simulationModel.SetGameState(node.State);
                    ApplyMoveAndAdvance(_simulationModel, ref chosenMove);

                    MCTSNode childNode = new MCTSNode(
                        node,
                        _simulationModel.SaveSnapshot(),
                        chosenMove,
                        node.Depth + 1,
                        _simulationModel.IsGameOver || node.Depth + 1 >= _maxSearchDepth);

                    node.Children.Add(childNode);
                    node = childNode;
                }
            }

            float reward = RunRollout(_simulationModel, evaluatingPlayer, node.Depth);
            Backpropagate(node, reward);
        }

        return GetBestRootChildIndex(rootNode);
    }

    private void EnsureNodeMovesInitialized(MCTSNode node)
    {
        if (node.MovesInitialized)
            return;

        node.MovesInitialized = true;
        if (node.IsTerminal || node.Depth >= _maxSearchDepth)
        {
            node.IsTerminal = true;
            node.UnexpandedMoves = Array.Empty<MinimalMoveRecord>();
            node.UnexpandedMoveCount = 0;
            return;
        }

        int moveCount = _simulationModel.GetValidMovesMinMax(_rolloutMoveBuffer);
        if (_simulationModel.IsGameOver || moveCount <= 0)
        {
            node.IsTerminal = true;
            node.UnexpandedMoves = Array.Empty<MinimalMoveRecord>();
            node.UnexpandedMoveCount = 0;
            return;
        }

        InitializeMoves(node, _rolloutMoveBuffer, moveCount);
    }

    private static void InitializeMoves(MCTSNode node, MinimalMoveRecord[] sourceMoves, int moveCount)
    {
        node.MovesInitialized = true;
        node.UnexpandedMoves = new MinimalMoveRecord[moveCount];
        Array.Copy(sourceMoves, node.UnexpandedMoves, moveCount);
        node.UnexpandedMoveCount = moveCount;
    }

    private int ChooseSimulationMoveIndex(
        MinimalGM model,
        MinimalMoveRecord[] moves,
        int moveCount,
        int evaluatingPlayer)
    {
        if (moveCount <= 0)
            return 0;

        int moveIndex = _moveEvaluator != null
            ? _moveEvaluator.ChooseMoveIndex(model, moves, moveCount, evaluatingPlayer)
            : -1;

        if (moveIndex < 0 || moveIndex >= moveCount)
            moveIndex = 0;

        return moveIndex;
    }

    private MCTSNode SelectChildByUcb(MCTSNode node)
    {
        MCTSNode bestChild = node.Children[0];
        float bestValue = float.NegativeInfinity;
        float parentVisits = Math.Max(1, node.VisitCount);

        for (int i = 0; i < node.Children.Count; i++)
        {
            MCTSNode child = node.Children[i];
            float ucbValue;
            if (child.VisitCount <= 0)
            {
                ucbValue = float.PositiveInfinity;
            }
            else
            {
                float averageReward = child.TotalReward / child.VisitCount;
                float exploration = _explorationConstant *
                                    (float)Math.Sqrt(Math.Log(parentVisits + 1f) / child.VisitCount);
                ucbValue = averageReward + exploration;
            }

            if (ucbValue > bestValue)
            {
                bestValue = ucbValue;
                bestChild = child;
            }
        }

        return bestChild;
    }

    private float RunRollout(MinimalGM model, int evaluatingPlayer, int startingDepth)
    {
        int depth = startingDepth;

        while (!model.IsGameOver && depth < _maxSearchDepth)
        {
            int moveCount = model.GetValidMovesMinMax(_rolloutMoveBuffer);
            if (moveCount <= 0)
                break;

            int moveIndex = ChooseSimulationMoveIndex(model, _rolloutMoveBuffer, moveCount, evaluatingPlayer);
            MinimalMoveRecord move = _rolloutMoveBuffer[moveIndex];
            ApplyMoveAndAdvance(model, ref move);
            depth++;
        }

        return ComputeScoreDelta(model, evaluatingPlayer);
    }

    private static void ApplyMoveAndAdvance(MinimalGM model, ref MinimalMoveRecord move)
    {
        model.ExecuteMoveMinMax(ref move);

        if (model.AreDisplaysEmpty())
            model.SimEndRound();
        else
            model.AdvanceTurnMinMax();
    }

    private static float ComputeScoreDelta(MinimalGM model, int evaluatingPlayer)
    {
        float playerScore = model.Players[evaluatingPlayer].Score;
        float bestOpponentScore = float.NegativeInfinity;

        for (int playerIndex = 0; playerIndex < model.NumPlayers; playerIndex++)
        {
            if (playerIndex == evaluatingPlayer)
                continue;

            float opponentScore = model.Players[playerIndex].Score;
            if (opponentScore > bestOpponentScore)
                bestOpponentScore = opponentScore;
        }

        if (bestOpponentScore == float.NegativeInfinity)
            bestOpponentScore = 0f;

        return playerScore - bestOpponentScore;
    }

    private static void Backpropagate(MCTSNode node, float reward)
    {
        MCTSNode current = node;
        while (current != null)
        {
            current.VisitCount++;
            current.TotalReward += reward;
            current = current.Parent;
        }
    }

    private static void RemoveMoveAt(MinimalMoveRecord[] moves, ref int moveCount, int moveIndex)
    {
        int lastIndex = moveCount - 1;
        if (moveIndex < lastIndex)
            moves[moveIndex] = moves[lastIndex];

        moveCount = Math.Max(0, lastIndex);
    }

    private static int GetBestRootChildIndex(MCTSNode rootNode)
    {
        int bestIndex = -1;
        float bestAverageReward = float.NegativeInfinity;
        int bestVisitCount = -1;

        for (int i = 0; i < rootNode.Children.Count; i++)
        {
            MCTSNode child = rootNode.Children[i];
            if (child.VisitCount <= 0)
                continue;

            float averageReward = child.TotalReward / child.VisitCount;
            if (averageReward > bestAverageReward ||
                (Math.Abs(averageReward - bestAverageReward) < 0.0001f && child.VisitCount > bestVisitCount))
            {
                bestAverageReward = averageReward;
                bestVisitCount = child.VisitCount;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static GameMove MapMinimalMoveToGameMove(
        MinimalMoveRecord moveRecord,
        List<GameMove> validMoves)
    {
        FlowerColor expectedColor = (FlowerColor)moveRecord.ColorIndex;

        MoveSource expectedSource = MoveSource.Factory;
        if (moveRecord.IsCentralSource)
            expectedSource = MoveSource.Central;

        MoveTarget expectedTarget = MoveTarget.PlacementLine;
        if (moveRecord.TargetIsPenalty)
            expectedTarget = MoveTarget.PenaltyLine;

        int expectedLine = moveRecord.TargetLineIndex;
        int expectedFactory = moveRecord.FactoryIndex;

        for (int i = 0; i < validMoves.Count; i++)
        {
            GameMove candidateMove = validMoves[i];
            if (candidateMove.Color != expectedColor) continue;
            if (candidateMove.SourceEnum != expectedSource) continue;
            if (candidateMove.TargetEnum != expectedTarget) continue;
            if (!candidateMove.IsPenalty && candidateMove.TargetLineIndex != expectedLine) continue;
            if (expectedSource == MoveSource.Factory && candidateMove.FactoryIndex != expectedFactory) continue;
            return candidateMove;
        }

        for (int i = 0; i < validMoves.Count; i++)
        {
            GameMove candidateMove = validMoves[i];
            bool hasSameColor = candidateMove.Color == expectedColor;
            bool hasSameTargetType = candidateMove.TargetEnum == expectedTarget;
            bool hasCompatibleLine = candidateMove.IsPenalty || candidateMove.TargetLineIndex == expectedLine;

            if (hasSameColor && hasSameTargetType && hasCompatibleLine)
                return candidateMove;
        }

        return validMoves[0];
    }

    private sealed class MCTSNode
    {
        public readonly MCTSNode Parent;
        public readonly GameState State;
        public readonly MinimalMoveRecord MoveFromParent;
        public readonly int Depth;
        public readonly List<MCTSNode> Children = new List<MCTSNode>();

        public MinimalMoveRecord[] UnexpandedMoves;
        public int UnexpandedMoveCount;
        public bool MovesInitialized;
        public bool IsTerminal;
        public int VisitCount;
        public float TotalReward;

        public MCTSNode(MCTSNode parent, GameState state, MinimalMoveRecord moveFromParent, int depth, bool isTerminal)
        {
            Parent = parent;
            State = state;
            MoveFromParent = moveFromParent;
            Depth = depth;
            IsTerminal = isTerminal;
        }
    }
}
