//=^..^=   =^..^=   VERSION 1.0.3 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

using System.Collections.Generic;

// Emily brain: Optimizerbut with 3 genomes for Early, Mid and Late game.
#region EMILY BRAIN
public class EmilyAIBrain : IPlayerAIBrain, IEvolvableBrain
{
    private const int StageCount = 3;

    private readonly OptimizerAIBrain _worker;
    private readonly float[][] _stageGenes;
    private readonly int _genesPerStage;

    public string BrainName => "Emily";

    public EmilyAIBrain()
    {
        _worker = new OptimizerAIBrain();
        _genesPerStage = _worker.GeneCount;
        _stageGenes = new float[StageCount][];

        float[] seed = new float[_genesPerStage];
        _worker.GetGenes(seed);
        for (int s = 0; s < StageCount; s++)
        {
            _stageGenes[s] = new float[_genesPerStage];
            System.Array.Copy(seed, _stageGenes[s], _genesPerStage);
        }
    }

    public EmilyAIBrain(float[] genes) : this()
    {
        SetGenes(genes);
    }

    public EmilyAIBrain(BasicGAGenome early, BasicGAGenome mid, BasicGAGenome late) : this()
    {
        ApplyStageGenome(0, early);
        ApplyStageGenome(1, mid);
        ApplyStageGenome(2, late);
    }

    public GameMove ChooseMove(GameModel model, List<GameMove> validMoves)
    {
        int stage = GetStageIndexForRound(model.CurrentRound);
        _worker.SetGenes(_stageGenes[stage]);
        return _worker.ChooseMove(model, validMoves);
    }

    public int GeneCount => _genesPerStage * StageCount;

    public void GetGenes(float[] destination)
    {
        int idx = 0;
        for (int s = 0; s < StageCount; s++)
            for (int g = 0; g < _genesPerStage; g++)
                destination[idx++] = _stageGenes[s][g];
    }

    public void SetGenes(float[] source)
    {
        int expected = GeneCount;
        if (source == null || source.Length < expected) return;

        int idx = 0;
        for (int s = 0; s < StageCount; s++)
            for (int g = 0; g < _genesPerStage; g++)
                _stageGenes[s][g] = source[idx++];
    }

    public void RandomInitialise(float[] genes, System.Random rng)
    {
        for (int i = 0; i < genes.Length; i++)
            genes[i] = (float)(rng.NextDouble() * 20.0);
    }

    public string GetGeneName(int index)
    {
        int stage = index / _genesPerStage;
        int local = index % _genesPerStage;

        string prefix = stage == 0 ? "Round1to2" : (stage == 1 ? "Round3to4" : "Round5Plus");
        return prefix + "." + _worker.GetGeneName(local);
    }

    private static int GetStageIndexForRound(int round)
    {
        int r = round <= 0 ? 1 : round;
        if (r <= 2) return 0;
        if (r <= 4) return 1;
        return 2;
    }

    private void ApplyStageGenome(int stageIndex, BasicGAGenome genome)
    {
        if (genome == null || stageIndex < 0 || stageIndex >= StageCount)
            return;

        OptimizerAIBrain temp = new OptimizerAIBrain(genome);
        float[] genes = new float[_genesPerStage];
        temp.GetGenes(genes);
        _stageGenes[stageIndex] = genes;
    }
}
#endregion
// Minimal-model Emily for GA simulations.
#region MINIMAL EMILY BRAIN

public class MinEmilyBrain : IMinimalAIBrain, IEvolvableBrain
{
    private const int StageCount = 3;

    private readonly float[][] _stageGenes;
    private readonly int _genesPerStage;

    private GameConfigSnapshot _config;
    private MinOptimizerBrain[] _workers;

    public string BrainName => "Emily (Minimal)";

    public MinEmilyBrain()
    {
        _config = GameConfig.CreateSnapshot();

        OptimizerAIBrain template = new OptimizerAIBrain();
        _genesPerStage = template.GeneCount;

        _stageGenes = new float[StageCount][];
        float[] seed = new float[_genesPerStage];
        template.GetGenes(seed);
        for (int s = 0; s < StageCount; s++)
        {
            _stageGenes[s] = new float[_genesPerStage];
            System.Array.Copy(seed, _stageGenes[s], _genesPerStage);
        }

        BuildWorkers();
    }

    public MinEmilyBrain(float[] genes, GameConfigSnapshot config) : this()
    {
        if (config != null)
            _config = config;
        SetGenes(genes);
        BuildWorkers();
    }

    public MinEmilyBrain(BasicGAGenome early, BasicGAGenome mid, BasicGAGenome late) : this()
    {
        ApplyStageGenome(0, early);
        ApplyStageGenome(1, mid);
        ApplyStageGenome(2, late);
        BuildWorkers();
    }

    public int ChooseMoveIndex(MinimalGM model, GameMove[] moves, int moveCount)
    {
        int stage = GetStageIndexForRound(model.CurrentRound);
        return _workers[stage].ChooseMoveIndex(model, moves, moveCount);
    }

    public int GeneCount => _genesPerStage * StageCount;

    public void GetGenes(float[] destination)
    {
        int idx = 0;
        for (int s = 0; s < StageCount; s++)
            for (int g = 0; g < _genesPerStage; g++)
                destination[idx++] = _stageGenes[s][g];
    }

    public void SetGenes(float[] source)
    {
        int expected = GeneCount;
        if (source == null || source.Length < expected) return;

        int idx = 0;
        for (int s = 0; s < StageCount; s++)
            for (int g = 0; g < _genesPerStage; g++)
                _stageGenes[s][g] = source[idx++];

        BuildWorkers();
    }

    public void RandomInitialise(float[] genes, System.Random rng)
    {
        for (int i = 0; i < genes.Length; i++)
            genes[i] = (float)(rng.NextDouble() * 20.0);
    }

    public string GetGeneName(int index)
    {
        int stage = index / _genesPerStage;
        int local = index % _genesPerStage;

        string prefix = stage == 0 ? "Round1to2" : (stage == 1 ? "Round3to4" : "Round5Plus");
        return prefix + "." + new OptimizerAIBrain().GetGeneName(local);
    }

    private void BuildWorkers()
    {
        _workers = new MinOptimizerBrain[StageCount];
        for (int s = 0; s < StageCount; s++)
            _workers[s] = new MinOptimizerBrain(_stageGenes[s], _config);
    }

    private static int GetStageIndexForRound(int round)
    {
        int r = round <= 0 ? 1 : round;
        if (r <= 2) return 0;
        if (r <= 4) return 1;
        return 2;
    }

    private void ApplyStageGenome(int stage, BasicGAGenome genome)
    {
        if (genome == null) return;

        OptimizerAIBrain temp = new OptimizerAIBrain(genome);
        float[] genes = new float[_genesPerStage];
        temp.GetGenes(genes);
        System.Array.Copy(genes, _stageGenes[stage], _genesPerStage);
    }
}

#endregion