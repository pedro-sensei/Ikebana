using System.Collections.Generic;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


// AI brain that just picks a random valid move.
#region RANDOM BRAIN
public class RandomAIBrain : IPlayerAIBrain
{
    #region FIELDS AND PARAMETERS
    public string BrainName
    {
        get { return "Random AI"; }
    }

    private System.Random _random;
    #endregion

    #region CONSTRUCTORS

    public RandomAIBrain()
    {
        _random = new System.Random();
    }

    public RandomAIBrain(int seed)
    {
        _random = new System.Random(seed);
    }
    #endregion

    #region IPlayerAIBrain

    public GameMove ChooseMove(GameModel model, List<GameMove> validMoves)
    {
        int index = _random.Next(validMoves.Count);
        return validMoves[index];
    }
    #endregion
}
#endregion

//Version for minimal GM
//TODO: UNIFY GMs
#region MINIMAL RANDOM BRAIN
public class MinimalRandomBrain : IMinimalAIBrain
{
    #region FIELDS AND PARAMETERS
    public string BrainName
    {
        get { return "Random (Minimal)"; }
    }

    private readonly System.Random _rng;
    #endregion

    #region CONSTRUCTORS

    public MinimalRandomBrain(int seed = 0)
    {
        if (seed == 0)
            _rng = new System.Random();
        else
            _rng = new System.Random(seed);
    }
    #endregion

    #region IMinimalAIBrain

    public int ChooseMoveIndex(MinimalGM model, GameMove[] moves, int moveCount)
    {
        return _rng.Next(moveCount);
    }
    #endregion
}

#endregion