using System.Collections.Generic;
using System.Text;
using Fusion;
using TMPro;
using UnityEngine;

namespace Starter.Shooter
{
    /// <summary>
    /// CLASE 3 · Interfaz de la partida: sala de espera, cuenta atrás y carteles.
    /// Se monta sola: añadid este componente a un GameObject vacío de la escena.
    ///
    /// NO ENVÍA NADA POR LA RED. Solo:
    ///  · LEE estado [Networked]: GameFlow.State, GameFlow.Countdown y Player.IsReady de todos.
    ///  · ESCUCHA eventos locales que disparan los RPC: GameFlow.Announced y GameFlow.Notified.
    /// Por eso todos ven lo mismo: todos leen el mismo estado.
    /// </summary>
    public class UIGameFlow : MonoBehaviour
    {
        [Header("Sonidos (opcional)")]
        [Tooltip("Suena en cada número de la cuenta atrás")]
        public AudioClip CountdownClip;
        [Tooltip("Suena con cada cartel grande")]
        public AudioClip AnnounceClip;

        [Header("Carteles")]
        public float BannerSeconds = 2.5f;
        public float NotificationSeconds = 5f;

        private GameFlow _gameFlow;
        private CanvasGroup _canvasGroup;
        private AudioSource _audio;

        // Sala de espera
        private GameObject _waitingPanel;
        private TextMeshProUGUI _waitingList;
        private TextMeshProUGUI _waitingHint;

        // Cuenta atrás, cartel y avisos
        private TextMeshProUGUI _countdownText;
        private TextMeshProUGUI _bannerText;
        private TextMeshProUGUI _notificationText;
        private TextMeshProUGUI _phaseText;

        private float _bannerTime = -100f;
        private int _lastCountdownNumber = -1;

        private readonly List<string> _notifications = new List<string>();
        private readonly List<float> _notificationTimes = new List<float>();
        private readonly List<Player> _players = new List<Player>();
        private readonly StringBuilder _builder = new StringBuilder();

        private void Awake()
        {
            BuildInterface();

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
        }

        private void Start()
        {
            // GameFlow es un objeto de escena: existe desde el principio (aunque aún no esté en red)
            _gameFlow = FindFirstObjectByType<GameFlow>();

            if (_gameFlow != null)
            {
                // Nos suscribimos a los eventos que disparan sus RPC
                _gameFlow.Announced += ShowBanner;
                _gameFlow.Notified += ShowNotification;
            }
        }

        private void OnDestroy()
        {
            if (_gameFlow != null)
            {
                _gameFlow.Announced -= ShowBanner;
                _gameFlow.Notified -= ShowNotification;
            }
        }

        private void Update()
        {
            // Sin conexión (estamos en el menú): no mostramos nada
            if (_gameFlow == null || _gameFlow.IsNetworkReady == false)
            {
                _canvasGroup.alpha = 0f;
                return;
            }

            _canvasGroup.alpha = 1f;

            var state = _gameFlow.State;

            UpdateWaitingPanel(state == GameFlow.GameState.Waiting);
            UpdateCountdown(state == GameFlow.GameState.Countdown);
            UpdateBanner();
            UpdateNotifications();

            _phaseText.text = $"Fase: <b>{StateName(state)}</b>";
        }

        // ------------------------------------------------------------------
        // Lo que LEEMOS del estado de red
        // ------------------------------------------------------------------

        private void UpdateWaitingPanel(bool visible)
        {
            _waitingPanel.SetActive(visible);
            if (visible == false)
                return;

            var runner = _gameFlow.Runner;

            // Todos los jugadores de la sala y su [Networked] IsReady
            _players.Clear();
            foreach (var playerRef in runner.ActivePlayers)
            {
                var playerObject = runner.GetPlayerObject(playerRef);
                var player = playerObject != null ? playerObject.GetComponent<Player>() : null;
                if (player != null)
                    _players.Add(player);
            }

            _builder.Clear();
            bool localReady = false;

            foreach (var player in _players)
            {
                string nickname = string.IsNullOrEmpty(player.Nickname) ? "Jugador" : player.Nickname.ToString();

                if (player.IsReady)
                    _builder.Append("<color=#7CD96B>LISTO</color>      ");
                else
                    _builder.Append("<color=#A0A8B3>esperando</color>  ");

                _builder.Append(nickname);

                if (player.HasStateAuthority)
                {
                    _builder.Append(" <color=#E8C26A>(tú)</color>");
                    localReady = player.IsReady;
                }

                _builder.Append('\n');
            }

            _waitingList.text = _builder.ToString();
            _waitingHint.text = localReady
                ? "Estás <color=#7CD96B><b>LISTO</b></color>. Pulsa <b>R</b> para cancelar"
                : "Pulsa <b>R</b> cuando estés listo";
        }

        private void UpdateCountdown(bool visible)
        {
            _countdownText.gameObject.SetActive(visible);
            if (visible == false)
            {
                _lastCountdownNumber = -1;
                return;
            }

            // TickTimer: todos ven el mismo número porque cuenta ticks de la simulación
            int number = Mathf.Max(1, _gameFlow.CountdownSecondsLeft);
            _countdownText.text = number.ToString();

            if (number != _lastCountdownNumber)
            {
                _lastCountdownNumber = number;
                _countdownText.transform.localScale = Vector3.one * 1.4f;
                Play(CountdownClip);
            }

            _countdownText.transform.localScale = Vector3.Lerp(_countdownText.transform.localScale, Vector3.one, Time.deltaTime * 6f);
        }

        // ------------------------------------------------------------------
        // Lo que nos llega por EVENTOS (RPC)
        // ------------------------------------------------------------------

        /// <summary>Cartel grande. Público: UIWaves también lo usa para anunciar al jefe.</summary>
        public void ShowBanner(string message)
        {
            ShowBanner(message, UIBuilder.Gold);
        }

        public void ShowBanner(string message, Color color)
        {
            _bannerText.text = message;
            _bannerText.color = color;
            _bannerText.transform.localScale = Vector3.one * 1.3f;
            _bannerTime = Time.time;
            Play(AnnounceClip);
        }

        public void ShowNotification(string message)
        {
            _notifications.Add(message);
            _notificationTimes.Add(Time.time);

            if (_notifications.Count > 4)
            {
                _notifications.RemoveAt(0);
                _notificationTimes.RemoveAt(0);
            }
        }

        private void UpdateBanner()
        {
            float age = Time.time - _bannerTime;
            float alpha = age < BannerSeconds ? 1f : Mathf.Clamp01(1f - (age - BannerSeconds) / 0.5f);

            var color = _bannerText.color;
            color.a = alpha;
            _bannerText.color = color;

            _bannerText.transform.localScale = Vector3.Lerp(_bannerText.transform.localScale, Vector3.one, Time.deltaTime * 6f);
        }

        private void UpdateNotifications()
        {
            // Quitamos los avisos viejos
            while (_notificationTimes.Count > 0 && Time.time - _notificationTimes[0] > NotificationSeconds)
            {
                _notifications.RemoveAt(0);
                _notificationTimes.RemoveAt(0);
            }

            _builder.Clear();
            foreach (var notification in _notifications)
            {
                _builder.Append(notification).Append('\n');
            }

            _notificationText.text = _builder.ToString();
        }

        // ------------------------------------------------------------------

        private void Play(AudioClip clip)
        {
            if (clip != null)
                _audio.PlayOneShot(clip);
        }

        public static string StateName(GameFlow.GameState state)
        {
            switch (state)
            {
                case GameFlow.GameState.Waiting: return "Esperando";
                case GameFlow.GameState.Countdown: return "Cuenta atrás";
                case GameFlow.GameState.Playing: return "Jugando";
                case GameFlow.GameState.Victory: return "Victoria";
                case GameFlow.GameState.Defeat: return "Derrota";
                default: return state.ToString();
            }
        }

        private void BuildInterface()
        {
            // Por debajo del menú del Starter (que así siempre queda encima)
            var canvas = UIBuilder.CreateCanvas(transform, "GameFlowCanvas", -1);
            _canvasGroup = canvas.GetComponent<CanvasGroup>();
            var root = canvas.transform;

            // Sala de espera (centro)
            var panel = UIBuilder.CreatePanel(root, "WaitingPanel", new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(720f, 470f), UIBuilder.PanelColor);
            _waitingPanel = panel.gameObject;
            var title = UIBuilder.CreateText(panel.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(680f, 70f), 52f, UIBuilder.Gold);
            title.text = "SALA DE ESPERA";
            var subtitle = UIBuilder.CreateText(panel.transform, "Subtitle", new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(680f, 40f), 24f, UIBuilder.Grey);
            subtitle.text = "La partida empieza cuando todos estén listos";
            _waitingList = UIBuilder.CreateText(panel.transform, "List", new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(560f, 230f), 30f, UIBuilder.Cream, TextAlignmentOptions.TopLeft);
            _waitingHint = UIBuilder.CreateText(panel.transform, "Hint", new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(680f, 50f), 32f, UIBuilder.Cream);

            // Cuenta atrás (centro, enorme)
            _countdownText = UIBuilder.CreateText(root, "Countdown", new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(400f, 260f), 220f, UIBuilder.Gold);
            _countdownText.gameObject.SetActive(false);

            // Cartel grande (por encima de la mira)
            _bannerText = UIBuilder.CreateText(root, "Banner", new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(1600f, 120f), 76f, UIBuilder.Gold);
            _bannerText.color = new Color(1f, 1f, 1f, 0f);

            // Avisos pequeños (derecha)
            _notificationText = UIBuilder.CreateText(root, "Notifications", new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(620f, 200f), 26f, UIBuilder.Cream, TextAlignmentOptions.BottomRight);

            // Fase actual (abajo a la derecha): útil para ver el estado de red en clase
            _phaseText = UIBuilder.CreateText(root, "Phase", new Vector2(1f, 0f), new Vector2(-24f, 16f), new Vector2(500f, 36f), 22f, UIBuilder.Grey, TextAlignmentOptions.BottomRight);
        }
    }
}
