using UnityEngine;
using System.Collections;



//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


#region GAME SIMULATOR RUNNER

// Designed for testing and comparing different AI brain configurations.
// 
// TODO: Consider adding support to MinimalModel for faster simulations.
// Results are printed to the console and optionally to a UI text field.

public class GameSimulatorRunner : MonoBehaviour
{
    #region FIELDS AND PROPERTIES
    [Header("Simulation Configuration")]
    [SerializeField] private SimulationPlayerConfig[] playerConfigs;

    [Tooltip("Total number of games to simulate")]
    [SerializeField] private int numberOfGames = 1000;

    [Tooltip("Games per frame (higher = faster but may freeze editor)")]
    [SerializeField] private int gamesPerBatch = 5;

    [Tooltip("Max simulation work time per frame in ms before yielding.")]
    [Range(1f, 33f)]
    [SerializeField] private float maxFrameBudgetMs = 8f;

    [Tooltip("Log every finished game. Disable for large simulation runs.")]
    [SerializeField] private bool logEveryGame = false;

    [Tooltip("Progress log cadence when 'Log Every Game' is disabled.")]
    [SerializeField] private int progressLogInterval = 25;

    [Header("MinMax Safety (Simulator)")]
    [Tooltip("Clamp MinMax depth/time for simulator stability.")]
    [SerializeField] private bool forceFastMinMaxInSimulator = true;

    [Tooltip("Max depth used by MinMax brains when fast mode is enabled.")]
    [Range(1, 15)]
    [SerializeField] private int simulatorMinMaxDepthCap = 6;

    [Tooltip("Max time budget (ms) per MinMax move when fast mode is enabled.")]
    [Range(25, 10000)]
    [SerializeField] private float simulatorMinMaxTimeCapMs = 250f;

    [Tooltip("Game config to use for simulations. If null, global defaults are used.")]
    [SerializeField] private GameConfigData gameConfigData;

    [Header("Auto Run")]
    [Tooltip("Start simulation automatically on Play")]
    [SerializeField] private bool autoRun = true;

    [Header("Output (Optional)")]
    [SerializeField] private LogSystem resultsLog;

    private bool _isRunning = false;
   // private SimulationBatchResult _lastResult;

    //public SimulationBatchResult LastResult => _lastResult;
    public bool IsRunning => _isRunning;
    #endregion

    #region UNITY EDITOR METHODS
    private void Start()
    {
        if (autoRun)
        {
            StartSimulation();
        }
    }


    /// Starts the batch simulation.
    /// Link to the button in scene or call directly.
    public void StartSimulation()
    {
        if (_isRunning)
        {
            Debug.LogWarning("Simulation already running.");
            return;
        }

        if (playerConfigs == null || playerConfigs.Length < 2)
        {
            Debug.LogError("Need at least 2 player configs for simulation.");
            return;
        }

        StartCoroutine(RunSimulationCoroutine());
    }


    //Coroutine to run simulation in groups.
    private IEnumerator RunSimulationCoroutine()
    {
        _isRunning = true;

        if (resultsLog != null)
            resultsLog.ClearLogs();

        // Apply the assigned game config (or keep current defaults)
        if (gameConfigData != null)
            GameConfig.Initialize(gameConfigData);

        // Build brains
        int numPlayers = playerConfigs.Length;
        IPlayerAIBrain[] brains = new IPlayerAIBrain[numPlayers];
        string[] names = new string[numPlayers];

        for (int i = 0; i < numPlayers; i++)
        {
            int depth = playerConfigs[i].MinMaxDepth;
            float timeLimitMs = playerConfigs[i].MinMaxTimeLimitMs;

            if (forceFastMinMaxInSimulator &&
                (playerConfigs[i].BrainType == AIBrainType.MinMax ||
                 playerConfigs[i].BrainType == AIBrainType.MinMaxOptimizer))
            {
                if (depth > simulatorMinMaxDepthCap) depth = simulatorMinMaxDepthCap;
                if (timeLimitMs > simulatorMinMaxTimeCapMs) timeLimitMs = simulatorMinMaxTimeCapMs;
            }

            brains[i] = AIBrainFactory.CreateBrain(
                brainType: playerConfigs[i].BrainType,
                minMaxDepth: depth,
                minMaxTimeLimitMs: timeLimitMs,
                minMaxEvaluator: playerConfigs[i].MinMaxEvaluator,
                optimizerWeights: playerConfigs[i].OptimizerWeights,
                mlAgentModel: playerConfigs[i].MLAgentModel,
                numPlayers: numPlayers,
                emilyEarlyWeights: playerConfigs[i].EmilyEarlyWeights,
                emilyMidWeights: playerConfigs[i].EmilyMidWeights,
                emilyLateWeights: playerConfigs[i].EmilyLateWeights,
                relativeImmediateSimulationWeight: playerConfigs[i].RelativeImmediateSimulationWeight,
                relativeTerminalDeltaWeight: playerConfigs[i].RelativeTerminalDeltaWeight,
                relativeExpectedMoveWeight: playerConfigs[i].RelativeExpectedMoveWeight,
                relativeUsePhaseAwareImmediateWeight: playerConfigs[i].RelativeUsePhaseAwareImmediateWeight,
                mlValueSearchPolicy: playerConfigs[i].MLValueSearchPolicy,
                mlValueMctsIterations: playerConfigs[i].MLValueMctsIterations,
                mlValueMctsNewRoundSamples: playerConfigs[i].MLValueMctsNewRoundSamples
            );
            if (string.IsNullOrEmpty(playerConfigs[i].DisplayName))
            {
                names[i] = brains[i].BrainName;
            }
            else
            {
                names[i] = playerConfigs[i].DisplayName;
            }
        }

        Debug.Log($"Starting simulation: {numberOfGames} games, {numPlayers} players");
        AppendResultsLine($"Starting simulation: {numberOfGames} games, {numPlayers} players");
        for (int i = 0; i < numPlayers; i++)
        {
            Debug.Log($"  Player {i}: {names[i]} ({brains[i].BrainName})");
            AppendResultsLine($"  Player {i}: {names[i]} ({brains[i].BrainName})");
        }

        SimulationBatchResult batch = new SimulationBatchResult(numPlayers, numberOfGames);
        for (int i = 0; i < numPlayers; i++)
            batch.PlayerNames[i] = names[i];

        GameSimulator simulator = new GameSimulator();

        //To measure performance in the end
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

        int gamesCompleted = 0;

        var frameStopwatch = new System.Diagnostics.Stopwatch();
        while (gamesCompleted < numberOfGames)
        {
            frameStopwatch.Restart();
            int batchEnd = gamesCompleted + gamesPerBatch;
            if (batchEnd > numberOfGames)
                batchEnd = numberOfGames;

            for (int g = gamesCompleted; g < batchEnd; g++)
            {
                SimulationGameResult result = simulator.RunGame(brains, names);
                batch.AddGameResult(result);
                gamesCompleted++;

                if (logEveryGame)
                {
                    string gameLog = $"Game {g + 1}/{numberOfGames} completed. Winner: {result.PlayerNames[result.WinnerIndex]} (Score: {result.Scores[result.WinnerIndex]})";
                    Debug.Log(gameLog);
                    AppendResultsLine(gameLog);
                }
                else
                {
                    int completed = g + 1;
                    int safeInterval = progressLogInterval < 1 ? 1 : progressLogInterval;
                    if (completed == numberOfGames || completed % safeInterval == 0)
                    {
                        Debug.Log($"Completed {completed}/{numberOfGames} games...");
                    }
                }

                if ((float)frameStopwatch.Elapsed.TotalMilliseconds >= maxFrameBudgetMs)
                    break;
            }

            // Yield to prevent editor freeze
            yield return null;
        }

        stopwatch.Stop();
        batch.CalculateFinalStatistics();
        //_lastResult = batch;

        double elapsedSec = stopwatch.Elapsed.TotalSeconds;
        double gamesPerSec = numberOfGames / elapsedSec;
  

        string summary = batch.GetSummary();
        summary += $"\n  ********* PERFORMANCE STATS *************";
        summary += $"\n  Total time:   {elapsedSec:F2}s";
        summary += $"\n  Games/sec:    {gamesPerSec:F0}";
        summary += $"\n  *****************************************";

        Debug.Log(summary);

        if (resultsLog != null)
            resultsLog.AddLog(summary);

        _isRunning = false;
    }

    private void AppendResultsLine(string line)
    {
        if (resultsLog == null)
            return;

        resultsLog.AddLog(line);
    }
    #endregion
}

#endregion

#region SIMULATION_PLAYER_CONFIG 


/// Auxiliary class to hold configuration for each player in the simulation.
/// Allows selecting brain type and parameters in editor.

[System.Serializable]
public class SimulationPlayerConfig
{
    [Tooltip("Display name for results")]
    public string DisplayName;

    public AIBrainType BrainType = AIBrainType.Random;

    //[Tooltip("Weights asset (only for GreedyAdjustable)")]
   // public GAGenome GreedyWeights;

    [Tooltip("Weights asset (used by Optimizer and Friendly)")]
    public BasicGAGenome OptimizerWeights;

    [Tooltip("Emily early-game weights (rounds 1-2).")]
    public BasicGAGenome EmilyEarlyWeights;

    [Tooltip("Emily mid-game weights (rounds 3-4).")]
    public BasicGAGenome EmilyMidWeights;

    [Tooltip("Emily late-game weights (rounds 5+).")]
    public BasicGAGenome EmilyLateWeights;

    [Tooltip("Search depth for MinMax / MinMax brains. Ignored by other brain types.")]
    [Range(1, 15)]
    public int MinMaxDepth = 15;

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

    [Tooltip("Search policy used by the MLMCTSValue brain.")]
    public MLValueSearchPolicy MLValueSearchPolicy = MLValueSearchPolicy.MCTS;

    [Tooltip("MCTS rollout iterations per root move for the MLMCTSValue brain.")]
    [Range(1, 2048)]
    public int MLValueMctsIterations = 128;

    [Tooltip("Number of random new-round samples for MCTS after an end of round.")]
    [Range(1, 16)]
    public int MLValueMctsNewRoundSamples = 4;
}

#endregion

#region SIMULATION GRAPHICS

//TODO: CREATE GRAPHICAL RESULTS VIEWER
#endregion