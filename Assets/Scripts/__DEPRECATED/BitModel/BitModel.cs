using UnityEngine;
//NOT WORKING YET
/*
/// Design goals:
///   - Zero heap allocations during search (all value types).
///   - Moves carry enough information to undo without cloning the state.
///   - All operations via static helpers in BitModelOps so the structs stay plain data.
///
/// Standard game constants (hard-coded for max perf):
///   Grid        : 5×5 = 25 cells
///   Colors      : up to 6 (3 bits each)
///   Lines       : up to 6, capacity 1-6
///   Penalty     : up to 7 slots
///   Displays   : up to 9, 4 tiles each
///   Score       : 0-255
///   Players     : up to 4


//  BIT PLAYER

//
//  playerData (int, 32 bits):
//    [1:0]   playerIndex      (0-3)
//    [26:2]  wallOccupation   (25 bits, one per cell row*5+col)
//    [30:27] penaltyCount     (0-7, 4 bits)
//    [31]    hasFirstToken    (1 bit)
//
//  lineAndScoreData (long, 64 bits):
//    For each of 6 placement lines (8 bits each = 48 bits total):
//      bits [3:0]  lineColor  (0-5 = color, 7 = empty)
//      bits [7:4]  lineCount  (0-6)
//    Lines packed low-to-high: line0 at [7:0], line1 at [15:8], …, line5 at [47:40]
//    [55:48] score            (0-255, 8 bits)
//    [63:56] forbidden0_5    (6 bits used — one bit per color forbidden on ANY line
//                             is not enough; see below)
//
//  FORBIDDEN per line:
//    We need 1 bit per color per line (6 colors × 6 lines = 36 bits).
///    This fits nicely in a separate int + the remaining 8 bits of the long.
///    However, to keep the player as just 2 words (int + long) we use a
///    separate int:
///
///  forbiddenData (int, 32 bits):
///    6 lines × 6 colors = 36 bits — does NOT fit in 32 bits for the full 6×6 case.
///    For standard 5 colors × 5 lines = 25 bits — fits.
///    Layout: line L color C ? bit (L * 6 + C), set = forbidden.
///    We cap at 5 lines × 6 colors = 30 bits (fits in int).
//
public struct BitPlayerBoard
{
    public int  playerData;       // index(2) + wall(25) + penaltyCount(4) + hasToken(1) = 32
    public long lineAndScoreData; // 6 lines × 8 bits(48) + score(8) + reserved(8)       = 64
    public int  forbiddenData;    // 5 lines × 6 colors = 30 bits                         ? 32
}


//  BIT GAME MOVE  (32 bits — fits in a single int)

//
//  moveData encoding (low to high):
//    [3:0]   sourceIndex       4 bits  (0-8 = factory, 15 = central display)
//    [6:4]   destIndex         3 bits  (0-5 = placement line, 7 = penalty)
//    [9:7]   chosenColor       3 bits  (0-5)
//    [13:10] chosenColorCount  4 bits  (1-15)
//    [16:14] leftover0Color    3 bits  (color of 1st non-picked tile, 7 = none)
//    [19:17] leftover1Color    3 bits  (color of 2nd non-picked tile, 7 = none)
//    [22:20] leftover2Color    3 bits  (color of 3rd non-picked tile, 7 = none)
//    [26:23] toPenaltyCount    4 bits  (0-15 flowers that overflow to penalty)
//    [31:27] toDiscardCount    5 bits  (0-20 flowers that overflow past penalty cap)

//
public struct BitGameMove
{
    public int moveData;
}


//  BIT UNDO INFO  

//
//  Because the move itself is only 32 bits, undo information that cannot
//  be derived from the move + post-state is stored here.
//  undoData (int, 32 bits):
//    [3:0]   prevPenaltyCount   (0-7)
//    [6:4]   prevLineColor      (0-5 or 7=empty)
//    [10:7]  prevLineCount      (0-6)
//    [11]    gotFirstToken      (bool)
//    [15:12] prevCentralCount   (0-15, for central picks — how many tiles
//                                of picked color were in central, to know
//                                how many to restore on undo; capped at 15)
//    [31:16] reserved
//
public struct BitUndoInfo
{
    public int undoData;
}


//  BIT GAME STATE

//
//  Central display — stores count per color (6 colors × 5 bits = 30 bits)
//    plus 1 bit for hasFirstPlayerToken. Fits in int (32 bits).
//    Layout: color C ? bits [C*5+4 : C*5], value = count of that color (0-20).
//    bit 30 = hasFirstPlayerToken.
//
//  Displays — each factory has 4 tile slots × 3 bits = 12 bits + 1 isEmpty bit = 13 bits.
//    9 factories × 13 bits = 117 bits ? two longs (128 bits).
//    factoryData1: factories 0-4 (5 × 13 = 65 bits, fits in long)
//    factoryData2: factories 5-8 (4 × 13 = 52 bits, fits in long)
//    Per factory (13 bits):
//      [2:0]   tile0 color (7 = empty slot)
//      [5:3]   tile1 color
//      [8:6]   tile2 color
//      [11:9]  tile3 color
//      [12]    isEmpty flag (1 = all picked / empty)
//
//  Bag / Discard — count per color. 6 colors × 5 bits = 30 bits each. Fits in int.
//
//  Meta — round, turn, phase, players, etc packed into one int:
//    [1:0]   currentPlayer    (0-3)
//    [3:2]   startingPlayer   (0-3)
//    [7:4]   currentRound     (0-15)
//    [15:8]  turnNumber       (0-255)
//    [17:16] phase            (0-2, RoundPhaseEnum)
//    [19:18] numPlayers       (2-4, stored as value-2 in 2 bits)
//    [23:20] numFactories     (0-9)
//    [24]    isGameOver       (bool)
//    [31:25] reserved
//
public struct BitGameState
{
    public int  centralData;     // 6 colors × 5 bits + hasToken = 31 bits
    public long factoryData1;    // factories 0-4  (65 bits)
    public long factoryData2;    // factories 5-8  (52 bits)
    public int  bagData;         // 6 colors × 5 bits = 30 bits
    public int  discardData;     // 6 colors × 5 bits = 30 bits
    public int  metaData;        // round/turn/phase/players packed

    // 4 players inline (no array — pure value type, no heap)
    public BitPlayerBoard player0;
    public BitPlayerBoard player1;
    public BitPlayerBoard player2;
    public BitPlayerBoard player3;
}
*/