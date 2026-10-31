using System;
using Fusion;
using UnityEngine;
using System.Collections.Generic;

namespace Starter.Shooter
{
    /// <summary>
    /// CLASE 3 · El "director" de la partida. Decide en qué FASE estamos:
    ///
    ///     Esperando  ->  Cuenta atrás  ->  Jugando  ->  Victoria / Derrota
    ///
    /// CONCEPTOS DE RED QUE ENSEÑA
    /// 1. UN SOLO DUEÑO DECIDE. Es un objeto de escena con "Is Master Client Object":
    ///    su State Authority es el master client. Solo él cambia la fase; los demás la leen.
    /// 2. LA FASE ES ESTADO ([Networked]). La tienen todos, también quien entra tarde a la sala.
    /// 3. LOS CARTELES SON EVENTOS (RPC). "¡A defender!" llega solo a quien está conectado
    ///    en ese momento. Quien entra después NO lo verá (y no pasa nada, porque la fase sí la tiene).
    /// 4. LA CUENTA ATRÁS ES UN TickTimer. Cuenta ticks de simulación: termina en el mismo
    ///    momento en todos los ordenadores, aunque cada uno tenga un reloj distinto.
    /// </summary>
    public class GameFlow : NetworkBehaviour, IStateAuthorityChanged, IPlayerJoined, IPlayerLeft
    {
        public enum GameState
        {
            Waiting,    // Sala de espera: cada jugador pulsa R cuando está listo
            Countdown,  // Todos listos: 3, 2, 1...
            Playing,    // ¡A jugar! (salen los pollos)
            Victory,    // Superamos todas las oleadas
            Defeat,     // Cayó el castillo
        }

        [Header("Configuración")]
        [Tooltip("Segundos de cuenta atrás cuando todos están listos")]
        public float CountdownSeconds = 3f;

        // ------------------------------------------------------------------
        // ESTADO DE RED: lo ESCRIBE solo el master, lo LEEN todos.
        // ------------------------------------------------------------------

        /// <summary>Fase actual de la partida.</summary>
        [Networked, OnChangedRender(nameof(OnStateChanged))]
        public GameState State { get; set; }

        /// <summary>Temporizador de la cuenta atrás (solo corre en la fase Countdown).</summary>
        [Networked]
        public TickTimer Countdown { get; set; }

        // ------------------------------------------------------------------
        // EVENTOS LOCALES: la interfaz (UIGameFlow) se suscribe a ellos.
        // No viajan por la red: los disparan los RPC y OnStateChanged en cada ordenador.
        // ------------------------------------------------------------------
        public Action<string> Announced;        // cartel grande en el centro
        public Action<string> Notified;         // línea pequeña ("Antonio cogió la ametralladora")
        public Action<GameState> StateChanged;  // cambió la fase

        public bool IsNetworkReady => Object != null && Object.IsValid;

        /// <summary>Segundos que le quedan a la cuenta atrás (para la interfaz).</summary>
        public int CountdownSecondsLeft => Mathf.CeilToInt(Countdown.RemainingTime(Runner) ?? 0f);

        // RESERVA CLASE 4 · Puntos de cada jugador, por nombre. Los escribe el master; los lee
        // el jugador que vuelve después de caerse. Es ESTADO: sigue ahí aunque el jugador se vaya.
        [Networked, Capacity(16)]
        public NetworkDictionary<NetworkString<_32>, int> SavedScores => default;
        private Castle _castle;

        public override void Spawned()
        {
            _castle = FindFirstObjectByType<Castle>();
            _lastMaster = Object.StateAuthority;
        }

        public override void FixedUpdateNetwork()
        {
            // 1) SOLO EL DUEÑO DECIDE.
            //    (En Shared Mode, FixedUpdateNetwork ya solo corre en el State Authority,
            //    pero lo dejamos escrito porque es LA idea de toda esta clase.)
            if (HasStateAuthority == false)
                return;

            // RESERVA CLASE 4 · El master apunta los puntos de todos, por nombre
            SaveScores();

            switch (State)
            {
                case GameState.Waiting:
                    // 2) ¿Están todos listos? -> arranca la cuenta atrás
                    if (AreAllPlayersReady())
                    {
                        Countdown = TickTimer.CreateFromSeconds(Runner, CountdownSeconds);
                        State = GameState.Countdown;

                        // CLASE 4 · RECONEXIÓN. Política de sala durante la partida:
                    //  · IsVisible = false -> desaparece de la lista: no entran desconocidos.
                    //  · IsOpen sigue en true -> quien se cae PUEDE VOLVER escribiendo el nombre de la sala.
                    // (En la clase 3 cerrábamos también IsOpen: así nadie podía reconectar.)
                    // Solo el master puede cambiar estas propiedades de la sala.
                    // Runner.SessionInfo.IsOpen = false;
                    Runner.SessionInfo.IsVisible = false;
                    }
                    break;

                case GameState.Countdown:
                    // 3) Se acabó la cuenta atrás -> ¡a jugar!
                    if (Countdown.Expired(Runner))
                    {
                        Countdown = default;
                        State = GameState.Playing;

                        // Un EVENTO para todos los que están conectados ahora mismo
                        RPC_Announce("¡A defender el castillo!");
                    }
                    break;

                case GameState.Playing:
                    // 4) Si el castillo cae, perdemos
                    if (_castle != null && _castle.IsDestroyed)
                    {
                        State = GameState.Defeat;
                    }
                    break;
            }
        }

        /// <summary>
        /// Lo llama el WaveManager (en el master) cuando se supera la última oleada.
        /// </summary>
        public void Win()
        {
            if (HasStateAuthority && State == GameState.Playing)
            {
                State = GameState.Victory;
            }
        }

        // ------------------------------------------------------------------
        // RPCs: EVENTOS. Solo el master los envía (RpcSources.StateAuthority)
        // y les llegan a todos (RpcTargets.All), también al propio master.
        // ------------------------------------------------------------------

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_Announce(string message)
        {
            Announced?.Invoke(message);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_Notify(string message)
        {
            Notified?.Invoke(message);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Recorre los jugadores de la sala y mira su [Networked] IsReady.
        /// El master LEE el estado de los demás: no hace falta que nadie le mande nada.
        /// </summary>
        private bool AreAllPlayersReady()
        {
            int playerCount = 0;

            foreach (var playerRef in Runner.ActivePlayers)
            {
                var playerObject = Runner.GetPlayerObject(playerRef);
                if (playerObject == null)
                    return false; // Este jugador aún está cargando

                var player = playerObject.GetComponent<Player>();
                if (player == null || player.IsReady == false)
                    return false;

                playerCount++;
            }

            return playerCount > 0;
        }

        // ------------------------------------------------------------------
        // CLASE 4 · DESCONEXIONES
        // Fusion nos avisa de estas cosas EN CADA ORDENADOR. No hace falta ningún RPC:
        // cada uno se entera solo y pinta su aviso.
        // ------------------------------------------------------------------

        // Cuando alguien se va, su Player ya no existe: apuntamos los nombres mientras están
        private readonly Dictionary<PlayerRef, string> _names = new Dictionary<PlayerRef, string>();
        private PlayerRef _lastMaster;

        /// <summary>
        /// IStateAuthorityChanged · Cambió el dueño de ESTE objeto. Como GameFlow es
        /// "Is Master Client Object", eso solo pasa cuando cambia el master.
        /// </summary>
        public void StateAuthorityChanged()
        {
            var newMaster = Object.StateAuthority;
            if (newMaster == _lastMaster)
                return;

            _lastMaster = newMaster;
            string you = newMaster == Runner.LocalPlayer ? " (¡eres tú!)" : "";
            Notified?.Invoke($"Nuevo master: <b>{GetName(newMaster)}</b>{you}");
            Debug.Log($"[Clase 4] Nuevo master: {GetName(newMaster)}. Ahora él mueve pollos, oleadas y castillo.");
        }

        /// <summary>IPlayerJoined · Alguien entró a la sala.</summary>
        public void PlayerJoined(PlayerRef player)
        {
            if (player != Runner.LocalPlayer)
                Notified?.Invoke("Ha entrado un jugador");
        }

        /// <summary>IPlayerLeft · Alguien se fue: cerró el juego, se le cayó internet...</summary>
        public void PlayerLeft(PlayerRef player)
        {
            Notified?.Invoke($"<b>{GetName(player)}</b> se ha ido");
            _names.Remove(player);
        }

        public override void Render()
        {
            // Apuntamos el nombre de cada jugador mientras está en la sala
            foreach (var playerRef in Runner.ActivePlayers)
            {
                var player = GetPlayer(playerRef);
                if (player != null && string.IsNullOrEmpty(player.Nickname) == false)
                {
                    _names[playerRef] = player.Nickname;
                }
            }
        }

        private string GetName(PlayerRef playerRef)
        {
            return _names.TryGetValue(playerRef, out var name) ? name : $"Jugador {playerRef.PlayerId}";
        }

        private Player GetPlayer(PlayerRef playerRef)
        {
            var playerObject = Runner.GetPlayerObject(playerRef);
            return playerObject != null ? playerObject.GetComponent<Player>() : null;
        }

        /// <summary>
        /// RESERVA CLASE 4 · El master apunta los puntos de cada jugador por su nombre.
        /// Si alguien se cae y vuelve con el mismo nombre, Player.TryRestoreScore los recupera.
        /// </summary>
        private void SaveScores()
        {
            foreach (var playerRef in Runner.ActivePlayers)
            {
                var player = GetPlayer(playerRef);
                if (player == null || string.IsNullOrEmpty(player.Nickname))
                    continue;

                // Recién llegado que aún no ha recuperado sus puntos: no le pisamos lo guardado
                if (player.ScoreChecked == false)
                    continue;

                // El diccionario tiene sitio para 16 nombres
                if (SavedScores.Count >= SavedScores.Capacity && SavedScores.ContainsKey(player.Nickname) == false)
                    continue;

                SavedScores.Set(player.Nickname, player.ChickenKills);
            }
        }
        private void OnStateChanged()
        {
            // Se ejecuta en TODOS los ordenadores cuando les llega la nueva fase
            StateChanged?.Invoke(State);
        }
    }
}
