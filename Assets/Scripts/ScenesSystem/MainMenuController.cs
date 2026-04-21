using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=


// Controls the main menu screen. Shows/hides sub-menu panels 

public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    [Tooltip("The root panel of the main menu (buttons: New Game, Load, Settings, Credits, Quit).")]
    [SerializeField] private GameObject mainMenuPanel;

    [Tooltip("The New Game setup panel.")]
    [SerializeField] private GameObject newGamePanel;

    [Tooltip("The Load Game panel.")]
    [SerializeField] private GameObject loadGamePanel;
    
    [Tooltip("The Save Game panel.")] 
    [SerializeField] private GameObject saveGamePanel;
    
    [Tooltip("The Settings panel.")]
    [SerializeField] private GameObject settingsPanel;

    [Tooltip("The Credits panel.")]
    [SerializeField] private GameObject creditsPanel;

    [Header("Buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button saveGameButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button returnToMainMenuButton;
    [SerializeField] private Button quitButton;

    [Header("Toggle Home Screen")]
    [SerializeField] private bool isHomeScreen = true;
    private void Awake()
    {
        // Wire buttons if assigned
        if (newGameButton != null)  newGameButton.onClick.AddListener(OnNewGameClicked);
        if (loadGameButton != null) loadGameButton.onClick.AddListener(OnLoadGameClicked);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
        if (creditsButton != null)  creditsButton.onClick.AddListener(OnCreditsClicked);
        if (resumeButton != null) resumeButton.onClick.AddListener(OnReturnClicked);
        if (returnToMainMenuButton != null) returnToMainMenuButton.onClick.AddListener(OnReturnToMainMenuClicked);
        if (saveGameButton != null) saveGameButton.onClick.AddListener(OnSaveGameClicked);
        if (quitButton != null)     quitButton.onClick.AddListener(OnQuitClicked);
    }

    private void Start()
    {
        if (isHomeScreen)
        ShowMainMenu();

        SyncPauseStateWithMenus();
    }

    // Shows only the main menu panel, hides all sub-menus.

    public void ShowMainMenu()
    {
        SetPanel(mainMenuPanel, true);
        SetPanel(newGamePanel, false);
        SetPanel(loadGamePanel, false);
        SetPanel(settingsPanel, false);
        SetPanel(creditsPanel, false);
        SetPanel(saveGamePanel, false);
        SyncPauseStateWithMenus();
    }

    //
    public void OnInGameMenuClicked()
    {
        SetPanel(mainMenuPanel, true);
        SetPanel(newGamePanel, false);
        SetPanel(loadGamePanel, false);
        SetPanel(settingsPanel, false);
        SetPanel(creditsPanel, false);
        SetPanel(saveGamePanel, false);
        SyncPauseStateWithMenus();
    }
    public void OnReturnClicked()
    {
        SetPanel(mainMenuPanel, false);
        SetPanel(newGamePanel, false);
        SetPanel(loadGamePanel, false);
        SetPanel(settingsPanel, false);
        SetPanel(creditsPanel, false);
        SetPanel(saveGamePanel, false);
        SyncPauseStateWithMenus();
    }
    public void OnNewGameClicked()
    {
        SetPanel(mainMenuPanel, false);
        SetPanel(newGamePanel, true);
     
        SetPanel(loadGamePanel, false);
        SetPanel(settingsPanel, false);
        SetPanel(creditsPanel, false);
        SetPanel(saveGamePanel, false);
        SyncPauseStateWithMenus();
    }
    public void OnSaveGameClicked()
    {
        SetPanel(mainMenuPanel, false);
        SetPanel(saveGamePanel, true);
   
        SetPanel(newGamePanel, false);
        SetPanel(loadGamePanel, false);
        SetPanel(settingsPanel, false);
        SetPanel(creditsPanel, false);
        SyncPauseStateWithMenus();

    }

    public void OnLoadGameClicked()
    {
        SetPanel(mainMenuPanel, false);
        SetPanel(loadGamePanel, true);

        SetPanel(newGamePanel, false);

        SetPanel(settingsPanel, false);
        SetPanel(creditsPanel, false);
        SetPanel(saveGamePanel, false);
        SyncPauseStateWithMenus();
    }

    public void OnSettingsClicked()
    {
        SetPanel(mainMenuPanel, false);
        SetPanel(settingsPanel, true);

        SetPanel(newGamePanel, false);
        SetPanel(loadGamePanel, false);
        SetPanel(creditsPanel, false);
        SetPanel(saveGamePanel, false);
        SyncPauseStateWithMenus();

    }

    public void OnCreditsClicked()
    {
        SetPanel(mainMenuPanel, false);
        SetPanel(creditsPanel, true);

        SetPanel(newGamePanel, false);
        SetPanel(loadGamePanel, false);
        SetPanel(settingsPanel, false);
        SetPanel(saveGamePanel, false);
        SyncPauseStateWithMenus();
    }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnReturnToMainMenuClicked()
    {
        SceneManager.LoadScene("START");
    }


    // Called by "Back" buttons to return to the main menu.

    public void OnBackToMainMenu()
    {
        ShowMainMenu();
    }

    //Helpers

    private static void SetPanel(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }

    //Pause fix, some menus interacted poorly with the game.
    private void SyncPauseStateWithMenus()
    {
        GameController gc = null;
        if (GameResources.Instance != null && GameResources.Instance.GameController != null)
            gc = GameResources.Instance.GameController;
        else
            gc = GameController.Instance;

        if (gc == null) return;

        bool anyMenuActive = (mainMenuPanel != null && mainMenuPanel.activeSelf)
                          || (newGamePanel != null && newGamePanel.activeSelf)
                          || (loadGamePanel != null && loadGamePanel.activeSelf)
                          || (saveGamePanel != null && saveGamePanel.activeSelf)
                          || (settingsPanel != null && settingsPanel.activeSelf)
                          || (creditsPanel != null && creditsPanel.activeSelf);

        if (anyMenuActive)
            gc.PauseGame();
        else
            gc.ResumeGame();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (mainMenuPanel == null)
            Debug.LogWarning("[MainMenuController] Main menu panel is not assigned.", this);

        if (newGamePanel == null)
            Debug.LogWarning("[MainMenuController] New game panel is not assigned.", this);

        if (loadGamePanel == null)
            Debug.LogWarning("[MainMenuController] Load game panel is not assigned.", this);

        if (settingsPanel == null)
            Debug.LogWarning("[MainMenuController] Settings panel is not assigned.", this);

        if (creditsPanel == null)
            Debug.LogWarning("[MainMenuController] Credits panel is not assigned.", this);
    }
#endif
}
