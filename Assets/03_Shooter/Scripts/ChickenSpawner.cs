using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Starter.Shooter
{
    /// <summary>
    /// Spawns and moves flying chickens in the environment.
    /// Notice that nothing in this spawner actually needs to be networked
    /// as all the logic is on the state authority only.
    /// </summary>
    public class ChickenSpawner : NetworkBehaviour
    {
        public Chicken ChickenPrefab;
        public int ChickenCount = 20;
        public float SpawnRadius = 50f;
        public float SpawnHeightMin = 0f;
        public float SpawnHeightMax = 20f;
        public float SpeedMin = 5f;
        public float SpeedMax = 15f;
        public float DirectionDispersion = 10f;

        [Header("Chicken Siege")]
        [Tooltip("EJERCICIO 1: probabilidad de que un pollo salga dorado (vale 5 puntos)")]
        [Range(0f, 1f)]
        public float GoldenChance = 0.05f;
        [Tooltip("Collider al que apuntan los pollos (el del castillo). Vacío = el centro del spawner con dispersión")]
        public Collider Target;

        [Networked, Capacity(64)]
        private NetworkArray<Chicken> _chickens { get; }

        // CLASE 2: multiplicadores según la dificultad de la sala
        private float _speedMultiplier = 1f;
        private float _healthMultiplier = 1f;

        public override void Spawned()
        {
            // CLASE 2: leemos la propiedad de sala "Dificultad".
            // Se calcula en todos los clientes, así un nuevo master client ya la tiene.
            int difficulty = Difficulty.FromSession(Runner.SessionInfo);
            _speedMultiplier = Difficulty.SpeedMultiplier(difficulty);
            _healthMultiplier = Difficulty.HealthMultiplier(difficulty);

            if (HasStateAuthority == false)
                return;

            // On start just show all chickens
            for (int i = 0; i < ChickenCount; i++)
            {
                var chicken = Runner.Spawn(ChickenPrefab, Vector3.zero, Quaternion.identity);
                _chickens.Set(i, chicken);

                Respawn(chicken);
            }
        }

        public override void FixedUpdateNetwork()
        {
            for (int i = 0; i < _chickens.Length; i++)
            {
                var chicken = _chickens[i];

                if (chicken == null)
                    break;

                if (chicken.Health.IsFinished)
                {
                    Respawn(chicken);
                }
            }
        }

        private void Respawn(Chicken chicken)
        {
            var circlePosition = Random.insideUnitCircle.normalized * SpawnRadius;
            var position = new Vector3(circlePosition.x, Random.Range(SpawnHeightMin, SpawnHeightMax), circlePosition.y);

            Quaternion rotation;
            if (Target != null)
            {
                // CHICKEN SIEGE: apuntamos a un punto al azar dentro del collider del castillo.
                // Así siempre le pegan, aunque cambie el tamaño o la posición del castillo.
                var bounds = Target.bounds;
                var aimPoint = bounds.center + Vector3.Scale(Random.insideUnitSphere, bounds.extents * 0.6f);
                rotation = Quaternion.LookRotation(aimPoint - position);
            }
            else
            {
                var rotationToCenter = Quaternion.LookRotation(transform.position - position);
                var randomDispersion = Random.insideUnitSphere * DirectionDispersion;
                rotation = Quaternion.Euler(0f, randomDispersion.y, randomDispersion.z) * rotationToCenter;
            }

            float speed = Random.Range(SpeedMin, SpeedMax) * _speedMultiplier;

            // CLASE 2: más vida en Difícil (Revive usa InitialHealth)
            chicken.Health.InitialHealth = Mathf.CeilToInt(ChickenPrefab.Health.InitialHealth * _healthMultiplier);

            // EJERCICIO 1 (solución): el master decide si sale dorado; todos lo ven por OnChangedRender
            chicken.IsGolden = Random.value < GoldenChance;

            chicken.Respawn(position, rotation, speed, SpawnRadius * 2.5f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position, SpawnRadius);
        }
    }
}