using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.0.3 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// 
// NEW alternative version for updated heuristics.
// The old heuristics were broken.
// Tries to evolve without harcoding the mutations.
// This is cleaner and more reusable but I keep the old one
// to avoid breaking some parts of the system.
//

#region NEW GENETIC ALGORITHM
public class EvolvableGeneticAlgo
{
    #region CONFIGURATION
    public int PopulationSize = 50;
    public int Generations = 100;
    public int GamesPerEval = 20;
    public float MutationRate = 0.2f;
    public float MutationAmount = 5f;
    public int SurvivorsCount = 5;
    public int TournamentSize = 3;
    public bool Coevolution = false;
    public GAOpponentConfig[] Opponents = { new GAOpponentConfig() };
    public int RandomSeed = 42;
    public bool LogEachGeneration = true;
    public int numPlayers = 2;
    public int ThreadCount = 1;
    public GameConfigData GameConfigData;
    public TextMeshProUGUI LogOutput { get; internal set; }
    #endregion

    #region FIELDS AND PROPERTIES

    private IEvolvableBrain _template;
    private int _geneCount;

    // Population stored as flat gene arrays (no ScriptableObject dependency).
    private float[][] _population;
    private float[] _fitness;
    private float[] _bestEverGenes;
    private float _bestEverFitness = float.MinValue;
    private System.Random _rand;
    private int _currentGeneration;

    private MinimalGameSimulator[] _threadSimulators;
    private System.Random[] _threadRngs;
    private GameConfigSnapshot _configSnapshot;

    // Factory delegate: creates a fresh IMinimalAIBrain from a gene array.
    public delegate IMinimalAIBrain BrainFactory(float[] genes, GameConfigSnapshot config);
    private BrainFactory _brainFactory;

    public float BestFitness => _bestEverFitness;
    public float[] BestGenes => _bestEverGenes;
    public int CurrentGeneration => _currentGeneration;
    public bool IsComplete => _currentGeneration >= Generations;
    #endregion

    #region INITIALIZATION

    public void Initialize(IEvolvableBrain template, BrainFactory brainFactory)
    {
        _template = template;
        _brainFactory = brainFactory;
        _geneCount = template.GeneCount;

        _rand = new System.Random(RandomSeed);
        _currentGeneration = 0;
        _bestEverFitness = float.MinValue;
        _bestEverGenes = null;

        if (GameConfigData != null)
            GameConfig.Initialize(GameConfigData);
        _configSnapshot = GameConfig.CreateSnapshot();
        if (GameConfigData != null)
            GameConfig.ResetToDefaults();

        _population = new float[PopulationSize][];
        for (int i = 0; i < PopulationSize; i++)
        {
            _population[i] = new float[_geneCount];
            _template.RandomInitialise(_population[i], _rand);
        }
        _fitness = new float[PopulationSize];

        _threadSimulators = new MinimalGameSimulator[] { new MinimalGameSimulator(_configSnapshot) };
        _threadRngs = new System.Random[] { new System.Random(RandomSeed + 1) };
    }

    public void Dispose()
    {
        _population = null;
        _bestEverGenes = null;
    }
    #endregion

    #region GENERATIONS

    public bool ExecuteGeneration()
    {
        if (IsComplete) return true;

        EvaluateFitness();
        UpdateBestEver();
        if (LogEachGeneration) LogGeneration();

        float[][] parents = TournamentSelection();
        float[][] offspring = Crossover(parents);
        MutatePopulation(offspring);
        NewGenerationFormation(offspring);

        _currentGeneration++;
        return IsComplete;
    }
    #endregion

    #region FITNESS EVALUATION

    private void EvaluateFitness()
    {
        int threads = Math.Max(1, ThreadCount);

        IMinimalAIBrain[] opponentBrains = null;
        if (!Coevolution && Opponents != null)
        {
            opponentBrains = new IMinimalAIBrain[Opponents.Length];
            for (int o = 0; o < Opponents.Length; o++)
                opponentBrains[o] = CreateMinimalBrain(Opponents[o]);
        }

        if (threads <= 1)
        {
            for (int i = 0; i < PopulationSize; i++)
                _fitness[i] = EvaluateGenome(i, opponentBrains,
                                             _threadSimulators[0], _threadRngs[0]);
        }
        else
        {
            int seedCounter = 0;
            var configSnap = _configSnapshot;
            var threadSim = new ThreadLocal<MinimalGameSimulator>(() => new MinimalGameSimulator(configSnap));
            var threadRng = new ThreadLocal<System.Random>(
                () => new System.Random(RandomSeed + Interlocked.Increment(ref seedCounter)));

            try
            {
                var rangePartitioner = Partitioner.Create(0, PopulationSize);
                var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = threads };

                Parallel.ForEach(rangePartitioner, parallelOptions,
                (range, loopState) =>
                {
                    var sim = threadSim.Value;
                    var rng = threadRng.Value;
                    for (int i = range.Item1; i < range.Item2; i++)
                        _fitness[i] = EvaluateGenome(i, opponentBrains, sim, rng);
                });
            }
            finally
            {
                threadSim.Dispose();
                threadRng.Dispose();
            }
        }
    }

    private float EvaluateGenome(int genomeIndex, IMinimalAIBrain[] opponentBrains,
                                 MinimalGameSimulator sim, System.Random rng)
    {
        IMinimalAIBrain candidate = _brainFactory(_population[genomeIndex], _configSnapshot);
        float totalScore = 0f;
        int totalGames = 0;

        int loopCount = Coevolution
            ? GamesPerEval
            : (opponentBrains != null ? opponentBrains.Length * GamesPerEval : GamesPerEval);

        for (int g = 0; g < loopCount; g++)
        {
            IMinimalAIBrain[] match = new IMinimalAIBrain[numPlayers];
            match[0] = candidate;

            for (int p = 1; p < numPlayers; p++)
            {
                if (Coevolution)
                {
                    int oppIdx = rng.Next(PopulationSize);
                    if (oppIdx == genomeIndex) oppIdx = (oppIdx + 1) % PopulationSize;
                    match[p] = _brainFactory(_population[oppIdx], _configSnapshot);
                }
                else
                {
                    int configIdx = (g / GamesPerEval) % opponentBrains.Length;
                    match[p] = opponentBrains[configIdx];
                }
            }

            MinimalSimulationGameResult result = sim.RunGame(match);
            totalScore += EvolvableScoreFromResult(result);
            totalGames++;
        }

        return totalGames > 0 ? totalScore / totalGames : 0f;
    }
    
    private static IMinimalAIBrain CreateMinimalBrain(GAOpponentConfig config)
    {
        switch (config.BrainType)
        {
            //case AIBrainType.GreedyAdjustable when config.Genome != null:
            //    return new MinimalGreedyBrain(config.Genome);
            //case AIBrainType.GreedyAdjustableEML when config.Genome != null:
            //    return new MinimalGreedyBrainEML(config.Genome);
            //case AIBrainType.Greedy:
            //    return new MinimalGreedyBrain();
            case AIBrainType.Optimizer when config.OptimizerGenome != null:
                return new MinOptimizerBrain(config.OptimizerGenome);
            case AIBrainType.Optimizer:
                return new MinOptimizerBrain();
            case AIBrainType.Emily:
                return new MinEmilyBrain(config.EmilyEarlyGenome, config.EmilyMidGenome, config.EmilyLateGenome);
            default:
                return new MinimalRandomBrain();
       }
    }

    private static float EvolvableScoreFromResult(MinimalSimulationGameResult result)
    {
        float base_score;
        if (result.IsDraw)
            base_score = 0.1f;
        else if (result.WinnerIndex == 0)
            base_score = 1.0f;
        else
            base_score = -0.5f;

        int myScore = result.Scores[0];
        int bestOppScore = 0;
        for (int i = 1; i < result.Scores.Length; i++)
            if (result.Scores[i] > bestOppScore)
                bestOppScore = result.Scores[i];

        float margin = (myScore - bestOppScore) / 100f;
        if (margin < -0.5f) margin = -0.5f;
        else if (margin > 0.5f) margin = 0.5f;
        if (myScore <= 30 && bestOppScore > 30) margin = -0.5f;
        return base_score + margin;
    }
    #endregion

    #region SELECTION

    private float[][] TournamentSelection()
    {
        float[][] parents = new float[PopulationSize][];
        for (int i = 0; i < PopulationSize; i++)
        {
            int bestIdx = -1;
            for (int t = 0; t < TournamentSize; t++)
            {
                int challenger = _rand.Next(PopulationSize);
                if (bestIdx == -1 || _fitness[challenger] > _fitness[bestIdx])
                    bestIdx = challenger;
            }
            parents[i] = _population[bestIdx];
        }
        return parents;
    }
    #endregion

    #region CROSSOVER

    private float[][] Crossover(float[][] parents)
    {
        float[][] offspring = new float[PopulationSize][];
        int count = 0;
        for (int i = 0; i < PopulationSize; i += 2)
        {
            float[] p1 = parents[i];
            float[] p2 = parents[(i + 1) % PopulationSize];
            if (p1 == p2) p2 = _population[_rand.Next(PopulationSize)];

            float alpha = (float)_rand.NextDouble();
            float[] c1 = new float[_geneCount];
            float[] c2 = new float[_geneCount];

            for (int g = 0; g < _geneCount; g++)
            {
                c1[g] = alpha * p1[g] + (1f - alpha) * p2[g];
                c2[g] = (1f - alpha) * p1[g] + alpha * p2[g];
            }

            offspring[count++] = c1;
            if (count < PopulationSize) offspring[count++] = c2;
        }
        return offspring;
    }
    #endregion

    #region MUTATION

    private void MutatePopulation(float[][] offspring)
    {
        for (int i = 0; i < offspring.Length; i++)
        {
            if (offspring[i] == null) continue;
            for (int g = 0; g < _geneCount; g++)
                offspring[i][g] = MutateGene(offspring[i][g]);
        }
    }

    private float MutateGene(float value)
    {
        if ((float)_rand.NextDouble() > MutationRate) return value;
        float delta = (float)(_rand.NextDouble() * 2.0 - 1.0) * MutationAmount;
        return Math.Max(0f, value + delta);
    }
    #endregion

    #region NEW GENERATION FORMATION

    private void NewGenerationFormation(float[][] offspring)
    {
        var sortedIndices = Enumerable.Range(0, PopulationSize)
            .OrderByDescending(i => _fitness[i]).ToList();

        int eliteCount = Math.Min(SurvivorsCount, PopulationSize);

        float[][] nextGen = new float[PopulationSize][];
        int idx = 0;

        for (int i = 0; i < eliteCount; i++)
        {
            nextGen[idx] = new float[_geneCount];
            Array.Copy(_population[sortedIndices[i]], nextGen[idx], _geneCount);
            idx++;
        }

        int outIdx = 0;
        while (idx < PopulationSize && outIdx < offspring.Length)
        {
            if (offspring[outIdx] != null)
            {
                nextGen[idx] = new float[_geneCount];
                Array.Copy(offspring[outIdx], nextGen[idx], _geneCount);
                idx++;
            }
            outIdx++;
        }

        _population = nextGen;
        _fitness = new float[PopulationSize];
    }
    #endregion

    #region BEST EVER TRACKING

    private void UpdateBestEver()
    {
        for (int i = 0; i < PopulationSize; i++)
        {
            if (_fitness[i] >= _bestEverFitness)
            {
                _bestEverFitness = _fitness[i];
                if (_bestEverGenes == null)
                    _bestEverGenes = new float[_geneCount];
                Array.Copy(_population[i], _bestEverGenes, _geneCount);
            }
        }
    }
    #endregion

    #region LOGGING

    private void LogGeneration()
    {
        float avg = 0f;
        float best = float.MinValue;
        foreach (float f in _fitness) { avg += f; if (f > best) best = f; }
        avg /= PopulationSize;
        Debug.Log($"Gen {_currentGeneration:D3} | Best: {best:F3}  Avg: {avg:F3}  BestEver: {_bestEverFitness:F3}");
        if (LogOutput != null)
            LogOutput.text += $"\nGen {_currentGeneration:D3} | Best: {best:F3}  Avg: {avg:F3}  BestEver: {_bestEverFitness:F3}";
    }

    public string GetBestGenomeSummary()
    {
        if (_bestEverGenes == null) return "No training run yet.";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"Best fitness: {_bestEverFitness:F4}");
        for (int i = 0; i < _geneCount; i++)
            sb.AppendLine($"{_template.GetGeneName(i)}: {_bestEverGenes[i]:F2}");
        return sb.ToString();
    }
    #endregion
}
#endregion

//DEPRECATED 15/04/2026.
//OLD VERSION TO BE DEPRECATED
/*

#region OLD GENETIC ALGORITHM (TO BE DEPRECATED)

/*
// Implements a genetic algorithm to evolve GAGenome instances
// TODO: TO BE DEPRECATED and replaced by EvolvableGeneticAlgo
public class GeneticAlgo
{
    #region UNITY EDITOR FIELDS
    [Header("Genetic Algorithm Parameters")]
    [Tooltip("Number of individuals in each generation.")]
    public int   PopulationSize    = 50;
    [Tooltip("Number of generations to run.")]
    public int   Generations       = 100;
    [Tooltip("Number of games to play for each genome during fitness evaluation.")]
    public int   GamesPerEval      = 20;
    [Tooltip("Probability of mutating each gene in an offspring.")]
    public float MutationRate      = 0.2f;
    [Tooltip("Amount by which each gene is mutated.")]
    public float MutationAmount    = 5f;
    [Tooltip("Number of top-performing individuals to retain each generation.")]
    public int   SurvivorsCount    = 5;
    [Tooltip("Number of individuals participating in each tournament selection.")]
    public int   TournamentSize    = 3;
    [Tooltip("Whether to use coevolution during training.")]
    public bool  Coevolution       = false;
    [Tooltip("List of opponents to play against during fitness evaluation. Ignored if Coevolution is true.")]
    public GAOpponentConfig[] Opponents = { new GAOpponentConfig() };
    [Tooltip("Random seed for reproducibility.")]
    public int   RandomSeed        = 42;
    [Tooltip("Whether to log fitness stats each generation.")]
    public bool  LogEachGeneration = true;
    [Tooltip("Number of players in the game (2 or more).")]
    public int numPlayers = 2;
    [Tooltip("Number of threads for parallel fitness evaluation. 1 = single-threaded.")]
    public int ThreadCount = 1;

    [Tooltip("When true, uses GreedyAdjustableEML (round-gated) instead of GreedyAdjustable for candidate brains.")]
    public bool UseEarlyMidLateStrategies = false;

    [Tooltip("Game config data to use for simulations. If null, global defaults are used.")]
    public GameConfigData GameConfigData;

    [Tooltip("Log output")]
    public TextMeshProUGUI LogOutput { get; internal set; }

    //TODO: Consider adding ELO system.

    #endregion
    #region FIELDS AND PROPERTIES
    private List<GAGenome> _population;
    private float[]        _fitness;
    private GAGenome       _bestEverGenome;
    private float          _bestEverFitness = float.MinValue;
    private System.Random  _rand;
    private int            _currentGeneration;

    private List<GAGenome> _genomePool;
    private int            _poolNextFree;

    private MinimalGameSimulator[] _threadSimulators;
    private System.Random[]        _threadRngs;


    // Config snapshot to be read by threads.
    // Necessary when the hardcoded defaults 
    // were removed.

    private GameConfigSnapshot _configSnapshot;

    //Public getters 
    public GAGenome BestGenome        => _bestEverGenome;
    public float    BestFitness       => _bestEverFitness;
    public int      CurrentGeneration => _currentGeneration;
    public bool     IsComplete        => _currentGeneration >= Generations;

    #endregion
    #region INITIALIZATION AND REMOVAL
    public void Initialize()
    {
        _rand              = new System.Random(RandomSeed);
        _currentGeneration = 0;
        _bestEverFitness   = float.MinValue;
        _bestEverGenome    = null;

        // Build config snapshot.
        if (GameConfigData != null)
            GameConfig.Initialize(GameConfigData);
        _configSnapshot = GameConfig.CreateSnapshot();
        if (GameConfigData != null)
            GameConfig.ResetToDefaults();

        //Pool for population to avoid instantiation during runtime
        int poolSize = PopulationSize * 2 + 1;
        _genomePool  = new List<GAGenome>(poolSize);
        for (int i = 0; i < poolSize; i++)
            _genomePool.Add(ScriptableObject.CreateInstance<GAGenome>());
        _poolNextFree = 0;

        // Build initial population from the pool
        _population = new List<GAGenome>(PopulationSize);
        for (int i = 0; i < PopulationSize; i++)
        {
            GAGenome g = AcquireGenome(); //Avoids too much instantiation.
            g.RandomInitialise(); 
            _population.Add(g);
        }

        _fitness = new float[PopulationSize];

        _threadSimulators = new MinimalGameSimulator[] { new MinimalGameSimulator(_configSnapshot) };
        _threadRngs       = new System.Random[] { new System.Random(RandomSeed + 1) };
    }
    #endregion
    #region GENOME POOL MANAGEMENT
    

    // Returns a genome from the pre-allocated pool. 
    private GAGenome AcquireGenome()
    {
        if (_poolNextFree < _genomePool.Count)
            return _genomePool[_poolNextFree++];

        //Expansion in case the pool runs out.
        GAGenome g = ScriptableObject.CreateInstance<GAGenome>();
        _genomePool.Add(g);
        _poolNextFree++;
        return g;
    }

    // Resets the pool cursor to 0
    private void ResetPool()
    {
        _poolNextFree = 0;
    }

    // Destroys all pooled ScriptableObjects. 
    // Avoid leaking memory.
    public void Dispose()
    {
        if (_genomePool != null)
        {
            foreach (var g in _genomePool)
                if (g != null) UnityEngine.Object.Destroy(g);
            _genomePool.Clear();
        }
        if (_bestEverGenome != null)
        {
            UnityEngine.Object.Destroy(_bestEverGenome);
            _bestEverGenome = null;
        }
    }

    #endregion
    #region GENERATIONS

    // Runs one full generation. 
    // Returns true when training is complete.
    public bool ExecuteGeneration()
    {
        if (IsComplete) return true;

        EvaluateFitness();
        UpdateBestEver();

        if (LogEachGeneration) LogGeneration();

        List<GAGenome> parents   = TournamentSelection();
        List<GAGenome> offspring = Crossover(parents);
        MutatePopulation(offspring);
        NewGenerationFormation(offspring);

        _currentGeneration++;
        return IsComplete;
    }

    #endregion
    #region FITNESS EVALUATION

    //Based on game results.
    private void EvaluateFitness()
    {
        int threads = 1;
        if (ThreadCount > 1) threads = ThreadCount;
        
        // Snapshot all genome weights
        GenomeWeights[] weightSnapshots = new GenomeWeights[PopulationSize];
        for (int i = 0; i < PopulationSize; i++)
            weightSnapshots[i] = GenomeWeights.FromGenome(_population[i]);

        // Pre-create opponent brains
        //Changed to minimal to be able to run it.
        IMinimalAIBrain[] opponentBrainTemplates = null;
        if (!Coevolution && Opponents != null)
        {
            opponentBrainTemplates = new IMinimalAIBrain[Opponents.Length];
            for (int o = 0; o < Opponents.Length; o++)
                opponentBrainTemplates[o] = CreateMinimalBrain(Opponents[o]);
        }

        if (threads <= 1)
        {
            for (int i = 0; i < PopulationSize; i++)
                _fitness[i] = EvaluateGenome(i, weightSnapshots, opponentBrainTemplates,
                                            _threadSimulators[0], _threadRngs[0]);
        }
        else
        {
            int seedCounter = 0;
            var configSnap = _configSnapshot; // capture for lambda
            var threadSim = new ThreadLocal<MinimalGameSimulator>(() => new MinimalGameSimulator(configSnap));
            var threadRng = new ThreadLocal<System.Random>(

                () => new System.Random(RandomSeed + Interlocked.Increment(ref seedCounter)));

            try
            {
                var rangePartitioner = Partitioner.Create(0, PopulationSize);
                var parallelOptions  = new ParallelOptions { MaxDegreeOfParallelism = threads };

                Parallel.ForEach(rangePartitioner, parallelOptions,
                (range, loopState) =>
                {
                    var sim = threadSim.Value;
                    var rng = threadRng.Value;

                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        _fitness[i] = EvaluateGenome(i, weightSnapshots, opponentBrainTemplates,
                                                    sim, rng);
                    }
                });
            }
            finally
            {
                threadSim.Dispose();
                threadRng.Dispose();
            }
        }
    }

    // Evaluates a single genome's fitness. 
    // Thread-safe.
    private float EvaluateGenome(int genomeIndex, GenomeWeights[] weights,
                                 IMinimalAIBrain[] opponentBrains,
                                 MinimalGameSimulator sim, System.Random rng)
    {
        IMinimalAIBrain candidate = UseEarlyMidLateStrategies
            ? (IMinimalAIBrain)new MinimalGreedyBrainEML(weights[genomeIndex])
            : new MinimalGreedyBrain(weights[genomeIndex]);
        float totalScore = 0f;
        int   totalGames = 0;

        int loopCount = Coevolution ? GamesPerEval : (opponentBrains != null ? opponentBrains.Length * GamesPerEval : GamesPerEval);

        for (int g = 0; g < loopCount; g++)
        {
            IMinimalAIBrain[] match = new IMinimalAIBrain[numPlayers];
            match[0] = candidate;

            for (int p = 1; p < numPlayers; p++)
            {
                if (Coevolution)
                {
                    int oppIdx = rng.Next(PopulationSize);
                    if (oppIdx == genomeIndex) oppIdx = (oppIdx + 1) % PopulationSize;
                    match[p] = UseEarlyMidLateStrategies
                        ? (IMinimalAIBrain)new MinimalGreedyBrainEML(weights[oppIdx])
                        : new MinimalGreedyBrain(weights[oppIdx]);
                }
                else
                {
                    int configIdx = (g / GamesPerEval) % opponentBrains.Length;
                    match[p] = opponentBrains[configIdx];
                }
            }

            MinimalSimulationGameResult result = sim.RunGame(match);
            totalScore += ScoreFromResult(result);
            totalGames++;
        }

        //Avoid 0 division
        return totalGames > 0 ? totalScore / totalGames : 0f;
       
    }

    // Creates the appropriate IMinimalAIBrain.
    // Based on game config.
    // Allows multiple game setups.
    private static IMinimalAIBrain CreateMinimalBrain(GAOpponentConfig config)
    {
        switch (config.BrainType)
        {
            case AIBrainType.GreedyAdjustable when config.Genome != null:
                return new MinimalGreedyBrain(config.Genome);
            case AIBrainType.GreedyAdjustableEML when config.Genome != null:
                return new MinimalGreedyBrainEML(config.Genome);
            case AIBrainType.Greedy:
                return new MinimalGreedyBrain();
            case AIBrainType.Optimizer when config.OptimizerGenome != null:
                return new MinOptimizerBrain(config.OptimizerGenome);
            case AIBrainType.Optimizer:
                return new MinOptimizerBrain();
            default:
                return new MinimalRandomBrain();
        }
    }

    // Converts a game result into a fitness value.
    // Thread-safe.
    private static float ScoreFromResult(MinimalSimulationGameResult result)
    {
        float base_score;
        if (result.IsDraw)
            base_score = 0.1f;
        else if (result.WinnerIndex == 0)
            base_score = 1.0f;
        else
            base_score = -0.5f;

        int myScore      = result.Scores[0];
        int bestOppScore = 0;
        for (int i = 1; i < result.Scores.Length; i++)
        {
            if (result.Scores[i] > bestOppScore)
                bestOppScore = result.Scores[i];
        }
        float margin = (myScore - bestOppScore) / 100f;
        if (margin < -0.5f) margin = -0.5f;
        else if (margin > 0.5f) margin = 0.5f;
        if (myScore <= 30 && bestOppScore >30) margin = -0.5f; //Extra penalty for losing with a low score.  
        return base_score + margin;
    }
    #endregion
    #region SELECTION

    // Performs tournament selection to choose parents for crossover.
    private List<GAGenome> TournamentSelection()
    {
        List<GAGenome> parents = new List<GAGenome>(PopulationSize);
        for (int i = 0; i < PopulationSize; i++)
        {
            int bestIdx = -1;
            for (int t = 0; t < TournamentSize; t++)
            {
                int challenger = _rand.Next(PopulationSize);
                if (bestIdx == -1 || _fitness[challenger] > _fitness[bestIdx])
                    bestIdx = challenger;
            }
            parents.Add(_population[bestIdx]);
        }
        return parents;
    }
    #endregion
    #region CROSSOVER


    // Arithmetic crossover: each offspring gene = alpha*p1 + (1-alpha)*p2.
    // Offspring are acquired from the genome pool (no new ScriptableObjects).
    private List<GAGenome> Crossover(List<GAGenome> parents)
    {
        List<GAGenome> offspring = new List<GAGenome>(PopulationSize);
        for (int i = 0; i < PopulationSize; i += 2)
        {
            GAGenome p1 = parents[i];
            GAGenome p2 = parents[(i + 1) % PopulationSize];
            if (p1 == p2) p2 = _population[_rand.Next(PopulationSize)];

            float alpha = (float)_rand.NextDouble();
            GAGenome c1 = AcquireGenome();
            GAGenome c2 = AcquireGenome();

            BlendGenes(p1, p2, alpha, c1);
            BlendGenes(p1, p2, 1f - alpha, c2);

            offspring.Add(c1);
            if (offspring.Count < PopulationSize) offspring.Add(c2);
        }
        return offspring;
    }


    // Blends the genes of two parent genomes
    private void BlendGenes(GAGenome a, GAGenome b, float alpha, GAGenome child)
    {
        child.placingWeight            = alpha * a.placingWeight            + (1-alpha) * b.placingWeight;
        child.endGameBonusWeight       = alpha * a.endGameBonusWeight       + (1-alpha) * b.endGameBonusWeight;
        child.denyOpponentWeight       = alpha * a.denyOpponentWeight       + (1-alpha) * b.denyOpponentWeight;
        child.pushingPenaltiesWeight   = alpha * a.pushingPenaltiesWeight   + (1-alpha) * b.pushingPenaltiesWeight;
        child.topRowsWeight            = alpha * a.topRowsWeight            + (1-alpha) * b.topRowsWeight;
        child.centralPlacementWeight   = alpha * a.centralPlacementWeight   + (1-alpha) * b.centralPlacementWeight;
        child.avoidPartialLinesWeight  = alpha * a.avoidPartialLinesWeight  + (1-alpha) * b.avoidPartialLinesWeight;
        child.penaltyPerflower           = alpha * a.penaltyPerflower           + (1-alpha) * b.penaltyPerflower;
        child.firstPlayerTokenBaseBonus= alpha * a.firstPlayerTokenBaseBonus+ (1-alpha) * b.firstPlayerTokenBaseBonus;
        child.denyColorBaseBonus       = alpha * a.denyColorBaseBonus       + (1-alpha) * b.denyColorBaseBonus;
        child.disruptExistingLineBonus = alpha * a.disruptExistingLineBonus + (1-alpha) * b.disruptExistingLineBonus;
        child.denyCompletionBonus      = alpha * a.denyCompletionBonus      + (1-alpha) * b.denyCompletionBonus;
        child.poisonColorBonus         = alpha * a.poisonColorBonus         + (1-alpha) * b.poisonColorBonus;
        child.oneMoveFillingBonus      = alpha * a.oneMoveFillingBonus      + (1-alpha) * b.oneMoveFillingBonus;

        int aIdx = _population.IndexOf(a);
        int bIdx = _population.IndexOf(b);
        float fitA = aIdx >= 0 ? _fitness[aIdx] : 0f;
        float fitB = bIdx >= 0 ? _fitness[bIdx] : 0f;
        //child.earlyRoundsThreshold = fitA >= fitB ? a.earlyRoundsThreshold : b.earlyRoundsThreshold;
    }

    #endregion
    #region MUTATION

  
    // Creates mutations in the offspring population by randomly
    // altering their genes based on MutationRate and MutationAmount.
    private void MutatePopulation(List<GAGenome> offspring)
    {
        foreach (GAGenome genome in offspring)
            MutateGenome(genome);
    }



    // Performs the mutation of a single genome's genes. 
    // Each gene has a chance to be altered by a random 
    //amount within MutationAmount.
    private void MutateGenome(GAGenome g)
    {
        g.placingWeight            = MutateGene(g.placingWeight);
        g.endGameBonusWeight       = MutateGene(g.endGameBonusWeight);
        g.denyOpponentWeight       = MutateGene(g.denyOpponentWeight);
        g.pushingPenaltiesWeight   = MutateGene(g.pushingPenaltiesWeight);
        g.topRowsWeight            = MutateGene(g.topRowsWeight);
        g.centralPlacementWeight   = MutateGene(g.centralPlacementWeight);
        g.avoidPartialLinesWeight  = MutateGene(g.avoidPartialLinesWeight);
        g.penaltyPerflower           = MutateGene(g.penaltyPerflower);
        g.firstPlayerTokenBaseBonus= MutateGene(g.firstPlayerTokenBaseBonus);
        g.denyColorBaseBonus       = MutateGene(g.denyColorBaseBonus);
        g.disruptExistingLineBonus = MutateGene(g.disruptExistingLineBonus);
        g.denyCompletionBonus      = MutateGene(g.denyCompletionBonus);
        g.poisonColorBonus         = MutateGene(g.poisonColorBonus);
        g.oneMoveFillingBonus      = MutateGene(g.oneMoveFillingBonus);

    }



    // Mutates a single gene value with a certain probability.
    // Uses mutation rate to determine if mutation occurs,
    // and mutation amount to determine the range of mutation.

    private float MutateGene(float value)
    {
        if ((float)_rand.NextDouble() > MutationRate) return value;
        float delta = (float)(_rand.NextDouble() * 2.0 - 1.0) * MutationAmount;
        return Math.Max(0f, value + delta);
    }

    #endregion
    #region NEW GENERATION FORMATION


    // Elitism: keep top SurvivorsCount genomes, fill the rest with offspring..
    private void NewGenerationFormation(List<GAGenome> offspring)
    {
        var sortedIndices = Enumerable.Range(0, PopulationSize)
            .OrderByDescending(i => _fitness[i]).ToList();

        // Snapshot elite gene values before resetting the pool
        int eliteCount = Math.Min(SurvivorsCount, PopulationSize);
        GAGenome[] eliteSnapshots = new GAGenome[eliteCount];
        for (int i = 0; i < eliteCount; i++)
            eliteSnapshots[i] = _population[sortedIndices[i]];

        // Offspring gene values are already in pool slots from Crossover().
        // Snapshot them as well before pool reset.
        GAGenome[] offspringSnapshots = new GAGenome[offspring.Count];
        for (int i = 0; i < offspring.Count; i++)
            offspringSnapshots[i] = offspring[i];

        // Reset pool cursor — all existing genomes become available for reuse
        ResetPool();

        // Build new population: elites first, then offspring
        List<GAGenome> nextGen = new List<GAGenome>(PopulationSize);

        for (int i = 0; i < eliteCount; i++)
        {
            GAGenome g = AcquireGenome();
            g.CopyFrom(eliteSnapshots[i]);
            nextGen.Add(g);
        }

        int outIdx = 0;
        while (nextGen.Count < PopulationSize && outIdx < offspringSnapshots.Length)
        {
            GAGenome g = AcquireGenome();
            g.CopyFrom(offspringSnapshots[outIdx++]);
            nextGen.Add(g);
        }

        _population = nextGen;
        _fitness    = new float[PopulationSize];
    }

    #endregion
    #region BEST EVER TRACKING

    // Updates the best genome and fitness found so far across all generations.
    // On ties keep the new one (latest found).(>=)

    private void UpdateBestEver()
    {
        for (int i = 0; i < PopulationSize; i++)
        {
            if (_fitness[i] >= _bestEverFitness)
            {
                _bestEverFitness = _fitness[i];
                // bestEverGenome lives outside the pool — it's a standalone clone
                if (_bestEverGenome == null)
                    _bestEverGenome = _population[i].Clone();
                else
                    _bestEverGenome.CopyFrom(_population[i]);
            }
        }
    }

    #endregion
    #region LOGGING


    // Creates a log entry for the current generation, 
    // including best fitness, average fitness, and best-ever fitness.

    private void LogGeneration()
    {
        float avg  = 0f;
        float best = float.MinValue;
        foreach (float f in _fitness) { avg += f; if (f > best) best = f; }
        avg /= PopulationSize;
        Debug.Log($"Gen {_currentGeneration:D3} | Best: {best:F3}  Avg: {avg:F3}  BestEver: {_bestEverFitness:F3}");
        if (LogOutput != null)
        {
            LogOutput.text += ($"\nGen {_currentGeneration:D3} | Best: {best:F3}  Avg: {avg:F3}  BestEver: {_bestEverFitness:F3}");
        }
    }


    // Generates a formatted summary of the best genome's fitness and parameter values.

    public string GetBestGenomeSummary()
    {
        if (_bestEverGenome == null) return "No training run yet.";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"Best fitness: {_bestEverFitness:F4}");
        sb.AppendLine($"placingWeight:             {_bestEverGenome.placingWeight:F2}");
        sb.AppendLine($"endGameBonusWeight:        {_bestEverGenome.endGameBonusWeight:F2}");
        sb.AppendLine($"denyOpponentWeight:        {_bestEverGenome.denyOpponentWeight:F2}");
        sb.AppendLine($"pushingPenaltiesWeight:    {_bestEverGenome.pushingPenaltiesWeight:F2}");
        sb.AppendLine($"topRowsWeight:             {_bestEverGenome.topRowsWeight:F2}");
        sb.AppendLine($"earlyRoundsThreshold:      {_bestEverGenome.earlyRoundsThreshold}");
        sb.AppendLine($"centralPlacementWeight:    {_bestEverGenome.centralPlacementWeight:F2}");
        sb.AppendLine($"avoidPartialLinesWeight:   {_bestEverGenome.avoidPartialLinesWeight:F2}");
        sb.AppendLine($"penaltyPerflower:            {_bestEverGenome.penaltyPerflower:F2}");
        sb.AppendLine($"firstPlayerTokenBonus:     {_bestEverGenome.firstPlayerTokenBaseBonus:F2}");
        sb.AppendLine($"denyColorBaseBonus:        {_bestEverGenome.denyColorBaseBonus:F2}");
        sb.AppendLine($"disruptExistingLineBonus:  {_bestEverGenome.disruptExistingLineBonus:F2}");
        sb.AppendLine($"denyCompletionBonus:       {_bestEverGenome.denyCompletionBonus:F2}");
        sb.AppendLine($"poisonColorBonus:          {_bestEverGenome.poisonColorBonus:F2}");
        sb.AppendLine($"oneMoveFillingBonus:       {_bestEverGenome.oneMoveFillingBonus:F2}");
        return sb.ToString();
    }
    #endregion
}
#endregion

#region AUXILIARY CLASSES AND STRUCTS


// Safe to read from threads.
// Lighter than GAGenome (ScriptableObject) 
public struct GenomeWeights
{
    public float placingWeight;
    public float endGameBonusWeight;
    public float denyOpponentWeight;
    public float pushingPenaltiesWeight;
    public float topRowsWeight;
    public int earlyRoundsThreshold;
    public float centralPlacementWeight;
    public float avoidPartialLinesWeight;
    public float penaltyPerflower;
    public float firstPlayerTokenBaseBonus;
    public float denyColorBaseBonus;
    public float disruptExistingLineBonus;
    public float denyCompletionBonus;
    public float poisonColorBonus;
    public float oneMoveFillingBonus;
    public float lineCompletionBonus;

    public static GenomeWeights FromGenome(GAGenome g)
    {
        return new GenomeWeights
        {
            placingWeight = g.placingWeight,
            endGameBonusWeight = g.endGameBonusWeight,
            denyOpponentWeight = g.denyOpponentWeight,
            pushingPenaltiesWeight = g.pushingPenaltiesWeight,
            topRowsWeight = g.topRowsWeight,
            earlyRoundsThreshold = g.earlyRoundsThreshold,
            centralPlacementWeight = g.centralPlacementWeight,
            avoidPartialLinesWeight = g.avoidPartialLinesWeight,
            penaltyPerflower = g.penaltyPerflower,
            firstPlayerTokenBaseBonus = g.firstPlayerTokenBaseBonus,
            denyColorBaseBonus = g.denyColorBaseBonus,
            disruptExistingLineBonus = g.disruptExistingLineBonus,
            denyCompletionBonus = g.denyCompletionBonus,
            poisonColorBonus = g.poisonColorBonus,
            oneMoveFillingBonus = g.oneMoveFillingBonus,
            lineCompletionBonus = g.lineCompletionBonus,
        };
    }
}



// Represents the configuration for an AI opponent 
// using a genetic algorithm or other AI brain types.

[System.Serializable]
public class GAOpponentConfig
{
    public AIBrainType BrainType = AIBrainType.Random;

    [Tooltip("Required when BrainType is GreedyAdjustable. Assign a GAGenome SO.")]
    public GAGenome Genome;

    [Tooltip("Required when BrainType is Optimizer. Assign a BasicGAGenome SO.")]
    public BasicGAGenome OptimizerGenome;

    public IPlayerAIBrain CreateBrain()
    {
        return AIBrainFactory.CreateBrain(BrainType, Genome, optimizerWeights: OptimizerGenome);
    }
}
*/

