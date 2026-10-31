using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;

namespace Starter.Shooter
{
    /// <summary>
    /// Main player scrip - controls player movement and animations.
    /// </summary>
    public sealed class Player : NetworkBehaviour
    {
        [Header("References")]
        public Health Health;
        public SimpleKCC KCC;
        public PlayerInput PlayerInput;
        public Animator Animator;
        public Transform CameraPivot;
        public Transform CameraHandle;
        public Transform ScalingRoot;
        public UINameplate Nameplate;
        public Collider Hitbox;
        public Renderer[] HeadRenderers;
        public GameObject[] FirstPersonOverlayObjects;

        [Header("Movement Setup")]
        public float WalkSpeed = 2f;
        public float JumpImpulse = 10f;
        public float UpGravity = 25f;
        public float DownGravity = 40f;

        [Header("Movement Accelerations")]
        public float GroundAcceleration = 55f;
        public float GroundDeceleration = 25f;
        public float AirAcceleration = 25f;
        public float AirDeceleration = 1.3f;

        [Header("Fire Setup")]
        public LayerMask HitMask;
        public GameObject ImpactPrefab;
        public ParticleSystem MuzzleParticle;
        [Tooltip("Time between two shots when the fire button is held down")]
        public float FireCadence = 0.15f;

        [Header("Animation Setup")]
        public Transform ChestTargetPosition;
        public Transform ChestBone;

        [Header("Sounds")]
        public AudioSource FireSound;
        public AudioSource FootstepSound;
        public AudioClip JumpAudioClip;
        public AudioClip LandAudioClip;

        [Header("VFX")]
        public ParticleSystem DustParticles;

        [Networked, HideInInspector, Capacity(24), OnChangedRender(nameof(OnNicknameChanged))]
        public string Nickname { get; set; }
        [Networked, HideInInspector]
        public int ChickenKills { get; set; }

        // CLASE 3 · ¿Listo para empezar? Lo escribe SOLO el dueño de este jugador (pulsando R).
        // El master lo LEE para saber si todos están listos. Nadie tiene que mandarle nada.
        [Networked]
        public NetworkBool IsReady { get; set; }
        // CLASE 3 · Power-ups activos. Son MÍOS: los escribo yo y los demás los pueden leer.
        // TickTimer = "hasta qué tick dura". Termina a la vez en todos los ordenadores.

        // RESERVA CLASE 4 · ¿Ya miré si tenía puntos guardados? Hasta que esté en true,
        // el master NO apunta mis puntos (si no, guardaría un 0 encima de los buenos).
        [Networked]
        public NetworkBool ScoreChecked { get; set; }

        [Networked]
        public TickTimer MachineGunTimer { get; set; }
        [Networked]
        public TickTimer DoublePointsTimer { get; set; }

        public bool HasMachineGun => MachineGunTimer.ExpiredOrNotRunning(Runner) == false;
        public bool HasDoublePoints => DoublePointsTimer.ExpiredOrNotRunning(Runner) == false;

        [Networked, OnChangedRender(nameof(OnJumpingChanged))]
        private NetworkBool _isJumping { get; set; }
        [Networked]
        private Vector3 _hitPosition { get; set; }
        [Networked]
        private Vector3 _hitNormal { get; set; }
        [Networked]
        private int _fireCount { get; set; }

        public float RepairDistance = 6f;
        public int RepairAmount = 2;
        public float RepairInterval = 0.5f;

        private Castle _castle;

        private GameFlow _gameFlow;
        private float _nextRepairTime;

        private Vector3 _moveVelocity;
        private int _visibleFireCount;
        private float _fireCooldownEnd;

        private GameManager _gameManager;

        // CLASE 3 · Los pollos esperan a que la partida esté en juego
        

        public override void Spawned()
        {
            _castle = FindFirstObjectByType<Castle>();
            _gameFlow = FindFirstObjectByType<GameFlow>();

            if (HasStateAuthority)
            {
                _gameManager = FindFirstObjectByType<GameManager>();

                // Set player nickname that is saved in UIGameMenu
                Nickname = PlayerPrefs.GetString("PlayerName");
            }

            // In case the nickname is already changed,
            // we need to trigger the change manually
            OnNicknameChanged();

            // Reset visible fire count
            _visibleFireCount = _fireCount;

            if (HasStateAuthority)
            {
                // For input authority deactivate head renderers so they are not obstructing the view
                for (int i = 0; i < HeadRenderers.Length; i++)
                {
                    HeadRenderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }

                // Some objects (e.g. weapon) are renderer with secondary Overlay camera.
                // This prevents weapon clipping into the wall when close to the wall.
                int overlayLayer = LayerMask.NameToLayer("FirstPersonOverlay");
                for (int i = 0; i < FirstPersonOverlayObjects.Length; i++)
                {
                    FirstPersonOverlayObjects[i].layer = overlayLayer;
                }

                // Look rotation interpolation is skipped for local player.
                // Look rotation is set manually in Render.
                KCC.Settings.ForcePredictedLookRotation = true;
            }
        }

        private void Update()
        {
            // Solo el dueño de este jugador lee SU teclado
            if (Object == null || Object.IsValid == false || HasStateAuthority == false)
                return;

            var keyboard = Keyboard.current;   // using UnityEngine.InputSystem;
            if (keyboard == null)
                return;

            // CLASE 3 · R = listo / no listo. Solo tiene sentido en la sala de espera.
            if (keyboard.rKey.wasPressedThisFrame && _gameFlow != null && _gameFlow.State == GameFlow.GameState.Waiting)
            {
                // Es MI jugador: escribo mi estado directamente, sin RPC
                IsReady = !IsReady;
            }

            // CLASE 2 · Reparar el castillo con E (igual que antes)
            if (_castle == null || Health.IsAlive == false)
                return;

            if (keyboard.eKey.isPressed && Time.time >= _nextRepairTime &&
                Vector3.Distance(transform.position, _castle.transform.position) <= RepairDistance)
            {
                _castle.RPC_Repair(RepairAmount);
                _nextRepairTime = Time.time + RepairInterval;
            }
        }
        public override void FixedUpdateNetwork()
        {
            // RESERVA CLASE 4 · Recuperar mis puntos si vuelvo después de caerme
            if (ScoreChecked == false)
            {
                TryRestoreScore();
            }
            if (KCC.Position.y < -15f)
            {
                // Player fell, let's kill him
                Health.TakeHit(1000);
            }

            if (Health.IsFinished)
            {
                // Player is dead and death timer is finished, let's respawn the player
                Respawn(_gameManager.GetSpawnPosition());
            }

            var input = Health.IsAlive ? PlayerInput.CurrentInput : default;
            ProcessInput(input);

            if (KCC.IsGrounded)
            {
                // Stop jumping
                _isJumping = false;
            }

            KCC.SetActive(Health.IsAlive);

            PlayerInput.ResetInput();
        }

        public override void Render()
        {
            if (HasStateAuthority)
            {
                // Set look rotation for Render.
                KCC.SetLookRotation(PlayerInput.CurrentInput.LookRotation, -90f, 90f);
            }

            // Transform velocity vector to local space.
            var moveSpeed = transform.InverseTransformVector(KCC.RealVelocity);

            Animator.SetFloat(AnimatorId.SpeedX, moveSpeed.x, 0.1f, Time.deltaTime);
            Animator.SetFloat(AnimatorId.SpeedZ, moveSpeed.z, 0.1f, Time.deltaTime);
            Animator.SetBool(AnimatorId.Grounded, KCC.IsGrounded);
            Animator.SetFloat(AnimatorId.Pitch, KCC.GetLookRotation(true, false).x, 0.02f, Time.deltaTime);

            FootstepSound.enabled = KCC.IsGrounded && KCC.RealSpeed > 1f;
            ScalingRoot.localScale = Vector3.Lerp(ScalingRoot.localScale, Vector3.one, Time.deltaTime * 8f);

            var emission = DustParticles.emission;
            emission.enabled = KCC.IsGrounded && KCC.RealSpeed > 1f;

            ShowFireEffects();

            // Disable hits when player is dead
            Hitbox.enabled = Health.IsAlive;
        }

        private void Awake()
        {
            Health.HitReceived += OnHitReceived;
        }

        private void LateUpdate()
        {
            if (Health.IsAlive == false)
                return;

            // Update camera pivot (influences ChestIK)
            // (KCC look rotation is set earlier in Render)
            var pitchRotation = KCC.GetLookRotation(true, false);
            CameraPivot.localRotation = Quaternion.Euler(pitchRotation);

            // Dummy IK solution, we are snapping chest bone to prepared ChestTargetPosition position
            // Lerping blends the fixed position with little bit of animation position.
            float blendAmount = HasStateAuthority ? 0.05f : 0.2f;
            ChestBone.position = Vector3.Lerp(ChestTargetPosition.position, ChestBone.position, blendAmount);
            ChestBone.rotation = Quaternion.Lerp(ChestTargetPosition.rotation, ChestBone.rotation, blendAmount);

            // Only local player needs to update the camera
            if (HasStateAuthority)
            {
                // Transfer properties from camera handle to Main Camera.
                Camera.main.transform.SetPositionAndRotation(CameraHandle.position, CameraHandle.rotation);
            }
        }

        private void ProcessInput(GameplayInput input)
        {
            KCC.SetLookRotation(input.LookRotation, -90f, 90f);

            // It feels better when player falls quicker
            KCC.SetGravity(KCC.RealVelocity.y >= 0f ? UpGravity : DownGravity);

            // Calculate correct move direction from input (rotated based on latest KCC rotation)
            var moveDirection = KCC.TransformRotation * new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);
            var desiredMoveVelocity = moveDirection * WalkSpeed;

            float acceleration;
            if (desiredMoveVelocity == Vector3.zero)
            {
                // No desired move velocity - we are stopping.
                acceleration = KCC.IsGrounded == true ? GroundDeceleration : AirDeceleration;
            }
            else
            {
                acceleration = KCC.IsGrounded == true ? GroundAcceleration : AirAcceleration;
            }

            _moveVelocity = Vector3.Lerp(_moveVelocity, desiredMoveVelocity, acceleration * Runner.DeltaTime);
            float jumpImpulse = 0f;

            // Comparing current input buttons to previous input buttons - this prevents glitches when input is lost
            if (KCC.IsGrounded && input.Jump)
            {
                // Set world space jump vector
                jumpImpulse = JumpImpulse;
                _isJumping = true;
            }

            KCC.Move(_moveVelocity, jumpImpulse);

            // Update camera pivot so fire transform (CameraHandle) is correct
            var pitchRotation = KCC.GetLookRotation(true, false);
            CameraPivot.localRotation = Quaternion.Euler(pitchRotation);

            // The weapon keeps firing while the fire button is held down
            if (input.Fire && Time.time >= _fireCooldownEnd)
            {
                Fire();

                // CLASE 3 · Con la ametralladora disparamos 3 veces más rápido
                float cadence = HasMachineGun ? FireCadence / 3f : FireCadence;
                _fireCooldownEnd = Time.time + cadence;
            }
        }

        private void Fire()
        {
            // Clear hit position in case nothing will be hit
            _hitPosition = Vector3.zero;

            // Move fire position slightly forward to avoid hitting self when looking up.
            var firePosition = CameraHandle.position + CameraHandle.forward * 0.5f;

            // Whole projectile path and effects are immediately processed (= hitscan projectile)
            if (Physics.Raycast(firePosition, CameraHandle.forward, out var hitInfo, 200f, HitMask))
            {
                // Deal damage
                var health = hitInfo.collider != null ? hitInfo.collider.GetComponentInParent<Health>() : null;
                if (health != null && health.GetComponent<Chicken>() != null)
                {
                    health.Killed = OnEnemyKilled;
                    health.TakeHit(1, true);
                }

                // Save hit point to correctly show bullet path on all clients.
                // This however works only for single projectile per FUN and with higher fire cadence
                // some projectiles might not be fired on proxies because we save only the position
                // of the LAST hit.
                _hitPosition = hitInfo.point;
                _hitNormal = hitInfo.normal;
            }

            // In this example projectile count property (fire count) is used not only for weapon fire effects
            // but to spawn the projectile visuals themselves.
            _fireCount++;
        }

        /// <summary>
        /// RESERVA CLASE 4 · ¿Volvemos después de caernos? Buscamos nuestro nombre en
        /// GameFlow.SavedScores (lo escribe el master; nosotros solo LEEMOS).
        /// Se reintenta cada tick hasta que el GameFlow esté listo en este ordenador.
        /// </summary>
        private void TryRestoreScore()
        {
            if (_gameFlow == null || _gameFlow.Object == null || _gameFlow.Object.IsValid == false)
                return; // todavía no nos ha llegado: lo intentamos el próximo tick

            if (_gameFlow.SavedScores.TryGet(Nickname, out var savedScore) && savedScore > 0)
            {
                ChickenKills = savedScore;
                Debug.Log($"[Clase 4] Bienvenido de vuelta, {Nickname}: recuperas {savedScore} puntos");
            }

            ScoreChecked = true;
        }
        private void Respawn(Vector3 position)
        {
            ChickenKills = 0;
            Health.Revive();

            KCC.SetPosition(position);
            KCC.SetLookRotation(0f, 0f);

            _moveVelocity = Vector3.zero;
        }

        private void OnHitReceived()
        {
            // Show hit reaction by simple scale (but not for local player).
            // Scaling root scale is lerped back to one in Render.
            if (HasStateAuthority == false)
            {
                ScalingRoot.localScale = new Vector3(0.85f, 1.15f, 0.85f);
            }
        }

        private void OnEnemyKilled(Health enemyHealth)
        {
            var chicken = enemyHealth.GetComponent<Chicken>();
            if (chicken == null)
                return;

            // CLASE 3 · Cada tipo de pollo vale lo que diga su prefab (Points)
            int points = chicken.Points;

            // El pollo dorado vale x5
            if (chicken.IsGolden)
                points *= 5;

            // EJERCICIO (power-up Puntos x2)
            if (HasDoublePoints)
                points *= 2;

            ChickenKills += points;
        }

        private void ShowFireEffects()
        {
            // Notice we are not using OnChangedRender for fireCount property but instead
            // we are checking against a local variable and show fire effects only when visible
            // fire count is SMALLER. This prevents triggering false fire effects when
            // local player mispredicted fire (e.g. input got lost) and fireCount property got decreased.
            if (_visibleFireCount < _fireCount)
            {
                FireSound.PlayOneShot(FireSound.clip);
                MuzzleParticle.Play();
                Animator.SetTrigger(AnimatorId.Shoot);

                if (_hitPosition != Vector3.zero)
                {
                    // Impact gets destroyed automatically with DestroyAfter script
                    Instantiate(ImpactPrefab, _hitPosition, Quaternion.LookRotation(_hitNormal));
                }
            }

            _visibleFireCount = _fireCount;
        }

        private void OnJumpingChanged()
        {
            if (_isJumping)
            {
                AudioSource.PlayClipAtPoint(JumpAudioClip, KCC.Position, 0.5f);
            }
            else
            {
                AudioSource.PlayClipAtPoint(LandAudioClip, KCC.Position, 1f);
            }

            if (HasStateAuthority == false)
            {
                ScalingRoot.localScale = _isJumping ? new Vector3(0.5f, 1.5f, 0.5f) : new Vector3(1.25f, 0.75f, 1.25f);
            }
        }

        private void OnNicknameChanged()
        {
            if (HasStateAuthority)
                return; // Do not show nickname for local player

            Nameplate.SetNickname(Nickname);
        }

        private void OnTriggerEnter(Collider other)
        {
            // Solo el jugador LOCAL pide power-ups (igual que las monedas del Platformer).
            // Los demás ordenadores también ven el choque, pero no son dueños de este jugador.
            if (HasStateAuthority == false)
                return;

            var powerUp = other.GetComponentInParent<PowerUp>();
            if (powerUp != null)
            {
                // Se lo PEDIMOS al dueño del power-up. Si nos lo da, llamará a OnPowerUpCollected.
                powerUp.RequestPickup(OnPowerUpCollected);
            }
        }

        /// Solo se ejecuta si el master nos dio el power-up A NOSOTROS.
        private void OnPowerUpCollected(PowerUp powerUp)
        {
            switch (powerUp.Type)
            {
                case PowerUpType.MachineGun:
                    // Toca a MI jugador: escribo mi estado directamente
                    MachineGunTimer = TickTimer.CreateFromSeconds(Runner, powerUp.Duration);
                    break;

                case PowerUpType.Repair:
                    // Toca al CASTILLO, que NO es mío: se lo pido a su dueño con un RPC
                    if (_castle != null)
                    {
                        _castle.RPC_Repair(powerUp.RepairAmount);
                    }
                    break;
                    
                case PowerUpType.DoublePoints:
                    // Toca a MI marcador -> directo, como la ametralladora
                    DoublePointsTimer = TickTimer.CreateFromSeconds(Runner, powerUp.Duration);
                    break;
            }
        }


    }

    
}
