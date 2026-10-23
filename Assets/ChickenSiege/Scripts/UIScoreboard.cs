using System.Collections.Generic;
using System.Text;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Starter.Shooter
{
    /// <summary>
    /// CLASE 2 · Marcador: mantener Tab para ver a todos los jugadores de la sala ordenados por pollos abatidos.
    /// No envía nada por red: cada cliente LEE el estado [Networked] (Nickname, ChickenKills) de todos los Player.
    /// Runner.ActivePlayers = quién está en la sala · Runner.GetPlayerObject = su Player (lo registra el GameManager).
    /// </summary>
    public class UIScoreboard : MonoBehaviour
    {
        public GameManager GameManager;
        public CanvasGroup CanvasGroup;
        public TextMeshProUGUI ListText;
        [Tooltip("Un objeto del master client (el NetworkObject del ChickenSpawner): su dueño es el master")]
        public NetworkObject MasterReference;
        [Tooltip("Mostrar siempre, sin Tab (lo activa UICastle al terminar la partida)")]
        public bool ForceShow;

        private readonly List<Player> _players = new List<Player>();
        private readonly StringBuilder _builder = new StringBuilder();

        private void Update()
        {
            var keyboard = Keyboard.current;
            bool show = ForceShow || (keyboard != null && keyboard.tabKey.isPressed);

            var runner = GameManager != null ? GameManager.Runner : null;
            if (show == false || runner == null || runner.IsRunning == false)
            {
                CanvasGroup.alpha = 0f;
                return;
            }

            CanvasGroup.alpha = 1f;

            // Todos los jugadores de la sala y su Player
            _players.Clear();
            foreach (var playerRef in runner.ActivePlayers)
            {
                var playerObject = runner.GetPlayerObject(playerRef);
                if (playerObject == null)
                    continue;

                var player = playerObject.GetComponent<Player>();
                if (player != null)
                    _players.Add(player);
            }

            _players.Sort((a, b) => b.ChickenKills.CompareTo(a.ChickenKills));

            // El dueño de un objeto de escena es el master client
            PlayerRef master = MasterReference != null && MasterReference.IsValid ? MasterReference.StateAuthority : PlayerRef.None;

            _builder.Clear();
            _builder.Append("<color=#A0A8B3>Jugador<pos=80%>Pollos</color>\n");

            for (int i = 0; i < _players.Count; i++)
            {
                var player = _players[i];
                var owner = player.Object.StateAuthority;
                string nickname = string.IsNullOrEmpty(player.Nickname) ? $"Jugador {owner.PlayerId}" : player.Nickname.ToString();

                _builder.Append(i + 1).Append(". ");
                if (player.HasStateAuthority) _builder.Append("<b>");
                _builder.Append(nickname);
                if (player.HasStateAuthority) _builder.Append(" (tú)</b>");
                if (owner == master) _builder.Append(" <color=#E8C26A>[master]</color>");
                if (player.Health.IsAlive == false) _builder.Append(" <color=#E0393E>(caído)</color>");
                _builder.Append("<pos=80%>").Append(player.ChickenKills).Append('\n');
            }

            ListText.text = _builder.ToString();
        }
    }
}
