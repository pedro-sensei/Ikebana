using System.Collections;
using System.Diagnostics;
using Unity.Burst;
using UnityEngine;
using Debug = UnityEngine.Debug;

//=^..^=   =^..^=   VERSION 1.0.2 (April 2026)    =^..^=    =^..^=
//                    Last Update 01/042026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


//MIMIC the simulator runner but for minimalGM.
public class MinimalGameSimulatorRunner : MonoBehaviour
{
    [Header("Simulation Configuration")]
    [SerializeField] private MinimalSimulationPlayerConfig[] playerConfigs;

    [Tooltip("Total number of games to simulate")]
    [SerializeField] private int numberOfGames = 1000;

    [Tooltip("Games per frame (higher = faster but may freeze editor)")]
    [SerializeField] private int gamesPerBatch = 50;

    [Tooltip("Game config to use for simulations. If null, global defaults are used.")]
    [SerializeField] private GameConfigData gameConfigData;

    [Header("Auto Run")]
    [Tooltip("Start simulation automatically on Play")]
    [SerializeField] private bool autoRun = true;

    [Header("Output (Optional)")]
    [SerializeField] private TMPro.TextMeshProUGUI resultsText;

    private bool isRunning = false;
    private MinimalSimulationBatchResult lastResult;

    public MinimalSimulationBatchResult LastResult => lastResult;
    public bool IsRunning => isRunning;

    private void Start()
    {
        if (autoRun)
        {
            StartSimulation();
        }
    }

    public void StartSimulation()
    {
        if (isRunning)
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

    private IEnumerator RunSimulationCoroutine()
    {
        isRunning = true;

        int numPlayers = playerConfigs.Length;
        IMinimalAIBrain[] brains = new IMinimalAIBrain[numPlayers];
        string[] names = new string[numPlayers];

        for (int i = 0; i < numPlayers; i++)
        {
            brains[i] = MinimalBrainFactory.CreateBrain(
                playerConfigs[i].BrainType,
                playerConfigs[i].GreedyWeights,
                playerConfigs[i].EmilyEarlyWeights,
                playerConfigs[i].EmilyMidWeights,
                playerConfigs[i].EmilyLateWeights
            );
            names[i] = !string.IsNullOrEmpty(playerConfigs[i].DisplayName)
                ? playerConfigs[i].DisplayName
                : brains[i].BrainName;
        }

        Debug.Log($"[Minimal Sim] Starting simulation: {numberOfGames} games, {numPlayers} players");
        for (int i = 0; i < numPlayers; i++)
            Debug.Log($"  Player {i}: {names[i]} ({brains[i].BrainName})");

        MinimalSimulationBatchResult batch = new MinimalSimulationBatchResult(numPlayers, numberOfGames);
        for (int i = 0; i < numPlayers; i++)
            batch.PlayerNames[i] = names[i];

        if (gameConfigData != null)
            GameConfig.Initialize(gameConfigData);
        GameConfigSnapshot configSnapshot = GameConfig.CreateSnapshot();
        if (gameConfigData != null)
            GameConfig.ResetToDefaults();

        MinimalGameSimulator simulator = new MinimalGameSimulator(configSnapshot);
        Stopwatch stopwatch = Stopwatch.StartNew();

        int gamesCompleted = 0;

        while (gamesCompleted < numberOfGames)
        {
            int batchEnd = Mathf.Min(gamesCompleted + gamesPerBatch, numberOfGames);

            for (int g = gamesCompleted; g < batchEnd; g++)
            {
                MinimalSimulationGameResult result = simulator.RunGame(brains, names);
                batch.AddGameResult(result);
            }

            gamesCompleted = batchEnd;

            yield return null;
        }

        stopwatch.Stop();
        batch.FinalizeScoring();
        lastResult = batch;

        double elapsedSec = stopwatch.Elapsed.TotalSeconds;
        double gamesPerSec = elapsedSec > 0 ? numberOfGames / elapsedSec : 0;
        double msPerGame = elapsedSec > 0 ? (elapsedSec * 1000.0) / numberOfGames : 0;

        string summary = batch.GetSummary();
        summary += $"\n  ********* PERFORMANCE STATS *************";
        summary += $"\n  Total time:   {elapsedSec:F2}s";
        summary += $"\n  Games/sec:    {gamesPerSec:F0}";
        summary += $"\n  *****************************************";

        Debug.Log(summary);

        if (resultsText != null)
            resultsText.text = summary;

        isRunning = false;
    }
}

[System.Serializable]
public class MinimalSimulationPlayerConfig
{
    [Tooltip("Display name for results")]
    public string DisplayName;

    public AIBrainType BrainType = AIBrainType.Random;

    [Tooltip("Weights asset (only for GreedyAdjustable)")]
    public BasicGAGenome GreedyWeights;

    [Tooltip("Emily early-game weights (rounds 1-2).")]
    public BasicGAGenome EmilyEarlyWeights;

    [Tooltip("Emily mid-game weights (rounds 3-4).")]
    public BasicGAGenome EmilyMidWeights;

    [Tooltip("Emily late-game weights (rounds 5+).")]
    public BasicGAGenome EmilyLateWeights;
}

public static class MinimalBrainFactory
{
    public static IMinimalAIBrain CreateBrain(AIBrainType brainType, BasicGAGenome weights = null, BasicGAGenome emilyEarly = null, BasicGAGenome emilyMid = null, BasicGAGenome emilyLate = null, int minMaxDepth = 3)
    {
        switch (brainType)
        {
            // case AIBrainType.Greedy:
            //     return new MinimalGreedyBrain();

            // case AIBrainType.GreedyAdjustable:
            //     if (weights == null)
            //     {
            //         UnityEngine.Debug.LogWarning("GreedyAdjustable requires a GAGenome asset. Falling back to default MinimalGreedyBrain.");
            //         return new MinimalGreedyBrain();
            //     }
            //    return new MinimalGreedyBrain(weights);

            // case AIBrainType.GreedyAdjustableEML:
            //     if (weights == null)
            //    {
            //         UnityEngine.Debug.LogWarning("GreedyAdjustableEML requires a GAGenome asset. Falling back to default MinimalGreedyBrain.");
            //         return new MinimalGreedyBrain();
            //     }
            //     return new MinimalGreedyBrainEML(weights);
            //
            //TODO: INTEGRATE GAME MODELS INTO MINIMAL SIMULATOR
            //case AIBrainType.MinMax:
            //    return new MinMaxBrain(minMaxDepth);
            case AIBrainType.Rookie:
                return new MinRookieBrain();
            //case AIBrainType.Friendly:
            //    return new MinimalFriendlyBrain();
            case AIBrainType.Optimizer:
                return new MinOptimizerBrain();
            case AIBrainType.Emily:
                return new MinEmilyBrain(emilyEarly, emilyMid, emilyLate);
            case AIBrainType.GoodRandom:
                return new MinGoodRandomBrain();
            case AIBrainType.Random:
            default:
                return new MinimalRandomBrain();
        }
    }
}
