using System;
using System.Runtime.CompilerServices;

//NOT WORKING YET
/*
/// Static helper methods for reading, writing and manipulating the
/// game structures in BitModel


public static class BitModelOps
{
    
    //  CONSTANTS


    public const int MAX_COLORS          = 6;
    public const int MAX_GRID_SIZE       = 5;
    public const int MAX_GRID_CELLS      = 25;  // 5×5
    public const int MAX_PLACEMENT_LINES = 6;
    public const int MAX_PENALTY_SLOTS   = 7;
    public const int MAX_FACTORIES       = 9;
    public const int TILES_PER_FACTORY   = 4;
    public const int MAX_PLAYERS         = 4;
    public const int MAX_MOVES           = 200;

    public const int EMPTY_COLOR         = 7;   // sentinel in 3-bit fields
    public const int CENTRAL_SOURCE      = 15;  // sentinel in 4-bit source field

    // Standard penalty values: { -1, -1, -2, -2, -3, -3, -4 }
    // Stored as positive for subtraction.
    private static readonly int[] PENALTY_VALUES = { 1, 1, 2, 2, 3, 3, 4 };

    //  BIT PLAYER — ACCESSORS

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPlayerIndex(ref BitPlayerBoard p)
        => p.playerData & 0x3;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetPlayerIndex(ref BitPlayerBoard p, int index)
    {
        p.playerData = (p.playerData & ~0x3) | (index & 0x3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetWall(ref BitPlayerBoard p, int row, int col)
    {
        int bit = 2 + row * MAX_GRID_SIZE + col;
        return ((p.playerData >> bit) & 1) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetWall(ref BitPlayerBoard p, int row, int col, bool occupied)
    {
        int bit = 2 + row * MAX_GRID_SIZE + col;
        if (occupied) p.playerData |=  (1 << bit);
        else          p.playerData &= ~(1 << bit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetWallBits(ref BitPlayerBoard p)
        => (p.playerData >> 2) & 0x1FFFFFF; // 25 bits

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPenaltyCount(ref BitPlayerBoard p)
        => (p.playerData >> 27) & 0xF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetPenaltyCount(ref BitPlayerBoard p, int count)
    {
        p.playerData = (p.playerData & ~(0xF << 27)) | ((count & 0xF) << 27);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetHasFirstToken(ref BitPlayerBoard p)
        => ((p.playerData >> 31) & 1) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetHasFirstToken(ref BitPlayerBoard p, bool has)
    {
        if (has) p.playerData |=  (1 << 31);
        else     p.playerData &= ~(1 << 31);
    }

    // Each line occupies 8 bits: [3:0] color (0-5, 7=empty), [7:4] count (0-6)
    // Line L starts at bit L*8.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetLineColor(ref BitPlayerBoard p, int line)
        => (int)((p.lineAndScoreData >> (line * 8)) & 0x7);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetLineCount(ref BitPlayerBoard p, int line)
        => (int)((p.lineAndScoreData >> (line * 8 + 4)) & 0xF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetLine(ref BitPlayerBoard p, int line, int color, int count)
    {
        int shift = line * 8;
        long mask = 0xFFL << shift;
        long val = (long)((color & 0x7) | ((count & 0xF) << 4)) << shift;
        p.lineAndScoreData = (p.lineAndScoreData & ~mask) | val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ClearLine(ref BitPlayerBoard p, int line)
    {
        SetLine(ref p, line, EMPTY_COLOR, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetScore(ref BitPlayerBoard p)
        => (int)((p.lineAndScoreData >> 48) & 0xFF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetScore(ref BitPlayerBoard p, int score)
    {
        p.lineAndScoreData = (p.lineAndScoreData & ~(0xFFL << 48))
                           | ((long)(score & 0xFF) << 48);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddScore(ref BitPlayerBoard p, int delta)
    {
        int s = GetScore(ref p) + delta;
        if (s < 0)   s = 0;
        if (s > 255) s = 255;
        SetScore(ref p, s);
    }

    // Bit (line * MAX_COLORS + color) = 1 means that color is forbidden on that line.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsForbidden(ref BitPlayerBoard p, int line, int color)
        => ((p.forbiddenData >> (line * MAX_COLORS + color)) & 1) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetForbidden(ref BitPlayerBoard p, int line, int color)
    {
        p.forbiddenData |= (1 << (line * MAX_COLORS + color));
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLineFull(ref BitPlayerBoard p, int line, int capacity)
        => GetLineCount(ref p, line) >= capacity;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanPlace(ref BitPlayerBoard p, int line, int color, int lineCapacity, int allowedMask)
    {
        if (GetLineCount(ref p, line) >= lineCapacity) return false;
        if ((allowedMask & (1 << color)) == 0)         return false;
        if (IsForbidden(ref p, line, color))            return false;
        int lc = GetLineColor(ref p, line);
        if (lc != EMPTY_COLOR && lc != color)           return false;
        return true;
    }

    public static bool HasAnyCompleteRow(ref BitPlayerBoard p, int gridSize)
    {
        for (int r = 0; r < gridSize; r++)
        {
            bool full = true;
            for (int c = 0; c < gridSize; c++)
            {
                if (!GetWall(ref p, r, c)) { full = false; break; }
            }
            if (full) return true;
        }
        return false;
    }

    public static void ResetPlayer(ref BitPlayerBoard p, int index)
    {
        p.playerData = index & 0x3;
        p.lineAndScoreData = 0;
        // Set all 6 lines to empty color (7)
        for (int i = 0; i < MAX_PLACEMENT_LINES; i++)
            ClearLine(ref p, i);
        p.forbiddenData = 0;
    }


    //  BIT GAME MOVE — ENCODE / DECODE


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MoveGetSource(int m)       => m & 0xF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MoveGetDest(int m)         => (m >> 4) & 0x7;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MoveGetColor(int m)        => (m >> 7) & 0x7;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MoveGetCount(int m)        => (m >> 10) & 0xF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MoveGetLeftover(int m, int index)
        => (m >> (14 + index * 3)) & 0x7;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MoveGetToPenalty(int m)    => (m >> 23) & 0xF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MoveGetToDiscard(int m)    => (m >> 27) & 0x1F;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool MoveIsFromCentral(int m)  => (m & 0xF) == CENTRAL_SOURCE;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool MoveIsPenalty(int m)       => ((m >> 4) & 0x7) == 7;


    /// Encodes a complete move into a single int.
    /// leftover0/1/2: colors of non-picked tiles in the factory (EMPTY_COLOR if absent).

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int EncodeMoveData(
        int sourceIndex, int destIndex, int color, int colorCount,
        int leftover0, int leftover1, int leftover2,
        int toPenalty, int toDiscard)
    {
        return (sourceIndex & 0xF)
             | ((destIndex & 0x7)      << 4)
             | ((color & 0x7)          << 7)
             | ((colorCount & 0xF)     << 10)
             | ((leftover0 & 0x7)      << 14)
             | ((leftover1 & 0x7)      << 17)
             | ((leftover2 & 0x7)      << 20)
             | ((toPenalty & 0xF)      << 23)
             | ((toDiscard & 0x1F)     << 27);
    }

    public static BitGameMove CreateMove(
        int sourceIndex, int destIndex, int color, int colorCount,
        int leftover0, int leftover1, int leftover2,
        int toPenalty, int toDiscard)
    {
        BitGameMove m;
        m.moveData = EncodeMoveData(sourceIndex, destIndex, color, colorCount,
                                    leftover0, leftover1, leftover2,
                                    toPenalty, toDiscard);
        return m;
    }


    //  BIT UNDO INFO — ENCODE / DECODE

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int UndoGetPrevPenalty(int u)    => u & 0xF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int UndoGetPrevLineColor(int u)  => (u >> 4) & 0x7;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int UndoGetPrevLineCount(int u)  => (u >> 7) & 0xF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool UndoGetGotToken(int u)      => ((u >> 11) & 1) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int EncodeUndoData(int prevPenalty, int prevLineColor, int prevLineCount, bool gotToken)
    {
        return (prevPenalty & 0xF)
             | ((prevLineColor & 0x7) << 4)
             | ((prevLineCount & 0xF) << 7)
             | ((gotToken ? 1 : 0)    << 11);
    }


    //  BIT GAME STATE — META ACCESSORS

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetCurrentPlayer(ref BitGameState s)   => s.metaData & 0x3;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetCurrentPlayer(ref BitGameState s, int v)
    { s.metaData = (s.metaData & ~0x3) | (v & 0x3); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetStartingPlayer(ref BitGameState s)  => (s.metaData >> 2) & 0x3;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetStartingPlayer(ref BitGameState s, int v)
    { s.metaData = (s.metaData & ~(0x3 << 2)) | ((v & 0x3) << 2); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetCurrentRound(ref BitGameState s)    => (s.metaData >> 4) & 0xF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetCurrentRound(ref BitGameState s, int v)
    { s.metaData = (s.metaData & ~(0xF << 4)) | ((v & 0xF) << 4); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetTurnNumber(ref BitGameState s)      => (s.metaData >> 8) & 0xFF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetTurnNumber(ref BitGameState s, int v)
    { s.metaData = (s.metaData & ~(0xFF << 8)) | ((v & 0xFF) << 8); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPhase(ref BitGameState s)           => (s.metaData >> 16) & 0x3;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetPhase(ref BitGameState s, int v)
    { s.metaData = (s.metaData & ~(0x3 << 16)) | ((v & 0x3) << 16); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetNumPlayers(ref BitGameState s)      => ((s.metaData >> 18) & 0x3) + 2;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetNumPlayers(ref BitGameState s, int v)
    { s.metaData = (s.metaData & ~(0x3 << 18)) | (((v - 2) & 0x3) << 18); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetNumFactories(ref BitGameState s)    => (s.metaData >> 20) & 0xF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetNumFactories(ref BitGameState s, int v)
    { s.metaData = (s.metaData & ~(0xF << 20)) | ((v & 0xF) << 20); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetIsGameOver(ref BitGameState s)     => ((s.metaData >> 24) & 1) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetIsGameOver(ref BitGameState s, bool v)
    {
        if (v) s.metaData |=  (1 << 24);
        else   s.metaData &= ~(1 << 24);
    }


    //  BIT GAME STATE — PLAYER ACCESS 
    public static ref BitPlayerBoard GetPlayer(ref BitGameState s, int index)
    {
        switch (index)
        {
            case 0: return ref s.player0;
            case 1: return ref s.player1;
            case 2: return ref s.player2;
            default: return ref s.player3;
        }
    }


    //  CENTRAL DISPLAY — count per color + token
    //  centralData: color C count at bits [C*5+4 : C*5], bit 30 = hasToken.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetCentralCount(ref BitGameState s, int color)
        => (s.centralData >> (color * 5)) & 0x1F;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetCentralCount(ref BitGameState s, int color, int count)
    {
        int shift = color * 5;
        s.centralData = (s.centralData & ~(0x1F << shift)) | ((count & 0x1F) << shift);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddCentralCount(ref BitGameState s, int color, int delta)
    {
        SetCentralCount(ref s, color, GetCentralCount(ref s, color) + delta);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetCentralTotal(ref BitGameState s)
    {
        int total = 0;
        for (int c = 0; c < MAX_COLORS; c++)
            total += GetCentralCount(ref s, c);
        return total;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetCentralHasToken(ref BitGameState s)
        => ((s.centralData >> 30) & 1) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetCentralHasToken(ref BitGameState s, bool v)
    {
        if (v) s.centralData |=  (1 << 30);
        else   s.centralData &= ~(1 << 30);
    }


    //  FACTORIES
    //  Per factory: 13 bits (4 tiles × 3 bits + 1 isEmpty bit).
    //  factoryData1: factories 0-4, factoryData2: factories 5-8.

    private const int BITS_PER_FACTORY = 13;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GetFactoryLong(ref BitGameState s, int f, out long data, out int shift)
    {
        if (f < 5) { data = s.factoryData1; shift = f * BITS_PER_FACTORY; }
        else       { data = s.factoryData2; shift = (f - 5) * BITS_PER_FACTORY; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFactoryEmpty(ref BitGameState s, int f)
    {
        long data; int shift;
        GetFactoryLong(ref s, f, out data, out shift);
        return ((data >> (shift + 12)) & 1) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetFactoryTile(ref BitGameState s, int f, int slot)
    {
        long data; int shift;
        GetFactoryLong(ref s, f, out data, out shift);
        return (int)((data >> (shift + slot * 3)) & 0x7);
    }

    public static void SetFactory(ref BitGameState s, int f,
                                  int t0, int t1, int t2, int t3, bool isEmpty)
    {
        long val = (long)(t0 & 0x7)
                 | ((long)(t1 & 0x7) << 3)
                 | ((long)(t2 & 0x7) << 6)
                 | ((long)(t3 & 0x7) << 9)
                 | ((long)(isEmpty ? 1 : 0) << 12);

        if (f < 5)
        {
            int shift = f * BITS_PER_FACTORY;
            long mask = 0x1FFFL << shift;
            s.factoryData1 = (s.factoryData1 & ~mask) | (val << shift);
        }
        else
        {
            int shift = (f - 5) * BITS_PER_FACTORY;
            long mask = 0x1FFFL << shift;
            s.factoryData2 = (s.factoryData2 & ~mask) | (val << shift);
        }
    }

    public static void ClearFactory(ref BitGameState s, int f)
    {
        SetFactory(ref s, f, EMPTY_COLOR, EMPTY_COLOR, EMPTY_COLOR, EMPTY_COLOR, true);
    }

    public static int CountColorInFactory(ref BitGameState s, int f, int color)
    {
        int count = 0;
        for (int t = 0; t < TILES_PER_FACTORY; t++)
            if (GetFactoryTile(ref s, f, t) == color) count++;
        return count;
    }
    public static void GetFactoryTiles(ref BitGameState s, int f,
                                       out int t0, out int t1, out int t2, out int t3)
    {
        t0 = GetFactoryTile(ref s, f, 0);
        t1 = GetFactoryTile(ref s, f, 1);
        t2 = GetFactoryTile(ref s, f, 2);
        t3 = GetFactoryTile(ref s, f, 3);
    }


    //  BAG / DISCARD 

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetBagCount(ref BitGameState s, int color)
        => (s.bagData >> (color * 5)) & 0x1F;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetBagCount(ref BitGameState s, int color, int count)
    {
        int shift = color * 5;
        s.bagData = (s.bagData & ~(0x1F << shift)) | ((count & 0x1F) << shift);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetBagTotal(ref BitGameState s)
    {
        int total = 0;
        for (int c = 0; c < MAX_COLORS; c++)
            total += GetBagCount(ref s, c);
        return total;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetDiscardCount(ref BitGameState s, int color)
        => (s.discardData >> (color * 5)) & 0x1F;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetDiscardCount(ref BitGameState s, int color, int count)
    {
        int shift = color * 5;
        s.discardData = (s.discardData & ~(0x1F << shift)) | ((count & 0x1F) << shift);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddDiscardCount(ref BitGameState s, int color, int delta)
    {
        SetDiscardCount(ref s, color, GetDiscardCount(ref s, color) + delta);
    }


    //  WALL SCORING


    /// Calculates adjacency points for placing a tile at (row, col) on the wall.
    /// Same rules as Azul: count contiguous horizontal + vertical neighbors + self.

    public static int CalculateWallPoints(ref BitPlayerBoard p, int row, int col, int gridSize)
    {
        int h = 1, v = 1;
        for (int c = col - 1; c >= 0        && GetWall(ref p, row, c); c--) h++;
        for (int c = col + 1; c < gridSize  && GetWall(ref p, row, c); c++) h++;
        for (int r = row - 1; r >= 0        && GetWall(ref p, r, col); r--) v++;
        for (int r = row + 1; r < gridSize  && GetWall(ref p, r, col); r++) v++;
        if (h == 1 && v == 1) return 1;
        int pts = 0;
        if (h > 1) pts += h;
        if (v > 1) pts += v;
        return pts;
    }


    /// Calculates the total penalty points for a player's current penalty count.
    /// Returns a NEGATIVE value (or zero).

    public static int CalculatePenalty(ref BitPlayerBoard p)
    {
        int count = GetPenaltyCount(ref p);
        int total = 0;
        for (int i = 0; i < count && i < PENALTY_VALUES.Length; i++)
            total -= PENALTY_VALUES[i];
        return total;
    }


    /// Calculates end-game bonus for complete rows, columns, and colors.
    /// wallPattern: flat int[25] where wallPattern[row*5+col] = colorIndex for that cell.

    public static int CalculateEndGameBonus(ref BitPlayerBoard p, int gridSize, int numColors,
                                            int[] wallPattern,
                                            int bonusRow, int bonusCol, int bonusColor)
    {
        int bonus = 0;

        // Complete rows
        for (int r = 0; r < gridSize; r++)
        {
            bool full = true;
            for (int c = 0; c < gridSize; c++)
                if (!GetWall(ref p, r, c)) { full = false; break; }
            if (full) bonus += bonusRow;
        }
        // Complete columns
        for (int c = 0; c < gridSize; c++)
        {
            bool full = true;
            for (int r = 0; r < gridSize; r++)
                if (!GetWall(ref p, r, c)) { full = false; break; }
            if (full) bonus += bonusCol;
        }
        // Complete colors (all 5 of one color placed on wall)
        for (int color = 0; color < numColors; color++)
        {
            bool all = true;
            for (int r = 0; r < gridSize; r++)
            {
                // Find which column this color appears in on row r
                int targetCol = -1;
                for (int c = 0; c < gridSize; c++)
                {
                    if (wallPattern[r * gridSize + c] == color) { targetCol = c; break; }
                }
                if (targetCol < 0 || !GetWall(ref p, r, targetCol)) { all = false; break; }
            }
            if (all) bonus += bonusColor;
        }

        return bonus;
    }


    //  MOVE GENERATION

    public static int GetValidMoves(
        ref BitGameState state,
        BitGameMove[] moveBuffer,
        int numColors, int numLines,
        int[] lineCapacities, int[] allowedMasks)
    {
        int count = 0;
        if (GetIsGameOver(ref state)) return 0;
        if (GetPhase(ref state) != 1) return 0; // 1 = PlacementPhase

        int playerIdx = GetCurrentPlayer(ref state);
        ref BitPlayerBoard player = ref GetPlayer(ref state, playerIdx);
        int numFactories = GetNumFactories(ref state);

        // --- Factory moves ---
        for (int f = 0; f < numFactories; f++)
        {
            if (IsFactoryEmpty(ref state, f)) continue;

            // Collect tiles in this factory
            int t0, t1, t2, t3;
            GetFactoryTiles(ref state, f, out t0, out t1, out t2, out t3);
            int[] tiles = { t0, t1, t2, t3 };

            // For each distinct color in this factory
            for (int color = 0; color < numColors; color++)
            {
                int colorCount = 0;
                int lo0 = EMPTY_COLOR, lo1 = EMPTY_COLOR, lo2 = EMPTY_COLOR;
                int loIdx = 0;

                for (int t = 0; t < TILES_PER_FACTORY; t++)
                {
                    if (tiles[t] == EMPTY_COLOR) continue;
                    if (tiles[t] == color) colorCount++;
                    else
                    {
                        if      (loIdx == 0) lo0 = tiles[t];
                        else if (loIdx == 1) lo1 = tiles[t];
                        else if (loIdx == 2) lo2 = tiles[t];
                        loIdx++;
                    }
                }
                if (colorCount == 0) continue;

                // One move per valid placement line
                for (int line = 0; line < numLines; line++)
                {
                    if (count >= moveBuffer.Length) return count;
                    if (!CanPlace(ref player, line, color, lineCapacities[line], allowedMasks[line]))
                        continue;

                    int space = lineCapacities[line] - GetLineCount(ref player, line);
                    int toLine = colorCount < space ? colorCount : space;
                    int toPen  = colorCount - toLine;

                    moveBuffer[count++].moveData = EncodeMoveData(
                        f, line, color, colorCount, lo0, lo1, lo2, toPen, 0);
                }

                // Penalty move (all to penalty)
                if (count < moveBuffer.Length)
                {
                    moveBuffer[count++].moveData = EncodeMoveData(
                        f, 7, color, colorCount, lo0, lo1, lo2, colorCount, 0);
                }
            }
        }


        if (GetCentralTotal(ref state) > 0)
        {
            for (int color = 0; color < numColors; color++)
            {
                int colorCount = GetCentralCount(ref state, color);
                if (colorCount == 0) continue;

                for (int line = 0; line < numLines; line++)
                {
                    if (count >= moveBuffer.Length) return count;
                    if (!CanPlace(ref player, line, color, lineCapacities[line], allowedMasks[line]))
                        continue;

                    int space = lineCapacities[line] - GetLineCount(ref player, line);
                    int toLine = colorCount < space ? colorCount : space;
                    int toPen  = colorCount - toLine;

                    moveBuffer[count++].moveData = EncodeMoveData(
                        CENTRAL_SOURCE, line, color, colorCount,
                        EMPTY_COLOR, EMPTY_COLOR, EMPTY_COLOR, toPen, 0);
                }

                // Penalty move
                if (count < moveBuffer.Length)
                {
                    moveBuffer[count++].moveData = EncodeMoveData(
                        CENTRAL_SOURCE, 7, color, colorCount,
                        EMPTY_COLOR, EMPTY_COLOR, EMPTY_COLOR, colorCount, 0);
                }
            }
        }

        return count;
    }

    // 

    public static BitUndoInfo ExecuteMove(ref BitGameState state, BitGameMove move)
    {
        int m = move.moveData;
        int source     = MoveGetSource(m);
        int dest       = MoveGetDest(m);
        int color      = MoveGetColor(m);
        int colorCount = MoveGetCount(m);
        int toPenalty  = MoveGetToPenalty(m);
        bool isPenalty = dest == 7;
        bool isCentral = source == CENTRAL_SOURCE;

        int playerIdx = GetCurrentPlayer(ref state);
        ref BitPlayerBoard player = ref GetPlayer(ref state, playerIdx);

        // Save undo state
        int prevPenalty   = GetPenaltyCount(ref player);
        int prevLineColor = isPenalty ? EMPTY_COLOR : GetLineColor(ref player, dest);
        int prevLineCount = isPenalty ? 0           : GetLineCount(ref player, dest);
        bool gotToken = false;

        // --- Source ---
        if (!isCentral)
        {
            // Factory pick: dump leftovers to central
            for (int i = 0; i < 3; i++)
            {
                int leftover = MoveGetLeftover(m, i);
                if (leftover != EMPTY_COLOR)
                    AddCentralCount(ref state, leftover, 1);
            }
            ClearFactory(ref state, source);
        }
        else
        {
            // Central pick: remove chosen color, pick up token
            SetCentralCount(ref state, color, GetCentralCount(ref state, color) - colorCount);

            if (GetCentralHasToken(ref state))
            {
                gotToken = true;
                SetCentralHasToken(ref state, false);
                SetHasFirstToken(ref player, true);
                // Token takes 1 penalty slot
                SetPenaltyCount(ref player, GetPenaltyCount(ref player) + 1);
            }
        }

        // --- Target ---
        if (isPenalty)
        {
            // All flowers to penalty
            int pen = GetPenaltyCount(ref player);
            SetPenaltyCount(ref player, pen + colorCount);
        }
        else
        {
            // Place on line, overflow to penalty
            int lineCount = GetLineCount(ref player, dest);
            int lineColor = GetLineColor(ref player, dest);
            int cap       = colorCount - toPenalty; // flowers that fit on line
            SetLine(ref player, dest, color, lineCount + cap);

            if (toPenalty > 0)
            {
                int pen = GetPenaltyCount(ref player);
                SetPenaltyCount(ref player, pen + toPenalty);
            }
        }

        // Discard overflow (penalty count beyond capacity) goes to discard
        int penNow = GetPenaltyCount(ref player);
        if (penNow > MAX_PENALTY_SLOTS)
        {
            int overflow = penNow - MAX_PENALTY_SLOTS;
            SetPenaltyCount(ref player, MAX_PENALTY_SLOTS);
            // overflow flowers go to discard pile (they're of the picked color)
            AddDiscardCount(ref state, color, overflow);
        }

        BitUndoInfo undo;
        undo.undoData = EncodeUndoData(prevPenalty, prevLineColor, prevLineCount, gotToken);
        return undo;
    }


    //  MOVE UNDO

    public static void UndoMove(ref BitGameState state, BitGameMove move, BitUndoInfo undo)
    {
        int m = move.moveData;
        int source     = MoveGetSource(m);
        int dest       = MoveGetDest(m);
        int color      = MoveGetColor(m);
        int colorCount = MoveGetCount(m);
        bool isPenalty = dest == 7;
        bool isCentral = source == CENTRAL_SOURCE;

        int playerIdx = GetCurrentPlayer(ref state);
        ref BitPlayerBoard player = ref GetPlayer(ref state, playerIdx);

        int prevPenalty   = UndoGetPrevPenalty(undo.undoData);
        int prevLineColor = UndoGetPrevLineColor(undo.undoData);
        int prevLineCount = UndoGetPrevLineCount(undo.undoData);
        bool gotToken     = UndoGetGotToken(undo.undoData);

        // --- Undo discard overflow ---
        // If penalty was capped, some flowers went to discard — remove them.
        int penNow = GetPenaltyCount(ref player);
        // The number we actually added to penalty = (current penalty - prevPenalty) 
        // But overflow was discarded. Reconstruct:
        //   totalAdded = colorCount + (gotToken ? 1 : 0)
        //   actualInPenalty = penNow - prevPenalty  (what stayed in penalty)
        //   overflowed = totalAdded - actualInPenalty (if capped)
        int totalAdded = colorCount + (gotToken ? 1 : 0) + (isPenalty ? 0 : MoveGetToPenalty(m));
        // Wait, toPenalty is already part of colorCount distribution. Let me recalculate.
        // The execute adds: gotToken?1:0 to penalty, then either colorCount (if isPenalty)
        // or toPenalty (overflow from line placement).
        int penaltyAdded = (gotToken ? 1 : 0) + (isPenalty ? colorCount : MoveGetToPenalty(m));
        int expectedPen = prevPenalty + penaltyAdded;
        int overflowed = expectedPen > MAX_PENALTY_SLOTS ? expectedPen - MAX_PENALTY_SLOTS : 0;
        if (overflowed > 0)
            AddDiscardCount(ref state, color, -overflowed);

        // --- Undo target ---
        SetPenaltyCount(ref player, prevPenalty);

        if (!isPenalty)
        {
            SetLine(ref player, dest, prevLineColor, prevLineCount);
        }

        // --- Undo token ---
        if (gotToken)
        {
            SetHasFirstToken(ref player, false);
            SetCentralHasToken(ref state, true);
        }

        // --- Undo source ---
        if (!isCentral)
        {
            // Restore factory tiles: picked color tiles + leftovers
            int t0 = EMPTY_COLOR, t1 = EMPTY_COLOR, t2 = EMPTY_COLOR, t3 = EMPTY_COLOR;
            int slot = 0;
            for (int i = 0; i < colorCount && slot < 4; i++)
                SetSlot(ref t0, ref t1, ref t2, ref t3, slot++, color);
            for (int i = 0; i < 3; i++)
            {
                int leftover = MoveGetLeftover(m, i);
                if (leftover != EMPTY_COLOR && slot < 4)
                {
                    // Remove from central
                    AddCentralCount(ref state, leftover, -1);
                    SetSlot(ref t0, ref t1, ref t2, ref t3, slot++, leftover);
                }
            }
            SetFactory(ref state, source, t0, t1, t2, t3, false);
        }
        else
        {
            // Restore picked color count to central
            SetCentralCount(ref state, color, GetCentralCount(ref state, color) + colorCount);
        }
    }

    // Helper to set tile slot by index without an array
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetSlot(ref int t0, ref int t1, ref int t2, ref int t3, int slot, int val)
    {
        switch (slot)
        {
            case 0: t0 = val; break;
            case 1: t1 = val; break;
            case 2: t2 = val; break;
            case 3: t3 = val; break;
        }
    }


    //  TURN / ROUND MANAGEMENT

    public static void AdvanceTurn(ref BitGameState state)
    {
        int np = GetNumPlayers(ref state);
        int cp = GetCurrentPlayer(ref state);
        SetCurrentPlayer(ref state, (cp + 1) % np);
        SetTurnNumber(ref state, GetTurnNumber(ref state) + 1);
    }

    public static void RewindTurn(ref BitGameState state)
    {
        int np = GetNumPlayers(ref state);
        int cp = GetCurrentPlayer(ref state);
        SetCurrentPlayer(ref state, (cp - 1 + np) % np);
        SetTurnNumber(ref state, GetTurnNumber(ref state) - 1);
    }

    public static bool AreDisplaysEmpty(ref BitGameState state)
    {
        if (GetCentralTotal(ref state) > 0) return false;
        int nf = GetNumFactories(ref state);
        for (int f = 0; f < nf; f++)
            if (!IsFactoryEmpty(ref state, f)) return false;
        return true;
    }


    //  SCORING (end of round)

    public static bool ScorePlayer(
        ref BitGameState state, int playerIdx,
        int gridSize, int numColors, int numLines, int[] lineCapacities,
        int[] wallPattern)
    {
        ref BitPlayerBoard p = ref GetPlayer(ref state, playerIdx);

        for (int line = 0; line < numLines; line++)
        {
            int cap = lineCapacities[line];
            if (!IsLineFull(ref p, line, cap)) continue;

            int color = GetLineColor(ref p, line);
            if (color == EMPTY_COLOR) continue;

            // Find the wall column for this color on this row
            int wallCol = -1;
            for (int c = 0; c < gridSize; c++)
            {
                if (wallPattern[line * gridSize + c] == color) { wallCol = c; break; }
            }
            if (wallCol < 0) continue;

            SetWall(ref p, line, wallCol, true);
            int pts = CalculateWallPoints(ref p, line, wallCol, gridSize);
            AddScore(ref p, pts);

            // Excess flowers to discard (capacity - 1; one goes to wall)
            int excess = cap - 1;
            if (excess > 0)
                AddDiscardCount(ref state, color, excess);

            ClearLine(ref p, line);
            SetForbidden(ref p, line, color);
        }

        // Apply penalty
        int penalty = CalculatePenalty(ref p);
        AddScore(ref p, penalty);

        // Clear penalty, return token info
        bool hadToken = GetHasFirstToken(ref p);
        SetPenaltyCount(ref p, 0);
        SetHasFirstToken(ref p, false);

        if (hadToken)
            SetStartingPlayer(ref state, playerIdx);

        return HasAnyCompleteRow(ref p, gridSize);
    }

    /// <summary>
    /// Applies end-game bonuses for a player (complete rows, columns, colors).
    /// </summary>
    public static void ApplyEndGameBonus(
        ref BitGameState state, int playerIdx,
        int gridSize, int numColors, int[] wallPattern,
        int bonusRow, int bonusCol, int bonusColor)
    {
        ref BitPlayerBoard p = ref GetPlayer(ref state, playerIdx);
        int bonus = CalculateEndGameBonus(ref p, gridSize, numColors, wallPattern,
                                          bonusRow, bonusCol, bonusColor);
        AddScore(ref p, bonus);
    }


    //  STATE INITIALIZATION

    public static BitGameState CreateInitialState(int numPlayers, int numFactories,
                                                   int numColors, int countPerColor)
    {
        BitGameState state = default;

        SetNumPlayers(ref state, numPlayers);
        SetNumFactories(ref state, numFactories);
        SetPhase(ref state, 0); // RoundSetup
        SetCurrentRound(ref state, 0);
        SetTurnNumber(ref state, 1);
        SetCentralHasToken(ref state, true);

        // Fill bag
        for (int c = 0; c < numColors; c++)
            SetBagCount(ref state, c, countPerColor);

        // Clear factories
        for (int f = 0; f < numFactories; f++)
            ClearFactory(ref state, f);

        // Reset players
        for (int i = 0; i < numPlayers; i++)
        {
            ref BitPlayerBoard p = ref GetPlayer(ref state, i);
            ResetPlayer(ref p, i);
        }

        return state;
    }
    public static void FillFactoriesFromBag(ref BitGameState state, int tilesPerFactory,
                                            int numColors, Random rng)
    {
        int nf = GetNumFactories(ref state);

        for (int f = 0; f < nf; f++)
        {
            int t0 = EMPTY_COLOR, t1 = EMPTY_COLOR, t2 = EMPTY_COLOR, t3 = EMPTY_COLOR;
            bool anyDrawn = false;

            for (int t = 0; t < tilesPerFactory; t++)
            {
                int bagTotal = GetBagTotal(ref state);
                if (bagTotal == 0)
                    RefillBagFromDiscard(ref state);
                bagTotal = GetBagTotal(ref state);
                if (bagTotal == 0) break;

                // Draw a random flower from bag
                int pick = rng.Next(bagTotal);
                int drawn = -1;
                int accum = 0;
                for (int c = 0; c < numColors; c++)
                {
                    accum += GetBagCount(ref state, c);
                    if (pick < accum) { drawn = c; break; }
                }
                if (drawn < 0) continue;

                SetBagCount(ref state, drawn, GetBagCount(ref state, drawn) - 1);
                SetSlot(ref t0, ref t1, ref t2, ref t3, t, drawn);
                anyDrawn = true;
            }

            SetFactory(ref state, f, t0, t1, t2, t3, !anyDrawn);
        }
    }

    public static void RefillBagFromDiscard(ref BitGameState state)
    {
        for (int c = 0; c < MAX_COLORS; c++)
        {
            int d = GetDiscardCount(ref state, c);
            if (d > 0)
            {
                SetBagCount(ref state, c, GetBagCount(ref state, c) + d);
                SetDiscardCount(ref state, c, 0);
            }
        }
    }
 //  CONVERSION: MinimalGM ? BitGameState

    public static BitGameState FromGameState(GameState snap, int numColors)
    {
        BitGameState state = default;

        int numPlayers = snap.PlayerSnapshots != null ? snap.PlayerSnapshots.Length : 2;
        int numFactories = snap.FactoryCount != null ? snap.FactoryCount.Length : 0;

        SetNumPlayers(ref state, numPlayers);
        SetNumFactories(ref state, numFactories);
        SetCurrentPlayer(ref state, snap.CurrentPlayer);
        SetStartingPlayer(ref state, snap.StartingPlayer);
        SetCurrentRound(ref state, snap.CurrentRound);
        SetTurnNumber(ref state, snap.TurnNumber);
        SetPhase(ref state, (int)snap.Phase);
        SetIsGameOver(ref state, snap.IsGameOver);
        SetCentralHasToken(ref state, snap.CentralHasToken);

        // Central: count per color
        for (int c = 0; c < numColors; c++)
        {
            int cnt = 0;
            for (int i = 0; i < snap.CentralCount; i++)
                if (snap.Central[i] == c) cnt++;
            SetCentralCount(ref state, c, cnt);
        }

        // Displays
        for (int f = 0; f < numFactories; f++)
        {
            int fc = snap.FactoryCount[f];
            int t0 = EMPTY_COLOR, t1 = EMPTY_COLOR, t2 = EMPTY_COLOR, t3 = EMPTY_COLOR;
            if (fc > 0) t0 = snap.Factories[f][0];
            if (fc > 1) t1 = snap.Factories[f][1];
            if (fc > 2) t2 = snap.Factories[f][2];
            if (fc > 3) t3 = snap.Factories[f][3];
            SetFactory(ref state, f, t0, t1, t2, t3, fc == 0);
        }

        // Bag: count per color
        for (int c = 0; c < numColors; c++)
        {
            int cnt = 0;
            for (int i = 0; i < snap.BagCount; i++)
                if (snap.Bag[i] == c) cnt++;
            SetBagCount(ref state, c, cnt);
        }

        // Discard: count per color
        for (int c = 0; c < numColors; c++)
        {
            int cnt = 0;
            for (int i = 0; i < snap.DiscardCount; i++)
                if (snap.Discard[i] == c) cnt++;
            SetDiscardCount(ref state, c, cnt);
        }

        // Players
        for (int pi = 0; pi < numPlayers; pi++)
        {
            ref BitPlayerBoard bp = ref GetPlayer(ref state, pi);
            PlayerState ps = snap.PlayerSnapshots[pi];

            ResetPlayer(ref bp, pi);
            SetScore(ref bp, ps.Score);
            SetHasFirstToken(ref bp, ps.HasFirstPlayerToken);
            SetPenaltyCount(ref bp, ps.PenaltyCount);

            // Wall bits
            for (int r = 0; r < MAX_GRID_SIZE; r++)
                for (int c = 0; c < MAX_GRID_SIZE; c++)
                {
                    bool occ = (ps.WallBits & (1 << (r * MAX_GRID_SIZE + c))) != 0;
                    SetWall(ref bp, r, c, occ);
                }

            // Lines
            int lineCount = ps.LineColor != null ? ps.LineColor.Length : 0;
            for (int l = 0; l < lineCount; l++)
            {
                int lc = ps.LineColor[l] == 255 ? EMPTY_COLOR : ps.LineColor[l];
                SetLine(ref bp, l, lc, ps.LineCount[l]);
            }

            // Forbidden
            if (ps.Forbidden != null)
            {
                for (int l = 0; l < ps.Forbidden.Length && l < MAX_PLACEMENT_LINES; l++)
                {
                    for (int c = 0; c < numColors; c++)
                    {
                        if ((ps.Forbidden[l] & (1 << c)) != 0)
                            SetForbidden(ref bp, l, c);
                    }
                }
            }
        }

        return state;
    }
}
*/