using System;
using System.Collections.Generic;


//=^..^=   =^..^=   VERSION 1.0.1 (March 2026)    =^..^=    =^..^=
//                    Last Update 22/03/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=
/// <summary>
/// Model for a placement line on the player board.
/// Each line has a specific capacity (1 to 5 in the standard game) and can only hold flowers of the same color.
/// It should notify when flowers are placed or removed for animation purposes.
/// It should verify if a color can be placed in the line (not full, not different color, not forbidden color).
/// Forbidden colors are those that have already been placed in the grid in the corresponding row.
/// 
/// When playing without color restrictions on the grid the forbidden colors logic may be more complicated.
/// Since columns also have to be taken into account.
/// 
/// 
/// 
/// </summary>

public class PlacementLineModel :IFlowerTarget
{
    #region FIELDS AND PROPERTIES
    private int _lineIndex;
    private int _capacity;
    private List<FlowerPiece> _flowers;
    private FlowerColor? _currentColor;
    private HashSet<FlowerColor> forbiddenColors;
    private HashSet<FlowerColor> allowedColors;

    public int LineIndex => _lineIndex;
    public int Capacity => _capacity;
    public int Count => _flowers.Count;
    public bool IsFull => _flowers.Count >= _capacity;
    public bool IsEmpty => _flowers.Count == 0;
    public FlowerColor? CurrentColor => _currentColor;
    public IReadOnlyList<FlowerPiece> Flowers => _flowers;

    public HashSet<FlowerColor> ForbiddenColors => forbiddenColors;
    public HashSet<FlowerColor> AllowedColors => allowedColors;
    #endregion
    #region EVENTS
    public event Action<int> OnLineChanged;
    #endregion
    #region CONSTRUCTORS AND INITIALIZATION
    public PlacementLineModel(int lineIndex, int capacity)
    {
        _lineIndex = lineIndex;
        _capacity = capacity;
        _flowers = new List<FlowerPiece>(capacity);
        _currentColor = null;
        forbiddenColors = new HashSet<FlowerColor>();
        allowedColors = null; // null means all colors are allowed (standard setup)
    }

    /// <summary>
    /// Sets the colors that are allowed in this placement line based on the grid pattern.
    /// Only colors present in the corresponding grid row are valid placements.
    /// Must be called after construction when using non-standard grid patterns.
    /// </summary>
    public void SetAllowedColors(HashSet<FlowerColor> colors)
    {
        allowedColors = colors;
    }
    #endregion
    #region GAME LOGIC
    /// <summary>
    /// Checks if a flower of the given color can be placed in the line.
    /// </summary>
    public bool CanAcceptColor(FlowerColor color)
    {
        if (IsFull)
            return false;

        // Check if this color exists in the grid row at all
        if (allowedColors != null && !allowedColors.Contains(color))
            return false;

        if (forbiddenColors.Contains(color))
            return false;

        if (_currentColor.HasValue && _currentColor.Value != color)
            return false;

        return true;
    }

    /// <summary>
    /// Places flowers in the line. 
    /// Returns a PlaceResult with the number of flowers placed and the overflow flowers.
    /// </summary>
    public PlaceResult PlaceFlowers(List<FlowerPiece> flowersToPlace)
    {
        PlaceResult result = new PlaceResult
        {
            NumberOfFlowersPlaced = 0,
            OverflowFlowers = new List<FlowerPiece>()
        };

        if (flowersToPlace.Count == 0)
            return result;

        FlowerColor color = flowersToPlace[0].Color;

        if (!CanAcceptColor(color))
        {
            result.OverflowFlowers.AddRange(flowersToPlace);
            return result;
        }

        // Assingn the color to the line if it's the first flower being placed
        _currentColor = color;

        int availableSpace = _capacity - _flowers.Count;

        foreach (FlowerPiece flower in flowersToPlace)
        {
            if (result.NumberOfFlowersPlaced < availableSpace)
            {
                _flowers.Add(flower);
                result.NumberOfFlowersPlaced++;
            }
            else
            {
                result.OverflowFlowers.Add(flower);
            }
        }
        //NOTIFY that the line has changed so the UI can update
        OnLineChanged?.Invoke(_lineIndex);
        return result;
    }

    /// <summary>
    /// Add a color to the forbidden colors set. 
    /// This should be called when a flower of that color is placed in the grid in the corresponding row.
    /// </summary>
    public void AddForbiddenColor(FlowerColor color)
    {
        forbiddenColors.Add(color);
    }

    /// <summary>
    /// Extracts the first flower to place it on the grid (scoring phase).
    /// Returns the flower and the remaining flowers to discard.
    /// Only if the line is complete.
    /// </summary>
    public (FlowerPiece flowerForGrid, List<FlowerPiece> flowersToDiscard) ScoreLine()
    {
        if (!IsFull)
            return (null, new List<FlowerPiece>());

        FlowerPiece flowerForGrid = _flowers[0];
        List<FlowerPiece> flowersToDiscard = new List<FlowerPiece>();

        for (int i = 1; i < _flowers.Count; i++)
        {
            flowersToDiscard.Add(_flowers[i]);
        }

        _flowers.Clear();
        _currentColor = null;
        //NOTIFY that the line has changed so the UI can update
        OnLineChanged?.Invoke(_lineIndex);

        return (flowerForGrid, flowersToDiscard);
    }

    /// <summary>
    /// Clears the line and returns all the flowers that were in it.
    /// </summary>
    public List<FlowerPiece> Clear()
    {
        List<FlowerPiece> cleared = new List<FlowerPiece>(_flowers);
        _flowers.Clear();
        _currentColor = null;
        //NOTIFY that the line has changed so the UI can update
        OnLineChanged?.Invoke(_lineIndex);
        return cleared;
    }

    public int GetAvailableSpace()
    {
        return _capacity - _flowers.Count;
    }
    #endregion
    #region HELPER METHODS
    public PlacementLineModel Clone()
    {
        PlacementLineModel clone = new PlacementLineModel(_lineIndex, _capacity);
        clone._currentColor = _currentColor;
        clone.forbiddenColors = new HashSet<FlowerColor>(forbiddenColors);
        clone.allowedColors = allowedColors != null ? new HashSet<FlowerColor>(allowedColors) : null;
        clone._flowers = new List<FlowerPiece>(_flowers);
        return clone;
    }

    public bool AddFlower(FlowerPiece flower)
    {
        if (CanAcceptColor(flower.Color))
        {
            PlaceResult result = PlaceFlowers(new List<FlowerPiece> { flower });
            return result.NumberOfFlowersPlaced > 0;
        }
        return false;
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
}
