using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.1 (April 2026)    =^..^=    =^..^=
//                    Last Update 27/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=



// Controls AI turns: waits for turn start,
// picks a move, animates, then executes.
public class AIPlayerController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float aiMoveDelay = 1.0f;
    [SerializeField] private bool autoPlay = true;

    public IPlayerAIBrain[] Brains;
    private bool _isThinking;
    private bool _turnActive;
    private int _activeTurnPlayerIndex = -1;

    private void Awake()
    {
        //Subrscibe to turn events.
        GameEvents.OnTurnStart += HandleTurnStart;
        GameEvents.OnTurnEnd += HandleTurnEnd;
    }

    private void Start()
    {
        InitializeBrains();
        float savedMs = PlayerPrefs.GetFloat(GameController.AIThinkingTimePrefKey, GameController.AIThinkingTimeDefault) * 1000f;
        SetAIThinkingTimeMs(savedMs+1); //+1 to avoid 0 in selector.
    }

    private void OnDestroy()
    {
        GameEvents.OnTurnStart -= HandleTurnStart;
        GameEvents.OnTurnEnd -= HandleTurnEnd;
    }

    public void InitializeBrains()
    {
        GameController gc = GameResources.GetGameController();
        
        PlayerSlotConfig[] slots = gc.PlayerSlots;
        
        Brains = new IPlayerAIBrain[slots.Length];

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].PlayerType == PlayerType.AI)
            {
                Brains[i] = AIBrainFactory.CreateBrain(
                    brainType: slots[i].AIBrain,
                    minMaxDepth: slots[i].MinMaxDepth,
                    minMaxTimeLimitMs: slots[i].MinMaxTimeLimitMs,
                    minMaxEvaluator: slots[i].MinMaxEvaluator,
                    optimizerWeights: slots[i].OptimizerWeights,
                    mlAgentModel: slots[i].MLAgentModel,
                    numPlayers: slots.Length,
                    emilyEarlyWeights: slots[i].EmilyEarlyWeights,
                    emilyMidWeights: slots[i].EmilyMidWeights,
                    emilyLateWeights: slots[i].EmilyLateWeights,
                    relativeImmediateSimulationWeight: slots[i].RelativeImmediateSimulationWeight,
                    relativeTerminalDeltaWeight: slots[i].RelativeTerminalDeltaWeight,
                    relativeExpectedMoveWeight: slots[i].RelativeExpectedMoveWeight,
                    relativeUsePhaseAwareImmediateWeight: slots[i].RelativeUsePhaseAwareImmediateWeight);
            }
        }
    }

    #region GAMEPLAY
    private void HandleTurnStart(int playerIndex, int turnNumber)
    {
        //Safety check to try to solve AI turn freeze.
        if (Brains == null || playerIndex < 0 || playerIndex >= Brains.Length)
        {
            _turnActive = true;
            _activeTurnPlayerIndex = playerIndex;
            return;
        }

        _turnActive = true;
        _activeTurnPlayerIndex = playerIndex;

        // Ignore human turns
        if (!autoPlay || Brains == null || Brains[playerIndex] == null || _isThinking) return;

        StartCoroutine(ExecuteAITurn(playerIndex));
    }

    private void HandleTurnEnd(int playerIndex, int turnNumber)
    {
        if (_activeTurnPlayerIndex != playerIndex) return;

        _turnActive = false;
        //Use -1 to avoid other players, real index handled in turnstart.
        _activeTurnPlayerIndex = -1;
    }

    private IEnumerator ExecuteAITurn(int playerIndex)
    {
        //Safety check to try to solve AI turn freeze. This should not be needed, but just in case.
        if (Brains == null || playerIndex < 0 || playerIndex >= Brains.Length || Brains[playerIndex] == null)
            yield break;

        _isThinking = true;
        //Start Thinking for animations.
        GameEvents.AIThinkingStart(playerIndex);
        yield return new WaitForSeconds(aiMoveDelay);

        GameController gc = GameResources.GetGameController();
        if (gc == null)
        {
            GameEvents.AIThinkingEnd(playerIndex);
            _isThinking = false;
            yield break;
        }

        GameModel model = gc.Model;
        IPlayerAIBrain brain = Brains[playerIndex];

        if (gc.IsGameOver || model.CurrentPlayerIndex != playerIndex)
        {
            GameEvents.AIThinkingEnd(playerIndex);
            _isThinking = false;
            yield break;
        }

        List<GameMove> validMoves = MoveGenerator.GetAllValidMoves(model);
        if (validMoves.Count == 0)
        {
            //Debug.LogError("AI " + playerIndex + " has no moves!");
            GameEvents.AIThinkingEnd(playerIndex);
            _isThinking = false;
            yield break;
        }

        // Run the search on a background thread -  animations were freezing.
        //TODO: Check if this works better with unity jobs.
        GameMove chosenMove = validMoves[0];
        System.Exception threadException = null;
        int searchDone = 0;

        Thread searchThread = new Thread(() =>
        {
            try
            {
                chosenMove = brain.ChooseMove(model, validMoves);
            }
            catch (System.Exception e)
            {
                threadException = e;
            }
            finally
            {
                Interlocked.Exchange(ref searchDone, 1);
            }
        });
        searchThread.IsBackground = true;
        searchThread.Start();

        // Yield each frame until the background thread finishes
        while (Interlocked.CompareExchange(ref searchDone, 0, 0) == 0)
            yield return null;

        //Fallbacks with goodrandom 
        if (threadException != null)
        {
            Debug.LogException(threadException);
            // Fall back to a random legal move.
            IPlayerAIBrain tempBrain = new GoodRandomAIBrain();
            chosenMove = tempBrain.ChooseMove(model , validMoves);
        }

        if (gc.IsGameOver || gc.Model.CurrentPlayerIndex != playerIndex || !_turnActive || _activeTurnPlayerIndex != playerIndex)
        {
            GameEvents.AIThinkingEnd(playerIndex);
            _isThinking = false;
            yield break;
        }

        yield return AnimateMoveCoroutine(chosenMove, playerIndex);

        // Execute the chosen move
        bool success;
        int targetLine = chosenMove.IsPenalty ? -1 : chosenMove.TargetLineIndex;
        if (chosenMove.SourceIsFactory)
            success = gc.PickFromFactoryAndPlace(chosenMove.FactoryIndex, chosenMove.Color, targetLine);
        else
            success = gc.PickFromCentralAndPlace(chosenMove.Color, targetLine);

        if (!success)
            Debug.LogError("AI failed to execute move: " + chosenMove);

        GameEvents.AIThinkingEnd(playerIndex);
        _isThinking = false;
    }
    #endregion

    private IEnumerator AnimateMoveCoroutine(GameMove move, int playerIndex)
    {
        GameController gc = GameResources.GetGameController();
        if (gc == null || gc.Model == null)
            yield break;

        GameModel model = gc.Model;

        // Find the source position for animation
        Vector3 sourcePos = Vector3.zero;
        if (move.SourceIsFactory)
        {
            RectTransform rt;
            if (ViewRegistry.TryGetFactory(move.FactoryIndex, out rt))
                sourcePos = rt.position;
        }
        else
        {
            RectTransform rt;
            if (ViewRegistry.GetCentralDisplayPosition(out rt))
                sourcePos = rt.position;
        }

        // Determine animation target
        RectTransform targetRt = null;
        if (move.IsPenalty)
        {
            ViewRegistry.GetPenaltyLinePosition(playerIndex, out targetRt);
        }
        else
        {
            ViewRegistry.GetPlacementLinePosition(playerIndex, move.TargetLineIndex, out targetRt);
        }

        RectTransform centralRt;
        ViewRegistry.GetCentralDisplayPosition(out centralRt);
        RectTransform penaltyRt;
        ViewRegistry.GetPenaltyLinePosition(playerIndex, out penaltyRt);

        if (targetRt == null)
        {
            // Missing registry data should not kill the turn.
            // Apply the move normally if animation fails.
            yield break;
        }

        Vector3 targetPos = targetRt.position;
        Vector3 centralPos = centralRt != null ? centralRt.position : sourcePos;
        Vector3 penaltyPos = penaltyRt != null ? penaltyRt.position : targetPos;

        // Sort flowers into picked vs remaining for the central display
        List<FlowerPiece> picked = new List<FlowerPiece>();
        List<FlowerPiece> remaining = new List<FlowerPiece>();
        bool hasToken = false;

        if (move.SourceIsFactory)
        {
            for (int i = 0; i < model.FactoryDisplays.Length; i++)
            {
                DisplayModel factory = model.FactoryDisplays[i];
                if (factory.ViewIndex != move.FactoryIndex) continue;

                for (int t = 0; t < factory.flowers.Count; t++)
                {
                    if (factory.flowers[t].Color == move.Color)
                        picked.Add(factory.flowers[t]);
                    else
                        remaining.Add(factory.flowers[t]);
                }
                break;
            }
        }
        else
        {
            for (int t = 0; t < model.CentralDisplay.flowers.Count; t++)
            {
                if (model.CentralDisplay.flowers[t].Color == move.Color)
                    picked.Add(model.CentralDisplay.flowers[t]);
            }
            hasToken = model.CentralDisplay.HasFirstPlayerToken;
        }

        if (picked.Count == 0 && !hasToken) yield break;

        bool animDone = false;
        GameEvents.AIflowerAnimationRequested(
            sourcePos, targetPos, centralPos, penaltyPos,
            picked, remaining, hasToken, delegate { animDone = true; });

        // Wait for animation or safe timeout 3s
        float timeout = Time.time + 3f;
        while (!animDone && Time.time < timeout)
            yield return null;
    }

    //For settings menu to update time limit on all MinMax brains
    public void SetAIThinkingTimeMs(float ms)
    {
        if (Brains == null) return;
        for (int i = 0; i < Brains.Length; i++)
        {
            MinMaxBrain minMax = Brains[i] as MinMaxBrain;
            if (minMax != null)
            {
                minMax.SetTimeLimitMs(ms);
                continue;
            }
        }
    }

    public void SetAutoPlayEnabled(bool enabled)
    {
        autoPlay = enabled;

        if (enabled)
            TryExecuteActiveTurn();
    }

    private void TryExecuteActiveTurn()
    {
        if (!autoPlay || !_turnActive || _isThinking || _activeTurnPlayerIndex < 0)
            return;

        if (Brains == null || _activeTurnPlayerIndex >= Brains.Length || Brains[_activeTurnPlayerIndex] == null)
            return;

        StartCoroutine(ExecuteAITurn(_activeTurnPlayerIndex));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
    }
#endif
}