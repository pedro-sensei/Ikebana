
using System.Collections;
using UnityEngine;

//DRAFT - NOT WORKING YET.
public class TutorialController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameController gameController;
    [SerializeField] private AIPlayerController aiPlayerController;
    [SerializeField] private TMPro.TextMeshProUGUI tutorialText;

    [Header("Tutorial")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool clearTextWhenFinished = false;
    [SerializeField] private TutorialEvent[] tutorialEvents;

    private Coroutine _tutorialCoroutine;
    private bool _tutorialRunning;
    private bool _turnActive;

    private void Awake()
    {
        ResolveReferences();
        SubscribeToEvents();
        SetTutorialText(string.Empty);

        if (autoStart)
            SetAIAutomation(false);
    }

    private void Start()
    {
        if (autoStart)
            StartTutorial();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();

        if (_tutorialRunning)
            SetAIAutomation(true);
    }

    public void StartTutorial()
    {
        if (_tutorialCoroutine != null)
            return;

        ResolveReferences();
        SetAIAutomation(false);
        _tutorialCoroutine = StartCoroutine(RunTutorialCoroutine());
    }

    public void StopTutorial()
    {
        FinishTutorial();
    }

    private IEnumerator RunTutorialCoroutine()
    {
        _tutorialRunning = true;

        // Wait until the live game controller/model exists because this script can wake up before the board scene finishes booting.
        while (gameController == null || gameController.Model == null)
        {
            ResolveReferences();
            yield return null;
        }

        if (tutorialEvents != null)
        {
            for (int i = 0; i < tutorialEvents.Length; i++)
            {
                TutorialEvent tutorialEvent = tutorialEvents[i];
                if (tutorialEvent == null)
                    continue;

                if (tutorialEvent.EventType == TutorialEventType.Message)
                {
                    SetTutorialText(tutorialEvent.Message);
                    // Message steps are just timed callouts; move steps are the ones that wait for a legal player window.
                    float duration = Mathf.Max(0f, tutorialEvent.Duration);
                    if (duration > 0f)
                        yield return new WaitForSeconds(duration);
                    continue;
                }

                yield return WaitForMoveWindow();

                if (gameController == null || gameController.IsGameOver)
                    break;

                if (!ExecuteMove(tutorialEvent))
                {
                    Debug.LogError($"[TutorialController] Invalid tutorial move at index {i}.", this);
                    break;
                }

                yield return WaitForMoveResolution();
            }
        }

        FinishTutorial();
    }

    private IEnumerator WaitForMoveWindow()
    {
        while (true)
        {
            if (gameController == null)
            {
                ResolveReferences();
                yield return null;
                continue;
            }

            if (gameController.IsGameOver)
                yield break;

            if (!gameController.IsPaused &&
                gameController.Model != null &&
                gameController.Model.CurrentPhase == RoundPhaseEnum.PlacementPhase &&
                _turnActive)
            {
                // The tutorial piggybacks on the normal gameplay rules instead of forcing moves at invalid times.
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator WaitForMoveResolution()
    {
        yield return null;

        while (_turnActive && gameController != null && !gameController.IsGameOver)
            yield return null;
    }

    private bool ExecuteMove(TutorialEvent tutorialEvent)
    {
        int targetLineIndex = tutorialEvent.Target == MoveTarget.PenaltyLine
            ? -1
            : tutorialEvent.TargetLineIndex;

        if (tutorialEvent.Source == MoveSource.Factory)
        {
            return gameController.PickFromFactoryAndPlace(
                tutorialEvent.SourceIndex,
                tutorialEvent.Color,
                targetLineIndex);
        }

        return gameController.PickFromCentralAndPlace(
            tutorialEvent.Color,
            targetLineIndex);
    }

    private void FinishTutorial()
    {
        if (_tutorialCoroutine != null)
        {
            StopCoroutine(_tutorialCoroutine);
            _tutorialCoroutine = null;
        }

        _tutorialRunning = false;
        SetAIAutomation(true);

        if (clearTextWhenFinished)
            SetTutorialText(string.Empty);
    }

    private void HandleTurnStart(int playerIndex, int turnNumber)
    {
        _turnActive = true;
    }

    private void HandleTurnEnd(int playerIndex, int turnNumber)
    {
        _turnActive = false;
    }

    private void HandlePhaseStart(RoundPhaseEnum phase)
    {
        if (phase != RoundPhaseEnum.PlacementPhase)
            _turnActive = false;
    }

    private void HandleGameOver()
    {
        _turnActive = false;
    }

    private void ResolveReferences()
    {
        if (gameController == null)
            gameController = GameResources.Instance != null ? GameResources.Instance.GameController : FindFirstObjectByType<GameController>();

        if (aiPlayerController == null)
            aiPlayerController = GameResources.Instance != null ? GameResources.Instance.AIPlayerController : FindFirstObjectByType<AIPlayerController>();
    }

    private void SubscribeToEvents()
    {
        GameEvents.OnTurnStart += HandleTurnStart;
        GameEvents.OnTurnEnd += HandleTurnEnd;
        GameEvents.OnPhaseStart += HandlePhaseStart;
        GameEvents.OnGameOver += HandleGameOver;
    }

    private void UnsubscribeFromEvents()
    {
        GameEvents.OnTurnStart -= HandleTurnStart;
        GameEvents.OnTurnEnd -= HandleTurnEnd;
        GameEvents.OnPhaseStart -= HandlePhaseStart;
        GameEvents.OnGameOver -= HandleGameOver;
    }

    private void SetAIAutomation(bool enabled)
    {
        if (aiPlayerController != null)
            aiPlayerController.SetAutoPlayEnabled(enabled);
    }

    private void SetTutorialText(string message)
    {
        if (tutorialText != null)
            tutorialText.text = message ?? string.Empty;
    }
}

[System.Serializable]
public class TutorialEvent
{
    [SerializeField] private TutorialEventType eventType = TutorialEventType.Message;
    [SerializeField][TextArea(2, 6)] private string message;
    [SerializeField] private float duration = 2f;
    [SerializeField] private MoveSource source = MoveSource.Factory;
    [SerializeField] private int sourceIndex;
    [SerializeField] private FlowerColor color = FlowerColor.Blue;
    [SerializeField] private MoveTarget target = MoveTarget.PlacementLine;
    [SerializeField] private int targetLineIndex;

    public TutorialEventType EventType => eventType;
    public string Message => message;
    public float Duration => duration;
    public MoveSource Source => source;
    public int SourceIndex => sourceIndex;
    public FlowerColor Color => color;
    public MoveTarget Target => target;
    public int TargetLineIndex => targetLineIndex;
}

public enum TutorialEventType
{
    Message,
    Move
}
