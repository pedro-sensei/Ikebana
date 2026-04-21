using System.Collections.Generic;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.0.2 (April 2026)    =^..^=    =^..^=
//                    Last Update 01/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


// Keeps references to UI elements so animations can find them.
// Static so any script can look up positions without needing a direct reference.

public static class ViewRegistry
{
    private static Dictionary<int, RectTransform> _factories = new Dictionary<int, RectTransform>();
    private static Dictionary<int, RectTransform> _playerBoards = new Dictionary<int, RectTransform>();
    private static Dictionary<string, RectTransform> _placementLines = new Dictionary<string, RectTransform>();
    private static Dictionary<int, RectTransform> _penaltyLines = new Dictionary<int, RectTransform>();
    private static RectTransform _centralDisplay;

    #region REGISTRATION

    public static void RegisterFactory(int index, RectTransform rt)
    {
        _factories[index] = rt;
    }

    public static void UnregisterFactory(int index)
    {
        _factories.Remove(index);
    }

    public static void RegisterCentral(RectTransform rt)
    {
        _centralDisplay = rt;
    }

    public static void UnregisterCentral()
    {
        _centralDisplay = null;
    }

    public static void RegisterPlacementLine(int player, int line, RectTransform rt)
    {
        string key = player + "_" + line;
        _placementLines[key] = rt;
    }

    public static void RegisterPlayerBoard(int slotIndex, RectTransform rt)
    {
        _playerBoards[slotIndex] = rt;
    }

    public static void UnregisterPlayerBoard(int slotIndex)
    {
        _playerBoards.Remove(slotIndex);
    }

    public static void UnregisterPlacementLine(int player, int line)
    {
        string key = player + "_" + line;
        _placementLines.Remove(key);
    }

    public static void RegisterPenaltyLine(int player, RectTransform rt)
    {
        _penaltyLines[player] = rt;
    }

    public static void UnregisterPenaltyLine(int player)
    {
        _penaltyLines.Remove(player);
    }
    #endregion

    #region QUERIES

    public static bool TryGetFactory(int index, out RectTransform rt)
    {
        return _factories.TryGetValue(index, out rt);
    }

    public static bool GetCentralDisplayPosition(out RectTransform rt)
    {
        rt = _centralDisplay;
        return rt != null;
    }

    public static bool GetPlacementLinePosition(int player, int line, out RectTransform rt)
    {
        string key = player + "_" + line;
        return _placementLines.TryGetValue(key, out rt);
    }

    public static bool GetPlayerBoardPosition(int slotIndex, out RectTransform rt)
    {
        return _playerBoards.TryGetValue(slotIndex, out rt);
    }

    public static bool GetPenaltyLinePosition(int player, out RectTransform rt)
    {
        return _penaltyLines.TryGetValue(player, out rt);
    }

    public static void Clear()
    {
        _factories.Clear();
        _playerBoards.Clear();
        _placementLines.Clear();
        _penaltyLines.Clear();
        _centralDisplay = null;
    }
    #endregion
}