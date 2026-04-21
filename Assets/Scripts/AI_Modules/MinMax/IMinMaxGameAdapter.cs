// -----------------------------------------------------------------------
//  IMinMaxGameAdapter  –  game-agnostic interface for MinMax search.
//
//  The MinMax algorithm depends ONLY on this interface and IMinMaxEvaluator.
//  All game-specific logic (move generation, execute/undo, terminal
//  detection, state save/restore) lives in a concrete implementation.
//
//  Replacing the game only requires a new implementation of this
//  interface — the MinMax algorithm never changes.
// -----------------------------------------------------------------------
public interface IMinMaxGameAdapter
{
    // Returns the index of the player whose turn it is.
    int GetCurrentPlayer();

    // Generates all legal moves for the current player at the given
    // search depth into an internal buffer.  Returns the move count.
    int GenerateMoves(int depthIdx);

    // Executes the i-th move from an EXTERNAL root-move buffer at
    // the given depth, saves undo state, and advances the turn.
    // Returns true if the round continues (more moves possible).
    bool ExecuteRootMoveAndAdvance(int depthIdx, int moveIdx);

    // Executes the i-th move from the INTERNAL buffer at the given
    // depth, saves undo state, and advances the turn.
    // Returns true if the round continues.
    bool ExecuteMoveAndAdvance(int depthIdx, int moveIdx);

    // Undoes the last move executed at the given depth and
    // restores turn/player state.
    void UndoAndRestore(int depthIdx);

    // Returns true if the current state is terminal (game over,
    // round ended, no moves possible, etc.).
    bool IsTerminal();

    // Returns an estimate of remaining plies in the current round
    // (used to clamp the search depth so it doesn't exceed the round).
    int EstimateRemainingPly();

    // Evaluates the current game state from the perspective of the
    // given player.  Higher = better for that player.
    // Delegates to whatever IMinMaxEvaluator is configured.
    float Evaluate(int maximizingPlayer);
}
