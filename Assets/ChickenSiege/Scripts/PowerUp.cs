using System;
using Fusion;
using UnityEngine;

namespace Starter.Shooter
{
    public enum PowerUpType
    {
        MachineGun,     // Ametralladora: disparas 3 veces más rápido durante un rato
        Repair,         // Reparación: cura el castillo al instante
        DoublePoints,   // Puntos x2 durante un rato (EJERCICIO)
    }

    /// <summary>
    /// CLASE 3 · Power-up que se recoge al pasar por encima (como las monedas del Platformer).
    ///
    /// EL PROBLEMA: dos jugadores lo tocan en el mismo instante. Si cada ordenador decidiera
    /// por su cuenta, LOS DOS se lo quedarían.
    ///
    /// LA SOLUCIÓN: ARBITRAJE. Decide un único dueño.
    ///   1. El jugador que lo toca PIDE el power-up a su dueño (el master) -> RPC_RequestPickup
    ///   2. El master DECIDE: el primero que llega se lo lleva; el resto, nada.
    ///   3. El master AVISA SOLO AL GANADOR -> RPC_Collected con [RpcTarget]
    ///   4. El ganador aplica el efecto sobre lo SUYO (Player.OnPowerUpCollected)
    ///
    /// Es el mismo patrón que Assets/02_Platformer/Scripts/Coin.cs y que Health
    /// (RPC_TakeHit -> RPC_KilledBy). Mirad Coin.cs: casi idéntico.
    /// </summary>
    public class PowerUp : NetworkBehaviour
    {
        [Header("Tipo y efecto")]
        public PowerUpType Type;
        [Tooltip("Segundos que dura el efecto (Ametralladora y Puntos x2)")]
        public float Duration = 10f;
        [Tooltip("Vida que devuelve al castillo (Reparación)")]
        public int RepairAmount = 60;

        [Header("En el mapa")]
        [Tooltip("Segundos hasta que desaparece si nadie lo coge")]
        public float Lifetime = 40f;

        [Header("Referencias")]
        [Tooltip("Hijo con el modelo: gira, flota y se oculta al cogerlo")]
        public Transform VisualRoot;
        public Collider Trigger;
        [Tooltip("Suena en el ordenador de quien lo coge")]
        public AudioClip PickupClip;

        /// <summary>Lo asigna el jugador que lo pide. Solo se llama en SU ordenador.</summary>
        public Action<PowerUp> Collected;

        // ------------------------------------------------------------------
        // ESTADO DE RED
        // ------------------------------------------------------------------
        [Networked]
        private NetworkBool _isTaken { get; set; }
        [Networked]
        private TickTimer _despawnTimer { get; set; }

        public bool IsAvailable => Object != null && Object.IsValid && _isTaken == false;

        private bool _requested;   // local: para no pedirlo dos veces seguidas
        private GameFlow _gameFlow;
        private Vector3 _visualStartPosition;

        public override void Spawned()
        {
            _gameFlow = FindFirstObjectByType<GameFlow>();
            _visualStartPosition = VisualRoot.localPosition;

            if (HasStateAuthority)
            {
                // Si nadie lo coge, desaparece solo
                _despawnTimer = TickTimer.CreateFromSeconds(Runner, Lifetime);
            }
        }

        public override void FixedUpdateNetwork()
        {
            // Solo el dueño lo quita de la red (Despawn): al caducar o poco después de que lo cojan
            if (HasStateAuthority && _despawnTimer.Expired(Runner))
            {
                Runner.Despawn(Object);
            }
        }

        public override void Render()
        {
            // Todos lo ocultan en cuanto llega _isTaken = true
            bool visible = _isTaken == false;
            VisualRoot.gameObject.SetActive(visible);
            Trigger.enabled = visible;

            // Gira y flota (solo visual, cada ordenador a su ritmo)
            VisualRoot.localPosition = _visualStartPosition + new Vector3(0f, 0.25f * Mathf.Sin(Time.time * 2.5f), 0f);
            VisualRoot.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        }

        /// <summary>
        /// PASO 1 · Lo llama el jugador LOCAL que lo toca (Player.OnTriggerEnter).
        /// </summary>
        public void RequestPickup(Action<PowerUp> onCollected)
        {
            if (IsAvailable == false || _requested)
                return;

            _requested = true;
            Collected = onCollected;

            RPC_RequestPickup();
        }

        /// <summary>
        /// PASO 2 · Se ejecuta en el DUEÑO del power-up (el master). Él decide quién se lo lleva.
        /// Las peticiones le llegan de una en una: la primera gana.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestPickup(RpcInfo info = default)
        {
            // Ya se lo llevó otro (su petición llegó antes) -> no hacemos nada
            if (_isTaken)
                return;

            _isTaken = true;

            // Lo quitamos de la red en un momento: antes dejamos que llegue el aviso al ganador
            _despawnTimer = TickTimer.CreateFromSeconds(Runner, 1f);

            // PASO 3 · Avisamos SOLO al ganador (info.Source = quién envió la petición)
            RPC_Collected(info.Source);

            // Y una línea para todos: "Antonio cogió la ametralladora"
            if (_gameFlow != null)
            {
                _gameFlow.RPC_Notify($"{GetPlayerName(info.Source)} cogió {GetName(Type)}");
            }
        }

        /// <summary>
        /// PASO 4 · [RpcTarget]: este RPC solo se ejecuta en el ordenador del ganador.
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_Collected([RpcTarget] PlayerRef player)
        {
            if (PickupClip != null)
            {
                AudioSource.PlayClipAtPoint(PickupClip, transform.position, 1f);
            }

            Collected?.Invoke(this);
        }

        // ------------------------------------------------------------------

        public static string GetName(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.MachineGun: return "la AMETRALLADORA";
                case PowerUpType.Repair: return "una REPARACIÓN";
                case PowerUpType.DoublePoints: return "PUNTOS x2";
                default: return type.ToString();
            }
        }

        private string GetPlayerName(PlayerRef playerRef)
        {
            var playerObject = Runner.GetPlayerObject(playerRef);
            var player = playerObject != null ? playerObject.GetComponent<Player>() : null;

            if (player == null || string.IsNullOrEmpty(player.Nickname))
                return $"Jugador {playerRef.PlayerId}";

            return player.Nickname;
        }
    }
}
