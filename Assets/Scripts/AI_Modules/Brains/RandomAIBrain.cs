using System.Collections.Generic;

//=^..^=   =^..^=   VERSION 1.0.3 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// AI brain that just picks a random valid move.
#region RANDOM BRAIN
public class RandomAIBrain : IPlayerAIBrain
{
    private System.Random _random;

    public string BrainName
    {
        get { return "Random AI"; }
    }

    public RandomAIBrain()
    {
        _random = new System.Random();
    }

    public RandomAIBrain(int seed)
    {
        _random = new System.Random(seed);
    }

    public GameMove ChooseMove(GameModel model, List<GameMove> validMoves)
    {
        int index = _random.Next(validMoves.Count);
        return validMoves[index];
    }
}
#endregion

//Version for minimal GM
//TODO: UNIFY GMs
#region MINIMAL RANDOM BRAIN
public class MinimalRandomBrain : IMinimalAIBrain
{
    private readonly System.Random _rng;

    public string BrainName
    {
        get { return "Random (Minimal)"; }
    }

    public MinimalRandomBrain(int seed = 0)
    {
        if (seed == 0)
            _rng = new System.Random();
        else
            _rng = new System.Random(seed);
    }

    public int ChooseMoveIndex(MinimalGM model, GameMove[] moves, int moveCount)
    {
        return _rng.Next(moveCount);
    }
}

#endregion