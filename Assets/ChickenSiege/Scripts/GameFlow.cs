using System;
using Fusion;
using UnityEngine;

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
    public class GameFlow : NetworkBehaviour
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

        private Castle _castle;

        public override void Spawned()
        {
            _castle = FindFirstObjectByType<Castle>();
        }

        public override void FixedUpdateNetwork()
        {
            // 1) SOLO EL DUEÑO DECIDE.
            //    (En Shared Mode, FixedUpdateNetwork ya solo corre en el State Authority,
            //    pero lo dejamos escrito porque es LA idea de toda esta clase.)
            if (HasStateAuthority == false)
                return;

            switch (State)
            {
                case GameState.Waiting:
                    // 2) ¿Están todos listos? -> arranca la cuenta atrás
                    if (AreAllPlayersReady())
                    {
                        Countdown = TickTimer.CreateFromSeconds(Runner, CountdownSeconds);
                        State = GameState.Countdown;

                        // PASO 5 · DESCOMENTAD estas dos líneas después de la demo del que entra tarde:
                        // cerramos la sala para que nadie aparezca en mitad de una oleada.
                        // Solo el master puede cambiar estas propiedades de la sala.
                        Runner.SessionInfo.IsOpen = false;
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

        private void OnStateChanged()
        {
            // Se ejecuta en TODOS los ordenadores cuando les llega la nueva fase
            StateChanged?.Invoke(State);
        }
    }
}
