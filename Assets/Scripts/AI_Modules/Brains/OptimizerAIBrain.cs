using System;
using System.Collections.Generic;
using Unity.InferenceEngine;

//=^..^=   =^..^=   VERSION 1.0.3 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

//Optimizer brain: uses the heuristics and adjustable weights.
//Requires a SO with the weights or use defaults.
#region OPTIMIZER AI BRAIN
public class OptimizerAIBrain : OptimalHeuristicBase, IPlayerAIBrain, IEvolvableBrain
{
    public string BrainName => "Optimizer AI";

    // Gene layout
    private const int SCALAR_GENE_COUNT = 10;
    private static readonly string[] ScalarGeneNames =
    {
        "SimulateScoringWeight",
        "PenaltyWeight",
        "TilesPlacedWeight",
        "LineCompletionWeight",
        "TryPushPenaltyWeight",
        "TryDenyMoveWeight",
        "AvoidPartialLineWeight",
        "FirstPlayerTokenWeight",
        "PrioritizeCentralPlacementWeight",
        "PrioritizeTopRowsWeight",
    };

    //Game info.
    private GameModel _model;
    private PlayerModel _me;
    private PlayerModel _opp;

    
    public OptimizerAIBrain() { LoadConfigFromGlobal(); }
    
    public OptimizerAIBrain(BasicGAGenome genome)
    {
        LoadConfigFromGlobal();
        ApplyGenomeWeights(genome);
    }

    //Interface
    public GameMove ChooseMove(GameModel model, List<GameMove> validMoves)
    {
        GameMove bestMove = validMoves[0];
        float bestScore = float.MinValue;

        foreach (var move in validMoves)
        {
            float score = EvaluateMove(move, model);
            if (score > bestScore) { bestScore = score; bestMove = move; }
        }
        return bestMove;
    }

    protected void SetContext(GameModel model)
    {
        _model = model;
        _me = model.GetCurrentPlayer();
        int oppIdx = (_me.PlayerIndex + 1) % model.NumberOfPlayers;
        _opp = model.Players[oppIdx];
    }

    protected virtual float EvaluateMove(GameMove move, GameModel model)
    {
        SetContext(model);

        return EvaluateMove(
            move.SourceEnum, move.FactoryIndex, move.Color,
            move.TargetLineIndex, move.flowerCount, move.IsPenalty,
            model.CentralDisplay.HasFirstPlayerToken);
    }

    #region IEvolvableBrain

    public int GeneCount
    {
        get
        {
            int arrayGenes = 0;
            if (ChaseBonusRowWeights != null) arrayGenes += ChaseBonusRowWeights.Length;
            if (ChaseBonusColumnWeights != null) arrayGenes += ChaseBonusColumnWeights.Length;
            if (ChaseBonusColorWeights != null) arrayGenes += ChaseBonusColorWeights.Length;
            return SCALAR_GENE_COUNT + arrayGenes;
        }
    }

    public void GetGenes(float[] dest)
    {
        int idx = 0;
        dest[idx++] = SimulateScoringWeight;
        dest[idx++] = PenaltyWeight;
        dest[idx++] = TilesPlacedWeight;
        dest[idx++] = LineCompletionWeight;
        dest[idx++] = TryPushPenaltyWeight;
        dest[idx++] = TryDenyMoveWeight;
        dest[idx++] = AvoidPartialLineWeight;
        dest[idx++] = FirstPlayerTokenWeight;
        dest[idx++] = PrioritizeCentralPlacementWeight;
        dest[idx++] = PrioritizeTopRowsWeight;

        if (ChaseBonusRowWeights != null)
            for (int i = 0; i < ChaseBonusRowWeights.Length; i++)
                dest[idx++] = ChaseBonusRowWeights[i];
        if (ChaseBonusColumnWeights != null)
            for (int i = 0; i < ChaseBonusColumnWeights.Length; i++)
                dest[idx++] = ChaseBonusColumnWeights[i];
        if (ChaseBonusColorWeights != null)
            for (int i = 0; i < ChaseBonusColorWeights.Length; i++)
                dest[idx++] = ChaseBonusColorWeights[i];
    }

    public void SetGenes(float[] src)
    {
        int idx = 0;
        SimulateScoringWeight = src[idx++];
        PenaltyWeight = src[idx++];
        TilesPlacedWeight = src[idx++];
        LineCompletionWeight = src[idx++];
        TryPushPenaltyWeight = src[idx++];
        TryDenyMoveWeight = src[idx++];
        AvoidPartialLineWeight = src[idx++];
        FirstPlayerTokenWeight = src[idx++];
        PrioritizeCentralPlacementWeight = src[idx++];
        PrioritizeTopRowsWeight = src[idx++];

        if (ChaseBonusRowWeights != null)
            for (int i = 0; i < ChaseBonusRowWeights.Length; i++)
                ChaseBonusRowWeights[i] = src[idx++];
        if (ChaseBonusColumnWeights != null)
            for (int i = 0; i < ChaseBonusColumnWeights.Length; i++)
                ChaseBonusColumnWeights[i] = src[idx++];
        if (ChaseBonusColorWeights != null)
            for (int i = 0; i < ChaseBonusColorWeights.Length; i++)
                ChaseBonusColorWeights[i] = src[idx++];
    }

    public void RandomInitialise(float[] genes, System.Random rng)
    {
        for (int i = 0; i < genes.Length; i++)
            genes[i] = (float)(rng.NextDouble() * 20.0);
    }

    public string GetGeneName(int index)
    {
        if (index < SCALAR_GENE_COUNT)
            return ScalarGeneNames[index];

        int offset = SCALAR_GENE_COUNT;
        if (ChaseBonusRowWeights != null)
        {
            if (index < offset + ChaseBonusRowWeights.Length)
                return "ChaseBonusRow[" + (index - offset) + "]";
            offset += ChaseBonusRowWeights.Length;
        }
        if (ChaseBonusColumnWeights != null)
        {
            if (index < offset + ChaseBonusColumnWeights.Length)
                return "ChaseBonusColumn[" + (index - offset) + "]";
            offset += ChaseBonusColumnWeights.Length;
        }
        if (ChaseBonusColorWeights != null)
        {
            if (index < offset + ChaseBonusColorWeights.Length)
                return "ChaseBonusColor[" + (index - offset) + "]";
        }
        return "Gene[" + index + "]";
    }

    #endregion

    // Data accessors for GameModel
    #region Data Accessors
    protected override bool MyWallOccupied(int row, int col)
        => _me.Grid.IsOccupied(row, col);

    protected override bool MyLineFull(int line)
        => _me.PlacementLines[line].IsFull;

    protected override byte MyLineColor(int line)
    {
        var c = _me.PlacementLines[line].CurrentColor;
        return c.HasValue ? (byte)c.Value : (byte)255;
    }

    protected override int MyLineCount(int line)
        => _me.PlacementLines[line].Count;

    protected override bool MyLineEmpty(int line)
        => _me.PlacementLines[line].IsEmpty;

    protected override int MyLineCapacity(int line)
        => _me.PlacementLines[line].Capacity;

    protected override int MyPenaltyCount()
        => _me.PenaltyLine.Count;

    protected override int MyCalculatePenalty()
        => _me.PenaltyLine.CalculatePenalty();

    protected override int MyGetWallColumn(int row, int colorIndex)
        => _me.Grid.GetColumnForColor(row, (FlowerColor)colorIndex);

    protected override bool OppCanPlaceInLine(int line, byte color)
        => _opp.CanPlaceInLine(line, (FlowerColor)color);

    protected override byte OppLineColor(int line)
    {
        var c = _opp.PlacementLines[line].CurrentColor;
        return c.HasValue ? (byte)c.Value : (byte)255;
    }

    protected override int OppLineCount(int line)
        => _opp.PlacementLines[line].Count;

    protected override int OppLineCapacity(int line)
        => _opp.PlacementLines[line].Capacity;

    protected override int GetNumFactories()
        => _model.FactoryDisplays.Length;

    protected override int GetNonEmptyFactoryCount()
    {
        int count = 0;
        for (int i = 0; i < _model.FactoryDisplays.Length; i++)
            if (!_model.FactoryDisplays[i].IsEmpty) count++;
        return count;
    }

    protected override int GetFactoryColorCount(int factoryIndex, int colorIndex)
        => _model.FactoryDisplays[factoryIndex].CountColor((FlowerColor)colorIndex);

    protected override int GetFactoryflowerCount(int factoryIndex)
        => _model.FactoryDisplays[factoryIndex].Count;

    protected override int GetCentralColorCount(int colorIndex)
        => _model.CentralDisplay.CountColor((FlowerColor)colorIndex);

    protected override int GetCurrentRound()
        => _model.CurrentRound;
    #endregion
}

#endregion

// MinimalGM version of OptimizerAIBrain.
// TODO: Consider interface or merge.
#region MINIMAL OPTIMIZER BRAIN
public class MinOptimizerBrain : OptimalHeuristicBase, IMinimalAIBrain
{
    private MinimalGM _model;
    private MinimalPlayer _me;
    private MinimalPlayer _opp;

    public virtual string BrainName
    {
        get { return "Optimizer (Minimal)"; }
    }

    public MinOptimizerBrain()
    {
        // Apply the same defaults declared in BasicGAGenome so the
        // optimizer plays with sensible weights even without a SO.
        SimulateScoringWeight = 11.51325f;
        PenaltyWeight = 15.74914f;
        TilesPlacedWeight = 17.34546f;
        LineCompletionWeight = 13.17323f;
        TryPushPenaltyWeight = 4.776058f;
        TryDenyMoveWeight = 2.529862f;
        AvoidPartialLineWeight = 11.48462f;
        FirstPlayerTokenWeight = 4.032143f;
        PrioritizeCentralPlacementWeight = 22.29502f;
        PrioritizeTopRowsWeight = 4.646821f;

        //Based on last training results.
        ChaseBonusColorWeights = new float[] { 10.0f, 10.0f, 10.0f, 10.0f, 10.0f };
        ChaseBonusRowWeights = new float[] { 10.0f, 13.0f, 13.0f, 6.0f, 12.0f };
        ChaseBonusColumnWeights = new float[] { 16.0f, 6.0f, 6.0f, 6.0f, 16.0f };
    }

    public MinOptimizerBrain(BasicGAGenome genome) : this()
    {
        ApplyGenomeWeights(genome);
    }

    public MinOptimizerBrain(OptimizerGenomeWeights w) : this()
    {
        ApplyWeightsSnapshot(w);
    }

    public MinOptimizerBrain(float[] genes, GameConfigSnapshot config) : this()
    {
        LoadConfig(config);
        SetGenesFromArray(genes);
    }

    private void SetGenesFromArray(float[] src)
    {
        int idx = 0;
        SimulateScoringWeight = src[idx++];
        PenaltyWeight = src[idx++];
        TilesPlacedWeight = src[idx++];
        LineCompletionWeight = src[idx++];
        TryPushPenaltyWeight = src[idx++];
        TryDenyMoveWeight = src[idx++];
        AvoidPartialLineWeight = src[idx++];
        FirstPlayerTokenWeight = src[idx++];
        PrioritizeCentralPlacementWeight = src[idx++];
        PrioritizeTopRowsWeight = src[idx++];

        if (ChaseBonusRowWeights != null)
            for (int i = 0; i < ChaseBonusRowWeights.Length; i++)
                ChaseBonusRowWeights[i] = src[idx++];
        if (ChaseBonusColumnWeights != null)
            for (int i = 0; i < ChaseBonusColumnWeights.Length; i++)
                ChaseBonusColumnWeights[i] = src[idx++];
        if (ChaseBonusColorWeights != null)
            for (int i = 0; i < ChaseBonusColorWeights.Length; i++)
                ChaseBonusColorWeights[i] = src[idx++];
    }

    public int ChooseMoveIndex(MinimalGM model, GameMove[] moves, int moveCount)
    {
        int bestIdx = 0;
        float bestScore = float.MinValue;

        for (int i = 0; i < moveCount; i++)
        {
            float s = EvaluateMinimalMove(model, moves[i]);
            if (s > bestScore) { bestScore = s; bestIdx = i; }
        }
        return bestIdx;
    }

    protected void SetMinimalContext(MinimalGM model)
    {
        _model = model;
        if (_gameConfigSnapshot == null || _gameConfigSnapshot != model.Config)
            LoadConfig(model.Config);
        int myIdx = model.CurrentPlayer;
        int oppIdx = (myIdx + 1) % model.NumPlayers;
        _me = model.Players[myIdx];
        _opp = model.Players[oppIdx];
    }

    protected virtual float EvaluateMinimalMove(MinimalGM model, GameMove move)
    {
        SetMinimalContext(model);

        return EvaluateMove(
            move.SourceEnum, move.FactoryIndex, move.Color,
            move.TargetLineIndex, move.flowerCount, move.IsPenalty,
            model.CentralHasToken);
    }

    protected override bool MyWallOccupied(int row, int col)
    {
        return _me.IsWallOccupied(row, col);
    }

    protected override bool MyLineFull(int line)
    {
        return _me.IsLineFull(line);
    }

    protected override byte MyLineColor(int line)
    {
        return _me.GetLineColor(line);
    }

    protected override int MyLineCount(int line)
    {
        return _me.GetLineCount(line);
    }

    protected override bool MyLineEmpty(int line)
    {
        return _me.GetLineCount(line) == 0;
    }

    protected override int MyLineCapacity(int line)
    {
        return _me.GetLineCapacity(line);
    }

    protected override int MyPenaltyCount()
    {
        return _me.GetPenaltyCount();
    }

    protected override int MyCalculatePenalty()
    {
        return _me.CalculatePenalty(_model);
    }

    protected override int MyGetWallColumn(int row, int colorIndex)
    {
        return _gameConfigSnapshot.GetWallColumn(_model.CurrentPlayer, row, colorIndex);
    }

    protected override bool OppCanPlaceInLine(int line, byte color)
    {
        return _opp.CanPlaceInLine(line, color);
    }

    protected override byte OppLineColor(int line)
    {
        return _opp.GetLineColor(line);
    }

    protected override int OppLineCount(int line)
    {
        return _opp.GetLineCount(line);
    }

    protected override int OppLineCapacity(int line)
    {
        return _opp.GetLineCapacity(line);
    }

    protected override int GetNumFactories()
    {
        return _model.NumFactories;
    }

    protected override int GetNonEmptyFactoryCount()
    {
        int count = 0;
        for (int f = 0; f < _model.NumFactories; f++)
        {
            if (_model.GetFactoryflowerCount(f) > 0) count++;
        }
        return count;
    }

    protected override int GetFactoryColorCount(int factoryIndex, int colorIndex)
    {
        return _model.CountColorInFactory(factoryIndex, (FlowerColor)colorIndex);
    }

    protected override int GetFactoryflowerCount(int factoryIndex)
    {
        return _model.GetFactoryflowerCount(factoryIndex);
    }

    protected override int GetCentralColorCount(int colorIndex)
    {
        return _model.CountColorInCentral((FlowerColor)colorIndex);
    }

    protected override int GetCurrentRound()
    {
        return _model.CurrentRound;
    }
}

#endregion