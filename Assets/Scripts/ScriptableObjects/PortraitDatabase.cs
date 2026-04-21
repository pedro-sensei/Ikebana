using UnityEngine;

//=^..^=   =^..^=   VERSION 1.0.5 (April 2026)    =^..^=    =^..^=
//                    Last Update 15/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


/// ScriptableObject that holds the list of available player portraits.
[CreateAssetMenu(fileName = "PortraitDatabase", menuName = "Ikebana/Portrait Database")]
public class PortraitDatabase : ScriptableObject
{
    [Tooltip("All available player portraits. Index 0 is the default.")]
    public Sprite[] Portraits;


    public Sprite GetPortrait(int index)
    {
        if (Portraits == null || index < 0 || index >= Portraits.Length)
            return null;
        return Portraits[index];
    }

    public int Count
    {
        get { return Portraits != null ? Portraits.Length : 0; }
    }
}
