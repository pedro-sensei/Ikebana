using UnityEngine;
//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


/// Maps FlowerColor values to sprites saved in the settings and retrieved from the repo
/// .
//
[CreateAssetMenu(fileName = "FlowerSpriteData", menuName = "Ikebana/Flower Sprite Data")]
public class FlowerSpriteData : ScriptableObject
{
    public event System.Action OnSpritesChanged;

    [Header("Standard Colors")]
    [SerializeField] private Sprite blueSprite;
    [SerializeField] private Sprite yellowSprite;
    [SerializeField] private Sprite redSprite;
    [SerializeField] private Sprite pinkSprite;
    [SerializeField] private Sprite greenSprite;

    [Header("Extra Colors (non-standard setups only)")]
    [Tooltip("Only needed for special levels or variant rules that use additional colors.")]
    [SerializeField] private Sprite blackSprite;
    [Tooltip("Only needed for special levels or variant rules that use additional colors.")]
    [SerializeField] private Sprite whiteSprite;
    [Tooltip("Only needed for special levels or variant rules that use additional colors.")]
    [SerializeField] private Sprite orangeSprite;
    [Tooltip("Only needed for special levels or variant rules that use additional colors.")]
    [SerializeField] private Sprite purpleSprite;
    [Tooltip("Only needed for special levels or variant rules that use additional colors.")]
    [SerializeField] private Sprite turquoiseSprite;
    [Tooltip("Only needed for special levels or variant rules that use additional colors.")]
    [SerializeField] private Sprite lightBlueSprite;

    [Header("Special Tokens & Flowers")]
    [SerializeField] private Sprite firstPlayerTokenSprite;
    [Tooltip("Only needed for special levels or variant rules that use special flowers.")]
    [SerializeField] private Sprite special1Sprite;
    [Tooltip("Only needed for special levels or variant rules that use special flowers.")]
    [SerializeField] private Sprite special2Sprite;
    [Tooltip("Only needed for special levels or variant rules that use special flowers.")]
    [SerializeField] private Sprite special3Sprite;
    [Tooltip("Fallback sprite for empty or unassigned slots. Can be left empty.")]
    [SerializeField] private Sprite emptySprite;

    public Sprite GetSprite(FlowerColor color)
    {
        switch (color)
        {
            // Standard
            case FlowerColor.Blue:        return blueSprite;
            case FlowerColor.Yellow:      return yellowSprite;
            case FlowerColor.Red:         return redSprite;
            case FlowerColor.Pink:        return pinkSprite;
            case FlowerColor.Green:       return greenSprite;
            // Extra
            case FlowerColor.Black:       return blackSprite;
            case FlowerColor.White:       return whiteSprite;
            case FlowerColor.Orange:      return orangeSprite;
            case FlowerColor.Purple:      return purpleSprite;
            case FlowerColor.Turqoise:    return turquoiseSprite;
            case FlowerColor.LightBlue:   return lightBlueSprite;
            // Special
            case FlowerColor.FirstPlayer: return firstPlayerTokenSprite;
            case FlowerColor.Special1:    return special1Sprite;
            case FlowerColor.Special2:    return special2Sprite;
            case FlowerColor.Special3:    return special3Sprite;
            case FlowerColor.Empty:       return emptySprite;
            default:                      return null;
        }
    }


    /// First-player token sprite.

    public Sprite GetFirstPlayerTokenSprite()
    {
        return firstPlayerTokenSprite;
    }

    public void SetSprite(FlowerColor color, Sprite sprite)
    {
        switch (color)
        {
            case FlowerColor.Blue:   blueSprite   = sprite; break;
            case FlowerColor.Yellow: yellowSprite = sprite; break;
            case FlowerColor.Red:    redSprite    = sprite; break;
            case FlowerColor.Pink:   pinkSprite   = sprite; break;
            case FlowerColor.Green:  greenSprite  = sprite; break;
        }

        OnSpritesChanged?.Invoke();
    }
}
