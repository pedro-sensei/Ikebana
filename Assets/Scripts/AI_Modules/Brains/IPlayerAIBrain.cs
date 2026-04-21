using System.Collections.Generic;

//=^..^=   =^..^=   VERSION 1.0.3 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// Interface for AI brains.
// Gets the game state and list of valid moves, returns the chosen one.
//This is necessary to be able to swap AIs.

public interface IPlayerAIBrain
{
    string BrainName { get; }

    GameMove ChooseMove(GameModel model, List<GameMove> validMoves);
}

//TODO: UNIFY ALL IN ONE INTERFACE WITH A GAMEMODEL INTERFACE.
// Same but for the minimal game model used in training
public interface IMinimalAIBrain
{
    string BrainName { get; }

    // Chooses one of the valid moves. Returns an index in [0, moveCount).
    int ChooseMoveIndex(MinimalGM model, GameMove[] moves, int moveCount);
}
