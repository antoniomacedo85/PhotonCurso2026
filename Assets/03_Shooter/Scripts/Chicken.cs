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

        // EJERCICIO 1 (solución): pollo dorado. Lo decide el master en ChickenSpawner.Respawn
        // y todos lo pintan en OnGoldenChanged.
        [Networked, OnChangedRender(nameof(OnGoldenChanged))]
        public NetworkBool IsGolden { get; set; }

        public Color GoldenColor = new Color(1f, 0.78f, 0.15f);
        [Tooltip("Suena en todas las pantallas cuando muere un pollo dorado")]
        public AudioClip GoldenDeathClip;

        private bool _wasAlive = true;

        private Renderer[] _renderers;
        private Color[] _originalColors;

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

            // CHICKEN SIEGE: sonido especial al morir un pollo dorado (en todas las pantallas)
            bool alive = Health.IsAlive;
            if (_wasAlive && alive == false && IsGolden)
            {
                PlaySound(GoldenDeathClip, 1f, 1.1f, 0.5f);
            }
            _wasAlive = alive;

            // Scale back to default size after hit.
            ScalingRoot.localScale = Vector3.Lerp(ScalingRoot.localScale, Vector3.one, Time.deltaTime * 2f);
        }

        private void Awake()
        {
            Health.HitReceived += OnHitReceived;
        }

        public override void Spawned()
        {
            // Quien entra tarde también ve los pollos dorados que ya existen
            OnGoldenChanged();
            _wasAlive = Health.IsAlive;
        }

        private void OnGoldenChanged()
        {
            if (_renderers == null)
            {
                // Solo mallas del modelo (no las partículas)
                var all = Health.VisualRoot.GetComponentsInChildren<Renderer>(true);
                var list = new System.Collections.Generic.List<Renderer>();
                foreach (var r in all)
                {
                    if (r is MeshRenderer || r is SkinnedMeshRenderer)
                        list.Add(r);
                }

                _renderers = list.ToArray();
                _originalColors = new Color[_renderers.Length];
                for (int i = 0; i < _renderers.Length; i++)
                {
                    _originalColors[i] = GetColor(_renderers[i].material);
                }
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                SetColor(_renderers[i].material, IsGolden ? GoldenColor : _originalColors[i]);
            }
        }

        private static Color GetColor(Material material)
        {
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color")) return material.GetColor("_Color");
            return Color.white;
        }

        private static void SetColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
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
            PlaySound(HitClip, 0.8f, UnityEngine.Random.Range(1.1f, 1.35f), 0.8f);
        }

        private void PlaySound(AudioClip clip, float volume, float pitch, float spatialBlend)
        {
            if (clip == null)
                return;

            // Fuente temporal en la posición del pollo: el pollo puede reaparecer lejos antes de que acabe el sonido
            var soundObject = new GameObject("ChickenHitSound");
            soundObject.transform.position = transform.position;

            var source = soundObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.spatialBlend = spatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 10f;
            source.maxDistance = 150f;
            source.Play();

            Destroy(soundObject, clip.length / source.pitch + 0.1f);
        }
    }
}