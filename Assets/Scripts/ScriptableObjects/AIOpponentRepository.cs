using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

/// ScriptableObject repository of every available AI opponent.

[CreateAssetMenu(fileName = "AIOpponentRepository", menuName = "Ikebana/AI Opponent Repo")]
public class AIOpponentRepository : ScriptableObject
{
    [Tooltip("All available AI opponents. Order determines their position in menus.")]
    public AIOpponentData[] Opponents;

    public int Count => Opponents != null ? Opponents.Length : 0;

    public AIOpponentData Get(int index)
    {
        if (Opponents == null || index < 0 || index >= Opponents.Length)
            return null;
        return Opponents[index];
    }

    //Returns every opponent name in order
    public System.Collections.Generic.List<string> GetNames()
    {
        var names = new System.Collections.Generic.List<string>(Count);
        if (Opponents != null)
            foreach (var o in Opponents)
                names.Add(o != null ? o.OpponentName : "(empty)");
        return names;
    }
}
