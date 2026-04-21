using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;

//=^..^=   =^..^=   VERSION 1.0.1 (March 2026)    =^..^=    =^..^=
//                    Last Update 22/03/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

/// <summary>
/// Model for a display either factory or central. 
/// 
/// Factory: in standard game, there are 5 factory displays that start with 4 flowers each.
/// Central: gets leftover flowers from factory picks, 
/// and is where the first player token starts each round.
/// </summary>
[Serializable]
public class DisplayModel : IFlowerSource
{
    #region FIELDS AND PROPERTIES
    private List<FlowerPiece> _flowers;
    private bool _isCentral;
    private bool _hasFirstPlayerToken;
    private int _index;
    private int _capacity;

    public IReadOnlyList<FlowerPiece> flowers => _flowers;
    public bool IsCentral => _isCentral;
    public bool HasFirstPlayerToken => _hasFirstPlayerToken;
    public int Count => _flowers.Count;
    public bool IsEmpty => _flowers.Count == 0 && !_hasFirstPlayerToken;
    public int ViewIndex => _index;

    public bool IsFull => Count>=Capacity;  

    public int Capacity =>_capacity;
    #endregion

    #region EVENTS
    public event Action OnDisplayChanged;
    #endregion

    #region CONSTRUCTORS AND INITIALIZATION
    
    public DisplayModel(int index, bool isCentral)
    {
        _index = index;
        _isCentral = isCentral;
        _flowers = new List<FlowerPiece>();
        _hasFirstPlayerToken = false;
        _capacity = GameConfig.FLOWERS_PER_DISPLAY;
        if (_isCentral)
        {
            // Central display starts with the first player token
            PlaceFirstPlayerToken();
            _capacity = int.MaxValue; // Central display can hold unlimited flowers
        }
    }

    #endregion

    #region GAME LOGIC

    /// <summary>
    /// Add flowers to the display (used at the start of the round to populate factory displays,
    /// or when transferring flowers to the central display)
    /// </summary>
    public void Addflowers(List<FlowerPiece> newflowers)
    {
        _flowers.AddRange(newflowers);
        //NOTIFY that the display has changed so the UI can update
        OnDisplayChanged?.Invoke();
    }


    /// <summary>
    /// Place the first player token on the central display
    /// </summary>
    public void PlaceFirstPlayerToken()
    {
        if (!_isCentral) return;
        _hasFirstPlayerToken = true;
        //NOTIFY that the display has changed so the UI can update
        OnDisplayChanged?.Invoke();
    }

    /// <summary>
    /// This method handles picking flowers of a specific color from the display.
    /// It returns a PickResult struct with the two lists of picked flowers
    /// and remaining flowers, as well as whether the first player token 
    /// was picked (for central display).
    /// </summary>
    public PickResult PickColor(FlowerColor color)
    {
        PickResult result = new PickResult
        {
            Pickedflowers = new List<FlowerPiece>(),
            Remainingflowers = new List<FlowerPiece>(),
            PickedFirstPlayerToken = false
        };

        foreach (FlowerPiece flower in _flowers)
        {
            if (flower.Color == color)
            {
                result.Pickedflowers.Add(flower);
            }
            else
            {
                result.Remainingflowers.Add(flower);
            }
        }

        // If central and has first player token,
        // it is always picked when picking from the central display
        if (_isCentral && _hasFirstPlayerToken)
        {
            result.PickedFirstPlayerToken = true;
            _hasFirstPlayerToken = false;
        }

        _flowers.Clear();
        //NOTIFY that the display has changed so the UI can update
        OnDisplayChanged?.Invoke();
        return result;
    }



    public void AddStartingPlayerToken()
    {
        if (!_isCentral) return;
        _hasFirstPlayerToken = true;
        OnDisplayChanged?.Invoke();
    }


    public bool AddFlower(FlowerPiece flower)
    {
        if (IsFull) return false;
        _flowers.Add(flower);
        OnDisplayChanged?.Invoke();
        return true;
    }

    public FlowerPiece TakeFlowerByColor(FlowerColor color)
    {
        if (IsEmpty) return null;
        FlowerPiece flower = _flowers.Find(t => t.Color == color);
        if (flower != null)
        {
            _flowers.Remove(flower);
            OnDisplayChanged?.Invoke();
        }
        return flower;
    }

    public FlowerPiece TakeFlower()
    {
        if (IsEmpty) return null;
        FlowerPiece flower = _flowers[0];
        _flowers.RemoveAt(0);
        OnDisplayChanged?.Invoke();
        return flower;
    }


    #endregion

    #region HELPER METHODS
    public FlowerPiece[] GetAllFlowers()
    {
        return _flowers.ToArray();
    }


    public int[] GetColorCountsArray()
    {
        int[] counts = new int[GameConfig.NUM_COLORS];
        foreach (FlowerPiece flower in _flowers)
        {
            counts[(int)flower.Color]++;
        }
        return counts;
    }

    public int GetflowerCountByColor(FlowerColor color)
    {
        return CountColor(color);
    }
    // Gets a set of the available colors on the display,
    // for UI (like showing which colors can be picked)
    // Ignore special and empty
    public HashSet<FlowerColor> GetAvailableColors()
    {
        HashSet<FlowerColor> colors = new HashSet<FlowerColor>();
        foreach (FlowerPiece flower in _flowers)
        {
            if (flower.Color == FlowerColor.Special1) continue;
            if (flower.Color == FlowerColor.Special2) continue;
            if (flower.Color == FlowerColor.Special3) continue;
            if (flower.Color == FlowerColor.FirstPlayer) continue;
            if (flower.Color == FlowerColor.Empty) continue;
            colors.Add(flower.Color);
        }
        return colors;
    }

    // Returns the count of flowers of a specific color on the display
    public int CountColor(FlowerColor color)
    {
        int count = 0;
        foreach (FlowerPiece flower in _flowers)
        {
            if (flower.Color == color) count++;
        }
        return count;
    }

    // Gets an ordered list of the flowers on the display sorted by color, for UI purposes
    public List<FlowerPiece> GetflowersSortedByColor()
    {
        List<FlowerPiece> sorted = new List<FlowerPiece>(_flowers);
        sorted.Sort((a, b) => a.Color.CompareTo(b.Color));
        return sorted;
    }

    public void Clear()
    {
        _flowers.Clear();
        _hasFirstPlayerToken = false;
        //NOTIFY that the display has changed so the UI can update
        OnDisplayChanged?.Invoke();
    }

    #endregion
}

