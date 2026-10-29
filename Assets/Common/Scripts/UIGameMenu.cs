using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Starter
{
    /// <summary>
    /// Shows in-game menu, handles player connecting/disconnecting to the network game, cursor locking
    /// and visibility of on-screen controls for mobile platforms.
    /// </summary>
    public class UIGameMenu : MonoBehaviour
    {
        [Header("Start Game Setup")]
        [Tooltip("Specifies which game mode player should join - e.g. Platformer, ThirdPersonCharacter")]
        public string GameModeIdentifier;
        public NetworkRunner RunnerPrefab;
        public int MaxPlayerCount = 8;

        [Header("Debug")]
        [Tooltip("For debug purposes it is possible to force single-player game (starts faster)")]
        public bool ForceSinglePlayer;
        [Tooltip("For debug purposes it is possible to force on-screen controls on all platforms")]
        public bool ForceMobileControls;

        [Header("UI Setup")]
        public CanvasGroup PanelGroup;
        public TMP_InputField RoomText;
        public TMP_InputField NicknameText;
        public TextMeshProUGUI StatusText;
        public GameObject StartGroup;
        public GameObject DisconnectGroup;
        [Tooltip("On-screen controls (joystick and buttons) used on mobile platforms")]
        public GameObject MobileControls;

        private NetworkRunner _runnerInstance;
        private static string _shutdownStatus;

        [Header("Chicken Siege - Clase 2 (opcional)")]
        public TMP_Dropdown DifficultyDropdown;
        public UISessionList SessionList;

        private NetworkRunner _lobbyRunner;


        private void Start()
        {
            if (SessionList != null && (Application.isEditor && ForceSinglePlayer) == false)
            {
                _ = StartLobby();
            }
        }

        private async Task StartLobby()
        {
            _lobbyRunner = Instantiate(RunnerPrefab);
            _lobbyRunner.name = "LobbyRunner";

            // El prefab del Runner ya trae NetworkEvents: escuchamos la lista de salas
            var events = _lobbyRunner.GetComponent<NetworkEvents>();
            events.OnSessionListUpdate.AddListener(OnSessionListUpdate);

            var result = await _lobbyRunner.JoinSessionLobby(SessionLobby.Shared);
            if (result.Ok == false)
            {
                StatusText.text = $"Lobby: {result.ShutdownReason}";
            }
        }

        private async Task StopLobby()
        {
            if (_lobbyRunner == null)
                return;

            var runner = _lobbyRunner;
            _lobbyRunner = null;
            await runner.Shutdown();
        }

        private void OnSessionListUpdate(NetworkRunner runner, List<SessionInfo> sessions)
        {
            var filtered = new List<SessionInfo>();
            foreach (var session in sessions)
            {
                if (session.Properties.TryGetValue("GameMode", out var gameMode) && (string)gameMode == GameModeIdentifier)
                {
                    filtered.Add(session);
                }
            }

            // Le pasamos a la lista qué hacer al pulsar "Unirse": nuestro JoinSession (paso 2.5)
            SessionList.Refresh(filtered, JoinSession);
        }

        private void OnDestroy()
        {
            if (_lobbyRunner != null)
            {
                _ = _lobbyRunner.Shutdown();
                _lobbyRunner = null;
            }
        }

        public void JoinSession(SessionInfo session)
        {
            RoomText.text = session.Name;

            // Misma dificultad que la sala, para que las propiedades coincidan
            if (DifficultyDropdown != null)
            {
                DifficultyDropdown.value = Difficulty.FromSession(session);
            }

            StartGame();
        }
        public async void StartGame()
        {
            await Disconnect();

            await StopLobby();

            PlayerPrefs.SetString("PlayerName", NicknameText.text);
            // CLASE 4 · Apuntamos la sala para poder volver si se corta
            PlayerPrefs.SetString("LastRoom", RoomText.text);

            _runnerInstance = Instantiate(RunnerPrefab);

            // Add listener for shutdowns so we can handle unexpected shutdowns
            var events = _runnerInstance.GetComponent<NetworkEvents>();
            events.OnShutdown.AddListener(OnShutdown);

            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));

            var startArguments = new StartGameArgs()
            {
                GameMode = Application.isEditor && ForceSinglePlayer ? GameMode.Single : GameMode.Shared,
                SessionName = RoomText.text,
                PlayerCount = MaxPlayerCount,
                // We need to specify a session property for matchmaking to decide where the player wants to join.
                // Otherwise players from Platformer scene could connect to ThirdPersonCharacter game etc.
                SessionProperties = BuildSessionProperties(),
                Scene = sceneInfo,
            };

            StatusText.text = startArguments.GameMode == GameMode.Single ? "Starting single-player..." : "Connecting...";

            var startTask = _runnerInstance.StartGame(startArguments);
            await startTask;

            if (startTask.Result.Ok)
            {
                StatusText.text = "";
                PanelGroup.gameObject.SetActive(false);
            }
            else
            {
                // CLASE 4 · Explicamos el motivo (por ejemplo, GameClosed = la sala está cerrada)
                StatusText.text = $"No se pudo entrar: {Explain(startTask.Result.ShutdownReason)}";
            }
        }

        private Dictionary<string, SessionProperty> BuildSessionProperties()
            {
                var properties = new Dictionary<string, SessionProperty>
                {
                    ["GameMode"] = GameModeIdentifier,
                };

                if (DifficultyDropdown != null)
                {
                    properties[Difficulty.Key] = DifficultyDropdown.value;   // int: 0, 1 o 2
                }

                return properties;
            }

        public async void DisconnectClicked()
        {
            await Disconnect();
        }

        public async void BackToMenu()
        {
            await Disconnect();
            await StopLobby();

            SceneManager.LoadScene(0);
        }

        public void TogglePanelVisibility()
        {
            if (PanelGroup.gameObject.activeSelf && _runnerInstance == null)
                return; // Panel cannot be hidden if the game is not running

            PanelGroup.gameObject.SetActive(!PanelGroup.gameObject.activeSelf);
        }

        private void OnEnable()
        {
            var nickname = PlayerPrefs.GetString("PlayerName");
            if (string.IsNullOrEmpty(nickname))
            {
                nickname = "Player" + Random.Range(10000, 100000);
            }

            NicknameText.text = nickname;

            // CLASE 4 · Si se cortó la partida, dejamos escrita la sala para volver con un clic
            var lastRoom = PlayerPrefs.GetString("LastRoom");
            if (_shutdownStatus != null && string.IsNullOrEmpty(lastRoom) == false)
            {
                RoomText.text = lastRoom;
            }

            // Try to load previous shutdown status
            StatusText.text = _shutdownStatus != null ? _shutdownStatus : string.Empty;
            _shutdownStatus = null;
        }

        private void Update()
        {
            // Toggle menu visibility with Enter/Esc keys
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
                {
                    TogglePanelVisibility();
                }
            }

            if (PanelGroup.gameObject.activeSelf)
            {

                if (DifficultyDropdown != null)
                    DifficultyDropdown.interactable = _runnerInstance == null;
                if (SessionList != null)
                    SessionList.gameObject.SetActive(_runnerInstance == null);
                StartGroup.SetActive(_runnerInstance == null);
                DisconnectGroup.SetActive(_runnerInstance != null);
                RoomText.interactable = _runnerInstance == null;
                NicknameText.interactable = _runnerInstance == null;

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            else if (Shooter.UICastle.IsGameOver)
            {
                // CHICKEN SIEGE: fin de partida, cursor libre para pulsar "Volver al menú"
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // On-screen controls are shown on mobile platforms only and only when the menu is hidden
            MobileControls.SetActive(PanelGroup.gameObject.activeSelf == false && (Application.isMobilePlatform || ForceMobileControls));
        }

        public async Task Disconnect()
        {
            if (_runnerInstance == null)
                return;

            StatusText.text = "Disconnecting...";
            PanelGroup.interactable = false;

            // Remove shutdown listener since we are disconnecting deliberately
            var events = _runnerInstance.GetComponent<NetworkEvents>();
            events.OnShutdown.RemoveListener(OnShutdown);

            await _runnerInstance.Shutdown();
            _runnerInstance = null;

            // Reset of scene network objects is needed, reload the whole scene
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnShutdown(NetworkRunner runner, ShutdownReason reason)
        {
            // Unexpected shutdown happened (e.g. Host disconnected)

            // Save status into static variable, it will be used in OnEnable after scene load
            // CLASE 4 · En vez de "Shutdown: PhotonCloudTimeout", algo que se entienda
            _shutdownStatus = $"Se cortó la partida: {Explain(reason)}";
            Debug.LogWarning($"Shutdown: {reason}");

            // Reset of scene network objects is needed, reload the whole scene
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// CLASE 4 · ShutdownReason traducido. Es el "por qué" de cada desconexión.
        /// </summary>
        private static string Explain(ShutdownReason reason)
        {
            switch (reason)
            {
                case ShutdownReason.PhotonCloudTimeout:
                case ShutdownReason.ConnectionTimeout:
                    return "se perdió la conexión. Pulsa Start para volver a la sala.";
                case ShutdownReason.GameClosed:
                    return "la sala está cerrada (la partida ya empezó).";
                case ShutdownReason.GameIsFull:
                    return "la sala está llena.";
                case ShutdownReason.GameNotFound:
                    return "esa sala ya no existe.";
                case ShutdownReason.DisconnectedByPluginLogic:
                    return "el servidor nos ha desconectado.";
                default:
                    return reason.ToString();
            }
        }
    }
}
