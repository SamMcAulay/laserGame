using System.Collections.Generic;
using ProcGen;
using UnityEngine;
using UnityEngine.AI; // Required for NavMeshAgent

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    [Header("Speeds")]
    [SerializeField] private float roamSpeed = 3.5f;
    [SerializeField] private float chaseSpeed = 7.0f;

    [Header("Vision")]
    [Tooltip("How wide the enemy's cone of vision is (e.g., 0.5 = ~120 degrees).")]
    [Range(-1f, 1f)]
    [SerializeField] private float visionConeAngle = 0.5f;

    [Header("Blinding")]
    [Tooltip("How long the enemy is 'blinded' after being hit.")]
    [SerializeField] private float blindTime = 3.0f;
    [Tooltip("The laser angle (from LaserPointer) at or below which the enemy is blinded.")]
    [SerializeField] private float blindAngleThreshold = 1.0f;

    // --- Private State ---
    private NavMeshAgent agent;
    private Transform player;
    private LaserPointer playerLaser; 
    
    private enum State { Roaming, Chasing, Blinded }
    private State currentState;
    private float blindTimer;

    // A "deck" of potential destinations
    private List<Vector3> destinationDeck = new();

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        
        // Find the player by their tag
        try
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            player = playerObject.transform;
        }
        catch
        {
            Debug.LogError("ENEMY AI: Could not find object with 'Player' tag. Disabling AI.", this);
            this.enabled = false;
            return;
        }
        
        playerLaser = FindAnyObjectByType<LaserPointer>(); // <-- New, correct version

        if (playerLaser == null)
        {
            Debug.LogError("ENEMY AI: Could not find any active 'LaserPointer' script in the scene! Disabling AI.", this);
            this.enabled = false;
            return;
        }
        
        // Start by roaming
        SwitchToRoam();
    }

    void Update()
    {
        if (player == null) return;

        // --- State 1: Blinded ---
        if (currentState == State.Blinded)
        {
            blindTimer -= Time.deltaTime;
            if (blindTimer <= 0)
            {
                SwitchToRoam(); // Blinding wore off
            }
            return; // Do nothing else while blinded
        }

        // --- State 2 & 3: Roam/Chase ---
        bool canSeePlayer = CheckLineOfSight();

        if (canSeePlayer && currentState != State.Chasing)
        {
            // Principle 2: Saw the player
            SwitchToChase();
        }
        else if (!canSeePlayer && currentState == State.Chasing)
        {
            // Lost the player
            SwitchToRoam();
        }

        // --- State Behavior ---
        if (currentState == State.Chasing)
        {
            // Continuously update destination to the player
            agent.SetDestination(player.position);
        }
        else if (currentState == State.Roaming)
        {
            // Principle 1: Roam the map
            // If we've arrived at our destination, pick a new one
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
            {
                SetNewRoamDestination();
            }
        }
    }

    private void SwitchToChase()
    {
        currentState = State.Chasing;
        agent.speed = chaseSpeed;
        agent.SetDestination(player.position); // Go!
    }

    private void SwitchToRoam()
    {
        currentState = State.Roaming;
        agent.speed = roamSpeed;
        SetNewRoamDestination(); // Find a new room to visit
    }

  
    // Picks a new, unvisited room to travel to.
    private void SetNewRoamDestination()
    {
        // If our deck of destinations is empty, fill it up
        if (destinationDeck.Count == 0)
        {
            // Refill the deck from the generator's public list
            destinationDeck = new List<Vector3>(FloorGenerator.allRoomCenters);
        }

        // Pick a random destination from the deck
        int randomIndex = Random.Range(0, destinationDeck.Count);
        Vector3 newDest = destinationDeck[randomIndex];

        // Remove this destination from the deck so we don't visit it again soon
        destinationDeck.RemoveAt(randomIndex);

        // Set the new destination
        agent.SetDestination(newDest);
    }
    
    /// Checks if the player is in front of the enemy and has a clear line of sight.
    private bool CheckLineOfSight()
    {
        Vector3 eyePosition = transform.position + Vector3.up * 1.5f;
        Vector3 playerPos = player.position + Vector3.up * 1.5f;
        Vector3 directionToPlayer = (playerPos - eyePosition).normalized;
        float distanceToPlayer = Vector3.Distance(eyePosition, playerPos);
        
        Debug.DrawRay(eyePosition, directionToPlayer * distanceToPlayer, Color.red);

        // --- 1. Check if Player is "in front" (I have the radius set super wide so its not really in front but you get the deal) ---
        float dotProduct = Vector3.Dot(transform.forward, directionToPlayer);
        if (dotProduct < visionConeAngle)
        {
            return false; // Player is not in our vision cone
        }

        // --- 2. Player is in the cone, now check for walls (Line of Sight) ---
        RaycastHit hit;
        
        if (Physics.Raycast(eyePosition, directionToPlayer, out hit, distanceToPlayer))
        {
            if (hit.transform.CompareTag("Player"))
            {
                return true; // Yes! Clear line of sight.
            }
        }
        return false; // Hit a wall or nothing
    }
    // --- Blinding Mechanic ---
    private void OnParticleCollision(GameObject other)
    {
        if (currentState == State.Blinded)
        {
            return; // Already blind
        }
        
        // We get the LaserPointer script from the particle system that hit us
        LaserPointer laser = other.GetComponent<LaserPointer>();

        // We check if it's not null AND if it's the same script we found in Start()
        if (laser != null && laser == playerLaser)
        {
            // Check if the laser's angle is narrow enough to blind
            if (playerLaser.GetCurrentAngle() <= blindAngleThreshold)
            {
                // --- We are blinded! ---
                currentState = State.Blinded;
                blindTimer = blindTime;
                
                // Stop moving
                agent.SetDestination(transform.position); 
            }
        }
    }
}