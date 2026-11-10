using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Zhuzbai.Platformer
{
    public enum MenuCommand { Play, Settings, Controls, Quit, Resume, MainMenu, Back, Pause, HowToPlay, Retry, NextLevel, Replay, EnemyChallenge, CampaignStage, EnemyAIStage }
    public enum MenuScreen { Game, Main, Pause, Settings, Controls, HowToPlay, GameOver, Complete, Victory, StageSelection }

    public sealed class GameMenuController : MonoBehaviour
    {
        [SerializeField] private bool gameplayScene = false;
        [SerializeField] private GameObject mainPanel = null;
        [SerializeField] private GameObject stageSelectionPanel = null;
        [SerializeField] private GameObject pausePanel = null;
        [SerializeField] private GameObject settingsPanel = null;
        [SerializeField] private GameObject controlsPanel = null;
        [SerializeField] private GameObject modalShade = null;
        [SerializeField] private GameObject topButtons = null;
        [SerializeField] private PlayerFoundation player = null;
        [SerializeField] private PlayerInput playerInput = null;
        [SerializeField] private GameObject howToPlayPanel = null;
        [SerializeField] private GameObject gameOverPanel = null;
        [SerializeField] private GameObject levelCompletePanel = null;
        [SerializeField] private GameObject victoryPanel = null;
        [SerializeField] private UnityEngine.UI.Text completedCoins = null;
        [SerializeField] private string replayScene = "MainLevel";
        private LevelSession session;
        private PlayerHealth health;
        private MenuScreen? outcome;
        public bool HasOutcome => outcome.HasValue;
        public static bool IsGameplayBlocked { get; private set; }
        private static GameMenuController active;
        private readonly Stack<MenuScreen> history = new Stack<MenuScreen>();
        private InputAction pauseAction;
        private InputActionMap playerMap;
        private bool mapWasEnabled;
        private bool paused;
        private MenuScreen screen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { active = null; IsGameplayBlocked = false; }
        private void Awake() => active = this;
        private void Start()
        {
            Time.timeScale = 1;
            playerMap = playerInput != null ? playerInput.actions.FindActionMap("Player", true) : null;
            if (gameplayScene && player != null)
            {
                health = player.GetComponent<PlayerHealth>();
                session = FindAnyObjectByType<LevelSession>();
                health.Died += OnDied;
                if (session != null) session.Changed += OnSessionChanged;
            }
            Show(gameplayScene ? MenuScreen.Game : MenuScreen.Main, false);
        }
        private void OnEnable()
        {
            pauseAction = new InputAction("MenuBack", InputActionType.Button, "<Keyboard>/escape");
            pauseAction.AddBinding("<Gamepad>/start");
            pauseAction.performed += OnPauseAction;
            pauseAction.Enable();
        }
        private void OnDisable()
        {
            pauseAction?.Dispose();
            pauseAction = null;
        }
        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
            if (session != null) session.Changed -= OnSessionChanged;
            if (active != this) return;
            Time.timeScale = 1;
            IsGameplayBlocked = false;
            active = null;
            AudioManager.Instance?.SetGameplayPaused(false);
        }
        private void OnPauseAction(InputAction.CallbackContext context)
        {
            AudioManager.Instance?.Play(GameSound.Click);
            if (screen == MenuScreen.Game && gameplayScene) Execute(MenuCommand.Pause);
            else Execute(MenuCommand.Back);
        }
        public void Execute(MenuCommand command)
        {
            switch (command)
            {
                case MenuCommand.Play:
                    if (stageSelectionPanel != null) Show(MenuScreen.StageSelection);
                    else Load("MainLevel");
                    break;
                case MenuCommand.CampaignStage: Load("MainLevel"); break;
                case MenuCommand.EnemyChallenge:
                case MenuCommand.EnemyAIStage: Load("EnemyAI"); break;
                case MenuCommand.MainMenu: Load("MainMenu"); break;
                case MenuCommand.Settings: Show(MenuScreen.Settings); break;
                case MenuCommand.Controls: Show(MenuScreen.Controls); break;
                case MenuCommand.HowToPlay: if (gameplayScene) Show(MenuScreen.HowToPlay); break;
                case MenuCommand.Retry: Load(SceneManager.GetActiveScene().name); break;
                case MenuCommand.Replay: Load(string.IsNullOrEmpty(replayScene) ? "MainLevel" : replayScene); break;
                case MenuCommand.NextLevel:
                    if (session != null && session.Completed && session.Definition != null && !string.IsNullOrEmpty(session.Definition.NextScene)) Load(session.Definition.NextScene);
                    break;
                case MenuCommand.Pause: if (gameplayScene) Show(MenuScreen.Pause); break;
                case MenuCommand.Resume: history.Clear(); Show(outcome ?? MenuScreen.Game, false); break;
                case MenuCommand.Back:
                    Show(history.Count > 0 ? history.Pop() : outcome ?? (gameplayScene ? MenuScreen.Game : MenuScreen.Main), false);
                    break;
                case MenuCommand.Quit:
                    PlayerPrefs.Save();
#if UNITY_EDITOR
                    Debug.Log("QUIT selected: a standalone build will exit. Unity Editor remains open.");
#else
                    Application.Quit();
#endif
                    break;
            }
        }
        private void OnDied()
        {
            Finish(MenuScreen.GameOver);
            if (session != null && session.Definition != null && session.Definition.IsBossLevel)
            {
                if (gameOverPanel != null) gameOverPanel.SetActive(false);
                StartCoroutine(RestartBossLevel());
            }
        }
        private IEnumerator RestartBossLevel()
        {
            // Let PlayerHealth finish its death event before unloading the current scene.
            yield return null;
            Load(gameObject.scene.name);
        }
        private void OnSessionChanged()
        {
            if (!session.Completed || (health != null && health.IsDead)) return;
            if (completedCoins != null) completedCoins.text = $"COINS {session.Coins:00}/{session.TotalCoins:00}";
            Finish(session.Definition != null && string.IsNullOrEmpty(session.Definition.NextScene) ? MenuScreen.Victory : MenuScreen.Complete);
        }
        private void Finish(MenuScreen value)
        {
            if (!outcome.HasValue)
            {
                if (gameObject.scene.name == "EnemyAI" && (value == MenuScreen.Victory || value == MenuScreen.GameOver))
                    AudioManager.Instance?.PlayArenaOutcome(value == MenuScreen.Victory);
                else if (value == MenuScreen.GameOver) AudioManager.Instance?.PlayGameOver();
                else if (value == MenuScreen.Complete) AudioManager.Instance?.PlayVictory(null);
            }
            outcome = value; history.Clear(); Show(value, false);
        }
        private void Show(MenuScreen next, bool remember = true)
        {
            if (next == MenuScreen.Game && outcome.HasValue) next = outcome.Value;
            if (remember && next != screen) history.Push(screen);
            screen = next;
            if (mainPanel != null) mainPanel.SetActive(next == MenuScreen.Main);
            if (stageSelectionPanel != null) stageSelectionPanel.SetActive(next == MenuScreen.StageSelection);
            if (pausePanel != null) pausePanel.SetActive(next == MenuScreen.Pause);
            if (settingsPanel != null) settingsPanel.SetActive(next == MenuScreen.Settings);
            if (controlsPanel != null) controlsPanel.SetActive(next == MenuScreen.Controls);
            if (howToPlayPanel != null) howToPlayPanel.SetActive(next == MenuScreen.HowToPlay);
            if (gameOverPanel != null) gameOverPanel.SetActive(next == MenuScreen.GameOver);
            if (levelCompletePanel != null) levelCompletePanel.SetActive(next == MenuScreen.Complete);
            if (victoryPanel != null) victoryPanel.SetActive(next == MenuScreen.Victory);
            if (modalShade != null) modalShade.SetActive(next != MenuScreen.Game);
            if (topButtons != null) topButtons.SetActive(gameplayScene);
            bool block = gameplayScene && next != MenuScreen.Game;
            if (block != paused)
            {
                paused = block;
                player?.SetGameplayInputBlocked(block);
                if (block)
                {
                    mapWasEnabled = playerMap != null && playerMap.enabled;
                    playerMap?.Disable();
                }
                else if (mapWasEnabled) playerMap?.Enable();
                AudioManager.Instance?.SetGameplayPaused(block);
            }
            IsGameplayBlocked = !gameplayScene || block;
            Time.timeScale = block ? 0 : 1;
            GameObject panel = next switch { MenuScreen.Main => mainPanel, MenuScreen.StageSelection => stageSelectionPanel, MenuScreen.Pause => pausePanel,
                MenuScreen.Settings => settingsPanel, MenuScreen.Controls => controlsPanel, MenuScreen.HowToPlay => howToPlayPanel,
                MenuScreen.GameOver => gameOverPanel, MenuScreen.Complete => levelCompletePanel, MenuScreen.Victory => victoryPanel, _ => null };
            if (EventSystem.current != null)
            {
                EventSystem.current.sendNavigationEvents = next != MenuScreen.Game;
                EventSystem.current.SetSelectedGameObject(null);
                var first = panel != null ? panel.GetComponentInChildren<UnityEngine.UI.Selectable>() : null;
                if (first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
            }
        }
        private void Load(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"Scene {sceneName} is not in the active Build Profile. Run Apply Main Menu and Game UI.");
                return;
            }
            Time.timeScale = 1;
            PlayerPrefs.Save();
            AudioManager.Instance?.SetGameplayPaused(false);
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
