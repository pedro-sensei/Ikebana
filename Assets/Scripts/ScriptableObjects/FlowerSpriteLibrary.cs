using System;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.0.5 (April 2026)    =^..^=    =^..^=
//                    Last Update 15/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


[CreateAssetMenu(fileName = "FlowerSpriteLibrary", menuName = "Ikebana/Flower Sprite Library")]
public class FlowerSpriteLibrary : ScriptableObject
{

    [Serializable]
    public class ColorEntry
    {
        [Tooltip("The flower color this entry covers.")]
        public FlowerColor Color;

        [Tooltip("All sprite variants available for this color. " +
                 "Index 0 is the default. Order determines cycling order in the UI.")]
        public Sprite[] Variants;


        public Sprite GetVariant(int index)
        {
            if (Variants == null || Variants.Length == 0) return null;
            index = Mathf.Clamp(index, 0, Variants.Length - 1);
            return Variants[index];
        }

        public int VariantCount => Variants != null ? Variants.Length : 0;
    }

    [Tooltip("One entry per flower color.")]
    public ColorEntry[] Entries;

    public ColorEntry GetEntry(FlowerColor color)
    {
        if (Entries == null) return null;
        for (int i = 0; i < Entries.Length; i++)
            if (Entries[i] != null && Entries[i].Color == color)
                return Entries[i];
        return null;
    }

    public int VariantCount(FlowerColor color)
    {
        ColorEntry e = GetEntry(color);
        return e != null ? e.VariantCount : 0;
    }

    public Sprite GetVariant(FlowerColor color, int index)
    {
        ColorEntry e = GetEntry(color);
        return e != null ? e.GetVariant(index) : null;
    }
}
