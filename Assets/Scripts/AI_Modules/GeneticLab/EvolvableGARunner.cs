using System.Collections;
using TMPro;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// EvolvableGARunner — Modified version of original to accomodate new heuristics.

// MonoBehaviour runner for EvolvableGeneticAlgo.
#region RUNNER FOR EVOLVABLE GA
public class EvolvableGARunner : MonoBehaviour
{
    private enum BrainTemplateType
    {
        Optimizer,
        Friendly,
        Emily
    }

    #region CONFIGURATION PARAMETERS
    [Header("GA Parameters")]
    [Tooltip("Number of genomes in each generation.")]
    [SerializeField] private int   populationSize    = 50;
    [Tooltip("Number of generations.")]
    [SerializeField] private int   generations       = 100;
    [Tooltip("Number of games played in the fitness function against EACH opponent.")]
    [SerializeField] private int   gamesPerEval      = 20;
    [Tooltip("Chance for each gene to mutate each generation. 0-1")]
    [SerializeField] private float mutationRate      = 0.2f;
    [SerializeField] private float mutationAmount    = 5f;
    [SerializeField] private int   eliteCount    = 5;
    [Tooltip("Number of genomes competing in tournament selection.")]
    [SerializeField] private int   tournamentSize    = 3;
    [SerializeField] private int   randomSeed        = 42;

    [Header("Threading")]
    [SerializeField] private int   threadCount       = 1;

    [Header("Game Parameters")]
    [SerializeField] private int numPlayers = 2;

    [Tooltip("Setup for the game (2p, 3p, 4p...).")]
    [SerializeField] private GameConfigData gameConfigData;

    [Header("Opponent Configuration")]
    [SerializeField] private GAOpponentConfig[] opponents = { new GAOpponentConfig() };
    [Tooltip("If true opponents list is not used")]
    [SerializeField] private bool coevolution = false;

    [Header("Best Genome Selection")]
    [SerializeField] private BestGenomeSelectionMethod bestGenomeSelectionMethod = BestGenomeSelectionMethod.BestEver;
    [Tooltip("For Highlander, how many top genomes from each generation are archived as champions.")]
    [SerializeField] private int highlanderChampionsPerGeneration = 1;
    [Tooltip("For Highlander, how many archived champions are sampled as the final opponent pool for each finalist.")]
    [SerializeField] private int highlanderFinalOpponentCount = 3;

    [Header("Output")]
    [SerializeField] private bool logEachGeneration = true;

    [Header("Brain Template")]
    [SerializeField] private BrainTemplateType brainTemplate = BrainTemplateType.Optimizer;

    [Tooltip("Genome to SAVE results.")]
    [SerializeField] private BasicGAGenome targetOptimizerGenomeAsset;

    [Header("Emily Output Assets")]
    [Tooltip("SAVE  Emily early game (rounds 1-2).")]
    [SerializeField] private BasicGAGenome targetEmilyEarlyGenomeAsset;
    [Tooltip("SAVE Emily mid game (rounds 3-4).")]
    [SerializeField] private BasicGAGenome targetEmilyMidGenomeAsset;
    [Tooltip("SAVE Emily late game (rounds 5+).")]
    [SerializeField] private BasicGAGenome targetEmilyLateGenomeAsset;

    [Header("Auto Run")]
    [SerializeField] private bool autoRun = true;

    [Header("Log Output")]
    [SerializeField] private LogSystem logOutput;
    #endregion

    #region STATE
    private EvolvableGeneticAlgo _ga;
    private bool _isRunning = false;
    private IEvolvableBrain _template;

    public bool IsRunning                => _isRunning;
    public EvolvableGeneticAlgo GA       => _ga;
    #endregion

    private void Start()
    {
        if (autoRun) StartTraining();
    }

    public void StartTraining()
    {
        if (_isRunning)
        {
            Debug.LogWarning("Evolvable GA training already running.");
            return;
        }

        switch (brainTemplate)
        {
            case BrainTemplateType.Emily:
                _template = new EmilyAIBrain();
                break;
            case BrainTemplateType.Friendly:
                _template = new FriendlyAIBrain();
                break;
            default:
                _template = new OptimizerAIBrain();
                break;
        }

        _ga = new EvolvableGeneticAlgo
        {
            PopulationSize    = populationSize,
            Generations       = generations,
            GamesPerEval      = gamesPerEval,
            MutationRate      = mutationRate,
            MutationAmount    = mutationAmount,
            EliteCount    = eliteCount,
            TournamentSize    = tournamentSize,
            RandomSeed        = randomSeed,
            Opponents         = opponents,
            Coevolution       = coevolution,
            BestGenomeSelection = bestGenomeSelectionMethod,
            HighlanderChampionsPerGeneration = highlanderChampionsPerGeneration,
            HighlanderFinalOpponentCount = highlanderFinalOpponentCount,
            LogEachGeneration = logEachGeneration,
            LogOutput         = logOutput,
            numPlayers        = numPlayers,
            ThreadCount       = threadCount,
            GameConfigData    = gameConfigData,
        };

        if (logOutput != null)
            logOutput.ClearLogs();

        _ga.Initialize(_template, (genes, config) =>
        {
            switch (brainTemplate)
            {
                case BrainTemplateType.Emily:
                    return new MinEmilyBrain(genes, config);
                case BrainTemplateType.Friendly:
                    return new MinFriendlyBrain(genes, config);
                default:
                    return new MinOptimizerBrain(genes, config);
            }
        });

        string brainName = (_template as IPlayerAIBrain) != null
            ? (_template as IPlayerAIBrain).BrainName
            : brainTemplate.ToString();

        Debug.Log("//============= Starting Evolvable GA Training =============");
        Debug.Log($"Brain: {brainName}  Genes: {_template.GeneCount}");
        Debug.Log($"Population: {populationSize}  Generations: {generations}");
        Debug.Log($"Best Selection: {bestGenomeSelectionMethod}");
        Debug.Log("=============================================================");

        if (logOutput != null)
        {
            logOutput.AddLog("//============= Starting Evolvable GA Training =============");
            logOutput.AddLog($"Brain: {brainName}  Genes: {_template.GeneCount}");
            logOutput.AddLog($"Population: {populationSize}  Generations: {generations}");
            logOutput.AddLog($"Best Selection: {bestGenomeSelectionMethod}");
            logOutput.AddLog("=============================================================");
        }

        StartCoroutine(RunTrainingCoroutine());
    }

    public void StopTraining()
    {
        if (!_isRunning) return;
        StopAllCoroutines();
        _isRunning = false;
        if (_ga != null) _ga.Dispose();
        Debug.Log("Evolvable GA training stopped.");
    }

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

        string summary = _ga.GetRunStatisticsSummary() + "\n" + _ga.GetBestGenomeSummary();
        summary += $"\nElapsed: {sw.Elapsed.TotalSeconds:F1}s  " +
                   $"({(float)generations / sw.Elapsed.TotalSeconds:F1} gen/s)";

        Debug.Log(".oOo. EVOLVABLE GA TRAINING COMPLETE .oOo.");
        Debug.Log(summary);

        if (logOutput != null)
        {
            logOutput.AddLog(".oOo. EVOLVABLE GA TRAINING COMPLETE .oOo.");
            logOutput.AddLog(summary);
        }

        if (brainTemplate == BrainTemplateType.Emily)
            ApplyBestEmilyGenesToAssets();
        else if (targetOptimizerGenomeAsset != null)
            ApplyBestGenesToAsset();

        _ga.Dispose();
    }

    //NOT FLEXIBLE, need 1 for each brain type.
    private void ApplyBestGenesToAsset()
    {
        float[] genes = _ga.BestGenes;
        if (brainTemplate == BrainTemplateType.Emily)
        {
            Debug.LogWarning("ApplyBestGenesToAsset is only supported for Optimizer template.");
            return;
        }

        if (genes == null || targetOptimizerGenomeAsset == null) return;

        if (brainTemplate == BrainTemplateType.Friendly)
        {
            FriendlyAIBrain temp = new FriendlyAIBrain();
            temp.SetGenes(genes);

            targetOptimizerGenomeAsset.SimulateScoringWeight = genes[0];
            targetOptimizerGenomeAsset.PenaltyWeight = genes[1];
            targetOptimizerGenomeAsset.TilesPlacedWeight = genes[2];
            targetOptimizerGenomeAsset.LineCompletionWeight = genes[3];
            targetOptimizerGenomeAsset.TryPushPenaltyWeight = 0f;
            targetOptimizerGenomeAsset.TryDenyMoveWeight = 0f;
            targetOptimizerGenomeAsset.AvoidPartialLineWeight = 0f;
            targetOptimizerGenomeAsset.FirstPlayerTokenWeight = 0f;
            targetOptimizerGenomeAsset.PrioritizeCentralPlacementWeight = 0f;
            targetOptimizerGenomeAsset.PrioritizeTopRowsWeight = 0f;
            targetOptimizerGenomeAsset.ChaseBonusRowWeights = null;
            targetOptimizerGenomeAsset.ChaseBonusColumnWeights = null;
            targetOptimizerGenomeAsset.ChaseBonusColorWeights = null;
        }
        else
        {
            OptimizerAIBrain temp = new OptimizerAIBrain();
            temp.SetGenes(genes);

            targetOptimizerGenomeAsset.SimulateScoringWeight            = temp.SimulateScoringWeight;
            targetOptimizerGenomeAsset.PenaltyWeight                    = temp.PenaltyWeight;
            targetOptimizerGenomeAsset.TilesPlacedWeight                = temp.TilesPlacedWeight;
            targetOptimizerGenomeAsset.LineCompletionWeight             = temp.LineCompletionWeight;
            targetOptimizerGenomeAsset.TryPushPenaltyWeight             = temp.TryPushPenaltyWeight;
            targetOptimizerGenomeAsset.TryDenyMoveWeight                = temp.TryDenyMoveWeight;
            targetOptimizerGenomeAsset.AvoidPartialLineWeight           = temp.AvoidPartialLineWeight;
            targetOptimizerGenomeAsset.FirstPlayerTokenWeight           = temp.FirstPlayerTokenWeight;
            targetOptimizerGenomeAsset.PrioritizeCentralPlacementWeight = temp.PrioritizeCentralPlacementWeight;
            targetOptimizerGenomeAsset.PrioritizeTopRowsWeight          = temp.PrioritizeTopRowsWeight;

            targetOptimizerGenomeAsset.ChaseBonusRowWeights    = temp.ChaseBonusRowWeights != null ? (float[])temp.ChaseBonusRowWeights.Clone() : null;
            targetOptimizerGenomeAsset.ChaseBonusColumnWeights = temp.ChaseBonusColumnWeights != null ? (float[])temp.ChaseBonusColumnWeights.Clone() : null;
            targetOptimizerGenomeAsset.ChaseBonusColorWeights  = temp.ChaseBonusColorWeights != null ? (float[])temp.ChaseBonusColorWeights.Clone() : null;
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(targetOptimizerGenomeAsset);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"Best genome applied to: {targetOptimizerGenomeAsset.name}");
        if (logOutput != null)
            logOutput.AddLog($"Best genome applied to: {targetOptimizerGenomeAsset.name}");
#endif
    }

    private void ApplyBestEmilyGenesToAssets()
    {
        float[] genes = _ga.BestGenes;
        if (genes == null) return;

        int stageSize = new OptimizerAIBrain().GeneCount;
        if (genes.Length < stageSize * 3)
        {
            Debug.LogWarning("Emily genes length is smaller than expected. Cannot apply to stage assets.");
            return;
        }

        ApplyEmilyStage(genes, 0 * stageSize, stageSize, targetEmilyEarlyGenomeAsset, "Emily Early (R1-2)");
        ApplyEmilyStage(genes, 1 * stageSize, stageSize, targetEmilyMidGenomeAsset, "Emily Mid (R3-4)");
        ApplyEmilyStage(genes, 2 * stageSize, stageSize, targetEmilyLateGenomeAsset, "Emily Late (R5+)");
    }

    private static void ApplyEmilyStage(float[] allGenes, int offset, int count, BasicGAGenome target, string label)
    {
        if (target == null) return;

        float[] stage = new float[count];
        System.Array.Copy(allGenes, offset, stage, 0, count);

        OptimizerAIBrain temp = new OptimizerAIBrain();
        temp.SetGenes(stage);

        target.SimulateScoringWeight            = temp.SimulateScoringWeight;
        target.PenaltyWeight                    = temp.PenaltyWeight;
        target.TilesPlacedWeight                = temp.TilesPlacedWeight;
        target.LineCompletionWeight             = temp.LineCompletionWeight;
        target.TryPushPenaltyWeight             = temp.TryPushPenaltyWeight;
        target.TryDenyMoveWeight                = temp.TryDenyMoveWeight;
        target.AvoidPartialLineWeight           = temp.AvoidPartialLineWeight;
        target.FirstPlayerTokenWeight           = temp.FirstPlayerTokenWeight;
        target.PrioritizeCentralPlacementWeight = temp.PrioritizeCentralPlacementWeight;
        target.PrioritizeTopRowsWeight          = temp.PrioritizeTopRowsWeight;
        target.ChaseBonusRowWeights             = temp.ChaseBonusRowWeights != null ? (float[])temp.ChaseBonusRowWeights.Clone() : null;
        target.ChaseBonusColumnWeights          = temp.ChaseBonusColumnWeights != null ? (float[])temp.ChaseBonusColumnWeights.Clone() : null;
        target.ChaseBonusColorWeights           = temp.ChaseBonusColorWeights != null ? (float[])temp.ChaseBonusColorWeights.Clone() : null;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(target);
        UnityEditor.AssetDatabase.SaveAssets();
#endif
        Debug.Log($"Best genome applied to: {label} ({target.name})");
    }
}
#endregion

//Auxiliary class to configure opponents for the GA training.
[System.Serializable]
#region GAOpponentConfig
public class GAOpponentConfig
{
    public AIBrainType BrainType = AIBrainType.Random;

    [Tooltip("Required when BrainType is Optimizer or Friendly. Assign a BasicGAGenome SO.")]
    public BasicGAGenome OptimizerGenome;

    [Tooltip("Emily early-game weights (rounds 1-2).")]
    public BasicGAGenome EmilyEarlyGenome;

    [Tooltip("Emily mid-game weights (rounds 3-4).")]
    public BasicGAGenome EmilyMidGenome;

    [Tooltip("Emily late-game weights (rounds 5+).")]
    public BasicGAGenome EmilyLateGenome;

    public IPlayerAIBrain CreateBrain()
    {
        return AIBrainFactory.CreateBrain(
            BrainType,
            optimizerWeights: OptimizerGenome,
            emilyEarlyWeights: EmilyEarlyGenome,
            emilyMidWeights: EmilyMidGenome,
            emilyLateWeights: EmilyLateGenome);
    }
}

#endregion