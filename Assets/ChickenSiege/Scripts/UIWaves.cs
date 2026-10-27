using TMPro;
using UnityEngine;

namespace Starter.Shooter
{
    /// <summary>
    /// CLASE 3 · Interfaz de las oleadas (arriba a la derecha): "Oleada 2/6", pollos que quedan, pausa y barra del jefe.
    /// Se monta sola: añadid este componente al mismo GameObject que UIGameFlow.
    ///
    /// NO ENVÍA NADA POR LA RED. Solo LEE:
    ///  · [Networked] WaveManager.CurrentWave y WaveManager.Pause
    ///  · Chicken.All (la lista local que rellenan Spawned/Despawned)
    ///  · [Networked] Health.CurrentHealth del jefe
    /// Y escucha el evento WaveManager.BossIncoming (lo dispara un RPC).
    /// </summary>
    public class UIWaves : MonoBehaviour
    {
        [Header("Sonidos (opcional)")]
        [Tooltip("Suena cuando llega el jefe (por ejemplo, la bocina de goma)")]
        public AudioClip BossClip;

        private WaveManager _waveManager;
        private GameFlow _gameFlow;
        private UIGameFlow _uiGameFlow;
        private AudioSource _audio;

        private CanvasGroup _canvasGroup;
        private TextMeshProUGUI _waveTitle;
        private TextMeshProUGUI _waveInfo;
        private GameObject _bossGroup;
        private RectTransform _bossFill;
        private TextMeshProUGUI _bossText;

        private Chicken _boss;
        private int _bossMaxHealth;

        private void Awake()
        {
            BuildInterface();

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
        }

        private void Start()
        {
            _waveManager = FindFirstObjectByType<WaveManager>();
            _gameFlow = FindFirstObjectByType<GameFlow>();
            _uiGameFlow = FindFirstObjectByType<UIGameFlow>();

            if (_waveManager != null)
            {
                _waveManager.BossIncoming += OnBossIncoming;
            }
        }

        private void OnDestroy()
        {
            if (_waveManager != null)
            {
                _waveManager.BossIncoming -= OnBossIncoming;
            }
        }

        private void Update()
        {
            bool playing = _waveManager != null && _waveManager.IsNetworkReady &&
                           _gameFlow != null && _gameFlow.IsNetworkReady &&
                           _gameFlow.State != GameFlow.GameState.Waiting &&
                           _gameFlow.State != GameFlow.GameState.Countdown;

            if (playing == false)
            {
                _canvasGroup.alpha = 0f;
                return;
            }

            _canvasGroup.alpha = 1f;

            UpdateWaveText();
            UpdateBossBar();
        }

        private void UpdateWaveText()
        {
            int wave = Mathf.Max(1, _waveManager.CurrentWave);
            int total = _waveManager.Waves.Length;

            _waveTitle.text = $"OLEADA <b>{wave}</b>/{total}";

            if (_waveManager.IsPaused)
            {
                _waveInfo.text = $"<color=#7CD96B>Superada</color> · siguiente en <b>{_waveManager.PauseSecondsLeft}</b>";
            }
            else
            {
                _waveInfo.text = $"Pollos: <b>{Chicken.CountAlive()}</b>";
            }
        }

        private void UpdateBossBar()
        {
            // ¿Hay un jefe vivo? (lo buscamos en la lista local de pollos)
            if (_boss == null || _boss.Object == null || _boss.Object.IsValid == false || _boss.Health.IsAlive == false)
            {
                bool hadBoss = _boss != null;
                bool bossKilled = hadBoss && _boss.Object != null && _boss.Object.IsValid && _boss.Health.IsAlive == false;
                _boss = FindAliveBoss();

                if (bossKilled && _boss == null && _uiGameFlow != null)
                {
                    _uiGameFlow.ShowBanner("¡JEFE DERROTADO!", UIBuilder.Green);
                }

                if (_boss != null)
                {
                    _bossMaxHealth = 0;
                }
            }

            _bossGroup.SetActive(_boss != null);
            if (_boss == null)
                return;

            // La vida máxima solo la sabe bien el master (en Difícil se multiplica):
            // nos quedamos con la mayor que hayamos visto.
            int current = _boss.Health.CurrentHealth;
            _bossMaxHealth = Mathf.Max(_bossMaxHealth, current, 1);

            UIBuilder.SetBar(_bossFill, (float)current / _bossMaxHealth);
            _bossText.text = $"JEFE  {current}";
        }

        private static Chicken FindAliveBoss()
        {
            foreach (var chicken in Chicken.All)
            {
                if (chicken.IsBoss && chicken.Health.IsAlive)
                    return chicken;
            }
            return null;
        }

        private void OnBossIncoming()
        {
            // Llega por RPC a todos los que están en la sala
            if (_uiGameFlow != null)
            {
                _uiGameFlow.ShowBanner($"¡OLEADA {_waveManager.CurrentWave}: LLEGA EL JEFE!", UIBuilder.Red);
            }

            if (BossClip != null)
            {
                _audio.PlayOneShot(BossClip);
            }
        }

        private void BuildInterface()
        {
            var canvas = UIBuilder.CreateCanvas(transform, "WavesCanvas", -1);
            _canvasGroup = canvas.GetComponent<CanvasGroup>();
            var root = canvas.transform;

            // Arriba a la derecha (arriba en el centro están la barra del castillo y "Best Hunter")
            _waveTitle = UIBuilder.CreateText(root, "Wave", new Vector2(1f, 1f), new Vector2(-40f, -26f), new Vector2(600f, 52f), 40f, UIBuilder.Gold, TextAlignmentOptions.Right);
            _waveInfo = UIBuilder.CreateText(root, "WaveInfo", new Vector2(1f, 1f), new Vector2(-40f, -80f), new Vector2(600f, 38f), 26f, UIBuilder.Cream, TextAlignmentOptions.Right);

            // Barra del jefe, debajo
            var bossRoot = UIBuilder.CreateRect(root, "Boss", new Vector2(1f, 1f), new Vector2(-40f, -128f), new Vector2(460f, 62f));
            _bossGroup = bossRoot.gameObject;
            _bossFill = UIBuilder.CreateBar(bossRoot, "BossBar", new Vector2(1f, 0f), Vector2.zero, new Vector2(460f, 26f), UIBuilder.PanelColor, UIBuilder.Red);
            _bossText = UIBuilder.CreateText(bossRoot, "BossText", new Vector2(1f, 1f), Vector2.zero, new Vector2(460f, 32f), 26f, UIBuilder.Red, TextAlignmentOptions.Right);
            _bossGroup.SetActive(false);
        }
    }
}
