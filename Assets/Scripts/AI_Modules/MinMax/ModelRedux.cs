using System;

//=^..^=   =^..^=   VERSION 1.0.1 (March 2026)    =^..^=    =^..^=
//                    Last Update 22/03/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


// Model for MinMax, lighter than original. 
// KEEP IN MIND:
// - Don`t use "new" in move generation or execution.
// - Pre-allocate everything if possible.
// - Use only structs if possible.
// - Use undo, snapshot or similar.
//TODO: INCORPORATE UNDO IN MODEL AND REPLACE THIS.


// Encoding:
//   Bag/Discard/Central: int[numColors] color counts.
//   Displays:           int[numFactories * numColors] flattened color counts.
//   Placement lines:     encoded as (color, count) per line.
//   Wall:                bitmask int (row*gridSize+col).
public struct ModelRedux
{


    public const int MAX_FACTORIES     = 9;
    public const int MAX_PLAYERS       = 4;
    public const int MAX_MOVES         = 200;


    public int GridSize;
    public int NumColors;
    public int flowersPerColor;
    public int flowersPerFactory;
    public int PenaltyCapacity;
    public int[] PenaltyValues;
    public int BonusRow;
    public int BonusColumn;
    public int BonusColor;
    public int NumPlacementLines;
    public int[] PlacementLineCapacities;

    // Wall pattern lookup: GridPattern[row, col] = colorIndex
    //TODO: MAKE IT PER PLAYER FOR VARIANTS.
    public int[,] GridPattern;

    // Bag flower counts per color [numColors]
    public int[] Bag;
    // Discard flower counts per color [numColors]
    public int[] Discard;
    // Central display flower counts per color [numColors]
    public int[] Central;
    // Factory flower counts, flattened [numFactories * numColors]. Factory f, color c = index f*numColors+c.
    public int[] Displays;

    public bool CentralHasToken;
    public int  NumDisplays;
    public int  NumPlayers;
    public int  CurrentPlayer;
    public int  CurrentRound;
    public int  TurnNumber;
    public int  StartingPlayer;
    public bool IsGameOver;

    // Player boards
    public PlayerRedux[] Players;

    //GameConfig snapshot

    public static ModelRedux Create(int numPlayers, GameConfigSnapshot config)
    {
        int gridSize     = config.GridSize;
        int numColors    = config.NumColors;
        int numFactories = config.NumFactories;
        int numLines     = config.NumPlacementLines;

        // Build the wall pattern from the snapshot (use player 0's pattern as the shared one)
        int[,] wallPattern = config.WallPatterns[0];

        ModelRedux state = new ModelRedux
        {
            // Config
            GridSize       = gridSize,
            NumColors      = numColors,
            flowersPerColor  = config.flowersPerColor,
            flowersPerFactory = config.flowersPerFactory,
            PenaltyCapacity = config.PenaltyCapacity,
            PenaltyValues  = config.PenaltyValues,
            BonusRow       = config.BonusRow,
            BonusColumn    = config.BonusColumn,
            BonusColor     = config.BonusColor,
            GridPattern    = wallPattern,
            NumPlacementLines      = numLines,
            PlacementLineCapacities = (int[])config.PlacementLineCapacities.Clone(),

            // State arrays
            Bag          = new int[numColors],
            Discard      = new int[numColors],
            Central      = new int[numColors],
            Displays    = new int[numFactories * numColors],
            NumDisplays = numFactories,
            NumPlayers   = numPlayers,
            Players      = new PlayerRedux[numPlayers]
        };
        for (int p = 0; p < numPlayers; p++)
            state.Players[p] = PlayerRedux.Create(numLines);
        return state;
    }

    public static ModelRedux Create(int numPlayers)
    {
        GameConfigSnapshot config = GameConfig.CreateSnapshot();
        return Create(numPlayers, config);
    }

    // Resets for a new game.
    public void ResetForNewGame()
    {
        for (int c = 0; c < NumColors; c++)
        {
            Bag[c]     = flowersPerColor;
            Discard[c] = 0;
            Central[c] = 0;
        }
        Array.Clear(Displays, 0, Displays.Length);
        CentralHasToken = false;
        CurrentRound    = 0;
        TurnNumber      = 0;
        StartingPlayer  = 0;
        IsGameOver      = false;
        CurrentPlayer   = 0;
        for (int p = 0; p < NumPlayers; p++)
            Players[p].Reset(NumPlacementLines);
    }

    //Factory
    public int GetFactoryColor(int factory, int color)
    {
        return Displays[factory * NumColors + color];
    }

    public void SetFactoryColor(int factory, int color, int count)
    {
        Displays[factory * NumColors + color] = count;
    }

    public int GetFactoryTotalflowers(int factory)
    {
        int sum = 0;
        int offset = factory * NumColors;
        for (int c = 0; c < NumColors; c++) sum += Displays[offset + c];
        return sum;
    }

    public bool IsFactoryEmpty(int factory)
    {
        return GetFactoryTotalflowers(factory) == 0;
    }

    //GRid

    public int GetGridColumn(int row, int colorIndex)
    {
        for (int c = 0; c < GridSize; c++)
            if (GridPattern[row, c] == colorIndex) return c;
        return 0;
    }

    
    //Rounds.
    public void StartNewRound(Random rng)
    {
        CurrentRound++;
        CurrentPlayer   = StartingPlayer;
        TurnNumber      = 1;
        CentralHasToken = true;

        for (int c = 0; c < NumColors; c++)
        {
            Discard[c] += Central[c];
            Central[c]  = 0;
        }

        for (int f = 0; f < NumDisplays; f++)
        {
            int offset = f * NumColors;
            for (int c = 0; c < NumColors; c++) Displays[offset + c] = 0;

            for (int t = 0; t < flowersPerFactory; t++)
            {
                int bagTotal = BagTotal();
                if (bagTotal == 0) RefillBagFromDiscard();
                bagTotal = BagTotal();
                if (bagTotal == 0) break;

                // Pick a random flower from the bag
                int pick = rng.Next(bagTotal);
                int drawn = DrawFromBag(pick);
                Displays[offset + drawn]++;
            }
        }
    }

    private int BagTotal()
    {
        int sum = 0;
        for (int c = 0; c < NumColors; c++) sum += Bag[c];
        return sum;
    }

    private void RefillBagFromDiscard()
    {
        for (int c = 0; c < NumColors; c++)
        {
            Bag[c]    += Discard[c];
            Discard[c] = 0;
        }
    }

    private int DrawFromBag(int pick)
    {
        for (int c = 0; c < NumColors; c++)
        {
            if (pick < Bag[c]) { Bag[c]--; return c; }
            pick -= Bag[c];
        }
        //Draw first available
        for (int c = 0; c < NumColors; c++)
            if (Bag[c] > 0) { Bag[c]--; return c; }
        return 0;
    }

   
    public bool AreDisplaysEmpty()
    {
        for (int c = 0; c < NumColors; c++)
            if (Central[c] > 0) return false;
        for (int i = 0; i < NumDisplays * NumColors; i++)
            if (Displays[i] > 0) return false;
        return true;
    }

   //Valid moves to buffer
    public int GetValidMoves(MoveRedux[] moves)
    {
        int count = 0;
        if (IsGameOver) return 0;

        ref PlayerRedux player = ref Players[CurrentPlayer];

        // Factory moves
        for (int f = 0; f < NumDisplays; f++)
        {
            int offset = f * NumColors;
            for (int c = 0; c < NumColors; c++)
            {
                int flowerCount = Displays[offset + c];
                if (flowerCount == 0) continue;
                AppendMoves(moves, ref count, ref player, false, f, c, flowerCount);
            }
        }

        // Central moves
        for (int c = 0; c < NumColors; c++)
        {
            if (Central[c] == 0) continue;
            AppendMoves(moves, ref count, ref player, true, -1, c, Central[c]);
        }

        return count;
    }

    private void AppendMoves(MoveRedux[] moves, ref int count,
        ref PlayerRedux player, bool isCentral, int factory, int color, int flowerCount)
    {
        // One move per valid placement line
        for (int line = 0; line < NumPlacementLines; line++)
        {
            if (count >= moves.Length) return;
            if (player.CanPlaceInLine(line, color, PlacementLineCapacities[line]))
            {
                moves[count++] = new MoveRedux
                {
                    IsCentralSource = isCentral,
                    FactoryIndex    = factory,
                    ColorIndex      = color,
                    flowerCount       = flowerCount,
                    TargetIsPenalty = false,
                    TargetLineIndex = line
                };
            }
        }
        // Always allow penalty as a target
        if (count < moves.Length)
        {
            moves[count++] = new MoveRedux
            {
                IsCentralSource = isCentral,
                FactoryIndex    = factory,
                ColorIndex      = color,
                flowerCount       = flowerCount,
                TargetIsPenalty = true,
                TargetLineIndex = -1
            };
        }
    }

    //Execute move and create undo
    public MoveRecord ExecuteMove(MoveRedux move)
    {
        MoveRecord record = MoveRecord.Create(NumColors);
        ExecuteMove(move, ref record);
        return record;
    }

    // Executes a move writing undo 
    public void ExecuteMove(MoveRedux move, ref MoveRecord record)
    {
        ref PlayerRedux player = ref Players[CurrentPlayer];

        record.PlayerIndex            = CurrentPlayer;
        record.Move                   = move;
        record.PrevPenaltyCount       = player.PenaltyCount;
        record.PrevHasFirstPlayerToken = player.HasFirstPlayerToken;
        record.GotFirstPlayerToken    = false;
        record.flowersPlacedOnLine      = 0;
        record.flowersToPenalty         = 0;
        record.PrevLineColor          = -1;
        record.PrevLineCount          = 0;
        record.PrevCurrentPlayer      = CurrentPlayer;
        record.PrevTurnNumber         = TurnNumber;
        record.PrevCentralHasToken    = CentralHasToken;
        for (int i = 0; i < NumColors; i++)
            record.flowersDumpedToCentral[i] = 0;

        int color = move.ColorIndex;

        if (!move.IsCentralSource)
        {
            // Pick from factory: take all of chosen color, dump rest to central
            int fIdx   = move.FactoryIndex;
            int offset = fIdx * NumColors;
            for (int c = 0; c < NumColors; c++)
            {
                if (c == color) continue;
                int dump = Displays[offset + c];
                if (dump > 0)
                {
                    Central[c] += dump;
                    record.flowersDumpedToCentral[c] = dump;
                    Displays[offset + c] = 0;
                }
            }
            Displays[offset + color] = 0;
        }
        else
        {
            // Pick from central: remove all of chosen color
            Central[color] -= move.flowerCount;
            if (Central[color] < 0) Central[color] = 0;

            // First player token
            if (CentralHasToken)
            {
                record.GotFirstPlayerToken = true;
                CentralHasToken = false;
                player.HasFirstPlayerToken = true;
                if (player.PenaltyCount < PenaltyCapacity)
                    player.PenaltyCount++;
            }
        }

        int flowersInHand = move.flowerCount;

        if (move.TargetIsPenalty)
        {
            // All flowers go to penalty
            int added = Math.Min(flowersInHand, PenaltyCapacity - player.PenaltyCount);
            player.PenaltyCount += added;
            record.flowersToPenalty = added;
        }
        else
        {
            int line     = move.TargetLineIndex;
            int capacity = PlacementLineCapacities[line];

            record.PrevLineColor = player.LineColors[line];
            record.PrevLineCount = player.LineCounts[line];

            int spaceLeft = capacity - player.LineCounts[line];
            int placed    = Math.Min(spaceLeft, flowersInHand);
            int overflow  = flowersInHand - placed;

            player.LineColors[line] = color;
            player.LineCounts[line] += placed;
            record.flowersPlacedOnLine = placed;

            // Overflow goes to penalty
            int penAdded = Math.Min(overflow, PenaltyCapacity - player.PenaltyCount);
            player.PenaltyCount += penAdded;
            record.flowersToPenalty = penAdded;
        }
    }

    // Undoes a move using the record returned by ExecuteMove.
    // Restores the game state to exactly before the move happened.
    public void UndoMove(ref MoveRecord record)
    {
        ref PlayerRedux player = ref Players[record.PlayerIndex];
        MoveRedux move = record.Move;
        int color = move.ColorIndex;

        // Restore placement line state
        if (!move.TargetIsPenalty)
        {
            int line = move.TargetLineIndex;
            player.LineColors[line] = record.PrevLineColor;
            player.LineCounts[line] = record.PrevLineCount;
        }

        // Restore penalty count and token state
        player.PenaltyCount        = record.PrevPenaltyCount;
        player.HasFirstPlayerToken = record.PrevHasFirstPlayerToken;
        CentralHasToken            = record.PrevCentralHasToken;

        // Restore source display
        if (!move.IsCentralSource)
        {
            int fIdx   = move.FactoryIndex;
            int offset = fIdx * NumColors;
            Displays[offset + color] = move.flowerCount;
            for (int c = 0; c < NumColors; c++)
            {
                if (c == color) continue;
                Central[c] -= record.flowersDumpedToCentral[c];
                Displays[offset + c] = record.flowersDumpedToCentral[c];
            }
        }
        else
        {
            Central[color] += move.flowerCount;
        }

        // Restore turn state
        CurrentPlayer = record.PrevCurrentPlayer;
        TurnNumber    = record.PrevTurnNumber;
    }

    // Advances to the next player's turn. Returns true if the round continues.
    public bool AdvanceTurn()
    {
        TurnNumber++;
        CurrentPlayer = (CurrentPlayer + 1) % NumPlayers;
        return !AreDisplaysEmpty();
    }

    
    // Scores all players at end of round
    //bool for game end.
    public bool ScoreEndOfRound()
    {
        bool gameEnding = false;

        for (int p = 0; p < NumPlayers; p++)
        {
            ref PlayerRedux player = ref Players[p];

            // Place completed lines onto wall
            for (int line = 0; line < NumPlacementLines; line++)
            {
                int capacity = PlacementLineCapacities[line];
                if (player.LineCounts[line] < capacity) continue;

                int color = player.LineColors[line];
                if (color < 0 || color >= NumColors) continue;

                int col = GetGridColumn(line, color);
                player.SetWallOccupied(line, col, true, GridSize);
                player.Score += CalculateWallPoints(ref player, line, col);

                // Excess flowers to discard (capacity - 1; the placed one is on the wall)
                int excess = capacity - 1;
                Discard[color] += excess;

                player.LineCounts[line] = 0;
                player.LineColors[line] = -1;
                player.SetForbidden(line, color);
            }

            // Check for complete row
            if (player.HasAnyCompleteRow(GridSize))
                gameEnding = true;

            // Apply penalty
            player.Score += CalculatePenalty(player.PenaltyCount);
            if (player.Score < 0) player.Score = 0;

            // Return first player token
            if (player.HasFirstPlayerToken)
            {
                StartingPlayer = p;
                player.HasFirstPlayerToken = false;
            }
            player.PenaltyCount = 0;
        }

        IsGameOver = gameEnding;
        return gameEnding;
    }

    // Applies end-game bonuses (complete rows, columns, colors).
    public void ApplyEndGameBonuses()
    {
        for (int p = 0; p < NumPlayers; p++)
        {
            ref PlayerRedux player = ref Players[p];
            int bonus = 0;

            // Complete rows
            for (int r = 0; r < GridSize; r++)
            {
                bool full = true;
                for (int c = 0; c < GridSize; c++)
                    if (!player.IsWallOccupied(r, c, GridSize)) { full = false; break; }
                if (full) bonus += BonusRow;
            }

            // Complete columns
            for (int c = 0; c < GridSize; c++)
            {
                bool full = true;
                for (int r = 0; r < GridSize; r++)
                    if (!player.IsWallOccupied(r, c, GridSize)) { full = false; break; }
                if (full) bonus += BonusColumn;
            }

            // Complete colors
            for (int color = 0; color < NumColors; color++)
            {
                bool all = true;
                for (int r = 0; r < GridSize; r++)
                    if (!player.IsWallOccupied(r, GetGridColumn(r, color), GridSize)) { all = false; break; }
                if (all) bonus += BonusColor;
            }

            player.Score += bonus;
        }
    }

    //Helpers

    public int CalculateWallPoints(ref PlayerRedux p, int row, int col)
    {
        int h = 1, v = 1;
        for (int c = col - 1; c >= 0       && p.IsWallOccupied(row, c, GridSize); c--) h++;
        for (int c = col + 1; c < GridSize  && p.IsWallOccupied(row, c, GridSize); c++) h++;
        for (int r = row - 1; r >= 0       && p.IsWallOccupied(r, col, GridSize); r--) v++;
        for (int r = row + 1; r < GridSize  && p.IsWallOccupied(r, col, GridSize); r++) v++;
        if (h == 1 && v == 1) return 1;
        int pts = 0;
        if (h > 1) pts += h;
        if (v > 1) pts += v;
        return pts;
    }

    public int CalculatePenalty(int penaltyCount)
    {
        int total = 0;
        for (int i = 0; i < penaltyCount && i < PenaltyCapacity; i++)
            total += PenaltyValues[i];
        return total;
    }
}


public struct PlayerRedux
{
    public int   WallBits;
    public int[] LineColors;   // [gridSize] color index, -1 = empty
    public int[] LineCounts;   // [gridSize] flower count
    public int[] Forbidden;    // [gridSize] bitfield: bit c set = color c forbidden on that line
    public int   PenaltyCount;
    public int   Score;
    public bool  HasFirstPlayerToken;

    public static PlayerRedux Create(int gridSize)
    {
        int[] lineColors = new int[gridSize];
        for (int i = 0; i < gridSize; i++)
            lineColors[i] = -1;

        return new PlayerRedux
        {
            WallBits   = 0,
            LineColors = lineColors,
            LineCounts = new int[gridSize],
            Forbidden  = new int[gridSize],
            PenaltyCount = 0,
            Score      = 0,
            HasFirstPlayerToken = false
        };
    }

    public void Reset(int gridSize)
    {
        WallBits = 0;
        PenaltyCount = 0;
        Score = 0;
        HasFirstPlayerToken = false;
        for (int i = 0; i < gridSize; i++)
        {
            LineColors[i] = -1;
            LineCounts[i] = 0;
            Forbidden[i]  = 0;
        }
    }

    public bool IsWallOccupied(int row, int col, int gridSize)
    {
        return (WallBits & (1 << (row * gridSize + col))) != 0;
    }

    public void SetWallOccupied(int row, int col, bool occupied, int gridSize)
    {
        int bit = 1 << (row * gridSize + col);
        if (occupied) WallBits |=  bit;
        else          WallBits &= ~bit;
    }

    public bool HasAnyCompleteRow(int gridSize)
    {
        for (int r = 0; r < gridSize; r++)
        {
            bool full = true;
            for (int c = 0; c < gridSize; c++)
                if (!IsWallOccupied(r, c, gridSize)) { full = false; break; }
            if (full) return true;
        }
        return false;
    }

    public void SetForbidden(int line, int colorIndex)
    {
        Forbidden[line] |= (1 << colorIndex);
    }

    public bool IsForbidden(int line, int colorIndex)
    {
        return (Forbidden[line] & (1 << colorIndex)) != 0;
    }


    // Checks whether a color can be placed on a given line.
    // A line accepts a color if: not full, color not forbidden, and line is
    // either empty or already holds the same color.
    public bool CanPlaceInLine(int line, int color, int capacity)
    {
        if (LineCounts[line] >= capacity)                          return false;
        if (IsForbidden(line, color))                              return false;
        if (LineColors[line] != -1 && LineColors[line] != color)   return false;
        return true;
    }

    public bool IsLineFull(int line, int capacity)
    {
        return LineCounts[line] >= capacity;
    }
}


public struct MoveRedux
{
    
    public bool IsCentralSource;
    // 0-based factory index
    public int  FactoryIndex;
    public int  ColorIndex;
    public int  flowerCount;
    public bool TargetIsPenalty;
    // 0-based placement line, -1 when TargetIsPenalty
    public int  TargetLineIndex;

    public override string ToString()
    {
        string src = IsCentralSource ? "Central" : "Factory[" + FactoryIndex + "]";
        string dst = TargetIsPenalty ? "Penalty" : "Line[" + TargetLineIndex + "]";
        return src + " color=" + ColorIndex + " x" + flowerCount + " -> " + dst;
    }
}

public struct MoveRecord
{
    public int      PlayerIndex;
    public MoveRedux Move;

    // Flowers dumped from factory to central (for factory picks)
    public int[] flowersDumpedToCentral;

    // First player token
    public bool GotFirstPlayerToken;
    public bool PrevHasFirstPlayerToken;
    public bool PrevCentralHasToken;

    // Placement line state before the move
    public int PrevLineColor;
    public int PrevLineCount;

    // Flowers actually placed on line and sent to penalty
    public int flowersPlacedOnLine;
    public int flowersToPenalty;

    // Penalty count before any changes
    public int PrevPenaltyCount;

    // Turn state before the move
    public int PrevCurrentPlayer;
    public int PrevTurnNumber;

    // Pre-allocates internal array. Call once per depth level, then reuse.
    public static MoveRecord Create(int numColors)
    {
        return new MoveRecord
        {
            flowersDumpedToCentral = new int[numColors]
        };
    }
}
