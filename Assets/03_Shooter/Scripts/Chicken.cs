using System;
using Fusion;
using UnityEngine;

namespace Starter.Shooter
{
    /// <summary>
    /// Main script representing flying chicken enemies.
    /// </summary>
    public class Chicken : NetworkBehaviour
    {
        [Header("References")]
        public Health Health;
        public Transform ScalingRoot;
        public NetworkTransform NetworkTransform;
        public ParticleSystem FlyParticles;

        [Header("Chicken Siege")]
        [Tooltip("Daño que hace al castillo cuando choca contra él")]
        public int DamageToCastle = 10;
        [Tooltip("Cacareo cuando recibe un disparo que no lo mata (la muerte suena con DeathSound, dentro de DeathRoot)")]
        public AudioClip HitClip;

        // Properties are used only on master client however it needs
        // to be networked to properly work when master client is changed.
        [Networked]
        private Vector3 _startPosition { get; set; }
        [Networked]
        private float _speed { get; set; }
        [Networked]
        private float _maxTravelDistance { get; set; }

        public void Respawn(Vector3 position, Quaternion rotation, float speed, float maxTravelDistance)
        {
            Health.Revive();

            _startPosition = position;
            _speed = speed;
            _maxTravelDistance = maxTravelDistance;

            NetworkTransform.Teleport(position, rotation);
        }

        public override void FixedUpdateNetwork()
        {
            if (Health.IsAlive == false)
                return;

            if (Vector3.Distance(_startPosition, transform.position) > _maxTravelDistance)
            {
                // Chicken is too far, kill itself
                Health.TakeHit(1000);
                return;
            }

            // Move the chicken.
            // The chicken position and rotation is synchronized to all clients via NetworkTransform component.
            // Note: There is also a much more bandwidth efficient way when only start move parameters
            // are synchronized over the network and no NetworkTransform is necessary. It is a bit out of the scope
            // of this starter sample so check Projectile Essentials where same approach is explained for projectiles.
            transform.Translate(Vector3.forward * _speed * Runner.DeltaTime, Space.Self);
        }

        public override void Render()
        {
            var emission = FlyParticles.emission;
            emission.enabled = Health.IsAlive;

            // Scale back to default size after hit.
            ScalingRoot.localScale = Vector3.Lerp(ScalingRoot.localScale, Vector3.one, Time.deltaTime * 2f);
        }

        private void Awake()
        {
            Health.HitReceived += OnHitReceived;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Chickens are destroyed only on the state authority
            if (HasStateAuthority == false)
                return;

            // CLASE 2: un pollo ya muerto no vuelve a hacer daño
            if (Health.IsAlive == false)
                return;

            // CLASE 2: si chocamos contra el castillo, le hacemos daño.
            // TakeHit envía un RPC al dueño del castillo (el master client).
            var castle = other.GetComponentInParent<Castle>();
            if (castle != null)
            {
                castle.Health.TakeHit(DamageToCastle);
            }

            // Chicken collided, let's destroy it
            Health.TakeHit(1000);
        }

        private void OnHitReceived()
        {
            ScalingRoot.localScale = new Vector3(1.4f, 1.4f, 1.4f);

            // CHICKEN SIEGE: cacareo si el golpe no lo mata.
            // Si lo mata, suena DeathSound al activarse DeathRoot (así no suena doble).
            // Quien dispara recibe este evento antes de que baje la vida: con 1 de vida, el disparo mata.
            bool killed = Health.IsAlive == false || Health.CurrentHealth <= 1;
            if (killed == false)
            {
                PlayHitSound();
            }
        }

        private void PlayHitSound()
        {
            if (HitClip == null)
                return;

            // Fuente temporal en la posición del pollo: el pollo puede reaparecer lejos antes de que acabe el sonido
            var soundObject = new GameObject("ChickenHitSound");
            soundObject.transform.position = transform.position;

            var source = soundObject.AddComponent<AudioSource>();
            source.clip = HitClip;
            source.volume = 0.8f;
            source.pitch = UnityEngine.Random.Range(1.1f, 1.35f);
            source.spatialBlend = 0.8f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 10f;
            source.maxDistance = 150f;
            source.Play();

            Destroy(soundObject, HitClip.length / source.pitch + 0.1f);
        }
    }
}