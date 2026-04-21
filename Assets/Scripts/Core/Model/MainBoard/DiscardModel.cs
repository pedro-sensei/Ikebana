using System;
using System.Collections.Generic;
//=^..^=   =^..^=   VERSION 1.0.1 (March 2026)    =^..^=    =^..^=
//                    Last Update 22/03/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=
/// 
/// <summary>
/// Model for the discard pile. It holds flowers that have been discarded
/// in scoring phase or as an excess of penalty.
/// It should notify when a flower is discarded for animation purposes
/// It should also allow the bag to take all discarded flowers when it needs to be refilled, 
/// and notify the UI when this happens so it can update any indicators 
/// related to the discard pile.
/// 
/// </summary>
[Serializable]
public class DiscardModel :IFlowerTarget
{
    #region FIELDS AND PROPERTIES
    private List<FlowerPiece> _flowers;
    public int Count => _flowers.Count;
    public bool IsEmpty => _flowers.Count == 0;
    public IReadOnlyList<FlowerPiece> flowers => _flowers;

    public bool IsFull => throw new NotImplementedException();

    public int Capacity => throw new NotImplementedException();

    #endregion
    #region EVENTS
    public event Action<int> OnflowersDiscarded;
    #endregion
    #region CONSTRUCTORS AND INITIALIZATION
    /// <summary>
    /// standard constructor for an empty discard pile at the start of the game.
    /// </summary>
    public DiscardModel()
    {
        _flowers = new List<FlowerPiece>();
    }
    /// <summary>
    /// Contructor for loading from saved state. 
    /// It should initialize the discard pile with the flowers that were saved in the game state.
    /// </summary>
    /// <param name="savedState"></param>
    public DiscardModel(SerializedGameState savedState)
    {
        int[] savedDiscard = savedState.Discard;
        _flowers = new List<FlowerPiece>();
        foreach (int flowerData in savedDiscard)
        {
            _flowers.Add(new FlowerPiece((FlowerColor)flowerData));
        }
    }

    internal void Initialize()
    {
        _flowers = new List<FlowerPiece>();
    }

    #endregion
    #region GAME LOGIC
    /// <summary>
    /// Add a flower to the discard pile.
    /// </summary>
    public void Add(FlowerPiece flower)
    {
        if (flower == null || flower.IsFirstPlayerToken)
            return;

        _flowers.Add(flower);
    }

    /// <summary>
    /// Add multiple flowers to the discard pile. 
    /// For example when taking flowers from a factory
    /// and discarding the leftovers.
    /// </summary>
    public void AddRange(List<FlowerPiece> flowersToDiscard)
    {
        foreach (FlowerPiece flower in flowersToDiscard)
        {
            Add(flower);
        }

        // Notify how many flowers were discarded for animation purposes
        //TODO REPLACE NOTIFY WITH ALSO THE COLOR OF THE DISCARDED flowerS,
        //SO THE ANIMATION CAN BE MORE ACCURATE
        OnflowersDiscarded?.Invoke(flowersToDiscard.Count);
    }

    /// <summary>
    /// Takes all flowers from the discard pile (used by BagModel.RefillFrom).
    /// </summary>
    public List<FlowerPiece> TakeAll()
    {
        List<FlowerPiece> all = new List<FlowerPiece>(_flowers);
        _flowers.Clear();
        return all;
    }
    #endregion
    #region HELPER METHODS
    

    public void Clear()
    {
        _flowers.Clear();
    }

    public bool AddFlower(FlowerPiece flowerPiece)
    {
        _flowers.Add(flowerPiece);
        return true;
    }
    public int[] GetflowerCountsByColor()
    {
        //Index 0-4 correspond to Blue, Yellow, Red, Black, Green respectively
        int[] counts = new int[5];
        foreach (FlowerPiece flower in _flowers)
        {
            switch (flower.Color)
            {
                case FlowerColor.Blue:
                    counts[0]++;
                    break;
                case FlowerColor.Yellow:
                    counts[1]++;
                    break;
                case FlowerColor.Red:
                    counts[2]++;
                    break;
                case FlowerColor.Black:
                    counts[3]++;
                    break;
                case FlowerColor.Green:
                    counts[4]++;
                    break;
            }
        }
        return counts;
    }

    public FlowerPiece[] GetAllFlowers()
    {
        return _flowers.ToArray();
    }

    #endregion
}
