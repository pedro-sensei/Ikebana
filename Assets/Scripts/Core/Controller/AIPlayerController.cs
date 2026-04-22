using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;

//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

// Controls AI turns: waits for turn start,
// picks a move, animates, then executes.
public class AIPlayerController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float aiMoveDelay = 1.0f;

    public IPlayerAIBrain[] Brains;
    private bool _isThinking;

    private void Awake()
    {
        GameEvents.OnTurnStart += HandleTurnStart;
    }

    private void Start()
    {
        InitializeBrains();
        float savedMs = PlayerPrefs.GetFloat(GameController.AIThinkingTimePrefKey, GameController.AIThinkingTimeDefault) * 1000f;
        SetAIThinkingTimeMs(savedMs);
    }

    private void OnDestroy()
    {
        GameEvents.OnTurnStart -= HandleTurnStart;
    }

    public void InitializeBrains()
    {
        if (Brains != null) return;

        PlayerSlotConfig[] slots = GameController.Instance.PlayerSlots;
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
                    //slots[i].GreedyWeights,
            }
        }
    }

    #region GAMEPLAY
    private void HandleTurnStart(int playerIndex, int turnNumber)
    {
        // Ignore human turns or if already thinking
        if (Brains == null || Brains[playerIndex] == null || _isThinking) return;

        StartCoroutine(ExecuteAITurn(playerIndex));
    }

    private IEnumerator ExecuteAITurn(int playerIndex)
    {
        _isThinking = true;
        GameEvents.AIThinkingStart(playerIndex);
        yield return new WaitForSeconds(aiMoveDelay);

        GameController gc = GameResources.Instance.GameController;
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
            Debug.LogError("AI " + playerIndex + " has no moves!");
            GameEvents.AIThinkingEnd(playerIndex);
            _isThinking = false;
            yield break;
        }

        // Run the search on a background thread - Unity was freezing.
        GameMove chosenMove = validMoves[0];
        System.Exception threadException = null;
        bool searchDone = false;

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
                searchDone = true;
            }
        });
        searchThread.IsBackground = true;
        searchThread.Start();

        // Yield each frame until the background thread finishes
        while (!searchDone)
            yield return null;

        if (threadException != null)
        {
            Debug.LogException(threadException);
            chosenMove = validMoves[Random.Range(0, validMoves.Count)];
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
        GameModel model = GameResources.Instance.GameController.Model;

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

        // Determine animation target: penalty line or placement line
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
            // Fallback: if target slot transform is not registered, skip animation safely.
            yield break;
        }

        Vector3 targetPos = targetRt.position;
        Vector3 centralPos = centralRt != null ? centralRt.position : sourcePos;
        Vector3 penaltyPos = penaltyRt != null ? penaltyRt.position : targetPos;

        // Sort flowers into picked vs remaining for animation
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

        // Wait for animation or timeout after 3s
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
            MinMaxReduxBrain minMax = Brains[i] as MinMaxReduxBrain;
            if (minMax != null)
                minMax.SetTimeLimitMs(ms);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (aiMoveDelay < 0f)
            aiMoveDelay = 0f;
    }
#endif
}