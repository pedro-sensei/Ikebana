using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


/// Persists the player's color preferences for flower sprites.
public static class FlowerSpriteSettings
{
    // Only the five in-game colors are user-selectable.
    public static readonly FlowerColor[] SelectableColors =
    {
        FlowerColor.Blue,
        FlowerColor.Yellow,
        FlowerColor.Red,
        FlowerColor.Pink,
        FlowerColor.Green,
    };

    private const string PrefKeyPrefix = "FlowerSprite_";


    public static string PrefKey(FlowerColor color) => PrefKeyPrefix + color;

    //Returns the saved variant index (default 0)
    public static int LoadIndex(FlowerColor color)
        => PlayerPrefs.GetInt(PrefKey(color), 0);

    public static void SaveIndex(FlowerColor color, int index)
        => PlayerPrefs.SetInt(PrefKey(color), index);

    //  Apply saved choices to FlowerSpriteData
 
    public static void ApplyAll(FlowerSpriteRepository library, FlowerSpriteData spriteData)
    {
        if (library == null || spriteData == null) return;

        foreach (FlowerColor color in SelectableColors)
        {
            int index = LoadIndex(color);
            Sprite sprite = library.GetVariant(color, index);
            if (sprite != null)
                spriteData.SetSprite(color, sprite);
        }
    }

    public static void ApplyAndSave(FlowerColor color, int index,
                                    FlowerSpriteRepository library, FlowerSpriteData spriteData)
    {
        SaveIndex(color, index);

        if (library == null || spriteData == null) return;
        Sprite sprite = library.GetVariant(color, index);
        if (sprite != null)
            spriteData.SetSprite(color, sprite);
    }
}
