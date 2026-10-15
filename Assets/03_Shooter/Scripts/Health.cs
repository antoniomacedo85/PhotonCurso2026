using System;
using Fusion;
using UnityEngine;

namespace Starter.Shooter
{
    /// <summary>
    /// A common component that represents entity health.
    /// It is used for both players and chickens.
    /// </summary>
    public class Health : NetworkBehaviour
    {
        [Header("Setup")]
        public int InitialHealth = 3;
        public float DeathTime;

        [Header("References")]
        public GameObject VisualRoot;
        public GameObject DeathRoot;

        public Action HitReceived;
        public Action<Health> Killed;

        public bool IsAlive => CurrentHealth > 0;
        public bool IsFinished => CurrentHealth <= 0 && _deathCooldown.Expired(Runner);

        [Networked]
        public int CurrentHealth { get; set; }

        [Networked]
        private int _networkHits { get; set; }
        [Networked]
        private TickTimer _deathCooldown { get; set; }

        private int _localHits;

        public void TakeHit(int damage, bool reportKill = false)
        {
            if (IsAlive == false)
                return;

            RPC_TakeHit(damage, reportKill);

            if (HasStateAuthority == false)
            {
                // To have responsive hit reactions on all clients,
                // hit received is invoked right away.
                HitReceived?.Invoke();
                _localHits++;
            }
        }

        public void Revive()
        {
            CurrentHealth = InitialHealth;
            _deathCooldown = default;
        }

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                // Set initial health
                Revive();
            }

            // Synchronize local value with the networked one.
            _localHits = _networkHits;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            Killed = null;
        }

        public override void Render()
        {
            VisualRoot.SetActive(IsAlive && IsAliveInterpolated());
            DeathRoot.SetActive(IsAlive == false);

            // Check if hit should be shown
            if (_localHits < _networkHits)
            {
                HitReceived?.Invoke();
                _localHits = _networkHits;
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_TakeHit(int damage, bool reportKill = false, RpcInfo info = default)
        {
            if (IsAlive == false)
                return;

            CurrentHealth -= damage;
            _networkHits++;

            if (IsAlive == false)
            {
                // Entity died, let's start death cooldown
                CurrentHealth = 0;
                _deathCooldown = TickTimer.CreateFromSeconds(Runner, DeathTime);

                if (reportKill)
                {
                    // We are using targeted RPC to send kill confirmation
                    // only to the killer client
                    RPC_KilledBy(info.Source);
                }
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_KilledBy([RpcTarget] PlayerRef playerRef)
        {
            Killed?.Invoke(this);
        }

        private bool IsAliveInterpolated()
        {
            // We use interpolated value when checking if object should be made visible in Render.
            // This helps with showing player visual at the correct position right away after respawn
            // (= player won't be visible before KCC teleport that is interpolated as well).
            var interpolator = new NetworkBehaviourBufferInterpolator(this);
            return interpolator.Int(nameof(CurrentHealth)) > 0;
        }
    }
}