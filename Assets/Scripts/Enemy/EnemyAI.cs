using System.Collections.Generic;
using ProcGen;
using UnityEngine;
using UnityEngine.AI; // Required for NavMeshAgent

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AudioSource))]
public class EnemyAI : MonoBehaviour
{
    [Header("Speeds")]
    [SerializeField] private float roamSpeed = 3.5f;
    [SerializeField] private float investigateSpeed = 5.0f; // NEW
    [SerializeField] private float chaseSpeed = 7.0f;

    [Header("Vision")]
    [Range(-1f, 1f)]
    [SerializeField] private float visionConeAngle = 0.5f;

    [Header("Blinding")]
    [SerializeField] private float blindTime = 3.0f;
    [SerializeField] private float blindAngleThreshold = 1.0f;

    [Header("Audio")]
    [SerializeField] private List<AudioClip> footstepClips;
    [SerializeField] private float roamStepInterval = 0.7f;
    [SerializeField] private float investigateStepInterval = 0.5f; // NEW
    [SerializeField] private float chaseStepInterval = 0.35f;
    [SerializeField] private float roamPitch = 0.9f;
    [SerializeField] private float investigatePitch = 1.0f; // NEW
    [SerializeField] private float chasePitch = 1.2f;

    // --- Private State ---
    private NavMeshAgent agent;
    private Transform player;
    private LaserPointer playerLaser;
    private AudioSource audioSource;
    
    private enum State { Roaming, Investigating, Chasing, Blinded } // ADDED Investigating
    private State currentState;
    private float blindTimer;
    private float stepTimer;
    private Vector3 investigationTarget; // Where the noise was heard

    // --- NEW: Subscribe to the NoiseManager ---
    void OnEnable()
    {
        NoiseManager.OnNoiseMade += OnHeardNoise;
    }

    // --- NEW: Unsubscribe when destroyed ---
    void OnDisable()
    {
        NoiseManager.OnNoiseMade -= OnHeardNoise;
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();
        
        try
        {
            player = GameObject.FindGameObjectWithTag("Player").transform;
            playerLaser = FindAnyObjectByType<LaserPointer>(); // Unity 6+
        }
        catch { /* ... error handling ... */ }
        
        SwitchToRoam();
    }

    // --- NEW: This is our "ear" ---
    private void OnHeardNoise(Vector3 position, float radius)
    {
        // If we're blinded or already chasing, ignore sounds
        if (currentState == State.Blinded || currentState == State.Chasing)
        {
            return;
        }

        // Check if we are within the sound's radius
        float distanceToSound = Vector3.Distance(transform.position, position);
        if (distanceToSound <= radius)
        {
            // We heard it!
            SwitchToInvestigate(position);
        }
    }

    void Update()
    {
        if (player == null) return;

        // --- State 1: Blinded (Highest Priority) ---
        if (currentState == State.Blinded)
        {
            blindTimer -= Time.deltaTime;
            if (blindTimer <= 0) SwitchToRoam();
            return; 
        }

        // --- State 2: Chasing (Second Priority) ---
        bool canSeePlayer = CheckLineOfSight();
        if (canSeePlayer)
        {
            SwitchToChase();
        }
        else // --- State 3 & 4: Investigate/Roam ---
        {
            if (currentState == State.Chasing)
            {
                // We *just* lost them
                SwitchToRoam();
            }
            else if (currentState == State.Investigating)
            {
                // Have we arrived at the sound location?
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    SwitchToRoam(); // Arrived, saw nothing.
                }
            }
            else if (currentState == State.Roaming)
            {
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    SetNewRoamDestination();
                }
            }
        }
        
        HandleFootsteps();
    }

    private void HandleFootsteps()
    {
        if (agent.velocity.magnitude > 0.1f && currentState != State.Blinded)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0)
            {
                // --- Set timer based on 3 states ---
                if (currentState == State.Chasing) stepTimer = chaseStepInterval;
                else if (currentState == State.Investigating) stepTimer = investigateStepInterval;
                else stepTimer = roamStepInterval;

                PlayFootstep();
            }
        }
        else
        {
            stepTimer = 0; 
        }
    }

    private void PlayFootstep()
    {
        if (footstepClips == null || footstepClips.Count == 0) return;

        AudioClip clip = footstepClips[Random.Range(0, footstepClips.Count)];
        
        // --- Set pitch based on 3 states ---
        if (currentState == State.Chasing) audioSource.pitch = chasePitch;
        else if (currentState == State.Investigating) audioSource.pitch = investigatePitch;
        else audioSource.pitch = roamPitch;
        
        audioSource.pitch *= Random.Range(0.9f, 1.1f);
        audioSource.volume = Random.Range(0.8f, 1.0f);
        audioSource.PlayOneShot(clip);
    }

    private void SwitchToChase()
    {
        currentState = State.Chasing;
        agent.speed = chaseSpeed;
        agent.SetDestination(player.position);
    }

    // --- NEW: Investigation State ---
    private void SwitchToInvestigate(Vector3 targetPosition)
    {
        // Don't switch if we're already investigating a closer sound
        if (currentState == State.Investigating && 
            Vector3.Distance(transform.position, targetPosition) > agent.remainingDistance)
        {
            return; // The current sound is closer, keep going
        }

        currentState = State.Investigating;
        agent.speed = investigateSpeed;
        investigationTarget = targetPosition;
        agent.SetDestination(investigationTarget);
    }

    private void SwitchToRoam()
    {
        currentState = State.Roaming;
        agent.speed = roamSpeed;
        SetNewRoamDestination();
    }
    
    // --- (Rest of your script is unchanged) ---
    private List<Vector3> destinationDeck = new List<Vector3>();
    private void SetNewRoamDestination()
    {
        if (destinationDeck.Count == 0) { destinationDeck = new List<Vector3>(FloorGenerator.allRoomCenters); }
        int r = Random.Range(0, destinationDeck.Count);
        Vector3 d = destinationDeck[r];
        destinationDeck.RemoveAt(r);
        agent.SetDestination(d);
    }
    private bool CheckLineOfSight()
    {
        Vector3 p = transform.position + Vector3.up * 1.5f;
        Vector3 t = player.position + Vector3.up * 1.5f;
        Vector3 d = (t - p).normalized;
        float dist = Vector3.Distance(p, t);
        Debug.DrawRay(p, d * dist, Color.red);
        if (Vector3.Dot(transform.forward, d) < visionConeAngle) return false;
        RaycastHit hit;
        if (Physics.Raycast(p, d, out hit, dist)) { if (hit.transform.CompareTag("Player")) return true; }
        return false;
    }
    private void OnParticleCollision(GameObject o)
    {
        if (currentState == State.Blinded) return;
        LaserPointer l = o.GetComponent<LaserPointer>();
        if (l != null && l == playerLaser) { if (playerLaser.GetCurrentAngle() <= blindAngleThreshold) { currentState = State.Blinded; blindTimer = blindTime; agent.SetDestination(transform.position); } }
    }
}