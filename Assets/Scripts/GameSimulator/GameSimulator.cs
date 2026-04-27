using System;
using System;
using System.Collections.Generic;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

#region GAME SIMULATOR

/// <summary>
/// Runs complete games 
/// 
/// </summary>
public class GameSimulator
{
    #region FIELDS AND PROPERTIES
    private const int MAX_ROUNDS_SAFETY = 50;
    private const int MAX_TURNS_PER_ROUND = 500;
    private const int MAX_TOTAL_TURNS = 5000;
    private const int MAX_GAME_DURATION_MS = 15000;

    //[Tooltip("Enable detailed logging of each move and round. Can slow down performance significantly.")]
    //[SerializeField] private bool LogIsOn = false;

    #endregion

    #region PUBLIC METHODS

    /// Runs a single complete game and returns the result.
    public SimulationGameResult RunGame(IPlayerAIBrain[] brains, string[] names = null)
    {
        //Determine playerCount automatically.
        int numPlayers = brains.Length;
        if (names == null)
        {
            names = new string[numPlayers];
            for (int i = 0; i < numPlayers; i++)
                names[i] = brains[i].BrainName + " " + i;
        }

               
        GameModel model = new GameModel(numPlayers, names);
        //Sim methods substitutte the event-driven flow of the real game,
        //It's important that the sim methods comply and don't duplicate
        // Events driven by the simulator.
        
        int totalTurns  = 0;
        int totalRounds = 0;
        bool abortedBySafety = false;
        int shortRoundsCount = 0;
        System.Diagnostics.Stopwatch gameTimer = System.Diagnostics.Stopwatch.StartNew();

        model.GameSetup();

        //Round loop: continue until game over
        while (!model.CheckEndGameConditions() && totalRounds < MAX_ROUNDS_SAFETY)
        {
            if (gameTimer.ElapsedMilliseconds >= MAX_GAME_DURATION_MS)
            {
                abortedBySafety = true;
                Debug.LogWarning($"[GameSimulator] Game ABORTED by safety timer at {gameTimer.ElapsedMilliseconds}ms.");
                break;
            }

            totalRounds++;
            int turnsThisRound = 0;
            int totalTurnsAtRoundStart = totalTurns;
            model.SimStartRound();

            //Turn loop: continue until round over
            while (!model.CheckEndRoundConditions())
            {
                turnsThisRound++;
                if (turnsThisRound > MAX_TURNS_PER_ROUND || totalTurns >= MAX_TOTAL_TURNS)
                {
                    abortedBySafety = true;
                    Debug.LogWarning($"[GameSimulator] Safety break: Round {model.GetRoundNumber()} Turns in round:{turnsThisRound} TotalTurns:{totalTurns}");
                    break;
                }

                if (gameTimer.ElapsedMilliseconds >= MAX_GAME_DURATION_MS)
                {
                    abortedBySafety = true;
                    Debug.LogWarning($"[GameSimulator] Game ABORTED by safety timer at {gameTimer.ElapsedMilliseconds}ms.");
                    break;
                }

                int playerIndex       = model.GetCurrentPlayerIndex();
                IPlayerAIBrain brain  = brains[playerIndex];

                List<GameMove> validMoves = model.GetPossibleMoves();
                if (validMoves.Count == 0) break;

                GameMove move = brain.ChooseMove(model, validMoves);

                ExecuteMove(model, move);
                totalTurns++;
                model.SimEndTurn();
            }

            //Turn loop over-> end round.
            model.SimEndRound();

            if (turnsThisRound < 5)
                shortRoundsCount++;

            //Check if game was aborted.
            if (abortedBySafety)
            {
                model.SimEndGame();
                break;
            }

            //Check end game conditions after round ends.
            if (model.CheckEndGameConditions())
            {
                model.SimEndGame();
                break;
            }
        }

        if (!abortedBySafety && !model.CheckEndGameConditions())
        {
            abortedBySafety = true;
            model.SimEndGame();
        }

        //Game over, build result.
        gameTimer.Stop();
        return BuildResult(model, brains, totalRounds, totalTurns, abortedBySafety, shortRoundsCount, gameTimer.Elapsed.TotalMilliseconds);
    }

    //Builds the result struct for the log and statistics.
    private SimulationGameResult BuildResult(
        GameModel model,
        IPlayerAIBrain[] brains,
        int totalRounds,
        int totalTurns,
        bool abortedBySafety,
        int shortRoundsCount,
        double durationMs)
    {
        int numPlayers = brains.Length;
        SimulationGameResult result = new SimulationGameResult();
        result.NumPlayers = numPlayers;
        result.TotalRounds = totalRounds;
        result.TotalTurns = totalTurns;
        result.IsAborted = abortedBySafety;
        result.ShortRoundsCount = shortRoundsCount;
        result.DurationMs = durationMs;
        result.Scores = new int[numPlayers];
        result.PlayerNames = new string[numPlayers];

        int bestScore = int.MinValue;
        bool allPlayersHaveZeroScore = true;
        result.WinnerIndex = 0;

        for (int i = 0; i < numPlayers; i++)
        {
            result.Scores[i] = model.Players[i].Score;
            result.PlayerNames[i] = model.Players[i].PlayerName;
            if (result.Scores[i] != 0)
                allPlayersHaveZeroScore = false;
            if (model.Players[i].Score > bestScore)
            {
                bestScore = model.Players[i].Score;
                result.WinnerIndex = i;
            }
        }

        result.IsDraw = false;
        for (int i = 0; i < numPlayers; i++)
            if (i != result.WinnerIndex && result.Scores[i] == bestScore)
            { result.IsDraw = true; break; }

        result.AllPlayersHaveZeroScore = allPlayersHaveZeroScore;
        result.EndedInLessThanFiveRounds = totalRounds < 5;
        result.HasErrors = result.EndedInLessThanFiveRounds || shortRoundsCount > 0;

        return result;
    }

    /*
    //Runs multiple games and returns statistics.
    public SimulationBatchResult RunBatch(IPlayerAIBrain[] brains, int numberOfGames, string[] names = null)
    {
        int numPlayers = brains.Length;
        SimulationBatchResult batch = new SimulationBatchResult(numPlayers, numberOfGames);

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

        batch.CalculateFinalStatistics();
        return batch;
    }
    */


    #endregion

    #region HELPER METHODS
    // Executes a move directly on the model without any events or UI.

    private void ExecuteMove(GameModel model, GameMove move)
    {
        PlayerModel player = model.GetCurrentPlayer();
        PickResult pickResult;
        if(move.SourceIsFactory)
        {
            pickResult = model.PickFromFactory(move.FactoryIndex, move.Color);
        }
        else if (move.SourceEnum == MoveSource.Central)
        {
            pickResult = model.PickFromCentral(move.Color);
            if (pickResult.PickedFirstPlayerToken)
            player.ReceiveFirstPlayerToken();
        }
        else
        {
            Debug.LogError($"Invalid move source: {move.Source}");
            return;
        }
       
        if (move.IsPenalty)
        {
            List<FlowerPiece> excess = player.PenaltyLine.AddFlowers(pickResult.Pickedflowers);
            //Excess from penalty line goes to discard. (Edge case)
            if (excess.Count > 0) model.Discard.AddRange(excess);
        }

        //Normal placement in lines.
        else
        {
            //PlaceflowersInLine takes 0-based line index; 
            int lineIndex = move.TargetLineIndex;
            PlaceResult placeResult = player.PlaceflowersInLine(lineIndex, pickResult.Pickedflowers);
            
            //Excess flowers go to discard (EDGE).
            if (placeResult.Excessflowers != null && placeResult.Excessflowers.Count > 0)
                model.Discard.AddRange(placeResult.Excessflowers);
        }
    }

   
    #endregion
}
#endregion
#region SIMULATION RESULT AUXILIARY CLASSES


public struct SimulationGameResult
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
    public double DurationMs;
}
public class SimulationBatchResult
{
    public int NumPlayers;
    public int TotalGames;
    public string[] PlayerNames;
    public int SimulatedGames;

    // Player stats
    public int[] Wins;
    public int[] MinScore;
    public int[] MaxScore;
    public float[] AverageScore;
    public float[] MedianScore;
    public int[] BestDelta;
    public float[] MedianDelta;
    public float[] WinPercentage;
    public int Draws;

    // Game detail
    private List<int>[] _allScores;
    private List<int>[] _allDeltas;

    // Aggregate
    public float AverageRounds;
    public float AverageTurns;
    public double AverageGameDurationMs;
    public int LongestGameTurns;
    public int HighestScore;
    public int AbortedGames;
    public int GamesWithErrors;
    public int WarningGamesEndedWithZeroPoints;
    public int ErrorGamesEndedInLessThanFiveRounds;
    public int ErrorRoundsEndedInLessThanFiveTurns;
    private int _totalRounds;
    private int _totalTurns;
    private double _totalGameDurationMs;

    public SimulationBatchResult(int numPlayers, int totalGames)
    {
        NumPlayers = numPlayers;
        TotalGames = totalGames;
        PlayerNames = new string[numPlayers];
        SimulatedGames = 0;
        Wins = new int[numPlayers];
        MinScore = new int[numPlayers];
        MaxScore = new int[numPlayers];
        AverageScore = new float[numPlayers];
        MedianScore = new float[numPlayers];
        BestDelta = new int[numPlayers];
        MedianDelta = new float[numPlayers];
        WinPercentage = new float[numPlayers];
        Draws = 0;
        AverageGameDurationMs = 0d;
        LongestGameTurns = 0;
        HighestScore = int.MinValue;
        AbortedGames = 0;
        GamesWithErrors = 0;
        WarningGamesEndedWithZeroPoints = 0;
        ErrorGamesEndedInLessThanFiveRounds = 0;
        ErrorRoundsEndedInLessThanFiveTurns = 0;

        _allScores = new List<int>[numPlayers];
        _allDeltas = new List<int>[numPlayers];
        for (int i = 0; i < numPlayers; i++)
        {
            _allScores[i] = new List<int>(totalGames);
            _allDeltas[i] = new List<int>(totalGames);
            MinScore[i] = int.MaxValue;
            MaxScore[i] = int.MinValue;
            BestDelta[i] = int.MinValue;
        }

        _totalRounds = 0;
        _totalTurns = 0;
        _totalGameDurationMs = 0d;
    }

    public void AddGameResult(SimulationGameResult result)
    {
        SimulatedGames++;

        // Update wins/draws
        if (result.IsDraw)
        {
            Draws++;
        }
        else
        {
            Wins[result.WinnerIndex]++;
        }

        // Update scores and min/max per player
        for (int i = 0; i < NumPlayers; i++)
        {
            int score = result.Scores[i];
            _allScores[i].Add(score);

            int delta = score - GetBestOpponentScore(result.Scores, i);
            _allDeltas[i].Add(delta);

            if (score < MinScore[i]) MinScore[i] = score;
            if (score > MaxScore[i]) MaxScore[i] = score;
            if (delta > BestDelta[i]) BestDelta[i] = delta;
            if (score > HighestScore) HighestScore = score;
        }

        if (result.IsAborted) AbortedGames++;
        if (result.HasErrors) GamesWithErrors++;
        if (result.AllPlayersHaveZeroScore) WarningGamesEndedWithZeroPoints++;
        if (result.EndedInLessThanFiveRounds) ErrorGamesEndedInLessThanFiveRounds++;
        ErrorRoundsEndedInLessThanFiveTurns += result.ShortRoundsCount;

        _totalRounds += result.TotalRounds;
        _totalTurns += result.TotalTurns;
        _totalGameDurationMs += result.DurationMs;

        if (result.TotalTurns > LongestGameTurns)
            LongestGameTurns = result.TotalTurns;
    }

    public void CalculateFinalStatistics()
    {
        if (SimulatedGames == 0)
            return;

        for (int i = 0; i < NumPlayers; i++)
        {
            // Average score
            long sum = 0;
            foreach (int s in _allScores[i])
                sum += s;
            AverageScore[i] = (float)sum / SimulatedGames;

            // Win percentage
            WinPercentage[i] = (float)Wins[i] / SimulatedGames * 100f;

            // Median score
            MedianScore[i] = CalculateMedian(_allScores[i]);
            MedianDelta[i] = CalculateMedian(_allDeltas[i]);

            if (BestDelta[i] == int.MinValue)
                BestDelta[i] = 0;
        }

        AverageRounds = (float)_totalRounds / SimulatedGames;
        AverageTurns = (float)_totalTurns / SimulatedGames;
        AverageGameDurationMs = _totalGameDurationMs / SimulatedGames;

        if (HighestScore == int.MinValue)
            HighestScore = 0;
    }

    private static int GetBestOpponentScore(int[] scores, int playerIndex)
    {
        int bestOpponentScore = int.MinValue;

        for (int i = 0; i < scores.Length; i++)
        {
            if (i == playerIndex) continue;
            if (scores[i] > bestOpponentScore)
                bestOpponentScore = scores[i];
        }

        return bestOpponentScore == int.MinValue ? 0 : bestOpponentScore;
    }

    private static float CalculateMedian(List<int> values)
    {
        if (values == null || values.Count == 0)
            return 0f;

        List<int> sorted = new List<int>(values);
        sorted.Sort();

        if (sorted.Count % 2 == 0)
        {
            int mid = sorted.Count / 2;
            return (sorted[mid - 1] + sorted[mid]) / 2f;
        }

        return sorted[sorted.Count / 2];
    }

    /// Returns a string summary of the results.
    public string GetSummary()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.AppendLine(":¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:.:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:.:¨:..:¨:.:¨:");
        sb.AppendLine($"  SIMULATION RESULTS — {SimulatedGames} games");
        sb.AppendLine(":¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:.:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:.:¨:..:¨:.:¨:");
        sb.AppendLine($"  Avg Rounds/Game: {AverageRounds:F1}  |  Avg Turns/Game: {AverageTurns:F1}");
        sb.AppendLine($"  Draws: {Draws} ({(SimulatedGames > 0 ? (float)Draws / SimulatedGames * 100f : 0f):F1}%)");
        sb.AppendLine($"  Longest Game: {LongestGameTurns} turns  |  Highest Score: {HighestScore}");
        sb.AppendLine(":¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:.:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:.:¨:..:¨:.:¨:");

        for (int i = 0; i < NumPlayers; i++)
        {
            sb.AppendLine($"  Player {i}: {PlayerNames[i]}");
            sb.AppendLine($"    Wins:    {Wins[i]} ({WinPercentage[i]:F1}%)");
            sb.AppendLine($"    Score:   min={MinScore[i]}  max={MaxScore[i]}  avg={AverageScore[i]:F1}  median={MedianScore[i]:F1}");
            sb.AppendLine($"    Delta:   best={BestDelta[i]}  median={MedianDelta[i]:F1}");
            sb.AppendLine("....................................................................................");
        }

        sb.AppendLine("  Simulation Error Log Results:");
        sb.AppendLine($"    [Warning] Games ended with 0 points for both players: {WarningGamesEndedWithZeroPoints}");
        sb.AppendLine($"    [Error] Games ended in less than 5 rounds: {ErrorGamesEndedInLessThanFiveRounds}");
        sb.AppendLine($"    [Error] Rounds ended in less than 5 turns: {ErrorRoundsEndedInLessThanFiveTurns}");
        sb.AppendLine("  Simulator Statistics:");
        sb.AppendLine($"    Number of simulated games: {SimulatedGames}");
        sb.AppendLine($"    Number of aborted games: {AbortedGames}");
        sb.AppendLine($"    Number of games with errors: {GamesWithErrors}");
        sb.AppendLine($"    Average game duration: {AverageGameDurationMs:F2} ms");

        sb.AppendLine("\":¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:.:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:..:¨:.:¨:..:¨:.:¨:");
        return sb.ToString();
    }
}
#endregion