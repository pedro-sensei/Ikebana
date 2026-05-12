using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
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

public enum BestGenomeSelectionMethod
{
    BestEver,
    BestLastGen,
    Highlander
}

public class EvolvableGeneticAlgo
{
    #region CONFIGURATION
    public int PopulationSize = 50;
    public int Generations = 100;
    public int GamesPerEval = 20;
    public float MutationRate = 0.2f;
    public float MutationAmount = 5f;
    public int EliteCount = 5;
    public int TournamentSize = 3;
    public bool Coevolution = false;
    public GAOpponentConfig[] Opponents = { new GAOpponentConfig() };
    public int RandomSeed = 42;
    public bool LogEachGeneration = true;
    public int numPlayers = 2;
    public int ThreadCount = 1;
    public GameConfigData GameConfigData;
    public BestGenomeSelectionMethod BestGenomeSelection = BestGenomeSelectionMethod.BestEver;
    public int HighlanderChampionsPerGeneration = 1;
    public int HighlanderFinalOpponentCount = 3;
    public LogSystem LogOutput { get; internal set; }
    #endregion

    #region FIELDS AND PROPERTIES

    private IEvolvableBrain _template;
    private int _geneCount;

    // Population stored as flat gene arrays (no ScriptableObject dependency).
    private float[][] _population;
    private float[] _fitness;
    private float[] _bestEverGenes;
    private float _bestEverFitness = float.MinValue;
    private float[] _bestLastGenerationGenes;
    private float _bestLastGenerationFitness = float.MinValue;
    private int _bestLastGenerationGeneration = -1;
    private float[] _selectedBestGenes;
    private float _selectedBestFitness = float.MinValue;
    private int _selectedBestGeneration = -1;
    private System.Random _rand;
    private int _currentGeneration;
    private int _bestEverGeneration = -1;
    private bool _highlanderResolved;

    private int _totalGamesPlayed;
    private int _abortedGames;
    private int _gamesLessThanFiveRounds;
    private int _gamesZeroPointsBothPlayers;

    private MinimalGameSimulator[] _threadSimulators;
    private System.Random[] _threadRngs;
    private GameConfigSnapshot _configSnapshot;
    private List<GenerationChampion> _highlanderChampions;

    // Factory delegate: creates a fresh IMinimalAIBrain from a gene array.
    public delegate IMinimalAIBrain BrainFactory(float[] genes, GameConfigSnapshot config);
    private BrainFactory _brainFactory;

    private sealed class GenerationChampion
    {
        public float[] Genes;
        public float Fitness;
        public int Generation;
    }

    public float BestFitness
    {
        get
        {
            EnsureBestGenomeSelectionResolved();
            return _selectedBestFitness;
        }
    }

    public float[] BestGenes
    {
        get
        {
            EnsureBestGenomeSelectionResolved();
            return _selectedBestGenes;
        }
    }
    public int CurrentGeneration => _currentGeneration;
    public int BestGeneration
    {
        get
        {
            EnsureBestGenomeSelectionResolved();
            return _selectedBestGeneration;
        }
    }
    public bool IsComplete => _currentGeneration >= Generations;
    public int TotalGamesPlayed => _totalGamesPlayed;
    public int AbortedGames => _abortedGames;
    public int GamesLessThanFiveRounds => _gamesLessThanFiveRounds;
    public int GamesZeroPointsBothPlayers => _gamesZeroPointsBothPlayers;
    #endregion

    #region INITIALIZATION

    public void Initialize(IEvolvableBrain template, BrainFactory brainFactory)
    {
        _template = template;
        _brainFactory = brainFactory;
        _geneCount = template.GeneCount;

        _rand = new System.Random(RandomSeed);
        ResetTrainingState();

        _configSnapshot = CreateTrainingConfigSnapshot();

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

    private void ResetTrainingState()
    {
        _currentGeneration = 0;
        _bestEverFitness = float.MinValue;
        _bestEverGenes = null;
        _bestEverGeneration = -1;
        _bestLastGenerationGenes = null;
        _bestLastGenerationFitness = float.MinValue;
        _bestLastGenerationGeneration = -1;
        _selectedBestGenes = null;
        _selectedBestFitness = float.MinValue;
        _selectedBestGeneration = -1;
        _highlanderResolved = false;
        _highlanderChampions = new List<GenerationChampion>();
        _totalGamesPlayed = 0;
        _abortedGames = 0;
        _gamesLessThanFiveRounds = 0;
        _gamesZeroPointsBothPlayers = 0;
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
        UpdateBestGenomeSelectionState();
        if (LogEachGeneration) LogGeneration();

        float[][] parents = TournamentSelection();
        float[][] offspring = Crossover(parents);
        MutatePopulation(offspring);
        NewGenerationFormation(offspring);

        _currentGeneration++;
        if (IsComplete)
            FinalizeBestGenomeSelection();

        return IsComplete;
    }
    #endregion

    #region FITNESS EVALUATION

    private void EvaluateFitness()
    {
        int threads = Math.Max(1, ThreadCount);

        IMinimalAIBrain[] opponentBrains = CreateOpponentBrains();

        if (threads <= 1)
        {
            EvaluateFitnessSingleThread(opponentBrains);
        }
        else
        {
            EvaluateFitnessParallel(opponentBrains, threads);
        }
    }

    private IMinimalAIBrain[] CreateOpponentBrains()
    {
        if (Coevolution)
            return null;

        if (Opponents == null)
            return null;

        IMinimalAIBrain[] opponentBrains = new IMinimalAIBrain[Opponents.Length];
        for (int i = 0; i < Opponents.Length; i++)
            opponentBrains[i] = CreateMinimalBrain(Opponents[i], _configSnapshot);

        return opponentBrains;
    }

    private void EvaluateFitnessSingleThread(IMinimalAIBrain[] opponentBrains)
    {
        for (int i = 0; i < PopulationSize; i++)
        {
            _fitness[i] = EvaluateGenome(i, opponentBrains, _threadSimulators[0], _threadRngs[0]);
        }
    }

    private void EvaluateFitnessParallel(IMinimalAIBrain[] opponentBrains, int threads)
    {
        int seedCounter = 0;
        GameConfigSnapshot configSnapshot = _configSnapshot;
        ThreadLocal<MinimalGameSimulator> threadSimulators =
            new ThreadLocal<MinimalGameSimulator>(delegate { return new MinimalGameSimulator(configSnapshot); });
        ThreadLocal<System.Random> threadRandoms = new ThreadLocal<System.Random>(
            delegate { return new System.Random(RandomSeed + Interlocked.Increment(ref seedCounter)); });

        try
        {
            OrderablePartitioner<Tuple<int, int>> populationRanges = Partitioner.Create(0, PopulationSize);
            ParallelOptions options = new ParallelOptions();
            options.MaxDegreeOfParallelism = threads;

            Parallel.ForEach(populationRanges, options, delegate (Tuple<int, int> range, ParallelLoopState loopState)
            {
                MinimalGameSimulator simulator = threadSimulators.Value;
                System.Random rng = threadRandoms.Value;

                for (int i = range.Item1; i < range.Item2; i++)
                    _fitness[i] = EvaluateGenome(i, opponentBrains, simulator, rng);
            });
        }
        finally
        {
            threadSimulators.Dispose();
            threadRandoms.Dispose();
        }
    }

    private float EvaluateGenome(int genomeIndex, IMinimalAIBrain[] opponentBrains,
                                 MinimalGameSimulator sim, System.Random rng)
    {
        IMinimalAIBrain candidate = _brainFactory(_population[genomeIndex], _configSnapshot);
        float totalScore = 0f;
        int totalGames = 0;
        int localPlayed = 0;
        int localAborted = 0;
        int localShortGames = 0;
        int localZeroGames = 0;

        int loopCount = GetEvaluationCount(opponentBrains);

        for (int g = 0; g < loopCount; g++)
        {
            IMinimalAIBrain[] match = CreateMatchup(candidate, genomeIndex, g, opponentBrains, rng);

            localPlayed++;

            try
            {
                MinimalSimulationGameResult result = sim.RunGame(match);
                if (result.IsAborted)
                    localAborted++;
                if (result.EndedInLessThanFiveRounds)
                    localShortGames++;
                if (result.AllPlayersHaveZeroScore)
                    localZeroGames++;

                totalScore += EvolvableScoreFromResult(result);
                totalGames++;
            }
            catch (Exception ex)
            {
                localAborted++;
                totalScore -= 1f;
                totalGames++;
                Debug.LogWarning($"[EvolvableGeneticAlgo] Game aborted due to exception while evaluating genome {genomeIndex}: {ex.Message}");
            }
        }

        Interlocked.Add(ref _totalGamesPlayed, localPlayed);
        Interlocked.Add(ref _abortedGames, localAborted);
        Interlocked.Add(ref _gamesLessThanFiveRounds, localShortGames);
        Interlocked.Add(ref _gamesZeroPointsBothPlayers, localZeroGames);

        if (totalGames > 0)
            return totalScore / totalGames;

        return 0f;
    }

    private int GetEvaluationCount(IMinimalAIBrain[] opponentBrains)
    {
        if (Coevolution)
            return GamesPerEval;

        if (opponentBrains == null)
            return GamesPerEval;

        return opponentBrains.Length * GamesPerEval;
    }

    private IMinimalAIBrain[] CreateMatchup(IMinimalAIBrain candidate, int genomeIndex, int gameIndex,
                                            IMinimalAIBrain[] opponentBrains, System.Random rng)
    {
        IMinimalAIBrain[] match = new IMinimalAIBrain[numPlayers];
        match[0] = candidate;

        // Seat 0 is always the candidate so the fitness extraction remains stable.
        for (int playerIndex = 1; playerIndex < numPlayers; playerIndex++)
        {
            match[playerIndex] = CreateOpponentForMatch(genomeIndex, gameIndex, opponentBrains, rng);
        }

        return match;
    }

    private IMinimalAIBrain CreateOpponentForMatch(int genomeIndex, int gameIndex,
                                                   IMinimalAIBrain[] opponentBrains, System.Random rng)
    {
        if (Coevolution)
            return CreateCoevolutionOpponent(genomeIndex, rng);

        return GetConfiguredOpponent(gameIndex, opponentBrains);
    }

    private IMinimalAIBrain CreateCoevolutionOpponent(int genomeIndex, System.Random rng)
    {
        int opponentIndex = rng.Next(PopulationSize);
        if (opponentIndex == genomeIndex)
            opponentIndex = (opponentIndex + 1) % PopulationSize;

        return _brainFactory(_population[opponentIndex], _configSnapshot);
    }

    private IMinimalAIBrain GetConfiguredOpponent(int gameIndex, IMinimalAIBrain[] opponentBrains)
    {
        if (opponentBrains == null || opponentBrains.Length == 0)
            return new MinimalRandomBrain();

        int opponentIndex = (gameIndex / GamesPerEval) % opponentBrains.Length;
        return opponentBrains[opponentIndex];
    }
    
    private GameConfigSnapshot CreateTrainingConfigSnapshot()
    {
        if (GameConfigData == null)
            return GameConfig.CreateSnapshot();

        int originalNumPlayers = GameConfigData.NumPlayers;
        int originalNumFactories = GameConfigData.NumFactories;

        try
        {
            GameConfigData.NumPlayers = numPlayers;
            if (!GameConfigData.NonStandardSetups)
                GameConfigData.NumFactories = GameConfig.GetDisplayCount(numPlayers);

            GameConfig.Initialize(GameConfigData);
            return GameConfig.CreateSnapshot();
        }
        finally
        {
            GameConfig.ResetToDefaults();
            GameConfigData.NumPlayers = originalNumPlayers;
            GameConfigData.NumFactories = originalNumFactories;
        }
    }

    private static IMinimalAIBrain CreateMinimalBrain(GAOpponentConfig config, GameConfigSnapshot configSnapshot)
    {
        switch (config.BrainType)
        {
            //case AIBrainType.GreedyAdjustable when config.Genome != null:
            //    return new MinimalGreedyBrain(config.Genome);
            //case AIBrainType.GreedyAdjustableEML when config.Genome != null:
            //    return new MinimalGreedyBrainEML(config.Genome);
            //case AIBrainType.Greedy:
            //    return new MinimalGreedyBrain();
            case AIBrainType.Friendly:
                return CreateFriendlyBrain(config, configSnapshot);
            case AIBrainType.Optimizer:
                return CreateOptimizerBrain(config, configSnapshot);
            case AIBrainType.Emily:
                return new MinEmilyBrain(config.EmilyEarlyGenome, config.EmilyMidGenome, config.EmilyLateGenome, configSnapshot);
            default:
                return new MinimalRandomBrain();
       }
    }

    private static IMinimalAIBrain CreateFriendlyBrain(GAOpponentConfig config, GameConfigSnapshot configSnapshot)
    {
        if (config.OptimizerGenome == null)
            return new MinFriendlyBrain((float[])null, configSnapshot);

        float[] genes = new float[]
        {
            config.OptimizerGenome.SimulateScoringWeight,
            config.OptimizerGenome.PenaltyWeight,
            config.OptimizerGenome.TilesPlacedWeight,
            config.OptimizerGenome.LineCompletionWeight
        };

        return new MinFriendlyBrain(genes, configSnapshot);
    }

    private static IMinimalAIBrain CreateOptimizerBrain(GAOpponentConfig config, GameConfigSnapshot configSnapshot)
    {
        if (config.OptimizerGenome == null)
            return new MinOptimizerBrain(configSnapshot);

        return new MinOptimizerBrain(config.OptimizerGenome, configSnapshot);
    }

    private static float EvolvableScoreFromResult(MinimalSimulationGameResult result)
    {
        float baseScore = GetBaseScore(result);

        int myScore = result.Scores[0];
        int bestOppScore = GetBestOpponentScore(result.Scores);
        float margin = ClampMargin((myScore - bestOppScore) / 100f);

        if (myScore <= 30 && bestOppScore > 30)
            margin = -0.5f;

        return baseScore + margin;
    }

    private static float GetBaseScore(MinimalSimulationGameResult result)
    {
        if (result.IsDraw)
            return 0.1f;

        if (result.WinnerIndex == 0)
            return 1.0f;

        return -0.5f;
    }

    private static int GetBestOpponentScore(int[] scores)
    {
        int bestOpponentScore = 0;
        for (int i = 1; i < scores.Length; i++)
        {
            if (scores[i] > bestOpponentScore)
                bestOpponentScore = scores[i];
        }

        return bestOpponentScore;
    }

    private static float ClampMargin(float margin)
    {
        if (margin < -0.5f)
            return -0.5f;

        if (margin > 0.5f)
            return 0.5f;

        return margin;
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
            float[] p2 = GetSecondParent(parents, i, p1);

            float alpha = (float)_rand.NextDouble();
            float[] c1 = new float[_geneCount];
            float[] c2 = new float[_geneCount];

            for (int g = 0; g < _geneCount; g++)
            {
                c1[g] = alpha * p1[g] + (1f - alpha) * p2[g];
                c2[g] = (1f - alpha) * p1[g] + alpha * p2[g];
            }

            offspring[count++] = c1;
            if (count < PopulationSize)
                offspring[count++] = c2;
        }
        return offspring;
    }

    private float[] GetSecondParent(float[][] parents, int firstParentIndex, float[] firstParent)
    {
        float[] secondParent = parents[(firstParentIndex + 1) % PopulationSize];
        if (secondParent == firstParent)
            return _population[_rand.Next(PopulationSize)];

        return secondParent;
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
        if ((float)_rand.NextDouble() > MutationRate)
            return value;

        float delta = (float)(_rand.NextDouble() * 2.0 - 1.0) * MutationAmount;
        return Math.Max(0f, value + delta);
    }
    #endregion

    #region NEW GENERATION FORMATION

    private void NewGenerationFormation(float[][] offspring)
    {
        List<int> sortedIndices = Enumerable.Range(0, PopulationSize)
            .OrderByDescending(i => _fitness[i]).ToList();

        int eliteCount = Math.Min(EliteCount, PopulationSize);

        float[][] nextGen = new float[PopulationSize][];
        int idx = 0;

        // Elitism keeps the strongest genomes unchanged before filling with offspring.
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

    private void UpdateBestGenomeSelectionState()
    {
        UpdateBestEver();
        UpdateBestLastGeneration();
        StoreHighlanderChampionsForGeneration();
        UpdateSelectedBestForTrainingMode();
    }

    private void UpdateBestEver()
    {
        for (int i = 0; i < PopulationSize; i++)
        {
            if (_fitness[i] >= _bestEverFitness)
            {
                _bestEverFitness = _fitness[i];
                _bestEverGeneration = _currentGeneration;
                if (_bestEverGenes == null)
                    _bestEverGenes = new float[_geneCount];
                Array.Copy(_population[i], _bestEverGenes, _geneCount);
            }
        }
    }

    private void UpdateBestLastGeneration()
    {
        int bestIndex = GetBestGenomeIndex();
        if (bestIndex < 0)
            return;

        _bestLastGenerationFitness = _fitness[bestIndex];
        _bestLastGenerationGeneration = _currentGeneration;
        _bestLastGenerationGenes = CloneGenes(_population[bestIndex]);
    }

    private void StoreHighlanderChampionsForGeneration()
    {
        if (_highlanderChampions == null)
            return;

        int championsToKeep = Math.Min(HighlanderChampionsPerGeneration, PopulationSize);
        if (championsToKeep <= 0)
            return;

        List<int> sortedIndices = Enumerable.Range(0, PopulationSize)
            .OrderByDescending(i => _fitness[i]).ToList();

        for (int i = 0; i < championsToKeep; i++)
        {
            int genomeIndex = sortedIndices[i];
            GenerationChampion champion = new GenerationChampion();
            champion.Genes = CloneGenes(_population[genomeIndex]);
            champion.Fitness = _fitness[genomeIndex];
            champion.Generation = _currentGeneration;
            _highlanderChampions.Add(champion);
        }
    }

    private void UpdateSelectedBestForTrainingMode()
    {
        if (BestGenomeSelection == BestGenomeSelectionMethod.BestEver)
        {
            SetSelectedBestGenome(_bestEverGenes, _bestEverFitness, _bestEverGeneration);
            return;
        }

        if (BestGenomeSelection == BestGenomeSelectionMethod.BestLastGen)
        {
            SetSelectedBestGenome(_bestLastGenerationGenes, _bestLastGenerationFitness, _bestLastGenerationGeneration);
        }
    }

    private void FinalizeBestGenomeSelection()
    {
        if (BestGenomeSelection == BestGenomeSelectionMethod.Highlander)
        {
            ResolveHighlanderChampion();
            return;
        }

        UpdateSelectedBestForTrainingMode();
    }

    private void EnsureBestGenomeSelectionResolved()
    {
        if (!IsComplete)
            return;

        if (BestGenomeSelection == BestGenomeSelectionMethod.Highlander && !_highlanderResolved)
        {
            ResolveHighlanderChampion();
            return;
        }

        if (_selectedBestGenes == null)
            FinalizeBestGenomeSelection();
    }

    private void ResolveHighlanderChampion()
    {
        _highlanderResolved = true;

        if (_highlanderChampions == null || _highlanderChampions.Count == 0)
        {
            SetSelectedBestGenome(_bestLastGenerationGenes, _bestLastGenerationFitness, _bestLastGenerationGeneration);
            return;
        }

        if (_highlanderChampions.Count == 1)
        {
            GenerationChampion onlyChampion = _highlanderChampions[0];
            SetSelectedBestGenome(onlyChampion.Genes, onlyChampion.Fitness, onlyChampion.Generation);
            return;
        }

        GenerationChampion bestChampion = null;
        float bestChampionFitness = float.MinValue;

        for (int i = 0; i < _highlanderChampions.Count; i++)
        {
            GenerationChampion candidate = _highlanderChampions[i];
            List<GenerationChampion> opponentPool = SelectHighlanderOpponentPool(i);
            float tournamentFitness = EvaluateHighlanderChampion(candidate, opponentPool);

            if (bestChampion == null || tournamentFitness >= bestChampionFitness)
            {
                bestChampion = candidate;
                bestChampionFitness = tournamentFitness;
            }
        }

        if (bestChampion != null)
            SetSelectedBestGenome(bestChampion.Genes, bestChampionFitness, bestChampion.Generation);
    }

    private List<GenerationChampion> SelectHighlanderOpponentPool(int championIndex)
    {
        List<GenerationChampion> pool = new List<GenerationChampion>();
        List<int> availableIndices = new List<int>();

        for (int i = 0; i < _highlanderChampions.Count; i++)
        {
            if (i != championIndex)
                availableIndices.Add(i);
        }

        int opponentsToKeep = Math.Min(HighlanderFinalOpponentCount, availableIndices.Count);
        for (int i = 0; i < opponentsToKeep; i++)
        {
            int pickedIndex = _rand.Next(availableIndices.Count);
            int championPoolIndex = availableIndices[pickedIndex];
            pool.Add(_highlanderChampions[championPoolIndex]);
            availableIndices.RemoveAt(pickedIndex);
        }

        return pool;
    }

    private float EvaluateHighlanderChampion(GenerationChampion champion, List<GenerationChampion> opponentPool)
    {
        if (opponentPool == null || opponentPool.Count == 0)
            return champion.Fitness;

        MinimalGameSimulator simulator = _threadSimulators[0];
        System.Random rng = _threadRngs[0];
        IMinimalAIBrain championBrain = _brainFactory(champion.Genes, _configSnapshot);
        float totalScore = 0f;
        int totalGames = 0;

        // Final Highlander games use a fixed pool per champion so all finalists are judged under comparable pressure.
        for (int gameIndex = 0; gameIndex < GamesPerEval; gameIndex++)
        {
            IMinimalAIBrain[] match = CreateHighlanderMatch(championBrain, opponentPool, gameIndex, rng);
            MinimalSimulationGameResult result = simulator.RunGame(match);

            totalScore += EvolvableScoreFromResult(result);
            totalGames++;
        }

        if (totalGames > 0)
            return totalScore / totalGames;

        return 0f;
    }

    private IMinimalAIBrain[] CreateHighlanderMatch(IMinimalAIBrain championBrain, List<GenerationChampion> opponentPool,
                                                    int gameIndex, System.Random rng)
    {
        IMinimalAIBrain[] match = new IMinimalAIBrain[numPlayers];
        match[0] = championBrain;

        for (int playerIndex = 1; playerIndex < numPlayers; playerIndex++)
        {
            int opponentIndex = rng.Next(opponentPool.Count);
            if (opponentPool.Count > 1)
                opponentIndex = (opponentIndex + gameIndex + playerIndex - 1) % opponentPool.Count;

            match[playerIndex] = _brainFactory(opponentPool[opponentIndex].Genes, _configSnapshot);
        }

        return match;
    }

    private int GetBestGenomeIndex()
    {
        int bestIndex = -1;
        for (int i = 0; i < PopulationSize; i++)
        {
            if (bestIndex == -1 || _fitness[i] > _fitness[bestIndex])
                bestIndex = i;
        }

        return bestIndex;
    }

    private float[] CloneGenes(float[] genes)
    {
        if (genes == null)
            return null;

        float[] copy = new float[genes.Length];
        Array.Copy(genes, copy, genes.Length);
        return copy;
    }

    private void SetSelectedBestGenome(float[] genes, float fitness, int generation)
    {
        _selectedBestGenes = CloneGenes(genes);
        _selectedBestFitness = fitness;
        _selectedBestGeneration = generation;
    }
    #endregion

    #region LOGGING

    private void LogGeneration()
    {
        float averageFitness;
        float bestFitness;
        CalculateGenerationStats(out averageFitness, out bestFitness);

        string message = $"Gen {_currentGeneration:D3} | Best: {bestFitness:F3}  Avg: {averageFitness:F3}  BestEver: {_bestEverFitness:F3}";
        Debug.Log(message);
        if (LogOutput != null)
            LogOutput.AddLog(message);
    }

    private void CalculateGenerationStats(out float averageFitness, out float bestFitness)
    {
        float totalFitness = 0f;
        bestFitness = float.MinValue;

        foreach (float fitnessValue in _fitness)
        {
            totalFitness += fitnessValue;
            if (fitnessValue > bestFitness)
                bestFitness = fitnessValue;
        }

        averageFitness = totalFitness / PopulationSize;
    }

    public string GetBestGenomeSummary()
    {
        EnsureBestGenomeSelectionResolved();

        if (_selectedBestGenes == null)
            return "No training run yet.";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"Best selection method: {BestGenomeSelection}");
        sb.AppendLine($"Best fitness: {_selectedBestFitness:F4}");
        sb.AppendLine($"Best generation: {_selectedBestGeneration + 1}");
        for (int i = 0; i < _geneCount; i++)
            sb.AppendLine($"{_template.GetGeneName(i)}: {_selectedBestGenes[i]:F2}");
        return sb.ToString();
    }

    public string GetRunStatisticsSummary()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("GA run statistics:");
        sb.AppendLine($"Games played: {_totalGamesPlayed}");
        sb.AppendLine($"Games aborted (errors or safety limits): {_abortedGames}");
        sb.AppendLine($"Games with less than 5 rounds: {_gamesLessThanFiveRounds}");
        sb.AppendLine($"Games with 0 points for both players: {_gamesZeroPointsBothPlayers}");
        return sb.ToString();
    }
    #endregion
}
#endregion