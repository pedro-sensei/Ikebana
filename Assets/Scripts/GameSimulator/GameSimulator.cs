using System;
using System.Collections.Generic;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.0.5 (April 2026)    =^..^=    =^..^=
//                    Last Update 15/04/2026 
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
    private const int MAX_TURNS_PER_ROUND_SAFETY = 500;
    private const int MAX_TOTAL_TURNS_SAFETY = 5000;
    private const int MAX_GAME_WALLCLOCK_MS = 15000;

    [Tooltip("Enable detailed logging of each move and round. Can slow down performance significantly.")]
    [SerializeField] private bool LogIsOn = false;

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
        System.Diagnostics.Stopwatch gameWatch = System.Diagnostics.Stopwatch.StartNew();

        model.GameSetup();

        //Game loop: continue until game over
        while (!model.CheckEndGameConditions() && totalRounds < MAX_ROUNDS_SAFETY)
        {
            if (gameWatch.ElapsedMilliseconds >= MAX_GAME_WALLCLOCK_MS)
            {
                abortedBySafety = true;
                Debug.LogWarning($"[GameSimulator] Game wall-clock safety break at {gameWatch.ElapsedMilliseconds}ms.");
                break;
            }

            totalRounds++;
            int turnsThisRound = 0;
            int totalTurnsAtRoundStart = totalTurns;
            model.SimStartRound();
                if (LogIsOn) Debug.Log($"--- Round {model.GetRoundNumber()} ---");
            while (!model.CheckEndRoundConditions())
            {
                turnsThisRound++;
                if (turnsThisRound > MAX_TURNS_PER_ROUND_SAFETY || totalTurns >= MAX_TOTAL_TURNS_SAFETY)
                {
                    abortedBySafety = true;
                    Debug.LogWarning($"[GameSimulator] Safety break triggered at round {model.GetRoundNumber()} after {turnsThisRound} turns (totalTurns={totalTurns}).");
                    break;
                }

                if (gameWatch.ElapsedMilliseconds >= MAX_GAME_WALLCLOCK_MS)
                {
                    abortedBySafety = true;
                    Debug.LogWarning($"[GameSimulator] Game wall-clock safety break at {gameWatch.ElapsedMilliseconds}ms.");
                    break;
                }

                int playerIndex       = model.GetCurrentPlayerIndex();
                IPlayerAIBrain brain  = brains[playerIndex];

                List<GameMove> validMoves = model.GetPossibleMoves();
                if (LogIsOn) Debug.Log($"Player {playerIndex} ({brain.BrainName}) has {validMoves.Count} valid moves.");
                if (validMoves.Count == 0) break;

                GameMove move = brain.ChooseMove(model, validMoves);
                if (LogIsOn) Debug.Log($"Player {playerIndex} ({brain.BrainName}) chooses: {move}");

                ExecuteMove(model, move);
                totalTurns++;
                model.SimEndTurn();
            }

            model.SimEndRound();

            if (abortedBySafety)
            {
                model.SimEndGame();
                break;
            }

            if (totalTurns == totalTurnsAtRoundStart && !model.CheckEndGameConditions())
            {
                abortedBySafety = true;
                Debug.LogWarning($"[GameSimulator] No turn-progress detected in round {model.GetRoundNumber()}; forcing game end.");
                model.SimEndGame();
                break;
            }

            if (model.CheckEndGameConditions())
            {
                model.SimEndGame();
                break;
            }
        }

        return BuildResult(model, brains, totalTurns);
    }

    /// Runs multiple games and returns statistics.
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
       
        //If target is penalty line.
        if (move.IsPenalty)
        {
            List<FlowerPiece> excess = player.PenaltyLine.AddFlowers(pickResult.Pickedflowers);
            //Excess from penalty line goes to discard. (Edge case)
            if (excess.Count > 0) model.Discard.AddRange(excess);
        }
        //Normal placement in lines.
        else
        {
            //PlaceflowersInLine takes 0-based line index; TargetLineIndex is now 0-based.
            int lineIndex = move.TargetLineIndex;
            PlaceResult placeResult = player.PlaceflowersInLine(lineIndex, pickResult.Pickedflowers);
            
            //Excess flowers go to discard.
            if (placeResult.Excessflowers != null && placeResult.Excessflowers.Count > 0)
                model.Discard.AddRange(placeResult.Excessflowers);
        }
    }

    //Builds the result struct for the log and statistics.
    private SimulationGameResult BuildResult(GameModel model, IPlayerAIBrain[] brains, int totalTurns)
    {
        int numPlayers = brains.Length;
        SimulationGameResult result = new SimulationGameResult();
        result.NumPlayers   = numPlayers;
        result.TotalRounds  = model.CurrentRound;
        result.TotalTurns   = totalTurns;
        result.Scores       = new int[numPlayers];
        result.PlayerNames  = new string[numPlayers];

        int bestScore = int.MinValue;
        result.WinnerIndex = 0;

        for (int i = 0; i < numPlayers; i++)
        {
            result.Scores[i]      = model.Players[i].Score;
            result.PlayerNames[i] = model.Players[i].PlayerName;
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

        return result;
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
}
public class SimulationBatchResult
{
    public int NumPlayers;
    public int TotalGames;
    public string[] PlayerNames;

    // Player stats
    public int[] Wins;
    public int[] MinScore;
    public int[] MaxScore;
    public float[] AverageScore;
    public float[] MedianScore;
    public float[] WinPercentage;
    public int Draws;

    // Game detail
    private List<int>[] _allScores;

    // Aggregate
    public float AverageRounds;
    public float AverageTurns;
    private int _totalRounds;
    private int _totalTurns;

    public SimulationBatchResult(int numPlayers, int totalGames)
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

        _allScores = new List<int>[numPlayers];
        for (int i = 0; i < numPlayers; i++)
        {
            _allScores[i] = new List<int>(totalGames);
            MinScore[i] = int.MaxValue;
            MaxScore[i] = int.MinValue;
        }

        _totalRounds = 0;
        _totalTurns = 0;
    }

    public void AddGameResult(SimulationGameResult result)
    {
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

            if (score < MinScore[i]) MinScore[i] = score;
            if (score > MaxScore[i]) MaxScore[i] = score;
        }

        _totalRounds += result.TotalRounds;
        _totalTurns += result.TotalTurns;
    }

    public void CalculateFinalStatistics()
    {
        for (int i = 0; i < NumPlayers; i++)
        {
            // Average score
            long sum = 0;
            foreach (int s in _allScores[i])
                sum += s;
            AverageScore[i] = (float)sum / TotalGames;

            // Win percentage
            WinPercentage[i] = (float)Wins[i] / TotalGames * 100f;

            // Median score
            List<int> sorted = new List<int>(_allScores[i]);
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

        AverageRounds = (float)_totalRounds / TotalGames;
        AverageTurns = (float)_totalTurns / TotalGames;
    }

    /// Returns a string summary of the results.
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
#endregion