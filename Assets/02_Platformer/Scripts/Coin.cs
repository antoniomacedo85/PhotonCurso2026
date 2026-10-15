using System;
using Fusion;
using UnityEngine;

namespace Starter.Platformer
{
    /// <summary>
    /// Coin object that can be picked up by player.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Coin : NetworkBehaviour
    {
        [Header("Setup")]
        public float RefreshTime = 4f;

        [Header("References")]
        public Collider Trigger;
        public GameObject VisualRoot;
        public ParticleSystem Particles;

        public Action CoinCollected;

        public bool IsActive => _activationCooldown.ExpiredOrNotRunning(Runner);

        [Networked]
        private TickTimer _activationCooldown { get; set; }

        public void RequestCollect()
        {
            if (IsActive == false)
                return;

            RPC_RequestCollect();

            // Even clients without authority will temporarily modify (predict) this networked property to have
            // immediate visual feedback (Render method makes sure the coin disappears and particle is stopped)
            _activationCooldown = TickTimer.CreateFromSeconds(Runner, RefreshTime);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            CoinCollected = null;
        }

        public override void Render()
        {
            bool isActive = IsActive;

            Trigger.enabled = isActive;

            // Show/hide coin visual
            VisualRoot.SetActive(isActive);

            // Start/stop particles emission
            var emission = Particles.emission;
            emission.enabled = isActive;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestCollect(RpcInfo info = default)
        {
            if (IsActive == false)
                return;

            _activationCooldown = TickTimer.CreateFromSeconds(Runner, RefreshTime);

            // We are using targeted RPC to send
            // collection message only to the right client (player)
            RPC_CoinCollected(info.Source);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_CoinCollected([RpcTarget] PlayerRef playerRef)
        {
            CoinCollected?.Invoke();
        }
    }
}