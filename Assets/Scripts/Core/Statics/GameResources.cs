using UnityEngine;
//=^..^=   =^..^=   VERSION 1.0.2 (April 2026)    =^..^=    =^..^=
//                    Last Update 01/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// Singleton that holds view assets and scene references.
// Avoids having to drag references into every game object by hand.
public class GameResources : MonoBehaviour
{
    #region FIELDS AND PROPERTIES
    public static GameResources Instance { get; private set; }

    [Header("flower Assets")]
    [Tooltip("Prefab for every flower in displays, placement lines, and penalty lines.")]
    [SerializeField] private GameObject flowerPrefab;

    [Tooltip("ScriptableObject that maps flower colors to sprites.")]
    [SerializeField] private FlowerSpriteData spriteData;

    [Tooltip("Library of all available sprite variants per flower color.")]
    [SerializeField] private FlowerSpriteLibrary spriteLibrary;

    [Header("Canvases")]
    [Tooltip("Top canvas used for dragged flowers and AI ghosts so they render on top.")]
    [SerializeField] private Canvas dragCanvas;

    [Header("Controllers")]
    [SerializeField] private GameController gameController;
    [SerializeField] private AIPlayerController aiPlayerController;
    [SerializeField] private MainMenuController mainMenuController;

    [Header("AI Animation")]
    [Tooltip("How long the AI flower fly animation lasts in seconds.")]
    [SerializeField] private float aiflowerAnimDuration = 0.35f;

    public GameObject FlowerPrefab
    {
        get { return flowerPrefab; }
    }

    public GameObject MainMenuController
    {
        get { return mainMenuController.gameObject; }
    }
 
    public FlowerSpriteData SpriteData
    {
        get { return spriteData; }
    }
    public FlowerSpriteLibrary SpriteLibrary
    {
        get { return spriteLibrary; }
    }
    public Canvas DragCanvas
    {
        get { return dragCanvas; }
    }
    public GameController GameController
    {
        get { return gameController; }
    }
    public AIPlayerController AIPlayerController
    {
        get { return aiPlayerController; }
    }
    public float AIflowerAnimDuration
    {
        get { return aiflowerAnimDuration; }
    }

    #endregion

    #region LIFECYCLE
    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        // Apply the player's saved sprite choices before any view reads from spriteData
        FlowerSpriteSettings.ApplyAll(spriteLibrary, spriteData);

        // Try to find controllers if not set in Inspector
        if (gameController == null)
            gameController = FindFirstObjectByType<GameController>();
        if (aiPlayerController == null)
            aiPlayerController = FindFirstObjectByType<AIPlayerController>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
    #endregion
}
