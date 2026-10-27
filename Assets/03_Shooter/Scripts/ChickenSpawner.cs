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

        // CLASE 3 · Los pollos esperan a que la partida esté en juego
        private GameFlow _gameFlow;

        [Range(0f, 1f)]
        public float GoldenChance = 0.15f;   // 5 % de pollos dorados

        [Networked, Capacity(64)]
        private NetworkArray<Chicken> _chickens { get; }

        // CLASE 3 · CALENTAMIENTO: multiplicadores según la dificultad de la sala
        private float _speedMultiplier = 1f;
        private float _healthMultiplier = 1f;

        public override void Spawned()
        {   
            // Leemos la propiedad de sala "Dificultad" (la puso quien creó la sala).
            // La calculamos en TODOS los clientes: si el master se va, el nuevo master ya la tiene.
            int difficulty = Difficulty.FromSession(Runner.SessionInfo);
            _speedMultiplier = Difficulty.SpeedMultiplier(difficulty);
            _healthMultiplier = Difficulty.HealthMultiplier(difficulty);

            _gameFlow = FindFirstObjectByType<GameFlow>();
        }

        private void SpawnChickens()
        {
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
            if (HasStateAuthority == false)
                return;

            // CLASE 3 · Hasta que la partida no esté "Jugando", no hay pollos
            if (_gameFlow != null && _gameFlow.State != GameFlow.GameState.Playing)
                return;

            // CLASE 3 · La primera vez que se juega, creamos los pollos (antes se hacía en Spawned)
            if (_chickens[0] == null)
            {
                SpawnChickens();
            }
            
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

            var rotationToCenter = Quaternion.LookRotation(transform.position - position);
            var randomDispersion = Random.insideUnitSphere * DirectionDispersion;
            var rotation = Quaternion.Euler(0f, randomDispersion.y, randomDispersion.z) * rotationToCenter;

            // Más rápidos en Difícil, más lentos en Fácil
            float speed = Random.Range(SpeedMin, SpeedMax) * _speedMultiplier;

            // Más vida en Difícil (Revive usa InitialHealth)
            chicken.Health.InitialHealth = Mathf.CeilToInt(ChickenPrefab.Health.InitialHealth * _healthMultiplier);

            chicken.IsGolden = Random.value < GoldenChance;

            chicken.Respawn(position, rotation, speed, SpawnRadius * 2.5f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position, SpawnRadius);
        }
    }
}