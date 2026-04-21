using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.0.2 (April 2026)    =^..^=    =^..^=
//                    Last Update 01/04/2026 

//DEPRECATED 15/04/2026.
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


// TO BE DEPRECATED
/*
// MonoBehaviour to run the genetic algorithm training process. 
// Configurable parameters are exposed in the Inspector.

public class GeneticAlgoRunner : MonoBehaviour
{
    #region CONFIGURATION PARAMETERS
    [Header("Use Early Mid Late Strategies")]
    [Tooltip("If true, the brain used will employ early, mid, and late game strategies. Else, weights are stable througout the game.")]
    [SerializeField] bool useEarlyMidLateStrategies = false;

    [Header("GA Parameters")]
    [SerializeField] private int   populationSize    = 50;
    [SerializeField] private int   generations       = 100;
    [SerializeField] private int   gamesPerEval      = 20;
    [SerializeField] private float mutationRate      = 0.2f;
    [SerializeField] private float mutationAmount    = 5f;
    [SerializeField] private int   survivorsCount    = 5;
    [SerializeField] private int   tournamentSize    = 3;
    [SerializeField] private int   randomSeed        = 42;

    [Header("Threading")]
    [Tooltip("Number of threads for parallel fitness evaluation. 1 = single-threaded.")]
    [SerializeField] private int   threadCount       = 1;

    public void SetPopulationSize(int value) => populationSize = value;
    public void SetGenerations(int value) => generations = value;
    public void SetGamesPerEval(int value) => gamesPerEval = value;
    public void SetMutationRate(float value) => mutationRate = value;
    public void SetMutationAmount(float value) => mutationAmount = value;
    public void SetSurvivorsCount(int value) => survivorsCount = value;
    public void SetTournamentSize(int value) => tournamentSize = value;
    public void SetRandomSeed(int value) => randomSeed = value;
    public void SetThreadCount(int value) => threadCount = Mathf.Max(1, value);


    [Header("Game Parameters")]
    [SerializeField] private int numPlayers = 2;

    [Tooltip("Game config to use for simulations. If null, global defaults are used.")]
    [SerializeField] private GameConfigData gameConfigData;

    public int NumPlayers => numPlayers;

    [Header("Opponent Configuration")]
    [Tooltip("One entry per opponent type to train against. GreedyAdjustable entries require a Genome asset.")]
    [SerializeField] private GAOpponentConfig[] opponents = { new GAOpponentConfig() };
    [SerializeField] private bool coevolution = false;
    public GAOpponentConfig[] Opponents => opponents;
    public bool Coevolution => coevolution;

    [Header("Output")]
    [SerializeField] private bool logEachGeneration = true;
    [Tooltip("Save best genome to JSON at the end of training")]
    [SerializeField] private bool saveToJson = true;
    [Tooltip("Optionally apply best genome to this asset (leave null to skip)")]
    [SerializeField] private GAGenome targetGenomeAsset;

    public void SetLogEachGeneration(bool value) => logEachGeneration = value;
    public void SetSaveToJson(bool value) => saveToJson = value;
    public void SetTargetGenomeAsset(GAGenome value) => targetGenomeAsset = value;

    [Header("Auto Run")]
    [SerializeField] private bool autoRun = true;

    [Header("Log Output")]
    [Tooltip("Optional UI element to display log messages in real-time during training.")]
    [SerializeField] private TextMeshProUGUI logOutput;
    #endregion
    #region STATE AND INITIALIZATION
    private GeneticAlgo _ga;
    private bool _isRunning = false;

    public bool IsRunning => _isRunning;
    public GeneticAlgo GA  => _ga;
    private void Start()

    {
        if (autoRun) StartTraining();
    }

    #endregion
    #region PUBLIC METHODS
   

    public void StartTraining()
    {
        if (_isRunning) { 
            Debug.LogWarning("GA training already running."); 
            if (logOutput != null) {
                logOutput.text += "\nWARNING: GA training already running.";
            }
        return; }

        // Validate opponent configs before starting
        if (opponents != null)
        {
            foreach (GAOpponentConfig o in opponents)
            {
                if (o.BrainType == AIBrainType.GreedyAdjustable && o.Genome == null)
                {
                    Debug.LogError("GeneticAlgoRunner: An opponent is set to GreedyAdjustable but has no Genome assigned. Assign a GAGenome asset in the Inspector.");
                    if (logOutput != null)
                        logOutput.text+=("\nERROR: An opponent is set to GreedyAdjustable but has no Genome assigned. Assign a GAGenome asset in the Inspector.");
                    return;
                }
                if (o.BrainType == AIBrainType.GreedyAdjustableEML && o.Genome == null)
                {
                    Debug.LogError("GeneticAlgoRunner: An opponent is set to GreedyAdjustableEML but has no Genome assigned. Assign a GAGenome asset in the Inspector.");
                    if (logOutput != null)
                        logOutput.text += ("\nERROR: An opponent is set to GreedyAdjustableEML but has no Genome assigned. Assign a GAGenome asset in the Inspector.");
                    return;
                }
                if (o.BrainType == AIBrainType.Optimizer && o.OptimizerGenome == null)
                {
                    Debug.LogError("GeneticAlgoRunner: An opponent is set to Optimizer but has no OptimizerGenome assigned. Assign a BasicGAGenome asset in the Inspector.");
                    if (logOutput != null)
                        logOutput.text += "\nERROR: An opponent is set to Optimizer but has no OptimizerGenome assigned. Assign a BasicGAGenome asset in the Inspector.";
                    return;
                }
            }
        }

        _ga = new GeneticAlgo
        {
            PopulationSize    = populationSize,
            Generations       = generations,
            GamesPerEval      = gamesPerEval,
            MutationRate      = mutationRate,
            MutationAmount    = mutationAmount,
            SurvivorsCount    = survivorsCount,
            TournamentSize    = tournamentSize,
            RandomSeed        = randomSeed,
            Opponents         = opponents,
            Coevolution       = coevolution,
            LogEachGeneration = logEachGeneration,
            LogOutput         = logOutput,
            numPlayers        = numPlayers,
            ThreadCount       = threadCount,
            UseEarlyMidLateStrategies = useEarlyMidLateStrategies,
            GameConfigData    = gameConfigData,
        };

        _ga.Initialize();

        System.Text.StringBuilder opponentLog = new System.Text.StringBuilder();
        foreach (var o in opponents)
            opponentLog.Append($"{o.BrainType}" +
                (o.BrainType == AIBrainType.GreedyAdjustable
                    ? $"({(o.Genome != null ? o.Genome.name : "NO GENOME")})" : "") + " ");

        Debug.Log("//==================== Starting GA Training ====================");
        Debug.Log($"Population: {populationSize}  Generations: {generations}  Games/eval: {gamesPerEval}");
        Debug.Log($"Mutation: {mutationRate}/{mutationAmount}  Survivors: {survivorsCount}  Threads: {threadCount}");
        Debug.Log($"Opponents: {opponentLog}  Coevolution: {coevolution}  EML: {useEarlyMidLateStrategies}  Seed: {randomSeed}");
        Debug.Log("===============================================================");
        if (logOutput != null)
        {
            logOutput.text += "\n//==================== Starting GA Training ====================";
            logOutput.text += $"\nPopulation: {populationSize}  Generations: {generations}  Games/eval: {gamesPerEval}";
            logOutput.text += $"\nMutation: {mutationRate}/{mutationAmount}  Survivors: {survivorsCount}  Threads: {threadCount}";
            logOutput.text += $"\nOpponents: {opponentLog}  Coevolution: {coevolution}  EML: {useEarlyMidLateStrategies}  Seed: {randomSeed}";
            logOutput.text += "\n===============================================================";
        }
        StartCoroutine(RunTrainingCoroutine());
    }

    
    public void StopTraining()
    {
        if (!_isRunning) return;
        StopAllCoroutines();
        _isRunning = false;
        if (_ga != null) _ga.Dispose();
        Debug.Log("GA training stopped.");
        if (logOutput != null)
            logOutput.text += "\nGA training stopped.";
    }

    #endregion
    #region AUXILIARY METHODS
    // Runs one generation per frame to avoid freezing the editor.
    private IEnumerator RunTrainingCoroutine()
    {
        _isRunning = true;
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        while (!_ga.IsComplete)
        {
            _ga.ExecuteGeneration();
            yield return null;
        }

        sw.Stop();
        _isRunning = false;

        string summary = _ga.GetBestGenomeSummary();
        summary += $"\nElapsed: {sw.Elapsed.TotalSeconds:F1}s  " +
                   $"({(float)generations / sw.Elapsed.TotalSeconds:F1} gen/s)";

        Debug.Log("//.oOo.oOo.oOo.oOo.oOo.oO TRAINING COMPLETE Oo.oOo.oOo.oOo.oOo.oOo.oOo.");
        Debug.Log(summary);
        Debug.Log(".oOo.oOo.oOo.oOo.oOo.oOo.oOo.oOo.oOo.oOo.oOo.oOo.oOo.oOo.oOo.oOo.oOo.");

        if (logOutput != null)
        {
            logOutput.text += "\n.oOo.oOo.oOo.oOo.oOo.oO TRAINING COMPLETE Oo.oOo.oOo.oOo.oOo.oOo.oOo.\"";
            logOutput.text += $"\n{summary}";
            logOutput.text += "\n.oOo.oOo.oOo.oOo.oOo.oOo.oOo.oOo.oOo.oOo.oOo.oOo.oOo.oOo.oOo.oOo.oOo.";
        }

        if (saveToJson)
            SaveBestGenomeToJson(_ga.BestGenome);

        if (targetGenomeAsset != null)
            ApplyBestGenomeToAsset(_ga.BestGenome, targetGenomeAsset);

        _ga.Dispose();
    }

    // Saves the best genome's weights to a JSON file
    private void SaveBestGenomeToJson(GAGenome genome)
    {
        if (genome == null) return;

        GAGenomeData data = GAGenomeData.FromGenome(genome);
        string json = JsonUtility.ToJson(data, prettyPrint: true);
        string path = Path.Combine(Application.persistentDataPath, "BestGAGenome.json");

        File.WriteAllText(path, json);
        Debug.Log($"Best genome saved to: {path}");
            if (logOutput != null)
                logOutput.text += $"\nBest genome saved to: {path}";
    }

    // Writes the best genome's values into an existing ScriptableObject 
    private void ApplyBestGenomeToAsset(GAGenome source, GAGenome target)
    {
        if (source == null || target == null) return;

        target.placingWeight             = source.placingWeight;
        target.endGameBonusWeight        = source.endGameBonusWeight;
        target.denyOpponentWeight        = source.denyOpponentWeight;
        target.pushingPenaltiesWeight    = source.pushingPenaltiesWeight;
        target.topRowsWeight             = source.topRowsWeight;
        target.earlyRoundsThreshold      = source.earlyRoundsThreshold;
        target.centralPlacementWeight    = source.centralPlacementWeight;
        target.avoidPartialLinesWeight   = source.avoidPartialLinesWeight;
        target.penaltyPerflower            = source.penaltyPerflower;
        target.firstPlayerTokenBaseBonus = source.firstPlayerTokenBaseBonus;
        target.denyColorBaseBonus        = source.denyColorBaseBonus;
        target.disruptExistingLineBonus  = source.disruptExistingLineBonus;
        target.denyCompletionBonus       = source.denyCompletionBonus;
        target.poisonColorBonus          = source.poisonColorBonus;
        target.oneMoveFillingBonus       = source.oneMoveFillingBonus;
        target.lineCompletionBonus       = source.lineCompletionBonus;
        //target.opponentLastMoveBonus     = source.opponentLastMoveBonus;
        //target.forcedPoisonLastMoveBonus = source.forcedPoisonLastMoveBonus;
        //target.selfPoisonPenalty         = source.selfPoisonPenalty;
        //target.poisonRemainingBonus      = source.poisonRemainingBonus;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(target);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"Best genome applied to: {target.name}");
        if (logOutput != null)
            logOutput.text += $"\nBest genome applied to: {target.name}";
#endif
    }
    #endregion
}
*/