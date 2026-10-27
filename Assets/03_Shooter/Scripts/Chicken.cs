using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Starter.Shooter
{
    /// <summary>
    /// Pollo volador (plantilla del Starter + Chicken Siege).
    ///
    /// CLASE 3 · CICLO DE VIDA COMPLETO
    ///  · Nace con Runner.Spawn (lo crea el WaveManager en el master) y se configura con Init()
    ///    ANTES de existir para los demás (onBeforeSpawned).
    ///  · Spawned() y Despawned() se ejecutan en TODOS los ordenadores: los usamos para
    ///    mantener la lista Chicken.All (cuántos pollos hay) en cada uno.
    ///  · Cuando termina su animación de muerte, su dueño lo quita de la red con Runner.Despawn.
    ///
    /// CLASE 3 · TIPOS DE POLLO
    ///  Speed, Points, DamageToCastle, IsBoss y Tint se ajustan en cada PREFAB VARIANT
    ///  (Chicken_Fast, Chicken_Giant, Chicken_Boss). La vida está en el componente Health.
    /// </summary>
    public class Chicken : NetworkBehaviour
    {
        [Header("References")]
        public Health Health;
        public Transform ScalingRoot;
        public NetworkTransform NetworkTransform;
        public ParticleSystem FlyParticles;

        [Header("Chicken Siege · Tipo de pollo")]
        [Tooltip("Velocidad media de vuelo (el WaveManager le suma un poco de azar y la dificultad)")]
        public float Speed = 6f;
        [Tooltip("Puntos para quien lo abate")]
        public int Points = 1;
        [Tooltip("Daño que hace al castillo cuando choca contra él")]
        public int DamageToCastle = 10;
        [Tooltip("Marca al jefe: la interfaz muestra su barra de vida")]
        public bool IsBoss;
        [Tooltip("Color del plumaje (blanco = el original)")]
        public Color Tint = Color.white;

        [Header("Chicken Siege · Sonidos")]
        [Tooltip("Cacareo cuando recibe un disparo que no lo mata (la muerte suena con DeathSound, dentro de DeathRoot)")]
        public AudioClip HitClip;
        [Tooltip("Suena en todas las pantallas cuando muere un pollo dorado")]
        public AudioClip GoldenDeathClip;

        [Header("Chicken Siege · Pollo dorado")]
        public Color GoldenColor = new Color(1f, 0.78f, 0.15f);

        /// <summary>
        /// CLASE 3 · Todos los pollos que existen EN ESTE ORDENADOR.
        /// No es [Networked]: cada ordenador la rellena solo en Spawned/Despawned.
        /// </summary>
        public static readonly List<Chicken> All = new List<Chicken>();

        // Lo decide el master al crearlo y todos lo pintan en OnGoldenChanged
        [Networked, OnChangedRender(nameof(OnGoldenChanged))]
        public NetworkBool IsGolden { get; set; }

        // Solo los usa el master, pero son [Networked] para que funcionen si cambia el master
        [Networked]
        private Vector3 _startPosition { get; set; }
        [Networked]
        private float _speed { get; set; }
        [Networked]
        private float _maxTravelDistance { get; set; }

        private bool _wasAlive = true;
        private Renderer[] _renderers;
        private Color[] _originalColors;

        /// <summary>Cuántos pollos quedan vivos (para saber si la oleada terminó).</summary>
        public static int CountAlive()
        {
            int count = 0;
            foreach (var chicken in All)
            {
                if (chicken.Health.IsAlive)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// CLASE 3 · Configura un pollo recién creado. Se llama en onBeforeSpawned:
        /// el pollo aún no existe para los demás, así que les llegará ya configurado.
        /// </summary>
        public void Init(float speed, float maxTravelDistance, bool golden)
        {
            _startPosition = transform.position;
            _speed = speed;
            _maxTravelDistance = maxTravelDistance;
            IsGolden = golden;
        }

        /// <summary>Plantilla original: reciclar un pollo (lo usa el ChickenSpawner antiguo).</summary>
        public void Respawn(Vector3 position, Quaternion rotation, float speed, float maxTravelDistance)
        {
            Health.Revive();

            _startPosition = position;
            _speed = speed;
            _maxTravelDistance = maxTravelDistance;

            NetworkTransform.Teleport(position, rotation);
        }

        public override void Spawned()
        {
            // CLASE 3 · Se ejecuta en TODOS los ordenadores (también en quien entra tarde)
            All.Add(this);

            // Quien entra tarde también ve los pollos dorados que ya existen
            OnGoldenChanged();
            _wasAlive = Health.IsAlive;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            // CLASE 3 · También en TODOS los ordenadores: lo quitamos de la lista
            All.Remove(this);
        }

        private void OnDestroy()
        {
            // Por si se cierra la partida de golpe (volver al menú)
            All.Remove(this);
        }

        public override void FixedUpdateNetwork()
        {
            // CLASE 3 · Ya terminó su animación de muerte: el dueño lo quita de la red.
            // Despawned() se ejecutará en todos los ordenadores.
            if (Health.IsFinished)
            {
                Runner.Despawn(Object);
                return;
            }


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
            transform.Translate(Vector3.forward * _speed * Runner.DeltaTime, Space.Self);
        }

        public override void Render()
        {
            var emission = FlyParticles.emission;
            emission.enabled = Health.IsAlive;

            // Sonido especial al morir un pollo dorado (en todas las pantallas)
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

        private void OnGoldenChanged()
        {
            if (_renderers == null)
            {
                // Solo mallas del modelo (no las partículas)
                var all = Health.VisualRoot.GetComponentsInChildren<Renderer>(true);
                var list = new List<Renderer>();
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

            // Dorado, o el color de su tipo (Tint) sobre el original
            for (int i = 0; i < _renderers.Length; i++)
            {
                SetColor(_renderers[i].material, IsGolden ? GoldenColor : _originalColors[i] * Tint);
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

            // Un pollo ya muerto no vuelve a hacer daño
            if (Health.IsAlive == false)
                return;

            // Si chocamos contra el castillo, le hacemos daño.
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

            // Cacareo si el golpe no lo mata.
            // Si lo mata, suena DeathSound al activarse DeathRoot (así no suena doble).
            // Quien dispara recibe este evento antes de que baje la vida: con 1 de vida, el disparo mata.
            bool killed = Health.IsAlive == false || Health.CurrentHealth <= 1;
            if (killed == false)
            {
                PlaySound(HitClip, 0.8f, Random.Range(1.1f, 1.35f), 0.8f);
            }
        }

        private void PlaySound(AudioClip clip, float volume, float pitch, float spatialBlend)
        {
            if (clip == null)
                return;

            // Fuente temporal en la posición del pollo: el pollo puede desaparecer antes de que acabe el sonido
            var soundObject = new GameObject("ChickenSound");
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
