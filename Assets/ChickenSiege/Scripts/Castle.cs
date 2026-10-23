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

        [Header("Ejercicio 2 · Temblor al recibir daño")]
        [Tooltip("Hijo con el modelo del castillo; es lo que tiembla")]
        public Transform ShakeRoot;
        public float ShakeDuration = 0.3f;
        public float ShakeIntensity = 0.3f;

        [Header("Chicken Siege · Sonidos")]
        [Tooltip("Suena cada vez que un pollo golpea el castillo")]
        public AudioClip HitClip;
        [Tooltip("Suena una vez cuando el castillo cae")]
        public AudioClip DestroyedClip;

        private float _shakeTimeLeft;
        private Vector3 _shakeRootStartPosition;
        private AudioSource _audio;
        private bool _wasDestroyed;
        private bool _soundReady;

        public bool IsNetworkReady => Object != null && Object.IsValid;
        public bool IsDestroyed => IsNetworkReady && Health.IsAlive == false;

        private void Awake()
        {
            // EJERCICIO 2 (solución): HitReceived es un evento LOCAL de Health.
            // Salta en Health.Render cuando sube el contador de golpes [Networked]:
            // en el master al instante y en el resto cuando les llega por red.
            Health.HitReceived += OnHitReceived;

            if (ShakeRoot != null)
            {
                _shakeRootStartPosition = ShakeRoot.localPosition;
            }

            // Una fuente de audio en el castillo: se oye desde lejos, algo más fuerte cerca
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0.6f;
            _audio.rolloffMode = AudioRolloffMode.Linear;
            _audio.minDistance = 20f;
            _audio.maxDistance = 200f;
        }

        public override void Render()
        {
            // Sonido de derrota: solo en el momento en que cae (no al entrar en una partida ya perdida)
            bool destroyed = IsDestroyed;
            if (_soundReady && destroyed && _wasDestroyed == false && DestroyedClip != null)
            {
                _audio.PlayOneShot(DestroyedClip);
            }
            _wasDestroyed = destroyed;
            _soundReady = true;

            if (ShakeRoot == null)
                return;

            if (_shakeTimeLeft > 0f)
            {
                _shakeTimeLeft -= Time.deltaTime;
                float strength = ShakeIntensity * (_shakeTimeLeft / ShakeDuration);
                ShakeRoot.localPosition = _shakeRootStartPosition + Random.insideUnitSphere * strength;
            }
            else
            {
                ShakeRoot.localPosition = _shakeRootStartPosition;
            }
        }

        private void OnHitReceived()
        {
            _shakeTimeLeft = ShakeDuration;

            if (HitClip != null && IsDestroyed == false)
            {
                _audio.pitch = Random.Range(0.9f, 1.1f);
                _audio.PlayOneShot(HitClip);
            }
        }
    }
}
