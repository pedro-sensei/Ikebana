using System.Collections.Generic;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


//Uses the reduced MinimalGM model to run fast simulations
//of games in the genetic algorithm and min-max modules.
// tries to reduce use of heap since Unity was freezing.
public class MinimalGameSimulator
{
    private const int MAX_ROUNDS_SAFETY = 50;
    private const int MAX_TURNS_PER_ROUND = 500;
    private const int MAX_TOTAL_TURNS = 5000;

    // Callers that want parallelism should create one MinimalGameSimulator per thread.
    private MinimalGM _minimalModel;
    private readonly GameMove[] _moveBuffer = new GameMove[200];
    private readonly GameConfigSnapshot _config;

    public MinimalGameSimulator()
    {
        _config = GameConfig.CreateSnapshot();
    }

    //Creates a simulator with a specific config snapshot.
    //REuse snapshot during runtime.
    public MinimalGameSimulator(GameConfigSnapshot config)
    {
        _config = config;
    }

    private MinimalGM GetOrCreateMinimalModel(int numPlayers)
    {
        if (_minimalModel == null || _minimalModel.NumPlayers != numPlayers)
           _minimalModel = new MinimalGM(numPlayers, _config);
       return _minimalModel;
    }

   //Mimics simulator with better performance.
    public MinimalSimulationGameResult RunGame(IMinimalAIBrain[] brains, string[] names = null)
    {
        int numPlayers = brains.Length;
        if (names == null)
        {
            names = new string[numPlayers];
            for (int i = 0; i < numPlayers; i++)
                names[i] = brains[i].BrainName;
        }

        MinimalGM model = GetOrCreateMinimalModel(numPlayers);
        model.ResetForNewGame();
        model.SimStartRound();

        int totalTurns  = 0;
        int totalRounds = 0;
        bool abortedBySafety = false;
        int shortRoundsCount = 0;

        while (!model.IsGameOver && totalRounds < MAX_ROUNDS_SAFETY)
        {
            totalRounds++;
            int turnsThisRound = 0;
            while (!model.AreDisplaysEmpty() && !model.IsGameOver)
            {
                turnsThisRound++;
                if (turnsThisRound > MAX_TURNS_PER_ROUND || totalTurns >= MAX_TOTAL_TURNS)
                {
                    abortedBySafety = true;
                    model.SimEndGame();
                    break;
                }

                int n = model.GetValidMoves(_moveBuffer);
                if (n == 0) break;

                int chosen = brains[model.CurrentPlayer].ChooseMoveIndex(model, _moveBuffer, n);
                if (chosen < 0 || chosen >= n)
                {
                    abortedBySafety = true;
                    model.SimEndGame();
                    break;
                }

                model.ExecuteMove(_moveBuffer[chosen]);
                totalTurns++;

                if (!model.AreDisplaysEmpty())
                    model.AdvanceTurnMinMax();
            }

            if (abortedBySafety)
                break;

            if (turnsThisRound < 5)
                shortRoundsCount++;

            if (!model.IsGameOver)
                model.SimEndRound();

            if (model.IsGameOver)
                break;
        }

        if (!abortedBySafety && !model.IsGameOver && totalRounds >= MAX_ROUNDS_SAFETY)
        {
            abortedBySafety = true;
            model.SimEndGame();
        }

        return BuildResultMinimal(model, brains, names, totalRounds, totalTurns, abortedBySafety, shortRoundsCount);
    }

    public MinimalSimulationBatchResult RunBatch(IMinimalAIBrain[] brains, int numberOfGames, string[] names = null)
    {
        int numPlayers = brains.Length;
        MinimalSimulationBatchResult batch = new MinimalSimulationBatchResult(numPlayers, numberOfGames);

        if (names == null)
        {
            names = new string[numPlayers];
            for (int i = 0; i < numPlayers; i++)
                names[i] = brains[i].BrainName;
        }
        for (int i = 0; i < numPlayers; i++)
            batch.PlayerNames[i] = names[i];

        for (int g = 0; g < numberOfGames; g++)
            batch.AddGameResult(RunGame(brains, names));

        batch.FinalizeScoring();
        return batch;
    }

    private MinimalSimulationGameResult BuildResultMinimal(
        MinimalGM model, IMinimalAIBrain[] brains, string[] names,
        int totalRounds, int totalTurns, bool abortedBySafety, int shortRoundsCount)
    {
        int numPlayers = brains.Length;
        MinimalSimulationGameResult result = new MinimalSimulationGameResult();
        result.NumPlayers   = numPlayers;
        result.TotalRounds  = totalRounds;
        result.TotalTurns   = totalTurns;
        result.IsAborted    = abortedBySafety;
        result.ShortRoundsCount = shortRoundsCount;
        result.Scores       = new int[numPlayers];
        result.PlayerNames  = new string[numPlayers];

        int bestScore = int.MinValue;
        bool allPlayersHaveZeroScore = true;
        result.WinnerIndex = 0;

        for (int i = 0; i < numPlayers; i++)
        {
            result.Scores[i]      = model.Players[i].Score;
            result.PlayerNames[i] = names[i];
            if (result.Scores[i] != 0)
                allPlayersHaveZeroScore = false;
            if (model.Players[i].Score > bestScore)
            {
                bestScore          = model.Players[i].Score;
                result.WinnerIndex = i;
            }
        }

        result.IsDraw = false;
        for (int i = 0; i < numPlayers; i++)
            if (i != result.WinnerIndex && result.Scores[i] == bestScore)
            { result.IsDraw = true; break; }

        result.AllPlayersHaveZeroScore = allPlayersHaveZeroScore;
        result.EndedInLessThanFiveRounds = totalRounds < 5;
        result.HasErrors = result.EndedInLessThanFiveRounds || shortRoundsCount > 0 || abortedBySafety;

        return result;
    }
}

public struct MinimalSimulationGameResult
{
    public int NumPlayers;
    public int[] Scores;
    public string[] PlayerNames;
    public int WinnerIndex;
    public bool IsDraw;
    public int TotalRounds;
    public int TotalTurns;
    public bool IsAborted;
    public bool HasErrors;
    public bool AllPlayersHaveZeroScore;
    public bool EndedInLessThanFiveRounds;
    public int ShortRoundsCount;
}

public class MinimalSimulationBatchResult
{
    public int NumPlayers;
    public int TotalGames;
    public string[] PlayerNames;

    // Per-player stats
    public int[] Wins;
    public int[] MinScore;
    public int[] MaxScore;
    public float[] AverageScore;
    public float[] MedianScore;
    public float[] WinPercentage;
    public int Draws;
    public int AbortedGames;
    public int ErrorGames;

    // Per-game detail for median calculation
    private List<int>[] allScores;

    // Aggregate
    public float AverageRounds;
    public float AverageTurns;
    private int totalRounds;
    private int totalTurns;

    public MinimalSimulationBatchResult(int numPlayers, int totalGames)
    {
        NumPlayers = numPlayers;
        TotalGames = totalGames;
        PlayerNames = new string[numPlayers];
        Wins = new int[numPlayers];
        MinScore = new int[numPlayers];
        MaxScore = new int[numPlayers];
        AverageScore = new float[numPlayers];
        MedianScore = new float[numPlayers];
        WinPercentage = new float[numPlayers];
        Draws = 0;
        AbortedGames = 0;
        ErrorGames = 0;

        allScores = new List<int>[numPlayers];
        for (int i = 0; i < numPlayers; i++)
        {
            allScores[i] = new List<int>(totalGames);
            MinScore[i] = int.MaxValue;
            MaxScore[i] = int.MinValue;
        }

        totalRounds = 0;
        totalTurns = 0;
    }

    public void AddGameResult(MinimalSimulationGameResult result)
    {
        if (result.IsAborted)
            AbortedGames++;
        if (result.HasErrors)
            ErrorGames++;

        if (result.IsDraw)
        {
            Draws++;
        }
        else
        {
            Wins[result.WinnerIndex]++;
        }

        for (int i = 0; i < NumPlayers; i++)
        {
            int score = result.Scores[i];
            allScores[i].Add(score);

            if (score < MinScore[i]) MinScore[i] = score;
            if (score > MaxScore[i]) MaxScore[i] = score;
        }

        totalRounds += result.TotalRounds;
        totalTurns += result.TotalTurns;
    }


    /// Calculates final statistics
    public void FinalizeScoring()
    {
        for (int i = 0; i < NumPlayers; i++)
        {
            // Average
            long sum = 0;
            foreach (int s in allScores[i])
                sum += s;
            AverageScore[i] = (float)sum / TotalGames;

            // Win percentage
            WinPercentage[i] = (float)Wins[i] / TotalGames * 100f;

            // Median
            List<int> sorted = new List<int>(allScores[i]);
            sorted.Sort();
            if (sorted.Count % 2 == 0)
            {
                int mid = sorted.Count / 2;
                MedianScore[i] = (sorted[mid - 1] + sorted[mid]) / 2f;
            }
            else
            {
                MedianScore[i] = sorted[sorted.Count / 2];
            }
        }

        AverageRounds = (float)totalRounds / TotalGames;
        AverageTurns = (float)totalTurns / TotalGames;
    }

    /// Returns a formatted string summary.
    public string GetSummary()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.AppendLine(":¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:.:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:.:¨:..:¨:.:¨:");
        sb.AppendLine($"  SIMULATION RESULTS — {TotalGames} games");
        sb.AppendLine(":¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:.:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:.:¨:..:¨:.:¨:");
        sb.AppendLine($"  Avg Rounds/Game: {AverageRounds:F1}  |  Avg Turns/Game: {AverageTurns:F1}");
        sb.AppendLine($"  Draws: {Draws} ({(float)Draws / TotalGames * 100f:F1}%)");
        sb.AppendLine(":¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:.:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:.:¨:..:¨:.:¨:");

        for (int i = 0; i < NumPlayers; i++)
        {
            sb.AppendLine($"  Player {i}: {PlayerNames[i]}");
            sb.AppendLine($"    Wins:    {Wins[i]} ({WinPercentage[i]:F1}%)");
            sb.AppendLine($"    Score:   min={MinScore[i]}  max={MaxScore[i]}  avg={AverageScore[i]:F1}  median={MedianScore[i]:F1}");
            sb.AppendLine("....................................................................................");
        }

        sb.AppendLine("\":¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:.:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:.:¨:..:¨:.:¨:");
        return sb.ToString();
    }
}
