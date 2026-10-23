using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Starter.Shooter
{
    /// <summary>
    /// CLASE 2 · Barra de vida del castillo, cartel de derrota y fin de partida.
    /// No hace nada de red: solo LEE el estado [Networked] del castillo.
    /// Como todos leen el mismo CurrentHealth, todos ven la misma barra y todos pierden a la vez.
    /// Al caer el castillo: cartel + marcador, cuenta atrás y vuelta al menú (o botón para volver ya).
    /// </summary>
    public class UICastle : MonoBehaviour
    {
        public Castle Castle;
        public CanvasGroup CanvasGroup;
        [Tooltip("Image con Image Type = Filled, Fill Method = Horizontal")]
        public Image HealthFill;
        public TextMeshProUGUI HealthText;
        [Tooltip("Cartel '¡El castillo cayó!'")]
        public GameObject DefeatGroup;

        [Header("Fin de partida")]
        public UIScoreboard Scoreboard;
        public UIGameMenu Menu;
        public TextMeshProUGUI CountdownText;
        public Button BackButton;
        public float ReturnToMenuSeconds = 8f;

        /// <summary>True mientras se muestra el fin de partida: UIGameMenu deja el cursor libre para el botón.</summary>
        public static bool IsGameOver { get; private set; }

        private float _gameOverTime = -1f;
        private bool _returning;

        private void Awake()
        {
            IsGameOver = false;

            if (BackButton != null)
                BackButton.onClick.AddListener(ReturnToMenu);
        }

        private void OnDestroy()
        {
            IsGameOver = false;
        }

        private void Update()
        {
            // Antes de conectarse el castillo todavía no existe en la red
            if (Castle == null || Castle.IsNetworkReady == false)
            {
                CanvasGroup.alpha = 0f;
                if (DefeatGroup != null) DefeatGroup.SetActive(false);
                return;
            }

            CanvasGroup.alpha = 1f;

            var health = Castle.Health;
            float ratio = health.InitialHealth > 0 ? (float)health.CurrentHealth / health.InitialHealth : 0f;

            HealthFill.fillAmount = Mathf.Clamp01(ratio);

            if (HealthText != null)
                HealthText.text = $"Castillo {Mathf.Max(0, health.CurrentHealth)}/{health.InitialHealth}";

            if (DefeatGroup != null)
                DefeatGroup.SetActive(Castle.IsDestroyed);

            if (Castle.IsDestroyed)
            {
                UpdateGameOver();
            }
        }

        private void UpdateGameOver()
        {
            if (_gameOverTime < 0f)
            {
                _gameOverTime = Time.time;
                IsGameOver = true;
            }

            if (Scoreboard != null)
                Scoreboard.ForceShow = true;

            float left = ReturnToMenuSeconds - (Time.time - _gameOverTime);

            if (CountdownText != null)
                CountdownText.text = $"Volviendo al menú en {Mathf.CeilToInt(Mathf.Max(0f, left))}…";

            if (left <= 0f)
            {
                ReturnToMenu();
            }
        }

        public void ReturnToMenu()
        {
            if (_returning)
                return;

            _returning = true;
            IsGameOver = false;

            // Apaga el Runner y recarga esta escena: volvemos al menú de Chicken Siege (lista de salas)
            if (Menu != null)
                Menu.DisconnectClicked();
        }
    }
}
