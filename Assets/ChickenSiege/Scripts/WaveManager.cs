using Fusion;
using UnityEngine;

namespace Starter.Shooter
{
    /// <summary>
    /// CLASE 3 · Oleadas de pollos. Sustituye al ChickenSpawner de la plantilla.
    ///
    /// La plantilla RECICLABA 20 pollos: nunca morían de verdad, se teletransportaban.
    /// Aquí cada pollo NACE con Runner.Spawn y MUERE con Runner.Despawn: ciclo de vida completo.
    ///
    /// CONCEPTOS DE RED QUE ENSEÑA
    /// 1. SOLO EL MASTER CREA POLLOS. Objeto de escena con "Is Master Client Object".
    /// 2. Runner.Spawn + onBeforeSpawned: configuramos el pollo ANTES de que exista para los
    ///    demás. Les llega ya con su velocidad, su vida y su color dorado.
    /// 3. LA OLEADA Y LA PAUSA SON [Networked]. La interfaz de todos las lee, y si el master
    ///    se va, el nuevo master sabe por qué oleada íbamos.
    /// 4. Chicken.All (lista que rellenan Spawned/Despawned en cada ordenador) nos dice
    ///    cuántos pollos quedan vivos.
    /// 5. RPC_BossIncoming: un EVENTO para que todos oigan y vean la llegada del jefe.
    /// </summary>
    public class WaveManager : NetworkBehaviour
    {
        /// <summary>Cuántos pollos de cada tipo salen en una oleada.</summary>
        [System.Serializable]
        public class Wave
        {
            public int Normal;
            public int Fast;
            public int Giant;
            public bool Boss;
        }

        [Header("Oleadas: 6, con jefe en la 3 y en la 6")]
        public Wave[] Waves =
        {
            new Wave { Normal = 6 },
            new Wave { Normal = 8, Fast = 2 },
            new Wave { Normal = 6, Boss = true },
            new Wave { Normal = 10, Fast = 3, Giant = 1 },
            new Wave { Normal = 12, Fast = 4, Giant = 2 },
            new Wave { Fast = 4, Giant = 2, Boss = true },
        };

        [Header("Tipos de pollo (prefabs). Vacío = ese tipo no sale")]
        public Chicken NormalPrefab;
        [Tooltip("EJERCICIO: variante veloz")]
        public Chicken FastPrefab;
        public Chicken GiantPrefab;
        public Chicken BossPrefab;

        [Header("Dónde nacen y a dónde van")]
        [Tooltip("Collider del castillo: los pollos apuntan a un punto al azar dentro de él")]
        public Collider Target;
        [Tooltip("Distancia mínima al castillo a la que nacen")]
        public float SpawnRadius = 55f;
        [Tooltip("Distancia extra al azar: así no llegan todos a la vez")]
        public float SpawnRadiusExtra = 35f;
        public float SpawnHeightMin = 3f;
        public float SpawnHeightMax = 14f;
        [Range(0f, 1f)]
        [Tooltip("Probabilidad de que un pollo normal salga dorado")]
        public float GoldenChance = 0.05f;

        [Header("Pausa entre oleadas")]
        public float PauseSeconds = 8f;

        [Header("Power-ups (bloque 4): aparecen en cada pausa")]
        [Tooltip("Prefabs con NetworkObject + PowerUp. Vacío = no salen")]
        public NetworkObject[] PowerUpPrefabs;
        public int PowerUpsPerPause = 2;

        // ------------------------------------------------------------------
        // ESTADO DE RED: lo escribe el master, lo leen todos (UIWaves)
        // ------------------------------------------------------------------

        /// <summary>Oleada actual: 0 = aún no empezamos · 1..Waves.Length.</summary>
        [Networked]
        public int CurrentWave { get; set; }

        /// <summary>Temporizador de la pausa entre oleadas.</summary>
        [Networked]
        public TickTimer Pause { get; set; }

        // ------------------------------------------------------------------

        /// <summary>Evento local: lo dispara RPC_BossIncoming en todos los ordenadores.</summary>
        public System.Action BossIncoming;

        public bool IsNetworkReady => Object != null && Object.IsValid;
        public bool IsPaused => Pause.IsRunning;
        public int PauseSecondsLeft => Mathf.CeilToInt(Pause.RemainingTime(Runner) ?? 0f);

        private GameFlow _gameFlow;
        private SpawnPoint[] _spawnPoints;
        private int _difficulty;

        public override void Spawned()
        {
            _gameFlow = FindFirstObjectByType<GameFlow>();
            _spawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);

            // La dificultad la eligió quien creó la sala (propiedad de sala, como en el calentamiento)
            _difficulty = Difficulty.FromSession(Runner.SessionInfo);
        }

        public override void FixedUpdateNetwork()
        {
            // 1) Solo el master dirige las oleadas, y solo mientras se juega
            if (HasStateAuthority == false)
                return;

            if (_gameFlow == null || _gameFlow.State != GameFlow.GameState.Playing)
                return;

            // 2) Primera oleada
            if (CurrentWave == 0)
            {
                StartWave(1);
                return;
            }

            // 3) En pausa: esperamos a que termine y lanzamos la siguiente
            if (Pause.IsRunning)
            {
                if (Pause.Expired(Runner))
                {
                    Pause = default;
                    StartWave(CurrentWave + 1);
                }
                return;
            }

            // 4) Oleada en curso: mientras quede algún pollo vivo, seguimos
            if (Chicken.CountAlive() > 0)
                return;

            // 5) ¡Oleada superada! ¿Era la última?
            if (CurrentWave >= Waves.Length)
            {
                _gameFlow.Win();
                return;
            }

            _gameFlow.RPC_Announce($"¡Oleada {CurrentWave} superada!");
            Pause = TickTimer.CreateFromSeconds(Runner, PauseSeconds);

            SpawnPowerUps();
        }

        private void StartWave(int number)
        {
            CurrentWave = number;
            var wave = Waves[number - 1];

            SpawnChickens(NormalPrefab, ScaleByDifficulty(wave.Normal), canBeGolden: true);
            SpawnChickens(FastPrefab, ScaleByDifficulty(wave.Fast), canBeGolden: false);
            SpawnChickens(GiantPrefab, ScaleByDifficulty(wave.Giant), canBeGolden: false);

            if (wave.Boss && BossPrefab != null)
            {
                SpawnChickens(BossPrefab, 1, canBeGolden: false);

                // EVENTO para todos: cartel + bocina
                RPC_BossIncoming();
            }
            else
            {
                _gameFlow.RPC_Announce($"Oleada {number}");
            }
        }

        /// <summary>Fácil = menos pollos, Difícil = más (el jefe siempre es uno).</summary>
        private int ScaleByDifficulty(int count)
        {
            if (count <= 0)
                return 0;

            return Mathf.Max(1, Mathf.RoundToInt(count * Difficulty.CountMultiplier(_difficulty)));
        }

        private void SpawnChickens(Chicken prefab, int count, bool canBeGolden)
        {
            if (prefab == null)
                return;

            for (int i = 0; i < count; i++)
            {
                SpawnChicken(prefab, canBeGolden);
            }
        }

        private void SpawnChicken(Chicken prefab, bool canBeGolden)
        {
            // Dónde nace: en un anillo alrededor del castillo, a distinta distancia y altura
            Vector3 center = Target != null ? Target.bounds.center : transform.position;
            var circle = Random.insideUnitCircle.normalized * (SpawnRadius + Random.Range(0f, SpawnRadiusExtra));
            var position = new Vector3(center.x + circle.x, Random.Range(SpawnHeightMin, SpawnHeightMax), center.z + circle.y);

            // Hacia dónde vuela: un punto al azar dentro del castillo
            var aimPoint = center;
            if (Target != null)
            {
                aimPoint += Vector3.Scale(Random.insideUnitSphere, Target.bounds.extents * 0.6f);
            }
            var rotation = Quaternion.LookRotation(aimPoint - position);

            // Lo decide el master (una sola vez) y viaja a todos ya decidido
            float speed = prefab.Speed * Random.Range(0.8f, 1.2f) * Difficulty.SpeedMultiplier(_difficulty);
            float healthMultiplier = Difficulty.HealthMultiplier(_difficulty);
            bool golden = canBeGolden && Random.value < GoldenChance;
            float maxTravelDistance = (SpawnRadius + SpawnRadiusExtra) * 2f;

            Runner.Spawn(prefab, position, rotation, onBeforeSpawned: (runner, networkObject) =>
            {
                // Esto se ejecuta ANTES de que el pollo exista para los demás:
                // les llegará ya configurado, sin un instante "a medio hacer".
                var chicken = networkObject.GetComponent<Chicken>();
                chicken.Health.InitialHealth = Mathf.CeilToInt(chicken.Health.InitialHealth * healthMultiplier);
                chicken.Init(speed, maxTravelDistance, golden);
            });
        }

        private void SpawnPowerUps()
        {
            if (PowerUpPrefabs == null || PowerUpPrefabs.Length == 0 || _spawnPoints.Length == 0)
                return;

            int count = PowerUpsPerPause;

            // Antes de una oleada con jefe, uno más (Waves[CurrentWave] es la SIGUIENTE oleada)
            if (CurrentWave < Waves.Length && Waves[CurrentWave].Boss)
            {
                count++;
            }

            for (int i = 0; i < count; i++)
            {
                var prefab = PowerUpPrefabs[Random.Range(0, PowerUpPrefabs.Length)];
                if (prefab == null)
                    continue;

                // En los mismos puntos donde aparecen los jugadores: siempre son alcanzables
                var spawnPoint = _spawnPoints[Random.Range(0, _spawnPoints.Length)];
                var offset = Random.insideUnitCircle * spawnPoint.Radius;
                var position = spawnPoint.transform.position + new Vector3(offset.x, 0.6f, offset.y);

                Runner.Spawn(prefab, position, Quaternion.identity);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_BossIncoming()
        {
            BossIncoming?.Invoke();
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = Target != null ? Target.bounds.center : transform.position;
            Gizmos.DrawWireSphere(center, SpawnRadius);
            Gizmos.DrawWireSphere(center, SpawnRadius + SpawnRadiusExtra);
        }
    }
}
