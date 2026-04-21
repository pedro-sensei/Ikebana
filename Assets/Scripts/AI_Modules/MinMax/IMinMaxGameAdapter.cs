//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


//  IMinMaxGameAdapter  –  game-agnostic interface for MinMax.

public interface IMinMaxGameAdapter
{
    // Returns the index of the player.
    int GetCurrentPlayer();

    // Generates all legal moves for the current player and returns the move count.
    int GenerateMoves(int plyDepth);

    // Executes the i-th move from EXTERNAL buffer
    // at the given depth
    // saves undo state
    // advances the turn
    // Returns true if the round continues.
    bool ExecuteRootMoveAndAdvance(int plyDepth, int moveIndex);

    // Executes the i-th move from the INTERNAL buffer
    // at the given depth
    //  saves undo state
    //  advances the turn
    // Returns true if the round continues.
    bool ExecuteMoveAndAdvance(int plyDepth, int moveIndex);

    // Undoes the last move
    // at the given depth 
    // restores turn/player state.
    void UndoAndRestore(int plyDepth);

    // Returns true if the current state is terminal.
    bool IsTerminal();

    // Returns an estimate of remaining moves in the current round
    int EstimateRemainingMoves();

    // Evaluates the current game state from the perspective of the
    // given player.  Higher = better for that player.
    float Evaluate(int maximizingPlayer);
}
