using UnityEngine;
using UnityEngine.UI;

//=^..^=   =^..^=   VERSION 1.0.2 (April 2026)    =^..^=    =^..^=
//                    Last Update 01/04/2026 
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
    [SerializeField] private Button returnButton;
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
        if (returnButton != null) returnButton.onClick.AddListener(OnReturnClicked);
        if (saveGameButton != null) saveGameButton.onClick.AddListener(OnSaveGameClicked);
        if (quitButton != null)     quitButton.onClick.AddListener(OnQuitClicked);
    }

    private void Start()
    {
        if (isHomeScreen)
        ShowMainMenu();
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
    }
    public void OnReturnClicked()
    {
        SetPanel(mainMenuPanel, false);
        SetPanel(newGamePanel, false);
        SetPanel(loadGamePanel, false);
        SetPanel(settingsPanel, false);
        SetPanel(creditsPanel, false);
        SetPanel(saveGamePanel, false);
    }
    public void OnNewGameClicked()
    {
        SetPanel(mainMenuPanel, false);
        SetPanel(newGamePanel, true);
     
        SetPanel(loadGamePanel, false);
        SetPanel(settingsPanel, false);
        SetPanel(creditsPanel, false);
        SetPanel(saveGamePanel, false);
    }
    public void OnSaveGameClicked()
    {
        SetPanel(mainMenuPanel, false);
        SetPanel(saveGamePanel, true);
   
        SetPanel(newGamePanel, false);
        SetPanel(loadGamePanel, false);
        SetPanel(settingsPanel, false);
        SetPanel(creditsPanel, false);

    }

    public void OnLoadGameClicked()
    {
        SetPanel(mainMenuPanel, false);
        SetPanel(loadGamePanel, true);

        SetPanel(newGamePanel, false);

        SetPanel(settingsPanel, false);
        SetPanel(creditsPanel, false);
        SetPanel(saveGamePanel, false);
    }

    public void OnSettingsClicked()
    {
        SetPanel(mainMenuPanel, false);
        SetPanel(settingsPanel, true);

        SetPanel(newGamePanel, false);
        SetPanel(loadGamePanel, false);
        SetPanel(creditsPanel, false);
        SetPanel(saveGamePanel, false);

    }

    public void OnCreditsClicked()
    {
        SetPanel(mainMenuPanel, false);
        SetPanel(creditsPanel, true);

        SetPanel(newGamePanel, false);
        SetPanel(loadGamePanel, false);
        SetPanel(settingsPanel, false);
        SetPanel(saveGamePanel, false);
    }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
}
