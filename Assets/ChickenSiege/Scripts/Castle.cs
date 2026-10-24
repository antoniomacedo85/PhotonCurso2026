using Fusion;
using UnityEngine;

namespace Starter.Shooter
{
    /// <summary>
    /// CLASE 2 · El castillo que defienden los jugadores.
    /// Es un objeto de escena: su State Authority es el master client (marcar "Is Master Client Object"
    /// en el NetworkObject, igual que el ChickenSpawner), así si el master se va, otro jugador lo hereda.
    /// La vida la lleva el componente Health de la plantilla ([Networked] CurrentHealth + daño por RPC).
    /// </summary>
    public class Castle : NetworkBehaviour
    {
        [Header("References")]
        public Health Health;

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_Repair(int amount)
        {
            if (Health.IsAlive == false)
                return;

            Health.CurrentHealth = Mathf.Min(Health.CurrentHealth + amount, Health.InitialHealth);
        }

        public bool IsNetworkReady => Object != null && Object.IsValid;
        public bool IsDestroyed => IsNetworkReady && Health.IsAlive == false;
    }
    

    
}
