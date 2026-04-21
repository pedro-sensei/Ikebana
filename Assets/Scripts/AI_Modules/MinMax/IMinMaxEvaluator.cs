// Interface for evaluating a game state from a specific player's perspective.
// -----------------------------------------------------------------------
//  IMinMaxEvaluator  -  game-state scoring interface for MinMax search.
//
//  Implementations are interchangeable: heuristic, projected score,
//  neural network, etc.  The MinMax algorithm and the game adapter
//  never depend on the evaluation logic directly.
// -----------------------------------------------------------------------
public interface IMinMaxEvaluator
{
    // Returns a score for the current game state from the perspective of
    // the given player.  Higher = better for that player.
    float Evaluate(MinimalGM model, int playerIndex);
}
