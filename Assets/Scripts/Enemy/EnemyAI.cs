using System.Collections.Generic;
using Game;
using ProcGen;
using UnityEngine;
using UnityEngine.AI;

namespace Enemy
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(AudioSource))]
    public class EnemyAI : MonoBehaviour
    {
      
        private static readonly int IsStun = Animator.StringToHash("isStun");

        [Header("Speeds")]
        [SerializeField] private float roamSpeed = 3.5f;
        [SerializeField] private float investigateSpeed = 5.0f;
        [SerializeField] private float chaseSpeed = 7.0f;

        [Header("Vision")]
        [Range(-1f, 1f)]
        [SerializeField] private float visionConeAngle = 0.5f; // the enemy sees in a cone infront of it, this adjusts said cone

        [Header("Blinding")]
        [SerializeField] private float blindTime = 3.0f;
        [SerializeField] private float blindAngleThreshold = 1.0f;

        [Header("Audio")]
        [SerializeField] private List<AudioClip> footstepClips;
        [Tooltip("The sound to play when the player is caught.")]
        [SerializeField] private AudioClip screamSound; 
        // these step intervals just determine how quickly the step sounds play
        [SerializeField] private float roamStepInterval = 0.7f;
        [SerializeField] private float investigateStepInterval = 0.5f;
        [SerializeField] private float chaseStepInterval = 0.35f;
        // same with these, just pitch for the sounds
        [SerializeField] private float roamPitch = 0.9f;
        [SerializeField] private float investigatePitch = 1.0f;
        [SerializeField] private float chasePitch = 1.2f;

        [Header("Attack")]
        [SerializeField] private float catchDistance = 1.5f; // determines how close the player has to be to get killed by the enemy, This was easier than a collider imo for adjustments

        // Private states
        private NavMeshAgent _agent;
        private Transform _player;
        private LaserPointer _playerLaser;
        private AudioSource _audioSource;
        private Animator _animator;
        private enum State { Roaming, Investigating, Chasing, Blinded, GameOver } 
        private State _currentState;
        private float _blindTimer;
        private float _stepTimer;
        private Vector3 _investigationTarget;

        void OnEnable()
        {
            NoiseManager.OnNoiseMade += OnHeardNoise;
        }

        void OnDisable()
        {
            NoiseManager.OnNoiseMade -= OnHeardNoise;
        }

        void Start()
        {
            _agent = GetComponent<NavMeshAgent>();
            _audioSource = GetComponent<AudioSource>();
        
            // Grab the Animator from the child model
            _animator = GetComponentInChildren<Animator>();

            try
            {
                // Grabs the player and laser. we need the laser for the blind
                _player = GameObject.FindGameObjectWithTag("Player").transform;
                _playerLaser = FindAnyObjectByType<LaserPointer>();
            }
            catch
            {
                // ignored, who needs console errors anyway
            }

            SwitchToRoam();
        }

        private void OnHeardNoise(Vector3 position, float radius)
        {
            // Ignore sounds if Blinded, Chasing, or if the game is already over
            if (_currentState == State.Blinded || _currentState == State.Chasing || _currentState == State.GameOver)
            {
                return;
            }
            // otherwise start walking towards that sound
            float distanceToSound = Vector3.Distance(transform.position, position);
            if (distanceToSound <= radius)
            {
                SwitchToInvestigate(position);
            }
        }

        void Update()
        {
            // Stop all logic if player is missing or game is over
            if (_player == null || _currentState == State.GameOver) return; 

            // Death check
            if (_currentState == State.Chasing)
            {
                float distToPlayer = Vector3.Distance(transform.position, _player.position);
            
                // If close enough to kill
                if (distToPlayer <= catchDistance)
                {
                    if (GameManager.Instance != null)
                    {
                        CatchPlayer(); 
                        return;
                    }
                }
            }

            // Blinded state
            if (_currentState == State.Blinded)
            {
                _blindTimer -= Time.deltaTime;
            
                // When blindness ends
                if (_blindTimer <= 0)
                {
                    // Turn off the Stun animation
                    if (_animator != null) _animator.SetBool(IsStun, false);
                    SwitchToRoam();
                }
                return; 
            }

            // Chase State
            bool canSeePlayer = CheckLineOfSight();
            if (canSeePlayer)
            {
                SwitchToChase();
            }
            else // Investigate and Roam states
            {
                if (_currentState == State.Chasing)
                {
                    SwitchToRoam();
                }
                else if (_currentState == State.Investigating)
                {
                    if (!_agent.pathPending && _agent.remainingDistance < 0.5f)
                    {
                        SwitchToRoam();
                    }
                }
                else if (_currentState == State.Roaming)
                {
                    if (!_agent.pathPending && _agent.remainingDistance < 0.5f)
                    {
                        SetNewRoamDestination();
                    }
                }
            }
        
            HandleFootsteps();
        }

        // Player Death helper
        private void CatchPlayer()
        {
            _currentState = State.GameOver;
            _agent.isStopped = true;

            // Stop footsteps and play scream
            _audioSource.Stop(); 
            if (screamSound != null)
            {
                _audioSource.PlayOneShot(screamSound); 
            }

            // Trigger the Death Screen
            GameManager.Instance.TriggerDeath();
        }

        // Footstep handler, manages the footstep sounds and stuff, Should I have used a looping sound here? yes. Did I? no
        private void HandleFootsteps()
        {
            // only play if moving
            if (_agent.velocity.magnitude > 0.1f && _currentState != State.Blinded && _currentState != State.GameOver)
            {
                _stepTimer -= Time.deltaTime;
                // time to play sound
                if (_stepTimer <= 0)
                {
                    // reset timer based on how angry the enemy is
                    if (_currentState == State.Chasing) _stepTimer = chaseStepInterval;
                    else if (_currentState == State.Investigating) _stepTimer = investigateStepInterval;
                    else _stepTimer = roamStepInterval;

                    PlayFootstep();
                }
            }
            else
            {
                // reset
                _stepTimer = 0; 
            }
        }

        // selects a random clip and applies different pitches based on state
        private void PlayFootstep()
        {
            // dont crash if no audio clips are there
            if (footstepClips == null || footstepClips.Count == 0) return;

            // pick a random clip
            AudioClip clip = footstepClips[Random.Range(0, footstepClips.Count)];
        
            // change the pitch and tone
            if (_currentState == State.Chasing) _audioSource.pitch = chasePitch;
            else if (_currentState == State.Investigating) _audioSource.pitch = investigatePitch;
            else _audioSource.pitch = roamPitch;
        
            // and slight variance so it doesnt sound super off
            _audioSource.pitch *= Random.Range(0.9f, 1.1f);
            _audioSource.volume = Random.Range(0.8f, 1.0f);
            _audioSource.PlayOneShot(clip);
        }

        // switches to chasing state
        private void SwitchToChase()
        {
            // increase movement speed, targets player
            _currentState = State.Chasing;
            _agent.speed = chaseSpeed;
            _agent.SetDestination(_player.position);
        }

        // switch to investigative state
        private void SwitchToInvestigate(Vector3 targetPosition)
        {
            // if we are already investigating a sound, ignore, new sounds that are furthur away
            if (_currentState == State.Investigating && 
                Vector3.Distance(transform.position, targetPosition) > _agent.remainingDistance)
            {
                return;
            }

            _currentState = State.Investigating;
            _agent.speed = investigateSpeed;
            _investigationTarget = targetPosition;
            _agent.SetDestination(_investigationTarget);
        }

        // switches to roam state
        private void SwitchToRoam()
        {
            // slows speed down and starts to roam
            
            _currentState = State.Roaming;
            _agent.speed = roamSpeed;
            SetNewRoamDestination();
        }
        
        // A list of rooms to visit, keps the enemy from bouncing between the same two rooms over and over
        // I have tried to make it so he wont visit the same room more than twice without moving to a new one first
        private List<Vector3> _destinationDeck = new List<Vector3>();
        
        // picks a room to go to, and refills the list if every room has been visited
        private void SetNewRoamDestination()
        {
            // refil if list empty
            if (_destinationDeck.Count == 0) { _destinationDeck = new List<Vector3>(FloorGenerator.allRoomCenters); }
            // pick a random index
            int r = Random.Range(0, _destinationDeck.Count);
            Vector3 d = _destinationDeck[r];
            // remove it
            _destinationDeck.RemoveAt(r);
            _agent.SetDestination(d);
        }

        // this checks if the player is visible, considers FOV and walls in the way
        private bool CheckLineOfSight()
        {
            // sets the eye level to 1.5 units, this is coming out of his chest just about, but lines up well with the player which I have kept the cylinder
            Vector3 p = transform.position + Vector3.up * 1.5f;
            Vector3 t = _player.position + Vector3.up * 1.5f;
            
            // calculate direction and distance
            Vector3 d = (t - p).normalized;
            float dist = Vector3.Distance(p, t);
        
            //  angle check, return false if player is not within the cone
            if (Vector3.Dot(transform.forward, d) < visionConeAngle) return false;
            
            // wall check, shoot a ray from the enemy to the player, if it hits something, check if the thing we hit is actually a player
            RaycastHit hit;
            if (Physics.Raycast(p, d, out hit, dist)) { if (hit.transform.CompareTag("Player")) return true; }
            // return false if we hit a wall
            return false;
        }

        // truggered when a particle, the laser, hits the enemy. 
        private void OnParticleCollision(GameObject o)
        {
            // Don't get stunned if already stunned or if the game is over
            if (_currentState == State.Blinded || _currentState == State.GameOver) return;

            LaserPointer l = o.GetComponent<LaserPointer>();
            if (l != null && l == _playerLaser) 
            { 
                if (_playerLaser.GetCurrentAngle() <= blindAngleThreshold) 
                { 
                    _currentState = State.Blinded; 
                    _blindTimer = blindTime; 
                    _agent.SetDestination(transform.position); 

                    // Turn ON the Stun animation
                    if (_animator != null) _animator.SetBool(IsStun, true);
                } 
            }
        }
    }
}