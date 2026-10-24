using System;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Starter
{
    /// <summary>
    /// CLASE 2 · Una fila de la lista de salas: nombre, jugadores, dificultad y botón Unirse.
    /// </summary>
    public class SessionRow : MonoBehaviour
    {
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI PlayersText;
        public TextMeshProUGUI DifficultyText;
        public Button JoinButton;

        private SessionInfo _session;
        private Action<SessionInfo> _onJoin;

        public void Setup(SessionInfo session, Action<SessionInfo> onJoin)
        {
            _session = session;
            _onJoin = onJoin;

            NameText.text = session.Name;
            PlayersText.text = $"{session.PlayerCount}/{session.MaxPlayers}";
            DifficultyText.text = Difficulty.Name(Difficulty.FromSession(session));

            // EXTRA E1 (solución): no se puede entrar a una sala llena o cerrada
            bool canJoin = session.IsOpen && session.PlayerCount < session.MaxPlayers;
            JoinButton.interactable = canJoin;
            NameText.alpha = canJoin ? 1f : 0.4f;
        }

        private void Awake()
        {
            JoinButton.onClick.AddListener(() => _onJoin?.Invoke(_session));
        }
    }
}
