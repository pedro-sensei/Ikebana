using System;
using System.Collections.Generic;
//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


/// <summary>
/// Model for the bag of flowers.
/// Game rules:
/// - The bag starts with a predefined number of flowers of each type.
/// - When the bag runs out of flowers it is automatically refilled from the discard.
/// - At the start of each round flowers are drawn from the bag to fill the displays.
/// </summary>

public class BagModel : IFlowerSource
{
    #region FIELDS AND PROPERTIES
    private List<FlowerPiece> _flowers;
    private Random _random;
    
    public int Count => _flowers.Count;
    public bool IsEmpty => _flowers.Count == 0;
    public bool IsFull=> false; // The bag has no maximum capacity

    public int Capacity => int.MaxValue; // The bag can hold an unlimited number of flowers

    public IReadOnlyList<FlowerPiece> flowers => _flowers; // Return a copy to prevent external modification

    //Only event triggered is bag refilled
    public event Action OnBagRefilled;

    #endregion
    #region CONSTRUCTORS AND INITIALIZATION

    //Constructor for empty bag
    public BagModel()
    {
        _random = new Random();
        _flowers = new List<FlowerPiece>();
    }

    //Constructor for loading from saved state
    public BagModel(SerializedGameState savedState)
    {
        foreach (var flowerData in savedState.Bag)
        {
            _flowers.Add(new FlowerPiece((FlowerColor)flowerData));
        }
    }
    
    /// <summary>
    /// Fills the bag with the standard set of flowers and shuffles it. 
    /// Should be called at the start of the game.
    /// </summary>
    public void Initialize()
    {
        _flowers.Clear();

        for (int c = 0; c < GameConfig.NUM_COLORS; c++)
        {
            FlowerColor color = (FlowerColor)c;
            for (int i = 0; i < GameConfig.COUNT_PER_COLOR; i++)
            {
                _flowers.Add(new FlowerPiece(color));
            }
        }

        Shuffle();
    }

    #endregion
    #region GAME LOGIC
    /// <summary>
    /// Draws a single flower from the bag. Returns null if the bag is empty.
    /// </summary>
  
    ///TODO: HANDLE EMPTY BAG CASE IN CALLING CODE (REFILL FROM DISCARD)
    public FlowerPiece Draw()
    {
        if (_flowers.Count == 0)
            return null;

        int lastIndex = _flowers.Count - 1;
        FlowerPiece flower = _flowers[lastIndex];
        _flowers.RemoveAt(lastIndex);
        return flower;
    }

    /// <summary>
    /// Refills the bag with flowers from the discard pile. 
    /// This should be called when the bag runs out of flowers and more flowers are needed, 
    /// and there are still flowers in the discard pile.
    /// </summary>
    public void RefillFrom(DiscardModel discard)
    {
        List<FlowerPiece> discarded = discard.TakeAll();
        _flowers.AddRange(discarded);
        Shuffle();
        //Notify any listeners that the bag has been refilled
        OnBagRefilled?.Invoke();
    }
    
    #endregion
    #region HELPER METHODS
    public void Clear()
    {
        _flowers.Clear();
    }

    /// <summary>
    /// Adds a flower to the bag (for testing or special game effects). 
    /// </summary>
    /// <param name="flowerPiece"></param>
    public bool AddFlower(FlowerPiece flowerPiece)
    {
        _flowers.Add(flowerPiece);
        return true;
    }

    /// <summary>
    /// Returns the number of flowers of each color in the bag (for UI display only)
    /// </summary>
    public Dictionary<FlowerColor, int> GetColorCounts()
    {
        Dictionary<FlowerColor, int> counts = new Dictionary<FlowerColor, int>();
        foreach (FlowerPiece flower in _flowers)
        {
            if (counts.ContainsKey(flower.Color))
                counts[flower.Color]++;
            else
                counts[flower.Color] = 1;
        }
        return counts;
    }


    // Shuffles the flowers in the bag using the Fisher-Yates algorithm.
    private void Shuffle()
    {
        for (int i = _flowers.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            FlowerPiece temp = _flowers[i];
            _flowers[i] = _flowers[j];
            _flowers[j] = temp;
        }
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

    public int[] GetColorCountsArray()
    {
        return GetflowerCountsByColor();
    }

    public int GetflowerCountByColor(FlowerColor color)
    {
        int[] counts = GetflowerCountsByColor();
        switch (color)
        {
            case FlowerColor.Blue:
                return counts[0];
            case FlowerColor.Yellow:
                return counts[1];
            case FlowerColor.Red:
                return counts[2];
            case FlowerColor.Black:
                return counts[3];
            case FlowerColor.Green:
                return counts[4];
            default:
                throw new ArgumentOutOfRangeException(nameof(color), color, null);
        }
    }
    

    public FlowerPiece TakeFlowerByColor(FlowerColor color)
    {
        FlowerPiece flower = _flowers.Find(t => t.Color == color);
        if (flower != null)
        {
            _flowers.Remove(flower);
        }
        return flower;
    }

    public FlowerPiece TakeFlower()
    {
        FlowerPiece flower = Draw();
        return flower;
    }

    public FlowerPiece[] GetAllFlowers()
    {
        return _flowers.ToArray();
    }

    #endregion
}