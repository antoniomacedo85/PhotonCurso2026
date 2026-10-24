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
        public int DamageToCastle = 10;

        // Properties are used only on master client however it needs
        // to be networked to properly work when master client is changed.
        [Networked]
        private Vector3 _startPosition { get; set; }
        [Networked]
        private float _speed { get; set; }
        [Networked]
        private float _maxTravelDistance { get; set; }

        [Networked, OnChangedRender(nameof(OnGoldenChanged))]
        public NetworkBool IsGolden { get; set; }

        public Color GoldenColor = new Color(1f, 0.78f, 0.15f);

        private Renderer[] _renderers;
        private Color[] _originalColors;

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

        public override void Spawned()
        {
            // Quien entra tarde también ve los dorados que ya existen
            OnGoldenChanged();
        }

        private void OnGoldenChanged()
        {
            // La primera vez guardamos las mallas del pollo y su color original
            if (_renderers == null)
            {
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
                    _originalColors[i] = GetColor(_renderers[i].material);
            }

            // Dorado o su color de siempre
            for (int i = 0; i < _renderers.Length; i++)
                SetColor(_renderers[i].material, IsGolden ? GoldenColor : _originalColors[i]);
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

        

        private void OnHitReceived()
        {
            ScalingRoot.localScale = new Vector3(1.4f, 1.4f, 1.4f);
        }

        private void OnTriggerEnter(Collider other)
        {
            // Chickens are destroyed only on the state authority
            if (HasStateAuthority == false)
                return;

            // Un pollo ya muerto no vuelve a hacer daño
            if (Health.IsAlive == false)
                return;

            // Si chocamos contra el castillo, le hacemos daño (RPC al dueño del castillo)
            var castle = other.GetComponentInParent<Castle>();
            if (castle != null)
            {
                castle.Health.TakeHit(DamageToCastle);
            }

            Health.TakeHit(1000);
        }
    }
}