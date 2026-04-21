using System;
using System.Collections.Generic;

//=^..^=   =^..^=   VERSION 1.0.1 (March 2026)    =^..^=    =^..^=
//                    Last Update 22/03/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

/// <summary>
/// Model for the penalty line on the player board. 
/// It holds flowers that were taken as overflow or placed directly
/// Capacity: Basic is 7 slots with progressive penalties: -1, -1, -2, -2, -3, -3, -4.
/// First player token always goes here.
/// 
/// </summary>
[Serializable]
public class PenaltyLineModel :IFlowerTarget
{
    #region FIELDS AND PROPERTIES
    private List<FlowerPiece> _flowers;
    private bool _hasFirstPlayerToken;

    public int Count => _flowers.Count;
    public int Capacity => GameConfig.PENALTY_LINE_CAPACITY;
    public bool IsFull => Count >= Capacity;
    public bool HasFirstPlayerToken => _hasFirstPlayerToken;
    public IReadOnlyList<FlowerPiece> Flowers => _flowers;

    public bool IsEmpty => throw new NotImplementedException();


    #endregion

    #region EVENTS
    public event Action OnPenaltyChanged;

    #endregion

    #region CONSTRUCTORS AND INITIALIZATION
    public PenaltyLineModel()
    {
        _flowers = new List<FlowerPiece>(GameConfig.PENALTY_LINE_CAPACITY);
        _hasFirstPlayerToken = false;
    }
    #endregion

    #region GAME LOGIC
    /// <summary>
    /// Add the first player token to the penalty line. 
    /// This should only be called once per round.
    /// </summary>
    public void AddFirstPlayerToken()
    {
        _hasFirstPlayerToken = true;
        _flowers.Add(new FlowerPiece(FlowerColor.FirstPlayer));
        
        //NOTIFY that the penalty line has changed so the UI can update
        OnPenaltyChanged?.Invoke();
    }

    /// <summary>
    /// Adds overflow flowers to the penalty line.
    /// Flowers that don't fit are returned so they can be sent to discard (official rule).
    /// </summary>
    public List<FlowerPiece> AddFlowers(List<FlowerPiece> overflowFlowers)
    {
        List<FlowerPiece> excessFlowers = new List<FlowerPiece>();

        foreach (FlowerPiece flower in overflowFlowers)
        {
            if (!IsFull)
            {
                _flowers.Add(flower);
            }
            else
            {
                excessFlowers.Add(flower);
            }
        }

        OnPenaltyChanged?.Invoke();
        return excessFlowers;
    }

    /// <summary>
    /// Calculates the total penalty points based on the number of flowers in the penalty line.
    /// </summary>
    public int CalculatePenalty()
    {
        int totalPenalty = 0;
        int slotsUsed = Count;

        for (int i = 0; i < slotsUsed && i < GameConfig.PENALTY_LINE_VALUES.Length; i++)
        {
            totalPenalty += GameConfig.PENALTY_LINE_VALUES[i];
        }

        return totalPenalty;
    }

    /// <summary>
    /// Clears the penalty line and returns the flowers to discard.
    /// The first player token is NOT discarded — it is indicated separately
    /// via the returned flag so the caller can reassign it.
    /// </summary>
    public (List<FlowerPiece> flowersToDiscard, bool returnFirstPlayerToken) Clear()
    {
        List<FlowerPiece> toDiscard = new List<FlowerPiece>();
        foreach (FlowerPiece flower in _flowers)
        {
            if (flower.Color != FlowerColor.FirstPlayer)
                toDiscard.Add(flower);
        }

        bool hadToken = _hasFirstPlayerToken;

        _flowers.Clear();
        _hasFirstPlayerToken = false;

        OnPenaltyChanged?.Invoke();

        return (toDiscard, hadToken);
    }

    internal PenaltyLineModel Clone()
    {
        PenaltyLineModel clone = new PenaltyLineModel();
        clone._flowers = new List<FlowerPiece>(_flowers);
        clone._hasFirstPlayerToken = _hasFirstPlayerToken;
        return clone;
    }

    public bool AddFlower(FlowerPiece flower)
    {
        if(IsFull)
            return false;
        _flowers.Add(flower);
        OnPenaltyChanged?.Invoke();
        return true;
    }

    public FlowerPiece[] GetAllFlowers()
    {
        return _flowers.ToArray();
    }

    void IFlowerTarget.Clear()
    {
        this.Clear();
    }

    public FlowerPiece GetFlowerAt(int t)
    {
        return _flowers[t];
    }
    #endregion

    #region HELPER METHODS

    #endregion

    #region VALIDATION
    #endregion
}
