using System.Collections.Generic;

//=^..^=   =^..^=   VERSION 1.0.2 (April 2026)    =^..^=    =^..^=
//                    Last Update 01/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=



/// Represents a single valid move.
///
/// Indexing  (0-based):
/// FactoryIndex   : 0-based (0 to N-1). Central is indicated by SourceEnum == MoveSource.Central.
/// TargetLineIndex: 0-based (0 to N-1). Penalty is indicated by TargetEnum == MoveTarget.PenaltyLine.

[System.Serializable]
public struct GameMove
{
    // ?? Identity ??
    public int PlayerIndex;

    // ?? Source ??
    public IFlowerSource Source;        // full model reference (null in minimal path)
    public MoveSource SourceEnum;       // lightweight enum (always set)
    public bool SourceIsFactory;
    public int  FactoryIndex;

    // ?? Target ??
    public IFlowerTarget Target;        // full model reference (null in minimal path)
    public MoveTarget TargetEnum;       // lightweight enum (always set)
    public int  TargetLineIndex;
    public bool IsPenalty;

    public FlowerColor Color;
    public int flowerCount;             
    public int flowersToLine;           
    public int flowersToPenalty;        
    public int flowersToDiscard;        

    public byte[] centralDumpColors;
    public int    centralDumpCount;

 
    public bool gotFirstPlayerToken;


    public int prevPenaltyCount;

    public int prevLineCount;

    public byte prevLineColor;

    public GameMove(int playerIndex, 
                    IFlowerSource source,
                    bool sourceIsFactory,
                    int factoryIndex, 
                    IFlowerTarget target,
                    int targetLineIndex,
                    bool isPenalty,
                    FlowerColor color,
                    int flowerCount,
                    int flowersToLine,
                    int flowersToPenalty,
                    int flowersToDiscard,
                    byte[] centralDumpColors,
                    int centralDumpCount)
    {
        PlayerIndex = playerIndex;
        Source = source;
        SourceEnum = sourceIsFactory ? MoveSource.Factory : MoveSource.Central;
        SourceIsFactory = sourceIsFactory;
        FactoryIndex = factoryIndex;
        Target = target;
        TargetEnum = isPenalty ? MoveTarget.PenaltyLine : MoveTarget.PlacementLine;
        TargetLineIndex = targetLineIndex;
        IsPenalty = isPenalty;
        Color = color;
        this.flowerCount = flowerCount;
        this.flowersToLine = flowersToLine;
        this.flowersToPenalty = flowersToPenalty;
        this.flowersToDiscard = flowersToDiscard;
        this.centralDumpColors = centralDumpColors;
        this.centralDumpCount = centralDumpCount;
        gotFirstPlayerToken = false;
        prevPenaltyCount = 0;
        prevLineCount = 0;
        prevLineColor = 255;
    }

    //For minimal model.
    public GameMove(int playerIndex,
                    MoveSource sourceEnum,
                    int factoryIndex,
                    FlowerColor color,
                    MoveTarget targetEnum,
                    int targetLineIndex,
                    int flowerCount,
                    int flowersToLine,
                    int flowersToPenalty)
    {
        PlayerIndex = playerIndex;
        Source = null;
        SourceEnum = sourceEnum;
        SourceIsFactory = sourceEnum == MoveSource.Factory;
        FactoryIndex = factoryIndex;
        Target = null;
        TargetEnum = targetEnum;
        TargetLineIndex = targetLineIndex;
        IsPenalty = targetEnum == MoveTarget.PenaltyLine;
        Color = color;
        this.flowerCount = flowerCount;
        this.flowersToLine = flowersToLine;
        this.flowersToPenalty = flowersToPenalty;
        this.flowersToDiscard = 0;
        this.centralDumpColors = null;
        this.centralDumpCount = 0;
        gotFirstPlayerToken = false;
        prevPenaltyCount = 0;
        prevLineCount = 0;
        prevLineColor = 255;
    }

    public override string ToString()
    {
        string src = SourceIsFactory ? "Factory " + FactoryIndex : "Central";
        string tgt = IsPenalty ? "Penalty" : "Line " + TargetLineIndex;
        return $"[{src} ? {Color} x{flowerCount} ? {tgt}]";
    }
}

public enum MoveSource
{
    Factory,
    Central
}

public enum MoveTarget
{
    PlacementLine,
    PenaltyLine
}

public struct PickResult
{
    public System.Collections.Generic.List<FlowerPiece> Pickedflowers;
    public System.Collections.Generic.List<FlowerPiece> Remainingflowers;
    public bool PickedFirstPlayerToken;
}


public struct PlaceResult
{
    public int NumberOfFlowersPlaced;
    public System.Collections.Generic.List<FlowerPiece> OverflowFlowers;
    public System.Collections.Generic.List<FlowerPiece> Excessflowers;
}