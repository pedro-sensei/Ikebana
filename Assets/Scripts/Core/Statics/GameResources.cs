using UnityEngine;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
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
    [SerializeField] private FlowerSpriteRepository spriteLibrary;

    [Header("Canvases")]
    [Tooltip("Top canvas used for dragged flowers and AI ghosts so they render on top.")]
    [SerializeField] private Canvas dragCanvas;

    [Header("Controllers")]
    [SerializeField] private GameController gameController;
    [SerializeField] private AIPlayerController aiPlayerController;
    [SerializeField] private MainMenuController mainMenuController;

    [Header("Game Setup Data")]
    [SerializeField] private GameSetupData gameSetupData;
    [SerializeField] private GameConfigData gameConfigData;
    [SerializeField] private PortraitRepository portraitRepository;
    [SerializeField] private AIOpponentRepository aiOpponentRepository;

    [Header("AI Animation")]
    [Tooltip("How long the AI flower fly animation lasts in seconds.")]
    [SerializeField] private float aiflowerAnimDuration = 0.35f;

    public GameObject FlowerPrefab
    {
        get { return flowerPrefab; }
    }

    public GameObject MainMenuController
    {
        get { return mainMenuController != null ? mainMenuController.gameObject : null; }
    }

    public MainMenuController MainMenuControllerRef
    {
        get { return mainMenuController; }
    }
 
    public FlowerSpriteData SpriteData
    {
        get { return spriteData; }
    }
    public FlowerSpriteRepository SpriteLibrary
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

    public GameSetupData GameSetupData
    {
        get { return gameSetupData; }
    }

    public GameConfigData GameConfigData
    {
        get { return gameConfigData; }
    }

    public PortraitRepository PortraitRepository
    {
        get { return portraitRepository; }
    }

    public AIOpponentRepository AIOpponentRepository
    {
        get { return aiOpponentRepository; }
    }

    public static GameController GetGameController()
    {
        if (Instance != null)
        {
            // Cache the scene controller lazily because GameResources can wake up before GameController in some scene setups.
            if (Instance.gameController == null)
                Instance.gameController = GameController.Instance;

            if (Instance.gameController != null)
                return Instance.gameController;
        }

        return GameController.Instance;
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

        // Apply sprite choices here before any board/display view asks for sprites.
        // Doing it later caused the first rendered frame to use old flower variants.
        // Apply the player's saved sprite choices before any view reads from spriteData
        FlowerSpriteSettings.ApplyAll(spriteLibrary, spriteData);

        ResolveSceneControllers();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void ResolveSceneControllers()
    {
        if (gameController == null)
            gameController = FindFirstObjectByType<GameController>();

        if (aiPlayerController == null)
            aiPlayerController = FindFirstObjectByType<AIPlayerController>();

        if (mainMenuController == null)
            mainMenuController = FindFirstObjectByType<MainMenuController>();
    }
    #endregion
}
