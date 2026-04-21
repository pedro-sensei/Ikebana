using System.Collections;
using TMPro;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.0.3 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=
// EvolvableGARunner — Modified version of original to accomodate new heuristics.

// MonoBehaviour runner for EvolvableGeneticAlgo.
#region RUNNER FOR EVOLVABLE GA
public class EvolvableGARunner : MonoBehaviour
{
    #region CONFIGURATION PARAMETERS
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
    [SerializeField] private int   threadCount       = 1;

    [Header("Game Parameters")]
    [SerializeField] private int numPlayers = 2;

    [Tooltip("Game config to use for simulations. If null, global defaults are used.")]
    [SerializeField] private GameConfigData gameConfigData;

    [Header("Opponent Configuration")]
    [SerializeField] private GAOpponentConfig[] opponents = { new GAOpponentConfig() };
    [SerializeField] private bool coevolution = false;

    [Header("Output")]
    [SerializeField] private bool logEachGeneration = true;
    [SerializeField] private bool saveToJson = true;

    [Header("Brain Template")]
    [SerializeField] private bool useEmilyTemplate = false;

    [Tooltip("Optional BasicGAGenome asset to write the best result into.")]
    [SerializeField] private BasicGAGenome targetOptimizerGenomeAsset;

    [Header("Emily Output Assets")]
    [Tooltip("Optional BasicGAGenome for Emily early game (rounds 1-2).")]
    [SerializeField] private BasicGAGenome targetEmilyEarlyGenomeAsset;
    [Tooltip("Optional BasicGAGenome for Emily mid game (rounds 3-4).")]
    [SerializeField] private BasicGAGenome targetEmilyMidGenomeAsset;
    [Tooltip("Optional BasicGAGenome for Emily late game (rounds 5+).")]
    [SerializeField] private BasicGAGenome targetEmilyLateGenomeAsset;

    [Header("Auto Run")]
    [SerializeField] private bool autoRun = true;

    [Header("Log Output")]
    [SerializeField] private TextMeshProUGUI logOutput;
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

        _template = useEmilyTemplate
            ? (IEvolvableBrain)new EmilyAIBrain()
            : new OptimizerAIBrain();

        _ga = new EvolvableGeneticAlgo
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
            GameConfigData    = gameConfigData,
        };

        _ga.Initialize(_template, (genes, config) =>
        {
            if (useEmilyTemplate)
                return new MinEmilyBrain(genes, config);
            return new MinOptimizerBrain(genes, config);
        });

        string brainName = (_template as IPlayerAIBrain) != null
            ? (_template as IPlayerAIBrain).BrainName
            : (useEmilyTemplate ? "Emily" : "Optimizer");

        Debug.Log("//============= Starting Evolvable GA Training =============");
        Debug.Log($"Brain: {brainName}  Genes: {_template.GeneCount}");
        Debug.Log($"Population: {populationSize}  Generations: {generations}");
        Debug.Log("=============================================================");

        if (logOutput != null)
        {
            logOutput.text += "\n//============= Starting Evolvable GA Training =============";
            logOutput.text += $"\nBrain: {brainName}  Genes: {_template.GeneCount}";
            logOutput.text += $"\nPopulation: {populationSize}  Generations: {generations}";
            logOutput.text += "\n=============================================================";
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

        string summary = _ga.GetBestGenomeSummary();
        summary += $"\nElapsed: {sw.Elapsed.TotalSeconds:F1}s  " +
                   $"({(float)generations / sw.Elapsed.TotalSeconds:F1} gen/s)";

        Debug.Log(".oOo. EVOLVABLE GA TRAINING COMPLETE .oOo.");
        Debug.Log(summary);

        if (logOutput != null)
        {
            logOutput.text += "\n.oOo. EVOLVABLE GA TRAINING COMPLETE .oOo.";
            logOutput.text += $"\n{summary}";
        }

        if (saveToJson)
            SaveBestGenesToJson();

        if (useEmilyTemplate)
            ApplyBestEmilyGenesToAssets();
        else if (targetOptimizerGenomeAsset != null)
            ApplyBestGenesToAsset();

        _ga.Dispose();
    }

    private void SaveBestGenesToJson()
    {
        float[] genes = _ga.BestGenes;
        if (genes == null) return;

        if (_template == null)
            _template = useEmilyTemplate ? (IEvolvableBrain)new EmilyAIBrain() : new OptimizerAIBrain();

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"fitness\": {_ga.BestFitness:F4},");
        sb.AppendLine($"  \"geneCount\": {genes.Length},");
        sb.AppendLine("  \"genes\": {");
        for (int i = 0; i < genes.Length; i++)
        {
            string comma = i < genes.Length - 1 ? "," : "";
            sb.AppendLine($"    \"{_template.GetGeneName(i)}\": {genes[i]:F4}{comma}");
        }
        sb.AppendLine("  }");
        sb.AppendLine("}");

        string path = System.IO.Path.Combine(Application.persistentDataPath, "BestOptimizerGenome.json");
        System.IO.File.WriteAllText(path, sb.ToString());
        Debug.Log($"Best genome saved to: {path}");
        if (logOutput != null)
            logOutput.text += $"\nBest genome saved to: {path}";
    }

    //NOT FLEXIBLE, need 1 for each brain type.
    private void ApplyBestGenesToAsset()
    {
        float[] genes = _ga.BestGenes;
        if (useEmilyTemplate)
        {
            Debug.LogWarning("ApplyBestGenesToAsset is only supported for Optimizer template.");
            return;
        }

        if (genes == null || targetOptimizerGenomeAsset == null) return;

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

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(targetOptimizerGenomeAsset);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"Best genome applied to: {targetOptimizerGenomeAsset.name}");
        if (logOutput != null)
            logOutput.text += $"\nBest genome applied to: {targetOptimizerGenomeAsset.name}";
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

    [Tooltip("Required when BrainType is Optimizer. Assign a BasicGAGenome SO.")]
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