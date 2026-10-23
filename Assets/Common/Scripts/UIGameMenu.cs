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

        [Header("Chicken Siege - Clase 2 (opcional)")]
        [Tooltip("Desplegable Fácil / Normal / Difícil. Vacío = la sala se crea sin dificultad.")]
        public TMP_Dropdown DifficultyDropdown;
        [Tooltip("Lista de salas del lobby. Vacío = no se entra al lobby (como antes).")]
        public UISessionList SessionList;

        private NetworkRunner _runnerInstance;
        // CLASE 2: segundo Runner que solo mira la lista de salas (lobby). Se apaga al entrar a jugar.
        private NetworkRunner _lobbyRunner;
        private static string _shutdownStatus;

        public async void StartGame()
        {
            await Disconnect();

            // CLASE 2: dejamos de mirar la lista antes de entrar a una sala
            await StopLobby();

            PlayerPrefs.SetString("PlayerName", NicknameText.text);

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
                StatusText.text = $"Connection Failed: {startTask.Result.ShutdownReason}";
            }
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

            // Try to load previous shutdown status
            StatusText.text = _shutdownStatus != null ? _shutdownStatus : string.Empty;
            _shutdownStatus = null;
        }

        private void Start()
        {
            // CLASE 2: al abrir el menú nos suscribimos a la lista de salas
            if (SessionList != null && (Application.isEditor && ForceSinglePlayer) == false)
            {
                _ = StartLobby();
            }
        }

        private void OnDestroy()
        {
            if (_lobbyRunner != null)
            {
                _ = _lobbyRunner.Shutdown();
                _lobbyRunner = null;
            }
        }

        // ---------------- CLASE 2: LOBBY Y LISTA DE SALAS ----------------

        private Dictionary<string, SessionProperty> BuildSessionProperties()
        {
            var properties = new Dictionary<string, SessionProperty>
            {
                // Separa los minijuegos del Starter (ya venía así)
                ["GameMode"] = GameModeIdentifier,
            };

            // Propiedad de sala: la dificultad elegida al crearla
            if (DifficultyDropdown != null)
            {
                properties[Difficulty.Key] = DifficultyDropdown.value;
            }

            return properties;
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
            if (SessionList == null)
                return;

            // Solo las salas de este minijuego
            var filtered = new List<SessionInfo>();
            foreach (var session in sessions)
            {
                if (session.Properties.TryGetValue("GameMode", out var gameMode) && (string)gameMode == GameModeIdentifier)
                {
                    filtered.Add(session);
                }
            }

            SessionList.Refresh(filtered, JoinSession);
        }

        // Lo llama el botón "Unirse" de cada fila de la lista
        public void JoinSession(SessionInfo session)
        {
            RoomText.text = session.Name;

            // Usamos la misma dificultad que la sala para que las propiedades coincidan
            if (DifficultyDropdown != null)
            {
                DifficultyDropdown.value = Difficulty.FromSession(session);
            }

            StartGame();
        }

        // -----------------------------------------------------------------

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
                StartGroup.SetActive(_runnerInstance == null);
                DisconnectGroup.SetActive(_runnerInstance != null);
                RoomText.interactable = _runnerInstance == null;
                NicknameText.interactable = _runnerInstance == null;

                // CLASE 2: la dificultad y la lista solo tienen sentido fuera de una partida
                if (DifficultyDropdown != null)
                    DifficultyDropdown.interactable = _runnerInstance == null;
                if (SessionList != null)
                    SessionList.gameObject.SetActive(_runnerInstance == null);

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (Shooter.UICastle.IsGameOver)
            {
                // CHICKEN SIEGE: fin de partida, cursor libre para el botón "Volver al menú"
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
            _shutdownStatus = $"Shutdown: {reason}";
            Debug.LogWarning(_shutdownStatus);

            // Reset of scene network objects is needed, reload the whole scene
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}