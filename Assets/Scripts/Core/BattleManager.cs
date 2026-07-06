using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Botte.Core
{
    public enum GameplayActionType
    {
        Attack,
        EndTurn,
        EquipPrep,
        FinishPrep,
        Sleep,
        DrawExtra,
        DeckChoice,
        PeekKeep,
        PeekDiscard,
        CardClicked,
        ItemClicked,
        EquipmentClicked,
        CardRightClicked,
        EquipSlotRightClicked
    }

    public class BattleManager : MonoBehaviour
    {
        public GameManager gameManager;
        public Botte.UI.BattleUI battleUI;

        [Header("Combat Phase Buttons")]
        public Button attackButton;
        public Button endTurnButton;

        [Header("Preparation Phase Buttons")]
        public Button drawPrepButton;
        public Button equipPrepButton;
        public Button finishPrepButton;

        [Header("End Phase Buttons")]
        public Button sleepButton;
        public Button drawExtraButton;

        [Header("Character Select (order: Warrior, Mage, Rogue, Necro)")]
        public Button[] p1ClassButtons;
        public Button[] p2ClassButtons;
        public Button startBattleButton;

        [Header("Winner Buttons")]
        public Button restartButton;
        public Button exitButton;

        [Header("Draw Choice Buttons")]
        public Button drawChoiceSpellButton;
        public Button drawChoiceEquipButton;
        public Button drawChoiceItemButton;

        [Header("Peek Buttons (manipolazione)")]
        public Button peekKeepButton;
        public Button peekDiscardButton;

        [Header("Main Menu Buttons")]
        public Button playMenuButton;
        public Button exitMenuButton;

        private GameState gameState;
        private TurnManager turnManager;
        private int prepDrawsThisPhase;
        private int equipsThisPhase;

        private HeroClass? selectedP1;
        private HeroClass? selectedP2;

        // Pending deck-draw flow (deck choice popup).
        private HeroState pendingDrawHero;
        private int pendingDrawCount;
        private bool pendingPeek;
        private System.Action pendingAfterDraws;

        // Pending peek (manipolazione) keep/discard.
        private DeckChoice pendingPeekDeck;
        private CardData pendingPeekCard;

        // --- Timer and Turn Counter Fields ---
        private float phaseTimer;
        private bool timerActive;
        private int lastSentSeconds = -1;
        private TextMeshProUGUI turnCounterText;
        private TextMeshProUGUI timerText;

        // Big centered "YOUR TURN / OPPONENT TURN" banner shown briefly at turn start.
        private TextMeshProUGUI turnAnnounceText;
        private Coroutine _turnAnnounceCoroutine;

        private void Start()
        {
            WireRuntimeListeners();
            ShowMainMenu();
            EnsureTurnAndTimerUI();
        }

        public bool IsHeroActive(HeroState hero)
        {
            return gameState != null && gameState.activePlayer == hero;
        }

        // ---------- Runtime wiring ----------
        private void WireRuntimeListeners()
        {
            if (playMenuButton != null) playMenuButton.onClick.AddListener(OnPlayMenuPressed);
            if (exitMenuButton != null) exitMenuButton.onClick.AddListener(OnExitPressed);

            if (p1ClassButtons != null)
            {
                for (int i = 0; i < p1ClassButtons.Length; i++)
                {
                    int idx = i;
                    if (p1ClassButtons[i] != null) p1ClassButtons[i].onClick.AddListener(() => SelectClass(1, idx));
                }
            }
            if (p2ClassButtons != null)
            {
                for (int i = 0; i < p2ClassButtons.Length; i++)
                {
                    int idx = i;
                    if (p2ClassButtons[i] != null) p2ClassButtons[i].onClick.AddListener(() => SelectClass(2, idx));
                }
            }
            if (startBattleButton != null) startBattleButton.onClick.AddListener(OnStartBattlePressed);
            if (restartButton != null) restartButton.onClick.AddListener(OnRestartPressed);
            if (exitButton != null) exitButton.onClick.AddListener(OnExitPressed);
            if (drawChoiceSpellButton != null) drawChoiceSpellButton.onClick.AddListener(() => OnDeckChoice(DeckChoice.Spell));
            if (drawChoiceEquipButton != null) drawChoiceEquipButton.onClick.AddListener(() => OnDeckChoice(DeckChoice.Equipment));
            if (drawChoiceItemButton != null) drawChoiceItemButton.onClick.AddListener(() => OnDeckChoice(DeckChoice.Item));
            if (peekKeepButton != null) peekKeepButton.onClick.AddListener(OnPeekKeep);
            if (peekDiscardButton != null) peekDiscardButton.onClick.AddListener(OnPeekDiscard);

            if (battleUI != null)
            {
                if (battleUI.p1BookButtons != null)
                    for (int i = 0; i < battleUI.p1BookButtons.Length; i++)
                    {
                        int idx = i;
                        if (battleUI.p1BookButtons[i] != null) battleUI.p1BookButtons[i].onClick.AddListener(() => OnBookSelected(true, idx));
                    }
                if (battleUI.p2BookButtons != null)
                    for (int i = 0; i < battleUI.p2BookButtons.Length; i++)
                    {
                        int idx = i;
                        if (battleUI.p2BookButtons[i] != null) battleUI.p2BookButtons[i].onClick.AddListener(() => OnBookSelected(false, idx));
                    }

                if (battleUI.p1ShowEquipButton != null) battleUI.p1ShowEquipButton.onClick.AddListener(() => OnShowEquipToggle(true));
                if (battleUI.p2ShowEquipButton != null) battleUI.p2ShowEquipButton.onClick.AddListener(() => OnShowEquipToggle(false));
            }
        }

        private void ShowMainMenu()
        {
            if (battleUI != null)
            {
                if (battleUI.mainMenuPanel != null) battleUI.mainMenuPanel.SetActive(true);
                if (battleUI.characterSelectPanel != null) battleUI.characterSelectPanel.SetActive(false);
                if (battleUI.battleScreen != null) battleUI.battleScreen.SetActive(false);
                if (battleUI.winnerOverlay != null) battleUI.winnerOverlay.SetActive(false);
                if (battleUI.drawChoicePanel != null) battleUI.drawChoicePanel.SetActive(false);
            }
        }

        public void OnPlayMenuPressed()
        {
            if (battleUI != null && battleUI.mainMenuPanel != null)
                battleUI.mainMenuPanel.SetActive(false);
            ShowCharacterSelect();
        }

        // Toggles the equipment-slot window for a player (button to the left of the hero stats).
        public void OnShowEquipToggle(bool isPlayer1)
        {
            if (battleUI == null) return;
            battleUI.ToggleEquipmentSlots(isPlayer1);
            if (gameState != null)
                battleUI.RefreshEquipment(isPlayer1 ? gameState.player1 : gameState.player2, isPlayer1);
        }

        public void OnBookSelected(bool isPlayer1, int bookIdx)
        {
            if (battleUI == null) return;
            battleUI.SetSelectedBook(isPlayer1, (BookType)bookIdx);
            if (gameState != null)
                battleUI.RefreshBook(isPlayer1 ? gameState.player1 : gameState.player2, isPlayer1);
        }

        // ---------- Turn and Timer UI Generator ----------
        private void EnsureTurnAndTimerUI()
        {
            var centerPanel = GameObject.Find("Canvas/BattleScreen/CenterPanel");
            if (centerPanel == null) return;

            var turnTextTrans = centerPanel.transform.Find("TurnText");
            if (turnTextTrans != null)
            {
                var turnTextRT = turnTextTrans.GetComponent<RectTransform>();
                if (turnTextRT != null)
                {
                    turnTextRT.anchoredPosition = new Vector2(0f, -45f);
                }
            }

            var phaseTextTrans = centerPanel.transform.Find("PhaseText");
            if (phaseTextTrans != null)
            {
                var phaseTextRT = phaseTextTrans.GetComponent<RectTransform>();
                if (phaseTextRT != null)
                {
                    phaseTextRT.anchoredPosition = new Vector2(0f, -75f);
                }
            }

            var turnCounterTrans = centerPanel.transform.Find("TurnCounterText");
            if (turnCounterTrans == null)
            {
                var go = new GameObject("TurnCounterText");
                go.transform.SetParent(centerPanel.transform, false);
                turnCounterText = go.AddComponent<TextMeshProUGUI>();
                turnCounterText.fontSize = 20f;
                turnCounterText.color = Color.yellow;
                turnCounterText.alignment = TextAlignmentOptions.Left;
                
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(-60f, -10f);
                rt.sizeDelta = new Vector2(120f, 30f);
            }
            else
            {
                turnCounterText = turnCounterTrans.GetComponent<TextMeshProUGUI>();
            }

            var timerTrans = centerPanel.transform.Find("TimerText");
            if (timerTrans == null)
            {
                var go = new GameObject("TimerText");
                go.transform.SetParent(centerPanel.transform, false);
                timerText = go.AddComponent<TextMeshProUGUI>();
                timerText.fontSize = 20f;
                timerText.color = Color.cyan;
                timerText.alignment = TextAlignmentOptions.Right;
                
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(60f, -10f);
                rt.sizeDelta = new Vector2(120f, 30f);
            }
            else
            {
                timerText = timerTrans.GetComponent<TextMeshProUGUI>();
            }
        }

        // Creates (once) the large centered banner used for the turn announcement.
        private void EnsureTurnAnnounceUI()
        {
            if (turnAnnounceText != null) return;

            var battleScreen = GameObject.Find("Canvas/BattleScreen");
            if (battleScreen == null) return;

            var existing = battleScreen.transform.Find("TurnAnnounce");
            if (existing != null)
            {
                turnAnnounceText = existing.GetComponent<TextMeshProUGUI>();
                return;
            }

            var go = new GameObject("TurnAnnounce");
            go.transform.SetParent(battleScreen.transform, false);
            turnAnnounceText = go.AddComponent<TextMeshProUGUI>();
            turnAnnounceText.fontSize = 64f;
            turnAnnounceText.fontStyle = FontStyles.Bold;
            turnAnnounceText.alignment = TextAlignmentOptions.Center;
            turnAnnounceText.raycastTarget = false; // never block clicks
            turnAnnounceText.enableWordWrapping = false;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 120f);
            rt.sizeDelta = new Vector2(760f, 120f);

            go.SetActive(false);
        }

        // Announces whose turn it is: "YOUR TURN" / "OPPONENT TURN" in multiplayer,
        // or the active hero's name in local hotseat mode. Auto-hides after 1.5s.
        private void ShowTurnIndicator()
        {
            EnsureTurnAnnounceUI();
            if (turnAnnounceText == null || gameState == null) return;

            HeroState active = gameState.activePlayer;
            string msg;
            Color col;
            if (RelayManager.IsMultiplayer)
            {
                if (IsMyTurn())
                {
                    msg = "YOUR TURN";
                    col = new Color32(0x2e, 0xcc, 0x71, 0xff); // green
                }
                else
                {
                    msg = "OPPONENT TURN";
                    col = new Color32(0xe9, 0x45, 0x60, 0xff); // red
                }
            }
            else
            {
                msg = $"TURNO DI {active.data.heroName.ToUpper()}";
                col = Color.white;
            }

            turnAnnounceText.text = msg;
            turnAnnounceText.color = col;

            if (_turnAnnounceCoroutine != null) StopCoroutine(_turnAnnounceCoroutine);
            _turnAnnounceCoroutine = StartCoroutine(TurnIndicatorRoutine());
        }

        private System.Collections.IEnumerator TurnIndicatorRoutine()
        {
            turnAnnounceText.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(1.5f);
            if (turnAnnounceText != null) turnAnnounceText.gameObject.SetActive(false);
        }

        public void UpdateTimerText(int secondsLeft)
        {
            EnsureTurnAndTimerUI();
            if (timerText != null)
            {
                if (secondsLeft > 0)
                {
                    timerText.text = $"TIME: {secondsLeft}s";
                }
                else
                {
                    timerText.text = "";
                }
            }

            if (turnCounterText != null && gameState != null)
            {
                turnCounterText.text = $"TURN {gameState.currentTurn}";
            }
        }

        // ---------- Multiplayer Helper Checks ----------
        private bool IsMyTurn()
        {
            if (!RelayManager.IsMultiplayer) return true;
            bool amHost = Unity.Netcode.NetworkManager.Singleton.IsServer;
            bool isP1Turn = gameState.activePlayer == gameState.player1;
            return amHost == isP1Turn;
        }

        private bool CanInteract()
        {
            return IsMyTurn();
        }

        private bool CanInteractWithOwner(HeroState owner)
        {
            if (!RelayManager.IsMultiplayer) return true;
            bool amHost = Unity.Netcode.NetworkManager.Singleton.IsServer;
            bool isP1Turn = gameState.activePlayer == gameState.player1;
            bool isOwnerP1 = owner == gameState.player1;
            return (amHost == isP1Turn) && (isOwnerP1 == amHost);
        }

        private HeroState GetMyHero()
        {
            if (Unity.Netcode.NetworkManager.Singleton.IsServer)
                return gameState.player1;
            else
                return gameState.player2;
        }

        private void ExecuteAction(GameplayActionType actionType, int arg1 = 0, int arg2 = 0)
        {
            if (RelayManager.IsMultiplayer)
            {
                RelayManager.Instance.SendGameplayAction(actionType, arg1, arg2);
            }
            else
            {
                ExecuteActionLocal(actionType, arg1, arg2);
            }
        }

        public void ExecuteActionLocal(GameplayActionType actionType, int arg1, int arg2)
        {
            HeroState active = gameState.activePlayer;
            switch (actionType)
            {
                case GameplayActionType.Attack:
                    OnAttackPressedLocal();
                    break;
                case GameplayActionType.EndTurn:
                    OnEndTurnPressedLocal();
                    break;
                case GameplayActionType.EquipPrep:
                    OnEquipPrepPressedLocal();
                    break;
                case GameplayActionType.FinishPrep:
                    OnFinishPrepPressedLocal();
                    break;
                case GameplayActionType.Sleep:
                    OnSleepPressedLocal();
                    break;
                case GameplayActionType.DrawExtra:
                    OnDrawExtraPressedLocal();
                    break;
                case GameplayActionType.DeckChoice:
                    OnDeckChoiceLocal((DeckChoice)arg1);
                    break;
                case GameplayActionType.PeekKeep:
                    OnPeekKeepLocal();
                    break;
                case GameplayActionType.PeekDiscard:
                    OnPeekDiscardLocal();
                    break;
                case GameplayActionType.CardClicked:
                    if (arg1 >= 0 && arg1 < active.hand.Count)
                    {
                        OnCardClickedLocal(active, (MagicData)active.hand[arg1]);
                    }
                    break;
                case GameplayActionType.ItemClicked:
                    if (arg1 >= 0 && arg1 < active.itemBook.Count)
                    {
                        OnItemClickedLocal(active, (ItemData)active.itemBook[arg1]);
                    }
                    break;
                case GameplayActionType.EquipmentClicked:
                    if (arg1 >= 0 && arg1 < active.equipmentBook.Count)
                    {
                        OnEquipmentClickedLocal(active, (EquipmentData)active.equipmentBook[arg1]);
                    }
                    break;
                case GameplayActionType.CardRightClicked:
                    if (arg1 == 0)
                    {
                        if (arg2 >= 0 && arg2 < active.hand.Count)
                            OnCardRightClickedLocal(active, active.hand[arg2]);
                    }
                    else if (arg1 == 1)
                    {
                        if (arg2 >= 0 && arg2 < active.equipmentBook.Count)
                            OnCardRightClickedLocal(active, active.equipmentBook[arg2]);
                    }
                    else if (arg1 == 2)
                    {
                        if (arg2 >= 0 && arg2 < active.itemBook.Count)
                            OnCardRightClickedLocal(active, active.itemBook[arg2]);
                    }
                    break;
                case GameplayActionType.EquipSlotRightClicked:
                    bool isP1 = arg2 == 1;
                    OnEquipSlotRightClickedLocal(isP1, (EquipmentSlot)arg1);
                    break;
            }
        }

        // ---------- Character select ----------
        private void ShowCharacterSelect()
        {
            selectedP1 = null;
            selectedP2 = null;
            if (startBattleButton != null) startBattleButton.interactable = false;

            // Populate class-button labels dynamically so F shows the bare (unarmed) damage.
            for (int i = 0; i < 4; i++)
            {
                RefreshClassButtonLabel(p1ClassButtons, i, (HeroClass)i);
                RefreshClassButtonLabel(p2ClassButtons, i, (HeroClass)i);
            }

            if (battleUI != null)
            {
                if (battleUI.characterSelectPanel != null) battleUI.characterSelectPanel.SetActive(true);
                if (battleUI.battleScreen != null) battleUI.battleScreen.SetActive(false);
                if (battleUI.winnerOverlay != null) battleUI.winnerOverlay.SetActive(false);
                if (battleUI.drawChoicePanel != null) battleUI.drawChoicePanel.SetActive(false);
            }

            // --- Multiplayer Mode Custom Selection Setup ---
            if (RelayManager.IsMultiplayer)
            {
                bool amHost = Unity.Netcode.NetworkManager.Singleton.IsServer;
                
                // P1 can only be picked by Host, P2 only by Client
                if (p1ClassButtons != null)
                {
                    for (int i = 0; i < p1ClassButtons.Length; i++)
                    {
                        if (p1ClassButtons[i] != null) p1ClassButtons[i].interactable = amHost;
                    }
                }
                if (p2ClassButtons != null)
                {
                    for (int i = 0; i < p2ClassButtons.Length; i++)
                    {
                        if (p2ClassButtons[i] != null) p2ClassButtons[i].interactable = !amHost;
                    }
                }

                // Only host can see and press startBattleButton
                if (startBattleButton != null)
                {
                    startBattleButton.gameObject.SetActive(amHost);
                    startBattleButton.interactable = false;
                }

                // Dynamically setup instruction text above the start button
                var canvas = GameObject.Find("Canvas");
                if (canvas != null)
                {
                    var selectPanel = canvas.transform.Find("CharacterSelectPanel");
                    if (selectPanel != null)
                    {
                        var instrTextTrans = selectPanel.Find("MultiplayerInstructionText");
                        TextMeshProUGUI instrText = null;
                        if (instrTextTrans == null)
                        {
                            var go = new GameObject("MultiplayerInstructionText");
                            go.transform.SetParent(selectPanel, false);
                            instrText = go.AddComponent<TextMeshProUGUI>();
                            instrText.fontSize = 24f;
                            instrText.color = Color.white;
                            instrText.alignment = TextAlignmentOptions.Center;
                            var rt = go.GetComponent<RectTransform>();
                            rt.anchoredPosition = new Vector2(0f, -220f);
                            rt.sizeDelta = new Vector2(600f, 50f);
                        }
                        else
                        {
                            instrText = instrTextTrans.GetComponent<TextMeshProUGUI>();
                        }
                        instrText.gameObject.SetActive(true);
                        instrText.text = "Scegli il tuo Eroe!";
                    }
                }
            }
            else
            {
                // Local mode restores full interactivity
                if (p1ClassButtons != null)
                {
                    for (int i = 0; i < p1ClassButtons.Length; i++)
                    {
                        if (p1ClassButtons[i] != null) p1ClassButtons[i].interactable = true;
                    }
                }
                if (p2ClassButtons != null)
                {
                    for (int i = 0; i < p2ClassButtons.Length; i++)
                    {
                        if (p2ClassButtons[i] != null) p2ClassButtons[i].interactable = true;
                    }
                }
                if (startBattleButton != null)
                {
                    startBattleButton.gameObject.SetActive(true);
                }

                var instrText = GameObject.Find("Canvas/CharacterSelectPanel/MultiplayerInstructionText");
                if (instrText != null)
                {
                    instrText.SetActive(false);
                }
            }
        }

        // Displays the hero's strength (F) directly.
        private void RefreshClassButtonLabel(Button[] buttons, int idx, HeroClass hc)
        {
            if (buttons == null || idx >= buttons.Length || buttons[idx] == null) return;
            HeroData d = GameManager.CreateHeroData(hc);
            var tmp = buttons[idx].GetComponentInChildren<TMP_Text>();
            if (tmp != null)
                tmp.text = $"{d.heroName}\n{hc}\nHP{d.maxHP}  F{d.strength} M{d.intelligence} S{d.agility}";
        }

        public void SelectClass(int player, int classIdx)
        {
            if (RelayManager.IsMultiplayer)
            {
                RelayManager.Instance.SendHeroSelection(player, classIdx);
                return;
            }
            SelectClassLocal(player, classIdx);
        }

        public void SelectClassLocal(int player, int classIdx)
        {
            HeroClass chosen = (HeroClass)classIdx;
            if (player == 1)
            {
                if (selectedP2.HasValue && selectedP2.Value == chosen)
                {
                    battleUI.AddLog($"Non puoi scegliere lo stesso eroe dell'altro giocatore ({chosen})!");
                    return;
                }
                selectedP1 = chosen;
            }
            else
            {
                if (selectedP1.HasValue && selectedP1.Value == chosen)
                {
                    battleUI.AddLog($"Non puoi scegliere lo stesso eroe dell'altro giocatore ({chosen})!");
                    return;
                }
                selectedP2 = chosen;
            }
            if (battleUI != null) battleUI.UpdateSelectionHighlight(player, classIdx, selectedP1, selectedP2);
            
            bool choicesValid = selectedP1.HasValue && selectedP2.HasValue && (selectedP1.Value != selectedP2.Value);
            if (startBattleButton != null) startBattleButton.interactable = choicesValid;

            // Update Multiplayer Instruction Text
            if (RelayManager.IsMultiplayer)
            {
                var instrText = GameObject.Find("Canvas/CharacterSelectPanel/MultiplayerInstructionText")?.GetComponent<TextMeshProUGUI>();
                if (instrText != null)
                {
                    bool hostSelected = selectedP1.HasValue;
                    bool clientSelected = selectedP2.HasValue;
                    bool amHost = Unity.Netcode.NetworkManager.Singleton.IsServer;

                    if (hostSelected && clientSelected)
                    {
                        if (amHost)
                        {
                            instrText.text = "Entrambi hanno scelto! Avvia la battaglia!";
                        }
                        else
                        {
                            instrText.text = "Entrambi hanno scelto! In attesa che l'Host avvii...";
                        }
                    }
                    else if ((amHost && hostSelected) || (!amHost && clientSelected))
                    {
                        instrText.text = "In attesa dell'altro giocatore...";
                    }
                    else
                    {
                        instrText.text = "Scegli il tuo Eroe!";
                    }
                }
            }
        }

        public void OnStartBattlePressed()
        {
            if (RelayManager.IsMultiplayer)
            {
                int seed = UnityEngine.Random.Range(1, 100000);
                RelayManager.Instance.SendStartBattle(seed);
                return;
            }
            OnStartBattlePressedLocal();
        }

        public void OnStartBattlePressedLocal()
        {
            if (!selectedP1.HasValue || !selectedP2.HasValue) return;

            gameManager.InitGame(selectedP1.Value, selectedP2.Value);
            gameState = gameManager.gameState;
            turnManager = new TurnManager(gameState);

            if (battleUI != null)
            {
                if (battleUI.characterSelectPanel != null) battleUI.characterSelectPanel.SetActive(false);
                if (battleUI.battleScreen != null) battleUI.battleScreen.SetActive(true);
                if (battleUI.winnerOverlay != null) battleUI.winnerOverlay.SetActive(false);
                battleUI.ClearLog();
            }

            // Hide the optional draw prep button as the 2 draws are now obligatory
            if (drawPrepButton != null) drawPrepButton.gameObject.SetActive(false);

            EnsureTurnAndTimerUI();
            SetAllButtonsInteractable(IsMyTurn());
            LogAction($"Battaglia iniziata! {GetStyledName(gameState.player1)} vs {GetStyledName(gameState.player2)}.");

            // Draw starting hands (Warrior: 3E/1I, Rogue: 2E/2I, Mage/Necro: 1E/2S/1I)
            DrawStartingHand(gameState.player1);
            DrawStartingHand(gameState.player2);

            OnPhaseEntered();
        }

        // ---------- Phase flow ----------
        private void OnPhaseEntered()
        {
            if (gameState == null || battleUI == null) return;

            HeroState active = gameState.activePlayer;
            HeroState opponent = (active == gameState.player1) ? gameState.player2 : gameState.player1;

            // A new turn always begins at the ResourceRecovery phase — announce it briefly.
            if (gameState.phase == GamePhase.ResourceRecovery)
            {
                ShowTurnIndicator();
            }

            if (gameState.phase == GamePhase.ResourceRecovery)
            {
                turnManager.RunResourceRecoveryPhase(active, opponent);
                if (CheckForWinner()) return;
                gameState.AdvancePhase(); // -> Preparation
            }

            battleUI.turnText.text = $"Turno {gameState.currentTurn} — tocca a {GetStyledName(active)}";
            battleUI.phaseText.text = $"Fase: {gameState.phase}";

            if (battleUI.prepButtonsContainer != null)
            {
                battleUI.prepButtonsContainer.SetActive(gameState.phase == GamePhase.Preparation);
                if (drawPrepButton != null) drawPrepButton.gameObject.SetActive(false);
            }
            if (battleUI.combatButtonsContainer != null) battleUI.combatButtonsContainer.SetActive(gameState.phase == GamePhase.Combat);
            if (battleUI.endButtonsContainer != null) battleUI.endButtonsContainer.SetActive(gameState.phase == GamePhase.EndPhase);

            // Equipment slots are hidden by default at the start of every phase.
            battleUI.SetEquipmentSlotsVisible(true, false);
            battleUI.SetEquipmentSlotsVisible(false, false);

            if (gameState.phase == GamePhase.Preparation)
            {
                prepDrawsThisPhase = 0;
                equipsThisPhase = 0;
                if (drawPrepButton != null) drawPrepButton.interactable = true;
                LogAction($"=== Fase di Preparazione di {GetStyledName(active)} ===");

                // Process poison damage tick during the preparation phase
                int poisonDamage = turnManager.ProcessPoisonTick(active);
                if (poisonDamage > 0)
                {
                    LogAction($"{GetStyledName(active)} subisce {poisonDamage} danni da Veleno all'inizio della Preparazione.");
                    if (CheckForWinner()) return;
                }

                // Automatically trigger the 2 obligatory prep phase draws
                TriggerPrepDraw(active, 1);
            }
            else if (gameState.phase == GamePhase.Combat)
            {
                LogAction($"=== Fase di Combattimento di {GetStyledName(active)} ===");
            }
            else if (gameState.phase == GamePhase.EndPhase)
            {
                LogAction($"=== Fase Finale di {GetStyledName(active)} ===");
            }

            // --- Reset and start timer ---
            if (gameState.phase == GamePhase.Preparation)
            {
                phaseTimer = 30f;
                timerActive = true;
            }
            else if (gameState.phase == GamePhase.Combat)
            {
                phaseTimer = 60f;
                timerActive = true;
            }
            else if (gameState.phase == GamePhase.EndPhase)
            {
                phaseTimer = 20f;
                timerActive = true;
            }
            else
            {
                timerActive = false;
                UpdateTimerText(0);
            }

            SetAllButtonsInteractable(IsMyTurn());
            RefreshAll();
        }

        private void Update()
        {
            if (gameState != null && battleUI != null && battleUI.battleScreen.activeSelf)
            {
                if (RelayManager.IsMultiplayer)
                {
                    if (Unity.Netcode.NetworkManager.Singleton.IsServer && timerActive)
                    {
                        phaseTimer -= Time.deltaTime;
                        int secondsLeft = Mathf.CeilToInt(phaseTimer);
                        if (secondsLeft != lastSentSeconds)
                        {
                            lastSentSeconds = secondsLeft;
                            RelayManager.Instance.SendTimerUpdate(secondsLeft);
                        }

                        if (phaseTimer <= 0)
                        {
                            timerActive = false;
                            OnTimerExpired();
                        }
                    }
                }
                else
                {
                    // Local timer
                    if (timerActive)
                    {
                        phaseTimer -= Time.deltaTime;
                        int secondsLeft = Mathf.CeilToInt(phaseTimer);
                        UpdateTimerText(secondsLeft);

                        if (phaseTimer <= 0)
                        {
                            timerActive = false;
                            OnTimerExpired();
                        }
                    }
                }
            }
        }

        private void OnTimerExpired()
        {
            if (RelayManager.IsMultiplayer)
            {
                if (Unity.Netcode.NetworkManager.Singleton.IsServer)
                {
                    if (gameState.phase == GamePhase.Preparation)
                    {
                        ExecuteAction(GameplayActionType.FinishPrep);
                    }
                    else if (gameState.phase == GamePhase.Combat)
                    {
                        ExecuteAction(GameplayActionType.EndTurn);
                    }
                    else if (gameState.phase == GamePhase.EndPhase)
                    {
                        ExecuteAction(GameplayActionType.Sleep);
                    }
                }
            }
            else
            {
                if (gameState.phase == GamePhase.Preparation)
                {
                    OnFinishPrepPressedLocal();
                }
                else if (gameState.phase == GamePhase.Combat)
                {
                    OnEndTurnPressedLocal();
                }
                else if (gameState.phase == GamePhase.EndPhase)
                {
                    OnSleepPressedLocal();
                }
            }
        }

        private void TriggerPrepDraw(HeroState hero, int drawNumber)
        {
            pendingDrawHero = hero;
            pendingDrawCount = 1;
            pendingPeek = false;
            pendingAfterDraws = () =>
            {
                prepDrawsThisPhase++;
                if (prepDrawsThisPhase < 2)
                {
                    TriggerPrepDraw(hero, 2);
                }
            };
            string label = (drawNumber == 1) ? "1° Pescaggio Obbligatorio" : "2° Pescaggio Obbligatorio";
            // Only the player actually drawing sees the draw popup (not the opponent).
            if (IsMyTurn()) battleUI.ShowDrawChoice(label);
        }

        // ---------- Preparation ----------
        public void OnDrawPrepPressed()
        {
            // Legacy button no longer active
        }

        public void OnEquipPrepPressed()
        {
            if (!CanInteract()) return;
            ExecuteAction(GameplayActionType.EquipPrep);
        }

        public void OnEquipPrepPressedLocal()
        {
            if (gameState == null || gameState.phase != GamePhase.Preparation) return;
            bool isP1 = gameState.activePlayer == gameState.player1;

            // Entering equip mode: force the equipment book AND show the equipment slots.
            battleUI.SetSelectedBook(isP1, BookType.Equipment);
            battleUI.SetEquipmentSlotsVisible(isP1, true);
            LogAction($"{GetStyledName(gameState.activePlayer)}: modalità equipaggiamento attiva. Clicca un pezzo nel Libro Equipaggiamento (max 2 per turno).");
            RefreshAll();
        }

        // Left-clicking an equipment card in the Equipment book during Preparation equips it.
        public void OnEquipmentClicked(HeroState owner, EquipmentData equip)
        {
            if (!CanInteractWithOwner(owner)) return;
            int idx = owner.equipmentBook.IndexOf(equip);
            ExecuteAction(GameplayActionType.EquipmentClicked, idx);
        }

        public void OnEquipmentClickedLocal(HeroState owner, EquipmentData equip)
        {
            if (gameState == null || gameState.activePlayer != owner)
            {
                battleUI.AddLog("Non è il tuo turno!");
                return;
            }
            if (gameState.phase != GamePhase.Preparation)
            {
                battleUI.AddLog("Puoi equipaggiare solo in Preparazione (in combattimento l'equipaggiamento si può solo ispezionare).");
                return;
            }
            if (equipsThisPhase >= 2)
            {
                battleUI.AddLog($"{owner.data.heroName} ha già equipaggiato 2 pezzi questo turno.");
                return;
            }

            // Both conditions required: the equipment book must be selected AND the slots shown.
            bool isP1 = owner == gameState.player1;
            BookType selBook = isP1 ? battleUI.p1SelectedBook : battleUI.p2SelectedBook;
            if (selBook != BookType.Equipment || !battleUI.IsEquipVisible(isP1))
            {
                battleUI.AddLog("Premi il pulsante Equipaggia per attivare la modalità (serve il Libro Equipaggiamento e gli slot mostrati).");
                return;
            }
            if (!owner.equipmentBook.Contains(equip)) return;

            if (!EquipmentSystem.MeetsRequirements(owner, equip, out string reqMsg))
            {
                battleUI.AddLog(reqMsg);
                return;
            }

            HeroState opponent = (owner == gameState.player1) ? gameState.player2 : gameState.player1;
            owner.equipmentBook.Remove(equip);
            var displaced = EquipmentSystem.Equip(owner, equip);
            LogAction($"{GetStyledName(owner)} equipaggia {equip.cardName} ({equip.slotType}).");
            foreach (var d in displaced)
            {
                DiscardEquipment(owner, opponent, d);
                LogAction($"{GetStyledName(owner)} sostituisce e scarta {d.cardName}.");
            }
            equipsThisPhase++;
            RefreshAll();
        }

        // Handles OnDiscard equipment effects then moves the piece to the discard pile.
        private void DiscardEquipment(HeroState owner, HeroState opponent, EquipmentData equip)
        {
            if (equip.specialEffect == EquipEffect.OnDiscardGainMana)
            {
                owner.currentMana = Mathf.Min(owner.GetModifiedIntelligence(), owner.currentMana + equip.effectValue);
                LogAction($"{equip.cardName}: {GetStyledName(owner)} guadagna {equip.effectValue} Mana.");
            }
            else if (equip.specialEffect == EquipEffect.OnDiscardDamage)
            {
                int dealt = CombatActions.DealDamage(owner, opponent, equip.effectValue, false, false);
                LogAction($"{equip.cardName}: infligge {dealt} danni a {GetStyledName(opponent)}.");
            }
            owner.discardPile.Add(equip);
        }

        public void OnFinishPrepPressed()
        {
            if (!CanInteract()) return;
            ExecuteAction(GameplayActionType.FinishPrep);
        }

        public void OnFinishPrepPressedLocal()
        {
            if (gameState == null || gameState.phase != GamePhase.Preparation) return;

            // Automatically complete any remaining draws
            int remainingDraws = 2 - prepDrawsThisPhase;
            if (remainingDraws > 0)
            {
                for (int i = 0; i < remainingDraws; i++)
                {
                    DrawOneFromDeck(gameState.activePlayer, DeckChoice.Spell);
                }
                prepDrawsThisPhase = 2;
                if (battleUI.drawChoicePanel != null) battleUI.drawChoicePanel.SetActive(false);
            }

            gameState.AdvancePhase(); // -> Combat
            OnPhaseEntered();
        }

        // ---------- Combat ----------
        public void OnAttackPressed()
        {
            if (!CanInteract()) return;
            ExecuteAction(GameplayActionType.Attack);
        }

        public void OnAttackPressedLocal()
        {
            if (gameState == null || gameState.phase != GamePhase.Combat) return;

            HeroState attacker = gameState.activePlayer;
            HeroState defender = (attacker == gameState.player1) ? gameState.player2 : gameState.player1;

            if (CombatActions.TryWeaponAttack(attacker, defender, 2))
            {
                RefreshAll();
                CheckForWinner();
            }
        }

        public void OnEndTurnPressed()
        {
            if (!CanInteract()) return;
            ExecuteAction(GameplayActionType.EndTurn);
        }

        public void OnEndTurnPressedLocal()
        {
            if (gameState == null || gameState.phase != GamePhase.Combat) return;
            gameState.AdvancePhase(); // -> EndPhase
            OnPhaseEntered();
        }

        // ---------- End phase ----------
        public void OnSleepPressed()
        {
            if (!CanInteract()) return;
            ExecuteAction(GameplayActionType.Sleep);
        }

        public void OnSleepPressedLocal()
        {
            if (gameState == null || gameState.phase != GamePhase.EndPhase) return;
            turnManager.RunEndPhase(gameState.activePlayer, EndPhaseChoice.Rest);
            EndRoundForActive();
        }

        public void OnDrawExtraPressed()
        {
            if (!CanInteract()) return;
            ExecuteAction(GameplayActionType.DrawExtra);
        }

        public void OnDrawExtraPressedLocal()
        {
            if (gameState == null || gameState.phase != GamePhase.EndPhase) return;
            // Draw 1 from a chosen deck, then end the round.
            HeroState active = gameState.activePlayer;
            RequestDeckDraw(active, 1, () =>
            {
                turnManager.RunEndPhase(active, EndPhaseChoice.DrawExtraCard);
                EndRoundForActive();
            });
        }

        // Repeatable cards are NO LONGER auto-discarded — they persist until the player
        // discards them by right-clicking. Round end just advances to the next hero.
        private void EndRoundForActive()
        {
            gameState.AdvancePhase(); // wraps to next hero ResourceRecovery
            OnPhaseEntered();
        }

        // ---------- Spell card usage (left click) ----------
        public void OnCardClicked(HeroState owner, MagicData spell)
        {
            if (!CanInteractWithOwner(owner)) return;
            int idx = owner.hand.IndexOf(spell);
            ExecuteAction(GameplayActionType.CardClicked, idx);
        }

        public void OnCardClickedLocal(HeroState owner, MagicData spell)
        {
            if (gameState == null || gameState.activePlayer != owner)
            {
                battleUI.AddLog("Non è il tuo turno!");
                return;
            }
            if (gameState.phase != GamePhase.Combat)
            {
                battleUI.AddLog("Puoi usare le carte solo durante la fase di Combattimento!");
                return;
            }
            if (spell.magicType == MagicType.Aura && owner.activeAuras.Contains(spell))
            {
                battleUI.AddLog($"{spell.cardName} è un'aura già attiva.");
                return;
            }
            if (spell.magicType == MagicType.Exhaustion && owner.exhaustedThisRound.Contains(spell))
            {
                battleUI.AddLog($"{spell.cardName} è già stata usata questo round.");
                return;
            }

            HeroState opponent = (owner == gameState.player1) ? gameState.player2 : gameState.player1;

            CastResult result = SpellActions.TryCastSpell(owner, opponent, spell);
            if (!result.success) return;

            owner.cardTypesUsedThisTurn.Add("Spell");
            ResolveDeferred(owner, opponent, result);

            // Per-magic-type bookkeeping.
            switch (spell.magicType)
            {
                case MagicType.Instant:
                    owner.hand.Remove(spell);
                    owner.discardPile.Add(spell);
                    break;
                case MagicType.Aura:
                    if (!owner.activeAuras.Contains(spell)) owner.activeAuras.Add(spell);
                    break;
                case MagicType.Exhaustion:
                    if (!owner.exhaustedThisRound.Contains(spell)) owner.exhaustedThisRound.Add(spell);
                    break;
                case MagicType.Repeatable:
                    // stays in the spellbook; only discarded by player choice (right-click)
                    break;
            }

            RefreshAll();
            CheckForWinner();
        }

        // ---------- Item card usage (left click) ----------
        public void OnItemClicked(HeroState owner, ItemData item)
        {
            if (!CanInteractWithOwner(owner)) return;
            int idx = owner.itemBook.IndexOf(item);
            ExecuteAction(GameplayActionType.ItemClicked, idx);
        }

        public void OnItemClickedLocal(HeroState owner, ItemData item)
        {
            if (gameState == null || gameState.activePlayer != owner)
            {
                battleUI.AddLog("Non è il tuo turno!");
                return;
            }
            if (gameState.phase != GamePhase.Combat)
            {
                battleUI.AddLog("Puoi usare gli oggetti solo durante la fase di Combattimento!");
                return;
            }

            HeroState opponent = (owner == gameState.player1) ? gameState.player2 : gameState.player1;

            CastResult result = ItemActions.TryUseItem(owner, opponent, item);
            if (!result.success) return;

            owner.cardTypesUsedThisTurn.Add("Item");

            // Items are always discarded into the shared item discard pile after use.
            owner.itemBook.Remove(item);
            gameState.itemDiscard.Add(item);

            ResolveDeferred(owner, opponent, result);

            RefreshAll();
            CheckForWinner();
        }

        // ---------- Right click: discard a card from the active hero's books ----------
        public void OnCardRightClicked(HeroState owner, CardData card)
        {
            if (!CanInteractWithOwner(owner)) return;
            int bookType = -1;
            int idx = -1;
            if (card is MagicData m && owner.hand.Contains(m))
            {
                bookType = 0;
                idx = owner.hand.IndexOf(m);
            }
            else if (card is EquipmentData eq && owner.equipmentBook.Contains(eq))
            {
                bookType = 1;
                idx = owner.equipmentBook.IndexOf(eq);
            }
            else if (card is ItemData item && owner.itemBook.Contains(item))
            {
                bookType = 2;
                idx = owner.itemBook.IndexOf(item);
            }
            if (bookType != -1 && idx != -1)
            {
                ExecuteAction(GameplayActionType.CardRightClicked, bookType, idx);
            }
        }

        public void OnCardRightClickedLocal(HeroState owner, CardData card)
        {
            if (gameState == null || gameState.activePlayer != owner)
            {
                battleUI.AddLog("Puoi scartare le carte solo durante il tuo turno!");
                return;
            }

            if (card is ItemData item && owner.itemBook.Contains(item))
            {
                owner.itemBook.Remove(item);
                gameState.itemDiscard.Add(item);
                LogAction($"{GetStyledName(owner)} scarta l'oggetto {item.cardName}.");
            }
            else if (card is MagicData spell && owner.hand.Contains(spell))
            {
                owner.hand.Remove(spell);
                owner.discardPile.Add(spell);
                owner.activeAuras.Remove(spell);
                LogAction($"{GetStyledName(owner)} scarta {spell.cardName} dal libro incantesimi.");
            }
            else if (card is EquipmentData eq && owner.equipmentBook.Contains(eq))
            {
                HeroState opp = (owner == gameState.player1) ? gameState.player2 : gameState.player1;
                owner.equipmentBook.Remove(eq);
                DiscardEquipment(owner, opp, eq);
                LogAction($"{GetStyledName(owner)} scarta l'equipaggiamento {eq.cardName}.");
            }
            else
            {
                return;
            }
            RefreshAll();
        }

        // ---------- Deferred effect resolution ----------
        private void ResolveDeferred(HeroState owner, HeroState opponent, CastResult result)
        {
            for (int i = 0; i < result.drawFromDiscardCount; i++) DrawFromOwnDiscard(owner);
            for (int i = 0; i < result.stealOpponentDiscardCount; i++) StealFromOpponentDiscard(owner, opponent);

            int deckDraws = result.drawSpellCount + result.drawChosenDeckCount;
            if (result.peekChosenDeckCount > 0)
            {
                RequestPeek(owner);
            }
            else if (deckDraws > 0)
            {
                RequestDeckDraw(owner, deckDraws, null);
            }
        }

        // ---------- Deck choice popup flow ----------
        private void RequestDeckDraw(HeroState hero, int count, System.Action after)
        {
            pendingDrawHero = hero;
            pendingDrawCount = count;
            pendingAfterDraws = after;
            pendingPeek = false;
            // Only the drawer sees the popup; the opponent's game state still syncs.
            if (battleUI.drawChoicePanel != null && IsMyTurn()) battleUI.drawChoicePanel.SetActive(true);
        }

        private void RequestPeek(HeroState hero)
        {
            pendingDrawHero = hero;
            pendingPeek = true;
            if (battleUI.drawChoicePanel != null && IsMyTurn()) battleUI.drawChoicePanel.SetActive(true);
        }

        public void OnDeckChoice(DeckChoice deck)
        {
            if (!CanInteract()) return;
            ExecuteAction(GameplayActionType.DeckChoice, (int)deck);
        }

        public void OnDeckChoiceLocal(DeckChoice deck)
        {
            if (pendingDrawHero == null) { if (battleUI.drawChoicePanel != null) battleUI.drawChoicePanel.SetActive(false); return; }

            if (battleUI.drawChoicePanel != null) battleUI.drawChoicePanel.SetActive(false);

            if (pendingPeek)
            {
                pendingPeek = false;
                DoPeek(pendingDrawHero, deck);
                RefreshAll();
                return;
            }

            DrawOneFromDeck(pendingDrawHero, deck);
            pendingDrawCount--;
            RefreshAll();

            if (pendingDrawCount > 0)
            {
                if (battleUI.drawChoicePanel != null && IsMyTurn()) battleUI.drawChoicePanel.SetActive(true);
            }
            else
            {
                System.Action after = pendingAfterDraws;
                pendingAfterDraws = null;
                pendingDrawHero = null;
                if (after != null) after.Invoke();
            }
        }

        public string GetStyledName(HeroState hero)
        {
            if (hero == null) return "";
            return Botte.UI.BattleUI.GetClassColorizedName(hero.data.heroClass, hero.data.heroName);
        }

        public void LogAction(string message)
        {
            if (battleUI != null)
            {
                battleUI.AddLog(""); // empty line separator between different turn actions
                battleUI.AddLog(message);
            }
        }

        public void OnEquipSlotRightClicked(bool isPlayer1, EquipmentSlot slot)
        {
            HeroState owner = isPlayer1 ? gameState.player1 : gameState.player2;
            if (!CanInteractWithOwner(owner)) return;
            ExecuteAction(GameplayActionType.EquipSlotRightClicked, (int)slot, isPlayer1 ? 1 : 2);
        }

        public void OnEquipSlotRightClickedLocal(bool isPlayer1, EquipmentSlot slot)
        {
            if (gameState == null) return;
            HeroState owner = isPlayer1 ? gameState.player1 : gameState.player2;
            if (gameState.activePlayer != owner)
            {
                battleUI.AddLog("Puoi disequipaggiare/scartare equipaggiamento solo durante il tuo turno!");
                return;
            }

            EquipmentData equip = owner.equippedItems[(int)slot];
            if (equip != null)
            {
                owner.equippedItems[(int)slot] = null;
                if (equip.slotType == EquipSlotType.TwoHandWeapon)
                {
                    owner.weaponTwoHandedEquipped = false;
                }
                owner.durability.Remove(slot);
                EquipmentSystem.RemoveAttributes(owner, equip);

                HeroState opponent = (owner == gameState.player1) ? gameState.player2 : gameState.player1;

                // Sacrifice ability triggered on right click
                if (equip.specialEffect == EquipEffect.SacrificeBlockThenBreak)
                {
                    owner.hasShield = true;
                    LogAction($"{GetStyledName(owner)} sacrifica {equip.cardName} e ottiene un BLOCCO completo per il prossimo attacco!");
                }

                DiscardEquipment(owner, opponent, equip);
                LogAction($"{GetStyledName(owner)} disequipaggia e scarta {equip.cardName} dallo slot attivo {slot}.");
                RefreshAll();
            }
        }

        private void DrawStartingHand(HeroState hero)
        {
            HeroClass hc = hero.data.heroClass;
            int numEquip = 0;
            int numSpells = 0;
            int numItems = 0;

            switch (hc)
            {
                case HeroClass.Warrior:
                    numEquip = 3; numItems = 1; break;
                case HeroClass.Rogue:
                    numEquip = 2; numItems = 2; break;
                case HeroClass.Mage:
                    numEquip = 1; numSpells = 2; numItems = 1; break;
                case HeroClass.Necro:
                    numEquip = 1; numSpells = 2; numItems = 1; break;
            }

            for (int i = 0; i < numEquip; i++) DrawStartingCard(hero, DeckChoice.Equipment);
            for (int i = 0; i < numSpells; i++) DrawStartingCard(hero, DeckChoice.Spell);
            for (int i = 0; i < numItems; i++) DrawStartingCard(hero, DeckChoice.Item);
        }

        private void DrawStartingCard(HeroState hero, DeckChoice deck)
        {
            if (deck == DeckChoice.Item)
            {
                if (gameState.itemDeck.Count > 0)
                {
                    int idx = Random.Range(0, gameState.itemDeck.Count);
                    CardData card = gameState.itemDeck[idx];
                    gameState.itemDeck.RemoveAt(idx);
                    if (hero.itemBook.Count < HeroState.MAX_BOOK_SIZE)
                    {
                        hero.itemBook.Add(card);
                    }
                    else
                    {
                        gameState.itemDiscard.Add(card);
                    }
                }
            }
            else if (deck == DeckChoice.Equipment)
            {
                if (hero.equipmentDeck.Count > 0)
                {
                    int idx = Random.Range(0, hero.equipmentDeck.Count);
                    CardData card = hero.equipmentDeck[idx];
                    hero.equipmentDeck.RemoveAt(idx);
                    if (hero.equipmentBook.Count < HeroState.MAX_BOOK_SIZE)
                    {
                        hero.equipmentBook.Add(card);
                    }
                    else
                    {
                        hero.discardPile.Add(card);
                    }
                }
            }
            else if (deck == DeckChoice.Spell)
            {
                if (hero.magicDeck.Count > 0)
                {
                    int idx = Random.Range(0, hero.magicDeck.Count);
                    CardData card = hero.magicDeck[idx];
                    hero.magicDeck.RemoveAt(idx);
                    if (hero.hand.Count < HeroState.MAX_BOOK_SIZE)
                    {
                        hero.hand.Add(card);
                    }
                    else
                    {
                        hero.discardPile.Add(card);
                    }
                }
            }
        }

        private void ApplyFatigue(HeroState hero)
        {
            hero.fatigueCount++;
            hero.currentHP = Mathf.Max(0, hero.currentHP - hero.fatigueCount);
            LogAction($"{GetStyledName(hero)} tenta di pescare da un mazzo vuoto e subisce <color=red>{hero.fatigueCount}</color> danni da Affaticamento! (HP rimanenti: {hero.currentHP})");
            RefreshAll();
            CheckForWinner();
        }

        // ---------- Concrete draws ----------
        private void DrawOneFromDeck(HeroState hero, DeckChoice deck)
        {
            string prepPrefix = "";
            if (gameState != null && gameState.phase == GamePhase.Preparation)
            {
                prepPrefix = (prepDrawsThisPhase == 0) ? "[1° pescaggio di preparazione] " : "[2° pescaggio di preparazione] ";
            }

            if (deck == DeckChoice.Item)
            {
                if (gameState.itemDeck.Count == 0)
                {
                    ApplyFatigue(hero);
                    return;
                }
                int idx = Random.Range(0, gameState.itemDeck.Count);
                CardData card = gameState.itemDeck[idx];
                gameState.itemDeck.RemoveAt(idx);

                if (hero.itemBook.Count >= HeroState.MAX_BOOK_SIZE)
                {
                    gameState.itemDiscard.Add(card);
                    LogAction($"{prepPrefix}{GetStyledName(hero)} pesca l'oggetto {card.cardName}, ma il suo Libro Oggetti è pieno ({HeroState.MAX_BOOK_SIZE}). Viene scartato immediatamente!");
                }
                else
                {
                    hero.itemBook.Add(card);
                    LogAction($"{prepPrefix}{GetStyledName(hero)} pesca l'oggetto {card.cardName}.");
                }
                return;
            }

            if (deck == DeckChoice.Equipment)
            {
                if (hero.equipmentDeck.Count == 0)
                {
                    ApplyFatigue(hero);
                    return;
                }
                int eIdx = Random.Range(0, hero.equipmentDeck.Count);
                CardData card = hero.equipmentDeck[eIdx];
                hero.equipmentDeck.RemoveAt(eIdx);

                if (hero.equipmentBook.Count >= HeroState.MAX_BOOK_SIZE)
                {
                    hero.discardPile.Add(card);
                    LogAction($"{prepPrefix}{GetStyledName(hero)} pesca l'equipaggiamento {card.cardName}, ma il suo Libro Equipaggiamento è pieno ({HeroState.MAX_BOOK_SIZE}). Viene scartato immediatamente!");
                }
                else
                {
                    hero.equipmentBook.Add(card);
                    LogAction($"{prepPrefix}{GetStyledName(hero)} pesca l'equipaggiamento {card.cardName}.");
                }
                return;
            }

            if (deck == DeckChoice.Spell)
            {
                if (hero.magicDeck.Count == 0)
                {
                    ApplyFatigue(hero);
                    return;
                }
                int sIdx = Random.Range(0, hero.magicDeck.Count);
                CardData spellCard = hero.magicDeck[sIdx];
                hero.magicDeck.RemoveAt(sIdx);

                bool isInstant = spellCard is MagicData m && m.magicType == MagicType.Instant;
                if (hero.hand.Count >= HeroState.MAX_BOOK_SIZE || (!isInstant && hero.IsSpellbookFull()))
                {
                    hero.discardPile.Add(spellCard);
                    LogAction($"{prepPrefix}{GetStyledName(hero)} pesca {spellCard.cardName}, ma il suo Libro Incantesimi è pieno ({HeroState.MAX_BOOK_SIZE}). Viene scartata immediatamente!");
                }
                else
                {
                    hero.hand.Add(spellCard);
                    LogAction($"{prepPrefix}{GetStyledName(hero)} pesca {spellCard.cardName}.");
                }
                return;
            }
        }

        private void DrawFromOwnDiscard(HeroState hero)
        {
            if (hero.discardPile.Count == 0) { battleUI.AddLog($"{hero.data.heroName} non ha carte negli scarti."); return; }
            int idx = Random.Range(0, hero.discardPile.Count);
            CardData card = hero.discardPile[idx];
            if (hero.hand.Count >= HeroState.MAX_BOOK_SIZE || (card is MagicData m && m.magicType != MagicType.Instant && hero.IsSpellbookFull()))
            {
                LogAction($"Libro incantesimi pieno: impossibile recuperare {card.cardName} dagli scarti.");
                return;
            }
            hero.discardPile.RemoveAt(idx);
            hero.hand.Add(card);
            LogAction($"{GetStyledName(hero)} recupera {card.cardName} dagli scarti.");
        }

        // Saccheggio: grab a card from opponent discard into own spellbook (ignores size limit).
        private void StealFromOpponentDiscard(HeroState owner, HeroState opponent)
        {
            if (opponent.discardPile.Count == 0) { battleUI.AddLog($"{opponent.data.heroName} non ha carte negli scarti da rubare."); return; }
            int idx = Random.Range(0, opponent.discardPile.Count);
            CardData card = opponent.discardPile[idx];
            opponent.discardPile.RemoveAt(idx);
            
            owner.hand.Add(card);
            LogAction($"{GetStyledName(owner)} ruba {card.cardName} dagli scarti di {GetStyledName(opponent)} (limite ignorato).");
        }

        // Manipolazione: reveal top card of a chosen deck, then keep or discard it.
        private void DoPeek(HeroState hero, DeckChoice deck)
        {
            CardData top = null;
            if (deck == DeckChoice.Item)
            {
                if (gameState.itemDeck.Count == 0) { battleUI.AddLog("Il mazzo oggetti è vuoto."); return; }
                top = gameState.itemDeck[0];
            }
            else if (deck == DeckChoice.Equipment)
            {
                if (hero.equipmentDeck.Count == 0) { battleUI.AddLog("Il mazzo equipaggiamento è vuoto."); return; }
                top = hero.equipmentDeck[0];
            }
            else
            {
                if (hero.magicDeck.Count == 0) { battleUI.AddLog($"Il mazzo incantesimi di {hero.data.heroName} è vuoto."); return; }
                top = hero.magicDeck[0];
            }
            pendingPeekDeck = deck;
            pendingPeekCard = top;
            if (IsMyTurn()) battleUI.ShowPeek(top.cardName);
        }

        public void OnPeekKeep()
        {
            if (!CanInteract()) return;
            ExecuteAction(GameplayActionType.PeekKeep);
        }

        public void OnPeekKeepLocal()
        {
            battleUI.HidePeek();
            if (pendingPeekCard == null) return;
            HeroState hero = pendingDrawHero;
            if (pendingPeekDeck == DeckChoice.Item)
            {
                gameState.itemDeck.Remove(pendingPeekCard);
                hero.itemBook.Add(pendingPeekCard);
                battleUI.AddLog($"{hero.data.heroName} tiene l'oggetto {pendingPeekCard.cardName}.");
            }
            else
            {
                bool isInstant = pendingPeekCard is MagicData m && m.magicType == MagicType.Instant;
                if (!isInstant && hero.IsSpellbookFull())
                {
                    battleUI.AddLog($"Libro pieno: {pendingPeekCard.cardName} viene scartata invece di essere tenuta.");
                    hero.magicDeck.Remove(pendingPeekCard);
                    hero.discardPile.Add(pendingPeekCard);
                }
                else
                {
                    hero.magicDeck.Remove(pendingPeekCard);
                    hero.hand.Add(pendingPeekCard);
                    battleUI.AddLog($"{hero.data.heroName} tiene {pendingPeekCard.cardName}.");
                }
            }
            pendingPeekCard = null;
            pendingDrawHero = null;
            RefreshAll();
        }

        public void OnPeekDiscard()
        {
            if (!CanInteract()) return;
            ExecuteAction(GameplayActionType.PeekDiscard);
        }

        public void OnPeekDiscardLocal()
        {
            battleUI.HidePeek();
            if (pendingPeekCard == null) return;
            HeroState hero = pendingDrawHero;
            if (pendingPeekDeck == DeckChoice.Item)
            {
                gameState.itemDeck.Remove(pendingPeekCard);
                gameState.itemDiscard.Add(pendingPeekCard);
            }
            else
            {
                hero.magicDeck.Remove(pendingPeekCard);
                hero.discardPile.Add(pendingPeekCard);
            }
            battleUI.AddLog($"{hero.data.heroName} scarta {pendingPeekCard.cardName} dalla cima del mazzo.");
            pendingPeekCard = null;
            pendingDrawHero = null;
            RefreshAll();
        }

        // ---------- Win / restart / exit ----------
        private bool CheckForWinner()
        {
            if (gameState == null) return false;
            if (gameState.player1.currentHP <= 0) { EndBattle(gameState.player2); return true; }
            if (gameState.player2.currentHP <= 0) { EndBattle(gameState.player1); return true; }
            return false;
        }

        private void EndBattle(HeroState winner)
        {
            SetAllButtonsInteractable(false);
            if (RelayManager.IsMultiplayer)
            {
                bool hostWon = winner == gameState.player1;
                string endMsg = hostWon ? "Game Finished - Host Won" : "Game Finished - Client Won";
                RelayManager.Instance.EndMultiplayerGame(endMsg);
                return;
            }

            if (battleUI != null)
            {
                battleUI.winnerOverlay.SetActive(true);
                battleUI.winnerText.text = $"{winner.data.heroName} vince!";
                LogAction($"{GetStyledName(winner)} vince! Partita conclusa.");
            }
        }

        public void OnRestartPressed()
        {
            gameState = null;
            turnManager = null;
            if (battleUI != null)
            {
                battleUI.ClearHands();
                if (battleUI.winnerOverlay != null) battleUI.winnerOverlay.SetActive(false);
            }
            ShowCharacterSelect();
        }

        public void OnExitPressed()
        {
            Debug.Log("Uscita dal gioco.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------- Helpers ----------
        private void RefreshAll()
        {
            battleUI.RefreshHero(gameState.player1, true);
            battleUI.RefreshHero(gameState.player2, false);
            battleUI.RefreshBook(gameState.player1, true);
            battleUI.RefreshBook(gameState.player2, false);
            battleUI.RefreshEquipment(gameState.player1, true);
            battleUI.RefreshEquipment(gameState.player2, false);
        }

        private void SetAllButtonsInteractable(bool value)
        {
            if (attackButton != null) attackButton.interactable = value;
            if (endTurnButton != null) endTurnButton.interactable = value;
            if (drawPrepButton != null) drawPrepButton.interactable = value;
            if (equipPrepButton != null) equipPrepButton.interactable = value;
            if (finishPrepButton != null) finishPrepButton.interactable = value;
            if (sleepButton != null) sleepButton.interactable = value;
            if (drawExtraButton != null) drawExtraButton.interactable = value;
        }
    }
}
