using NUnit.Framework;
using System;
using System.Collections.Generic;

//=^..^=   =^..^=   VERSION 1.0.2 (April 2026)    =^..^=    =^..^=
//                    Last Update 01/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


// A game model interface.
// Necesary to use different versions of the game model.
// Agnostic to internal implementation.

public interface IGameModel
{
    //Sets up the game to start a new game.
    public void GameSetup();


    //Starts a new game round.
    public void StartRound();

    //Starts a new game roun in simulation mode.
    public void SimStartRound();

    //Ends the current game round.
    public void EndRound();

    //Ends the current round in simulation mode.
    public void SimEndRound();

    //Starts a new turn.
    public void StartTurn();

    //Starts a new turn in simulation mode.
    public void SimStartTurn();

    //Ends the current turn.
    public void EndTurn();

    //Ends the current turn in simulation mode.
    public void SimEndTurn();

    //Ends the game.
    public void EndGame();
    public void SimEndGame();

    //Checks end game conditions.
    public bool CheckEndGameConditions();

    //Checks end round conditions.
    public bool CheckEndRoundConditions();

    //Undo the last move.
    public void UndoMove();
    
    //Undo the last turn.
    public void UndoTurn();

    //Undo the last round.
    public void UndoRound();

    //Gets a list of all the possible moves for the current player.
    public List<GameMove> GetPossibleMoves();

    //Gets the current game state.
    public GameState GetGameState();

    //Gets the current game state.
    public void SetGameState(GameState gameState);

    //Gets the current playerIndex.
    public int GetCurrentPlayerIndex();

    //Gets the current round number.
    public int GetRoundNumber();

    //Executes a move over the gameState
    void ExecuteMove(GameMove gameMove);

    //To determine winner and looser
   // bool CheckWinCondition();
  //  bool CheckLoseCondition();
}