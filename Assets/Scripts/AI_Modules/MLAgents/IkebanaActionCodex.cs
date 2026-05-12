//=^..^=   =^..^=   VERSION 1.1.1 (May 2026)    =^..^=    =^..^=
//                    Last Update 10/05/2026
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// Maps a game move to a single action index and back again.
// This keeps the ML action space flat and easy to mask.
public static class IkebanaActionCodex
{
    public const int MaxFactories = 9;
    public const int CentralSourceIndex = MaxFactories;
    public const int NumSources = MaxFactories + 1;
    public const int NumColors = 5;
    public const int NumLines = 5;
    public const int FloorTargetIndex = NumLines;
    public const int NumTargets = NumLines + 1;
    public const int ActionCount = NumSources * NumColors * NumTargets;

    public static int Encode(int sourceIndex, int colorIndex, int targetIndex)
    {
        return sourceIndex * (NumColors * NumTargets) + colorIndex * NumTargets + targetIndex;
    }

    public static bool TryEncode(GameMove move, out int actionIndex)
    {
        actionIndex = -1;

        int sourceIndex = CentralSourceIndex;
        if (move.SourceIsFactory)
            sourceIndex = move.FactoryIndex;

        int colorIndex = (int)move.Color;
        int targetIndex = move.TargetLineIndex;
        if (move.IsPenalty)
            targetIndex = FloorTargetIndex;

        if (sourceIndex < 0 || sourceIndex >= NumSources)
            return false;

        if (colorIndex < 0 || colorIndex >= NumColors)
            return false;

        if (targetIndex < 0 || targetIndex >= NumTargets)
            return false;

        actionIndex = Encode(sourceIndex, colorIndex, targetIndex);
        return true;
    }

    public static void Decode(int actionIndex, out int sourceIndex, out int colorIndex, out int targetIndex)
    {
        sourceIndex = actionIndex / (NumColors * NumTargets);
        int remainder = actionIndex % (NumColors * NumTargets);
        colorIndex = remainder / NumTargets;
        targetIndex = remainder % NumTargets;
    }

    public static void BuildActionLookup(GameMove[] moveBuffer, int moveCount, int[] actionToMove)
    {
        for (int i = 0; i < actionToMove.Length; i++)
            actionToMove[i] = -1;

        for (int i = 0; i < moveCount; i++)
        {
            int actionIndex;
            if (!TryEncode(moveBuffer[i], out actionIndex))
                continue;

            if (actionIndex >= 0 && actionIndex < actionToMove.Length && actionToMove[actionIndex] < 0)
                actionToMove[actionIndex] = i;
        }
    }
}
