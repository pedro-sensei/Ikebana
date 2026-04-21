using System;
using System.Collections.Generic;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=



//Minimal game model
//Made to reduce heap allocations and be efficient
//for MinMax and genetic algorithm.
//Sealed is supposed to optimize when compiled.
#region MINIMAL GM
public sealed class MinimalGM : IGameModel
{
    #region FIELDS AND PROPERTIES
    // Config (read from snapshot)
    private readonly int GRID_SIZE;
    private readonly int NUM_COLORS;
    private readonly int NUM_PLACEMENT_LINES;
    private readonly int flowerS_PER_COLOR;
    private readonly int TOTAL_flowerS;
    private readonly int flowerS_PER_FACTORY;
    private readonly int MAX_FACTORIES;
    private readonly int MAX_PLAYERS       = 4;
    private readonly int PENALTY_CAPACITY;
    private const    int MAX_MOVES         = 200;

    private readonly int[] PENALTY_VALUES;

    private readonly int BONUS_ROW;
    private readonly int BONUS_COLUMN;
    private readonly int BONUS_COLOR;

    private readonly GameConfigSnapshot _config;

    // Bag / Discard
    private readonly byte[] _bag;
    private readonly byte[] _discard;
    private int _bagCount;
    private int _discardCount;

    // Displays
    private readonly byte[][] _factories;
    private readonly int[]    _factoryCount;
    private int _numFactories;

    // Central display
    private readonly byte[] _central;
    private int  _centralCount;
    private bool _centralHasToken;

    // Players
    private readonly MinimalPlayer[] _players;

    //State
    private int  _numPlayers;
    private int  _currentRound;
    private int  _currentPlayer;
    private int  _turnNumber;
    private int  _startingPlayer;
    private bool _isGameOver;
    private RoundPhaseEnum _phase;

    //Random generator.
    private readonly Random _rng;

    // Reusable GameMoves instances.
    private readonly GameMove[] _moveBuffer = new GameMove[MAX_MOVES];
    private int _moveCount;

    // Pre-allocated buffer for central dump colors (sized to max factory tile count)
    private readonly byte[] _centralDumpBuffer;

    // Public properties
    public int  NumPlayers      => _numPlayers;
    public int  CurrentRound    => _currentRound;
    public int  CurrentPlayer   => _currentPlayer;
    public int  TurnNumber      => _turnNumber;
    public int  StartingPlayer  => _startingPlayer;
    public bool IsGameOver      => _isGameOver;
    public RoundPhaseEnum Phase => _phase;
    public MinimalPlayer[] Players      => _players;
    public int  NumFactories    => _numFactories;
    public int  CentralCount    => _centralCount;
    public bool CentralHasToken => _centralHasToken;
    public GameConfigSnapshot Config => _config;
    #endregion
   
    public MinimalGM(int numPlayers, int randomSeed = 0)
        : this(numPlayers, GameConfig.CreateSnapshot(), randomSeed) { }

    public MinimalGM(int numPlayers, GameConfigSnapshot config, int randomSeed = 0)
    {
        _config         = config;
        GRID_SIZE       = config.GridSize;
        NUM_COLORS      = config.NumColors;
        NUM_PLACEMENT_LINES = config.NumPlacementLines;
        flowerS_PER_COLOR = config.flowersPerColor;
        TOTAL_flowerS     = config.Totalflowers;
        flowerS_PER_FACTORY = config.flowersPerFactory;
        MAX_FACTORIES   = System.Math.Max(9, config.NumFactories);
        PENALTY_CAPACITY = config.PenaltyCapacity;
        PENALTY_VALUES  = config.PenaltyValues;
        BONUS_ROW       = config.BonusRow;
        BONUS_COLUMN    = config.BonusColumn;
        BONUS_COLOR     = config.BonusColor;

        _numPlayers   = numPlayers;
        _numFactories = config.NumFactories;
        _rng          = randomSeed == 0 ? new Random() : new Random(randomSeed);

        _bag     = new byte[TOTAL_flowerS];
        _discard = new byte[TOTAL_flowerS];
        _central = new byte[TOTAL_flowerS];

        _factories    = new byte[MAX_FACTORIES][];
        _factoryCount = new int[MAX_FACTORIES];
        for (int i = 0; i < MAX_FACTORIES; i++)
            _factories[i] = new byte[flowerS_PER_FACTORY];

        _centralDumpBuffer = new byte[flowerS_PER_FACTORY];

        _players = new MinimalPlayer[MAX_PLAYERS];
        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            int[] allowedPerLine = new int[GRID_SIZE];
            int[,] wp = config.WallPatterns[i % config.WallPatterns.Length];
            for (int row = 0; row < GRID_SIZE; row++)
            {
                int bits = 0;
                for (int col = 0; col < GRID_SIZE; col++)
                    bits |= (1 << wp[row, col]);
                allowedPerLine[row] = bits;
            }
            _players[i] = new MinimalPlayer(GRID_SIZE, PENALTY_CAPACITY,
                                            config.PlacementLineCapacities, allowedPerLine);
        }

        _startingPlayer = _rng.Next(numPlayers);
    }

    #region IGameModel METHODS AND PUBLIC INTERFACE
    public void GameSetup()
    {
        ResetForNewGame();
    }
    public void ResetForNewGame()
    {
        _bagCount        = 0;
        _discardCount    = 0;
        _centralCount    = 0;
        _centralHasToken = false;
        _currentRound    = 0;
        _isGameOver      = false;
        _phase           = RoundPhaseEnum.RoundSetup;
        _startingPlayer  = _rng.Next(_numPlayers);

        for (int i = 0; i < _numFactories; i++)
            _factoryCount[i] = 0;
        for (int i = 0; i < _numPlayers; i++)
            _players[i].Reset();

        FillBagFromScratch();
    }
    public void StartRound()
    {
        SimStartRound();
    }
    public void SimStartRound()
    {
        _phase         = RoundPhaseEnum.RoundSetup;
        _currentPlayer = _startingPlayer;
        _isGameOver    = false;
        _turnNumber    = 1;

        // Discard leftover central flowers from previous round
        for (int i = 0; i < _centralCount; i++)
            if (_discardCount < _discard.Length)
                _discard[_discardCount++] = _central[i];
        _centralCount    = 0;
        _centralHasToken = true;

        // Fill factories from bag
        for (int f = 0; f < _numFactories; f++)
        {
            _factoryCount[f] = 0;
            for (int t = 0; t < flowerS_PER_FACTORY; t++)
            {
                if (_bagCount == 0) RefillBagFromDiscard();
                if (_bagCount == 0) break;
                _factories[f][_factoryCount[f]++] = _bag[--_bagCount];
            }
        }

        _phase = RoundPhaseEnum.PlacementPhase;
    }
    public void StartTurn()
    {
        SimStartTurn();
    }
    public void SimStartTurn()
    {
        _turnNumber++;
        _currentPlayer = (_currentPlayer + 1) % _numPlayers;
    }
    public void EndTurn()
    {
        SimEndTurn();
    }
    public void SimEndTurn()
    {

        if (!AreDisplaysEmpty())
        {
            SimStartTurn();
        }
        else
        {
            SimEndRound(); 
        }
    }
    public void EndRound()
    {
        SimEndRound();
    }
    public void EndGame()
    {
        SimEndGame();
    }
    public void SimEndRound()
    {
        _phase = RoundPhaseEnum.ScoringPhase;

        bool gameOver = false;
        for (int i = 0; i < _numPlayers; i++)
        {
            ScorePlayer(i);
            if (_players[i].HasCompleteRow) gameOver = true;
        }

        if (gameOver)
        {
            SimEndGame();
        }
        else
        {
            _currentRound++;
            SimStartRound();
        }
    }
    public void SimEndGame()
    {
        for (int i = 0; i < _numPlayers; i++)
            ApplyEndGameBonus(i);
        _isGameOver = true;
    }
    public List<GameMove> GetPossibleMoves()
    {
        GetValidMoves();
        List<GameMove> moves = new List<GameMove>();
        for(int i = 0; i < _moveCount; i++)
        {
            moves.Add(GetMove(i));
        }
        return moves;
    }
    public GameMove GetMove(int i) => _moveBuffer[i];

    // ── Move generation ─────────────────────────────────────────────────

    private void AppendMovesForColor(
        GameMove[] moves, ref int count,
        MinimalPlayer player,
        MoveSource source, int factoryIndex, FlowerColor color, int flowerCount)
    {
        byte colorByte = (byte)color;

        // Compute the central dump colors once for factory source moves.
        // When picking from a factory, non-picked flowers go to central.
        int dumpCount = 0;
        if (source == MoveSource.Factory)
        {
            int f = factoryIndex;
            for (int i = 0; i < _factoryCount[f]; i++)
            {
                if (_factories[f][i] != colorByte)
                    _centralDumpBuffer[dumpCount++] = _factories[f][i];
            }
        }

        // One move per valid placement line
        for (int line = 0; line < NUM_PLACEMENT_LINES; line++)
        {
            if (count >= moves.Length) return;
            if (!player.CanPlaceInLine(line, colorByte)) continue;

            int spaceLeft = player.GetLineCapacity(line) - player.GetLineCount(line);
            int toLine    = System.Math.Min(flowerCount, spaceLeft);
            int toPenalty = flowerCount - toLine;

            GameMove m = new GameMove(
                _currentPlayer,
                source,
                factoryIndex,
                color,
                MoveTarget.PlacementLine,
                line,
                flowerCount,
                toLine,
                toPenalty);

            if (dumpCount > 0)
            {
                m.centralDumpColors = new byte[dumpCount];
                Array.Copy(_centralDumpBuffer, m.centralDumpColors, dumpCount);
                m.centralDumpCount = dumpCount;
            }
            moves[count++] = m;
        }

        // Penalty move (all flowers go to penalty)
        if (count < moves.Length)
        {
            GameMove m = new GameMove(
                _currentPlayer,
                source,
                factoryIndex,
                color,
                MoveTarget.PenaltyLine,
                0,
                flowerCount,
                0,
                flowerCount);

            if (dumpCount > 0)
            {
                m.centralDumpColors = new byte[dumpCount];
                Array.Copy(_centralDumpBuffer, m.centralDumpColors, dumpCount);
                m.centralDumpCount = dumpCount;
            }
            moves[count++] = m;
        }
    }

    public int GetValidMoves(GameMove[] moves)
    {
        int count = 0;
        if (_isGameOver || _phase != RoundPhaseEnum.PlacementPhase) return 0;

        MinimalPlayer player = _players[_currentPlayer];

        for (int f = 0; f < _numFactories; f++)
        {
            if (_factoryCount[f] == 0) continue;
            for (int c = 0; c < NUM_COLORS; c++)
            {
                int cnt = CountColorInFactory(f, (byte)c);
                if (cnt == 0) continue;
                AppendMovesForColor(moves, ref count, player, MoveSource.Factory, f, (FlowerColor)c, cnt);
            }
        }

        if (_centralCount > 0)
        {
            for (int c = 0; c < NUM_COLORS; c++)
            {
                int cnt = CountColorInCentral((byte)c);
                if (cnt == 0) continue;
                AppendMovesForColor(moves, ref count, player, MoveSource.Central, 0, (FlowerColor)c, cnt);
            }
        }

        _moveCount = count;
        return count;
    }
    public int GetValidMoves() => GetValidMoves(_moveBuffer);
    public int GetCurrentPlayerIndex()
    {
        return _currentPlayer;
    }
    public int GetRoundNumber()
    {
        return _currentRound;
    }
    public bool CheckEndRoundConditions()
    {
        return AreDisplaysEmpty();
    }
    public bool AreDisplaysEmpty()
    {
        if (_centralCount > 0) return false;
        for (int f = 0; f < _numFactories; f++)
            if (_factoryCount[f] > 0) return false;
        return true;
    }

    // ── Move execution ──────────────────────────────────────────────────

    /// <summary>
    /// Executes a move AND populates the undo fields on the move struct
    /// (gotFirstPlayerToken, prevPenaltyCount, prevLineCount, prevLineColor)
    /// so that UndoMove can reverse it without a full snapshot.
    /// Pass by ref so the undo fields are written back to the caller.
    /// </summary>
    public void ExecuteMove(ref GameMove move)
    {
        MinimalPlayer player = _players[_currentPlayer];
        byte color = (byte)move.Color;

        // Save undo state
        move.prevPenaltyCount = player.GetPenaltyCount();
        move.gotFirstPlayerToken = false;

        // ── Source handling ──
        if (move.SourceIsFactory)
        {
            int f = move.FactoryIndex;
            // Dump non-picked colors to central
            for (int i = 0; i < _factoryCount[f]; i++)
            {
                if (_factories[f][i] != color && _centralCount < _central.Length)
                    _central[_centralCount++] = _factories[f][i];
            }
            _factoryCount[f] = 0;
        }
        else
        {
            // Pick from central — optionally pick up first-player token
            if (_centralHasToken)
            {
                move.gotFirstPlayerToken = true;
                _centralHasToken = false;
                player.HasFirstPlayerToken = true;
                player.AddToPenalty(255);
            }
            // Remove picked color from central
            int newCount = 0;
            for (int i = 0; i < _centralCount; i++)
                if (_central[i] != color)
                    _central[newCount++] = _central[i];
            _centralCount = newCount;
        }

        // ── Target handling ──
        if (move.IsPenalty)
        {
            // All flowers to penalty
            for (int t = 0; t < move.flowerCount; t++)
                player.AddToPenalty(color);
        }
        else
        {
            int lineIdx = move.TargetLineIndex;
            // Save line state for undo
            move.prevLineCount = player.GetLineCount(lineIdx);
            move.prevLineColor = player.GetLineColor(lineIdx);

            int spaceLeft = player.GetLineCapacity(lineIdx) - player.GetLineCount(lineIdx);
            int placed = 0;
            for (; placed < spaceLeft && placed < move.flowerCount; placed++)
                player.AddToLine(lineIdx, color);
            // Overflow to penalty
            for (int t = placed; t < move.flowerCount; t++)
                player.AddToPenalty(color);
        }
    }

    /// <summary>
    /// Non-ref overload for callers that don't need undo (IGameModel interface).
    /// </summary>
    public void ExecuteMove(GameMove move)
    {
        ExecuteMove(ref move);
    }

    // ── Move undo ───────────────────────────────────────────────────────

    /// <summary>
    /// Reverses the effects of ExecuteMove using the undo fields stored in the move.
    /// The move must have been executed by ExecuteMove(ref GameMove) with the
    /// undo fields populated. The game state is restored to before the move.
    /// Zero allocations.
    /// </summary>
    public void UndoMove(ref GameMove move)
    {
        MinimalPlayer player = _players[move.PlayerIndex];
        byte color = (byte)move.Color;

        // ── Undo target ──
        // Restore penalty count (removes both overflow flowers and the token slot)
        player.SetPenaltyCount(move.prevPenaltyCount);

        if (!move.IsPenalty)
        {
            // Restore placement line to its previous state
            int lineIdx = move.TargetLineIndex;
            player.SetLineState(lineIdx, move.prevLineColor, move.prevLineCount);
        }

        // Undo first-player token pickup
        if (move.gotFirstPlayerToken)
        {
            player.HasFirstPlayerToken = false;
            _centralHasToken = true;
        }

        // ── Undo source ──
        if (move.SourceIsFactory)
        {
            int f = move.FactoryIndex;
            // Remove the dumped flowers from central.
            // They were appended at the end, so remove from tail.
            if (move.centralDumpColors != null)
                _centralCount -= move.centralDumpCount;

            // Restore factory: picked color flowers + dump colors
            _factoryCount[f] = 0;
            for (int t = 0; t < move.flowerCount; t++)
                _factories[f][_factoryCount[f]++] = color;
            if (move.centralDumpColors != null)
            {
                for (int t = 0; t < move.centralDumpCount; t++)
                    _factories[f][_factoryCount[f]++] = move.centralDumpColors[t];
            }
        }
        else
        {
            // Restore picked flowers back into central
            for (int t = 0; t < move.flowerCount; t++)
            {
                if (_centralCount < _central.Length)
                    _central[_centralCount++] = color;
            }
        }
    }

    /// <summary>IGameModel.UndoMove — not supported without a ref move. Use UndoMove(ref GameMove).</summary>
    public void UndoMove()
    {
        throw new NotImplementedException("Use UndoMove(ref GameMove) with the executed move for zero-allocation undo.");
    }

    /// <summary>
    /// Undoes a turn: reverses the move and the SimStartTurn() that preceded it.
    /// </summary>
    public void UndoTurn(ref GameMove move)
    {
        // Undo SimStartTurn: decrement turn, go back to previous player
        _turnNumber--;
        _currentPlayer = (_currentPlayer - 1 + _numPlayers) % _numPlayers;
        UndoMove(ref move);
    }

    public void UndoTurn()
    {
        throw new NotImplementedException("Use UndoTurn(ref GameMove) with the executed move.");
    }

    public void UndoRound()
    {
        throw new NotImplementedException("Round-level undo not supported. Use snapshot save/restore instead.");
    }

    // ── Snapshot / Restore ──────────────────────────────────────────────

    public GameState SaveSnapshot()
    {
        var snap = GameState.Create(_numPlayers, _numFactories, flowerS_PER_FACTORY);

        Array.Copy(_bag, snap.Bag, _bagCount);
        snap.BagCount = _bagCount;
        Array.Copy(_discard, snap.Discard, _discardCount);
        snap.DiscardCount = _discardCount;

        for (int f = 0; f < _numFactories; f++)
        {
            Array.Copy(_factories[f], snap.Factories[f], _factoryCount[f]);
            snap.FactoryCount[f] = _factoryCount[f];
        }

        Array.Copy(_central, snap.Central, _centralCount);
        snap.CentralCount    = _centralCount;
        snap.CentralHasToken = _centralHasToken;

        snap.CurrentRound   = _currentRound;
        snap.CurrentPlayer  = _currentPlayer;
        snap.TurnNumber     = _turnNumber;
        snap.StartingPlayer = _startingPlayer;
        snap.IsGameOver     = _isGameOver;
        snap.Phase          = _phase;

        for (int i = 0; i < _numPlayers; i++)
            snap.PlayerSnapshots[i] = _players[i].SaveSnapshot();

        return snap;
    }

    /// <summary>
    /// Fills an existing pre-allocated GameState struct without allocating.
    /// The caller must have created the GameState once via GameState.Create().
    /// Array.Copy reuses the existing arrays.
    /// </summary>
    public void FillSnapshot(ref GameState snap)
    {
        Array.Copy(_bag, snap.Bag, _bagCount);
        snap.BagCount = _bagCount;
        Array.Copy(_discard, snap.Discard, _discardCount);
        snap.DiscardCount = _discardCount;

        for (int f = 0; f < _numFactories; f++)
        {
            Array.Copy(_factories[f], snap.Factories[f], _factoryCount[f]);
            snap.FactoryCount[f] = _factoryCount[f];
        }

        Array.Copy(_central, snap.Central, _centralCount);
        snap.CentralCount    = _centralCount;
        snap.CentralHasToken = _centralHasToken;

        snap.CurrentRound   = _currentRound;
        snap.CurrentPlayer  = _currentPlayer;
        snap.TurnNumber     = _turnNumber;
        snap.StartingPlayer = _startingPlayer;
        snap.IsGameOver     = _isGameOver;
        snap.Phase          = _phase;

        for (int i = 0; i < _numPlayers; i++)
            _players[i].FillSnapshot(ref snap.PlayerSnapshots[i]);
    }

    public void RestoreSnapshot(ref GameState snap)
    {
        _bagCount = snap.BagCount;
        Array.Copy(snap.Bag, _bag, _bagCount);
        _discardCount = snap.DiscardCount;
        Array.Copy(snap.Discard, _discard, _discardCount);

        for (int f = 0; f < _numFactories; f++)
        {
            _factoryCount[f] = snap.FactoryCount[f];
            Array.Copy(snap.Factories[f], _factories[f], _factoryCount[f]);
        }

        _centralCount    = snap.CentralCount;
        Array.Copy(snap.Central, _central, _centralCount);
        _centralHasToken = snap.CentralHasToken;

        _currentRound   = snap.CurrentRound;
        _currentPlayer  = snap.CurrentPlayer;
        _turnNumber     = snap.TurnNumber;
        _startingPlayer = snap.StartingPlayer;
        _isGameOver     = snap.IsGameOver;
        _phase          = snap.Phase;

        for (int i = 0; i < _numPlayers; i++)
            _players[i].RestoreSnapshot(ref snap.PlayerSnapshots[i]);
    }

    // ── Display queries ─────────────────────────────────────────────────

    public int GetFactoryflowerCount(int factoryIndex)
        => _factoryCount[factoryIndex];
    public int CountColorInFactory(int factoryIndex, FlowerColor color)
        => CountColorInFactory(factoryIndex, (byte)color);
    public int CountColorInCentral(FlowerColor color)
        => CountColorInCentral((byte)color); 
    public bool CheckEndGameConditions()
    {
        return IsGameOver;
    }

    
    public bool CheckMLAgentWinCondition()
    {
        if (!_isGameOver) return false;
        int agentScore = _players[0].Score;
        for (int i = 1; i < _numPlayers; i++)
            if (_players[i].Score >= agentScore) return false;
        return true;
    }

    /// <summary>
    /// The agent loses if the game is over and it did not win.
    /// </summary>
    public bool CheckMLAgentLoseCondition()
    {
        if (!_isGameOver) return false;
        return !CheckMLAgentWinCondition();
    }

    public GameState GetGameState()
    {
        return SaveSnapshot();
    }
    public void SetGameState(GameState gameState)
    {
        RestoreSnapshot(ref gameState);
    }
    public int PenaltyValue(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= PENALTY_CAPACITY) return 0;
        return (PENALTY_VALUES[slotIndex]);
    }
    #endregion

 
    //  MINMAX INTERFACE  
    #region MINMAX INTERFACE

    public const int MAX_MINMAX_MOVES = 200;

    /// <summary>
    /// Fills <paramref name="moves"/> with legal moves for the current player.
    /// Returns the count.  Zero-allocation — same buffer pattern as ModelRedux.
    /// </summary>
    public int GetValidMovesMinMax(MinimalMoveRecord[] moves)
    {
        int count = 0;
        if (_isGameOver || _phase != RoundPhaseEnum.PlacementPhase) return 0;

        MinimalPlayer player = _players[_currentPlayer];

        for (int f = 0; f < _numFactories; f++)
        {
            if (_factoryCount[f] == 0) continue;
            for (int c = 0; c < NUM_COLORS; c++)
            {
                int cnt = CountColorInFactory(f, (byte)c);
                if (cnt == 0) continue;
                AppendMinMaxMoves(moves, ref count, player, false, f, (byte)c, cnt);
            }
        }

        for (int c = 0; c < NUM_COLORS; c++)
        {
            int cnt = CountColorInCentral((byte)c);
            if (cnt == 0) continue;
            AppendMinMaxMoves(moves, ref count, player, true, -1, (byte)c, cnt);
        }

        return count;
    }

    private void AppendMinMaxMoves(MinimalMoveRecord[] moves, ref int count,
        MinimalPlayer player, bool isCentral, int factory, byte color, int flowerCount)
    {
        for (int line = 0; line < NUM_PLACEMENT_LINES; line++)
        {
            if (count >= moves.Length) return;
            if (!player.CanPlaceInLine(line, color)) continue;
            MinimalMoveRecord m = new MinimalMoveRecord
            {
                IsCentralSource = isCentral,
                FactoryIndex    = factory,
                ColorIndex      = color,
                FlowerCount     = flowerCount,
                TargetIsPenalty = false,
                TargetLineIndex = line
            };
            moves[count++] = m;
        }
        if (count < moves.Length)
        {
            moves[count++] = new MinimalMoveRecord
            {
                IsCentralSource = isCentral,
                FactoryIndex    = factory,
                ColorIndex      = color,
                FlowerCount     = flowerCount,
                TargetIsPenalty = true,
                TargetLineIndex = -1
            };
        }
    }

    /// <summary>
    /// Executes a move and writes undo data into <paramref name="record"/>.
    /// Passed by ref so the inline dump bytes are written back to the caller's buffer.
    /// Zero-allocation.
    /// </summary>
    public void ExecuteMoveMinMax(ref MinimalMoveRecord record)
    {
        MinimalPlayer player = _players[_currentPlayer];
        byte color = record.ColorIndex;

        record.PlayerIndex            = _currentPlayer;
        record.PrevPenaltyCount       = player.GetPenaltyCount();
        record.PrevHasFirstPlayerToken = player.HasFirstPlayerToken;
        record.PrevCentralHasToken    = _centralHasToken;
        record.GotFirstPlayerToken    = false;
        record.DumpCount              = 0;

        if (!record.IsCentralSource)
        {
            int f = record.FactoryIndex;
            int dumpIdx = 0;
            for (int i = 0; i < _factoryCount[f]; i++)
            {
                byte tile = _factories[f][i];
                if (tile != color)
                {
                    if (_centralCount < _central.Length)
                        _central[_centralCount++] = tile;
                    if (dumpIdx < 8)
                        record.SetDump(dumpIdx++, tile);
                }
            }
            record.DumpCount   = dumpIdx;
            _factoryCount[f]   = 0;
        }
        else
        {
            if (_centralHasToken)
            {
                record.GotFirstPlayerToken = true;
                _centralHasToken           = false;
                player.HasFirstPlayerToken = true;
                player.AddToPenalty(255);
            }
            int newCount = 0;
            for (int i = 0; i < _centralCount; i++)
                if (_central[i] != color)
                    _central[newCount++] = _central[i];
            _centralCount = newCount;
        }

        if (record.TargetIsPenalty)
        {
            for (int t = 0; t < record.FlowerCount; t++)
                player.AddToPenalty(color);
        }
        else
        {
            int lineIdx = record.TargetLineIndex;
            record.PrevLineCount = player.GetLineCount(lineIdx);
            record.PrevLineColor = player.GetLineColor(lineIdx);

            int spaceLeft = player.GetLineCapacity(lineIdx) - player.GetLineCount(lineIdx);
            int placed    = System.Math.Min(spaceLeft, record.FlowerCount);
            for (int t = 0; t < placed; t++)
                player.AddToLine(lineIdx, color);
            for (int t = placed; t < record.FlowerCount; t++)
                player.AddToPenalty(color);
        }
    }

    /// <summary>
    /// Reverses the effects of <see cref="ExecuteMoveMinMax"/>.
    /// Must be called with the same record that was passed to ExecuteMoveMinMax.
    /// Zero-allocation.
    /// </summary>
    public void UndoMoveMinMax(ref MinimalMoveRecord record)
    {
        MinimalPlayer player = _players[record.PlayerIndex];
        byte color = record.ColorIndex;

        // Restore penalty and token
        player.SetPenaltyCount(record.PrevPenaltyCount);
        player.HasFirstPlayerToken = record.PrevHasFirstPlayerToken;
        _centralHasToken           = record.PrevCentralHasToken;

        // Restore placement line
        if (!record.TargetIsPenalty)
            player.SetLineState(record.TargetLineIndex, record.PrevLineColor, record.PrevLineCount);

        // Restore source
        if (!record.IsCentralSource)
        {
            int f = record.FactoryIndex;
            // Remove the dumped tiles that were appended to central
            _centralCount -= record.DumpCount;
            if (_centralCount < 0) _centralCount = 0;

            // Restore factory: picked-color tiles first, then dump tiles
            _factoryCount[f] = 0;
            for (int t = 0; t < record.FlowerCount; t++)
                _factories[f][_factoryCount[f]++] = color;
            for (int t = 0; t < record.DumpCount; t++)
                _factories[f][_factoryCount[f]++] = record.GetDump(t);
        }
        else
        {
            // Restore picked tiles back into central
            for (int t = 0; t < record.FlowerCount; t++)
                if (_centralCount < _central.Length)
                    _central[_centralCount++] = color;
        }
    }

    /// <summary>
    /// Advances to the next player. Returns true if the round continues (displays not empty).
    /// Mirrors ModelRedux.AdvanceTurn().
    /// </summary>
    public bool AdvanceTurnMinMax()
    {
        _turnNumber++;
        _currentPlayer = (_currentPlayer + 1) % _numPlayers;
        return !AreDisplaysEmpty();
    }

    /// <summary>
    /// Scores all players at the end of a round for evaluation purposes.
    /// Mutates the model — caller must save/restore surrounding state.
    /// Returns true when the game-ending condition (complete row) is triggered.
    /// </summary>
    public bool ScoreEndOfRoundMinMax()
    {
        bool gameEnding = false;
        for (int i = 0; i < _numPlayers; i++)
        {
            ScorePlayer(i);
            if (_players[i].HasCompleteRow) gameEnding = true;
        }
        _isGameOver = gameEnding;
        return gameEnding;
    }

    public void ForceCurrentPlayer(int player) { _currentPlayer = player; }
    public void ForceTurnNumber(int turn)       { _turnNumber    = turn;   }
    public void ForceStartingPlayer(int player)  { _startingPlayer = player; }
    public void ForceIsGameOver(bool value)       { _isGameOver     = value;  }
    public int  DiscardCount                      => _discardCount;
    public void ForceDiscardCount(int count)      { _discardCount   = count;  }

    /// <summary>
    /// Wall-point calculation for MinMax evaluation. Operates on a MinimalPlayer
    /// without allocating. Exposed here so MinMax doesn't need access to
    /// private fields.
    /// </summary>
    public int CalculateWallPointsForEval(MinimalPlayer p, int row, int col)
    {
        int h = 1, v = 1;
        for (int c = col - 1; c >= 0         && p.IsWallOccupied(row, c); c--) h++;
        for (int c = col + 1; c < GRID_SIZE  && p.IsWallOccupied(row, c); c++) h++;
        for (int r = row - 1; r >= 0         && p.IsWallOccupied(r, col); r--) v++;
        for (int r = row + 1; r < GRID_SIZE  && p.IsWallOccupied(r, col); r++) v++;
        if (h == 1 && v == 1) return 1;
        int pts = 0;
        if (h > 1) pts += h;
        if (v > 1) pts += v;
        return pts;
    }

    #endregion
    #region PRIVATE HELPER METHODS
    private void FillBagFromScratch()
    {
        int idx = 0;
        for (byte c = 0; c < NUM_COLORS; c++)
            for (int t = 0; t < flowerS_PER_COLOR; t++)
                _bag[idx++] = c;
        _bagCount = TOTAL_flowerS;
        ShuffleBag();
    }
    private void RefillBagFromDiscard()
    {
        for (int i = 0; i < _discardCount; i++)
            _bag[i] = _discard[i];
        _bagCount     = _discardCount;
        _discardCount = 0;
        ShuffleBag();
    }
    private void ShuffleBag()
    {
        for (int i = _bagCount - 1; i > 0; i--)
        {
            int  j   = _rng.Next(i + 1);
            byte tmp = _bag[i];
            _bag[i]  = _bag[j];
            _bag[j]  = tmp;
        }
    }
    private int CountColorInFactory(int f0, byte color)
    {
        int count = 0;
        for (int i = 0; i < _factoryCount[f0]; i++)
            if (_factories[f0][i] == color) count++;
        return count;
    }
    private int CountColorInCentral(byte color)
    {
        int count = 0;
        for (int i = 0; i < _centralCount; i++)
            if (_central[i] == color) count++;
        return count;
    }

    public int CountDistinctColorsInFactory(int factoryIndex)
    {
        int mask = 0;
        for (int i = 0; i < _factoryCount[factoryIndex]; i++)
            mask |= 1 << _factories[factoryIndex][i];
        int count = 0;
        while (mask != 0) { count++; mask &= mask - 1; }
        return count;
    }

    public int CountDistinctColorsInCentral()
    {
        int mask = 0;
        for (int i = 0; i < _centralCount; i++)
            mask |= 1 << _central[i];
        int count = 0;
        while (mask != 0) { count++; mask &= mask - 1; }
        return count;
    }

    private void ScorePlayer(int playerIndex)
    {
        MinimalPlayer p = _players[playerIndex];

        for (int line = 0; line < NUM_PLACEMENT_LINES; line++)
        {
            if (!p.IsLineFull(line)) continue;

            byte color = p.GetLineColor(line);
            int  col   = GetWallColumn(playerIndex, line, (int)color);

            p.SetWallOccupied(line, col, true);
            p.Score += CalculateWallPoints(p, line, col);

            // Discard excess flowers (capacity - 1; one flower goes to the wall)
            int excess = p.GetLineCapacity(line) - 1;
            for (int t = 0; t < excess && _discardCount < _discard.Length; t++)
                _discard[_discardCount++] = color;

            p.ClearLine(line);
            p.SetForbidden(line, (int)color);
        }

        p.HasCompleteRow = p.HasAnyCompleteRow();

        p.Score += p.CalculatePenalty(this);
        if (p.Score < 0) p.Score = 0;

        bool hadToken = p.ClearPenalty(_discard, ref _discardCount);
        if (hadToken) _startingPlayer = playerIndex;
    }
    private void ApplyEndGameBonus(int playerIndex)
    {
        MinimalPlayer p     = _players[playerIndex];
        int           bonus = 0;

        for (int r = 0; r < GRID_SIZE; r++)
        {
            bool full = true;
            for (int c = 0; c < GRID_SIZE; c++) { if (!p.IsWallOccupied(r, c)) { full = false; break; } }
            if (full) bonus += BONUS_ROW;
        }
        for (int c = 0; c < GRID_SIZE; c++)
        {
            bool full = true;
            for (int r = 0; r < GRID_SIZE; r++) { if (!p.IsWallOccupied(r, c)) { full = false; break; } }
            if (full) bonus += BONUS_COLUMN;
        }
        for (int color = 0; color < NUM_COLORS; color++)
        {
            bool allPlaced = true;
            for (int r = 0; r < GRID_SIZE; r++)
            {
                if (!p.IsWallOccupied(r, GetWallColumn(playerIndex, r, color))) { allPlaced = false; break; }
            }
            if (allPlaced) bonus += BONUS_COLOR;
        }

        p.Score += bonus;
    }
    private int GetWallColumn(int playerIndex, int row, int colorIndex)
    {
        return _config.GetWallColumn(playerIndex, row, colorIndex);
    }
    private int CalculateWallPoints(MinimalPlayer p, int row, int col)
    {
        int h = 1, v = 1;
        for (int c = col - 1; c >= 0       && p.IsWallOccupied(row, c); c--) h++;
        for (int c = col + 1; c < GRID_SIZE && p.IsWallOccupied(row, c); c++) h++;
        for (int r = row - 1; r >= 0       && p.IsWallOccupied(r, col); r--) v++;
        for (int r = row + 1; r < GRID_SIZE && p.IsWallOccupied(r, col); r++) v++;
        if (h == 1 && v == 1) return 1;
        int pts = 0;
        if (h > 1) pts += h;
        if (v > 1) pts += v;
        return pts;
    }

    #endregion
}
#endregion

//  MinimalMoveRecord  –  zero-allocation undo record for MinMax search.
#region MINMAX MOVE RECORD
public struct MinimalMoveRecord
{
    public int  PlayerIndex;
    public bool IsCentralSource;
    public int  FactoryIndex;
    public byte ColorIndex;
    public int  FlowerCount;
    public bool TargetIsPenalty;
    public int  TargetLineIndex;

    // Undo fields
    public int  PrevPenaltyCount;
    public bool PrevHasFirstPlayerToken;
    public bool PrevCentralHasToken;
    public byte PrevLineColor;
    public int  PrevLineCount;
    public bool GotFirstPlayerToken;

    // Inline fixed-size central dump buffer (max standard factory = 4 flowers).
    // Sized to 8 to cover variants without heap allocation.
    public byte Dump0; public byte Dump1; public byte Dump2; public byte Dump3;
    public byte Dump4; public byte Dump5; public byte Dump6; public byte Dump7;
    public int  DumpCount;

    public void SetDump(int index, byte value)
    {
        switch (index)
        {
            case 0: Dump0 = value; break; case 1: Dump1 = value; break;
            case 2: Dump2 = value; break; case 3: Dump3 = value; break;
            case 4: Dump4 = value; break; case 5: Dump5 = value; break;
            case 6: Dump6 = value; break; case 7: Dump7 = value; break;
        }
    }

    public byte GetDump(int index)
    {
        switch (index)
        {
            case 0: return Dump0; case 1: return Dump1;
            case 2: return Dump2; case 3: return Dump3;
            case 4: return Dump4; case 5: return Dump5;
            case 6: return Dump6; default: return Dump7;
        }
    }
}
#endregion

#region MINIMAL PLAYER CLASS
public sealed class MinimalPlayer
{
    private readonly int GRID_SIZE;
    private readonly int PENALTY_CAPACITY;

    private readonly int[] _lineCapacity;
    private readonly int[] _allowed;

    private int _wallBits;

    private readonly byte[] _lineColor;
    private readonly int[]  _lineCount;
    private readonly int[]  _forbidden;

    private readonly byte[] _penalty;
    private int              _penaltyCount;

    public int  Score               { get; set; }
    public bool HasFirstPlayerToken { get; set; }
    public bool HasCompleteRow      { get; set; }

    public MinimalPlayer() : this(5, 7, new int[]{ 1, 2, 3, 4, 5 }, null) { }

    public MinimalPlayer(int gridSize, int penaltyCapacity)
        : this(gridSize, penaltyCapacity, null, null) { }

    public MinimalPlayer(int gridSize, int penaltyCapacity, int[] lineCapacities, int[] allowedPerLine)
    {
        GRID_SIZE        = gridSize;
        PENALTY_CAPACITY = penaltyCapacity;

        _lineCapacity = new int[GRID_SIZE];
        _allowed      = new int[GRID_SIZE];
        for (int i = 0; i < GRID_SIZE; i++)
        {
            _lineCapacity[i] = (lineCapacities != null && i < lineCapacities.Length)
                ? lineCapacities[i] : (i + 1);
            _allowed[i] = (allowedPerLine != null && i < allowedPerLine.Length)
                ? allowedPerLine[i] : ~0;
        }

        _lineColor = new byte[GRID_SIZE];
        _lineCount = new int[GRID_SIZE];
        _forbidden = new int[GRID_SIZE];
        _penalty   = new byte[PENALTY_CAPACITY];
        Reset();
    }

    public void Reset()
    {
        _wallBits           = 0;
        _penaltyCount       = 0;
        Score               = 0;
        HasFirstPlayerToken = false;
        HasCompleteRow      = false;
        for (int i = 0; i < GRID_SIZE; i++)
        {
            _lineColor[i] = 255;
            _lineCount[i] = 0;
            _forbidden[i] = 0;
        }
        Array.Clear(_penalty, 0, PENALTY_CAPACITY);
    }

    // Pattern lines
    public bool CanPlaceInLine(int line, byte color)
    {
        if (_lineCount[line] >= _lineCapacity[line])              return false;
        if ((_allowed[line] & (1 << (int)color)) == 0)           return false;
        if ((_forbidden[line] & (1 << (int)color)) != 0)         return false;
        if (_lineColor[line] != 255 && _lineColor[line] != color) return false;
        return true;
    }

    public void AddToLine(int line, byte color)
    {
        _lineColor[line] = color;
        _lineCount[line]++;
    }
    public bool IsLineFull(int line)   => _lineCount[line] >= _lineCapacity[line];
    public int  GetLineCapacity(int line) => _lineCapacity[line];
    public byte GetLineColor(int line) => _lineColor[line];
    public int  GetLineCount(int line) => _lineCount[line];
    public void ClearLine(int line)
    {
        _lineColor[line] = 255;
        _lineCount[line] = 0;
    }

    /// <summary>
    /// Directly sets line state. Used by UndoMove to restore without allocation.
    /// </summary>
    public void SetLineState(int line, byte color, int count)
    {
        _lineColor[line] = color;
        _lineCount[line] = count;
    }

    public void SetForbidden(int row, int colorIndex)
        => _forbidden[row] |= (1 << colorIndex);

    public int  GetForbidden(int row)              => _forbidden[row];
    public void SetForbiddenBits(int row, int bits) { _forbidden[row] = bits; }

    // Wall
    public bool IsWallOccupied(int row, int col)
        => (_wallBits & (1 << (row * GRID_SIZE + col))) != 0;

    public void SetWallOccupied(int row, int col, bool occupied)
    {
        int bit = 1 << (row * GRID_SIZE + col);
        if (occupied) _wallBits |=  bit;
        else          _wallBits &= ~bit;
    }

    public bool HasAnyCompleteRow()
    {
        for (int r = 0; r < GRID_SIZE; r++)
        {
            bool full = true;
            for (int c = 0; c < GRID_SIZE; c++)
                if (!IsWallOccupied(r, c)) { full = false; break; }
            if (full) return true;
        }
        return false;
    }

    // Penalty
    public void AddToPenalty(byte value)
    {
        if (_penaltyCount < PENALTY_CAPACITY)
            _penalty[_penaltyCount++] = value;
    }

    /// <summary>
    /// Directly sets the penalty count. Used by UndoMove to truncate penalty
    /// back to its pre-move state without iterating.
    /// </summary>
    public void SetPenaltyCount(int count)
    {
        _penaltyCount = count;
    }

    /// <summary>
    /// Directly restores the wall bitmask. Used by MinMax eval restore.
    /// </summary>
    public void RestoreWallBits(int bits) { _wallBits = bits; }

    public int CalculatePenalty(MinimalGM gm)
    {
        int total = 0;
        for (int i = 0; i < _penaltyCount; i++)
            total += gm.PenaltyValue(i);
        return total;
    }

    public bool ClearPenalty(byte[] discard, ref int discardCount)
    {
        bool hadToken = false;
        for (int i = 0; i < _penaltyCount; i++)
        {
            if (_penalty[i] == 255) hadToken = true;
            else if (discardCount < discard.Length)
                discard[discardCount++] = _penalty[i];
        }
        _penaltyCount       = 0;
        HasFirstPlayerToken = false;
        return hadToken;
    }

    public int GetPenaltyCount() => _penaltyCount;
    public int WallBits          => _wallBits;

    public PlayerState SaveSnapshot()
    {
        var snap = PlayerState.Create(GRID_SIZE, PENALTY_CAPACITY);
        snap.WallBits           = _wallBits;
        snap.PenaltyCount       = _penaltyCount;
        snap.Score              = Score;
        snap.HasFirstPlayerToken = HasFirstPlayerToken;
        snap.HasCompleteRow     = HasCompleteRow;
        Array.Copy(_lineColor, snap.LineColor, GRID_SIZE);
        Array.Copy(_lineCount, snap.LineCount, GRID_SIZE);
        Array.Copy(_forbidden, snap.Forbidden, GRID_SIZE);
        Array.Copy(_penalty,   snap.Penalty,   PENALTY_CAPACITY);
        return snap;
    }

    /// <summary>
    /// Fills an existing pre-allocated PlayerState without allocating.
    /// The caller must have created the PlayerState once via PlayerState.Create().
    /// </summary>
    public void FillSnapshot(ref PlayerState snap)
    {
        snap.WallBits           = _wallBits;
        snap.PenaltyCount       = _penaltyCount;
        snap.Score              = Score;
        snap.HasFirstPlayerToken = HasFirstPlayerToken;
        snap.HasCompleteRow     = HasCompleteRow;
        Array.Copy(_lineColor, snap.LineColor, GRID_SIZE);
        Array.Copy(_lineCount, snap.LineCount, GRID_SIZE);
        Array.Copy(_forbidden, snap.Forbidden, GRID_SIZE);
        Array.Copy(_penalty,   snap.Penalty,   PENALTY_CAPACITY);
    }

    public void RestoreSnapshot(ref PlayerState snap)
    {
        _wallBits           = snap.WallBits;
        _penaltyCount       = snap.PenaltyCount;
        Score               = snap.Score;
        HasFirstPlayerToken = snap.HasFirstPlayerToken;
        HasCompleteRow      = snap.HasCompleteRow;
        Array.Copy(snap.LineColor, _lineColor, GRID_SIZE);
        Array.Copy(snap.LineCount, _lineCount, GRID_SIZE);
        Array.Copy(snap.Forbidden, _forbidden, GRID_SIZE);
        Array.Copy(snap.Penalty,   _penalty,   PENALTY_CAPACITY);
    }
}
#endregion


