using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
// For NavMesh

namespace ProcGen
{
    public class FloorGenerator : MonoBehaviour
    {
        // --- Singleton ---
        public static FloorGenerator Instance;

        [Header("Level Settings")]
        [Tooltip("The starting number of rooms for Level 1")]
        public int baseRoomCount = 15;
        [Tooltip("How many rooms to add for each new level")]
        public int roomCountIncreasePerLevel = 5;

        [Header("Player & Enemy")]
        [Tooltip("The Player PREFAB to spawn at the start")]
        public GameObject playerPrefab; // We use the prefab
        public GameObject enemyPrefab;

        // --- (Rest of your prefab lists are unchanged) ---
        [Header("Room Prefabs")]
        public GameObject startRoomPrefab;
        public List<GameObject> roomPrefabs;
        public GameObject enemySpawnRoomPrefab;

        [Header("End Room Prefabs")]
        public GameObject endRoomPrefab_N;
        public GameObject endRoomPrefab_S;
        public GameObject endRoomPrefab_E;
        public GameObject endRoomPrefab_W;

        [Header("Dead End Prefabs")]
        public GameObject deadEndPrefab_N;
        public GameObject deadEndPrefab_S;
        public GameObject deadEndPrefab_E;
        public GameObject deadEndPrefab_W;

        [Header("Navigation")]
        public NavMeshSurface navMeshSurface;

        [Header("Generation Settings")]
        public int gridSize = 20;
        [Range(0.0f, 1.0f)]
        public float density = 1.0f;

        // --- Public list for AI ---
        public static List<Vector3> allRoomCenters = new List<Vector3>();

        // --- Private State ---
        private int currentLevel = 1;
        private CharacterController playerInstance; // Stores a reference to the spawned player
        private List<GameObject> spawnedEnemies = new List<GameObject>(); // To track enemies
        private Dictionary<Vector2Int, Room> placedRooms = new Dictionary<Vector2Int, Room>();
        private List<OpenDoorway> openDoorways = new List<OpenDoorway>();
        private List<int> doorIndices = new List<int>();
        private System.Random rng = new System.Random();
        private List<GameObject> roomBag = new List<GameObject>();

        private class OpenDoorway { public Vector2Int gridPos; public string direction; }

        // --- Singleton Awake ---
        void Awake()
        {
            if (Instance == null) { Instance = this; }
            else { Destroy(gameObject); }
        }

        // --- UPDATED Start() ---
        void Start()
        {
            // Run the first level
            GenerateNewFloor();
            // Spawn the player for the very first time
            SpawnPlayer();
        }
    
        // --- NEW HELPER: Spawns the player from the prefab ---
        void SpawnPlayer()
        {
            if (playerPrefab == null)
            {
                Debug.LogError("Player Prefab is not assigned in FloorGenerator!");
                return;
            }
        
            GameObject playerObj = Instantiate(playerPrefab, new Vector3(0, 1, 0), Quaternion.identity);
            playerInstance = playerObj.GetComponent<CharacterController>();

            if (playerInstance == null)
            {
                Debug.LogWarning("Player Prefab has no CharacterController. Teleport will use Transform.");
            }
        }

        // --- NEW HELPER: Teleports the existing player ---
        void TeleportPlayer()
        {
            // 1. Find the player (if reference was lost)
            if (playerInstance == null)
            {
                playerInstance = FindAnyObjectByType<CharacterController>();
                if (playerInstance == null)
                {
                    Debug.LogError("Could not find Player in scene to teleport! Spawning a new one.");
                    SpawnPlayer(); // Failsafe: spawn a new player
                    return;
                }
            }

            // 2. Teleport the player
            if (playerInstance != null)
            {
                // Must disable controller to teleport it
                playerInstance.enabled = false; 
                playerInstance.transform.position = new Vector3(0, 1, 0);
                playerInstance.enabled = true;
            }
        }

        /// <summary>
        /// This is the public function the portal calls.
        /// </summary>
        public void GoToNextLevel()
        {
            currentLevel++;
            Debug.Log($"--- Starting Level {currentLevel} ---");

            // 1. Destroy the old floor and enemies
            DestroyCurrentFloor();

            // 2. Teleport the existing player
            TeleportPlayer();

            // 3. Generate the new, bigger floor
            GenerateNewFloor();
        }

        /// <summary>
        /// Destroys all rooms and enemies.
        /// </summary>
        public void DestroyCurrentFloor()
        {
            allRoomCenters.Clear();
            placedRooms.Clear();
            openDoorways.Clear();
            roomBag.Clear();

            // Destroy all room GameObjects (children of this transform)
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
        
            // Destroy all spawned enemies
            foreach (GameObject enemy in spawnedEnemies)
            {
                if (enemy != null) // Check if it wasn't already destroyed
                {
                    Destroy(enemy);
                }
            }
            spawnedEnemies.Clear();
        }

        // --- RENAMED: from GenerateFloor() to GenerateNewFloor() ---
        public void GenerateNewFloor()
        {
            // Calculate room count
            int roomsToSpawn = baseRoomCount + ((currentLevel - 1) * roomCountIncreasePerLevel);
        
            // Run the actual generation
            GenerateFloorInternal(roomsToSpawn);
        }
    
        /// <summary>
        /// This is your complete, working generation logic.
        /// </summary>
        void GenerateFloorInternal(int totalRooms)
        {
            allRoomCenters.Clear(); 

            // --- 1. Create the "Room Bag" ---
            roomBag.Clear();
            int roomsToPlace = Mathf.Max(0, totalRooms - 3); 

            if (roomPrefabs == null || roomPrefabs.Count == 0)
            {
                Debug.LogError("... 'roomPrefabs' list is empty ...", this);
                return;
            }
            while (roomBag.Count < roomsToPlace)
            {
                List<GameObject> tempDeck = new List<GameObject>(roomPrefabs);
                ShuffleList(tempDeck);
                roomBag.AddRange(tempDeck);
            }
            ShuffleList(roomBag);
            if (roomBag.Count > roomsToPlace)
            {
                roomBag.RemoveRange(roomsToPlace, roomBag.Count - roomsToPlace);
            }

            // --- 2. Place Start Room ---
            PlaceRoom(startRoomPrefab, Vector2Int.zero);

            // --- 3. Main Generation Loop ---
            int failsafe = 10000;
            while (roomBag.Count > 0 && openDoorways.Count > 0 && failsafe > 0)
            {
                failsafe--;
                bool roomPlacedThisIteration = false;
                doorIndices.Clear();
                for (int i = 0; i < openDoorways.Count; i++) { doorIndices.Add(i); }
                ShuffleList(doorIndices); 

                foreach (int index in doorIndices)
                {
                    OpenDoorway currentDoor = openDoorways[index];
                    Vector2Int newRoomPos = GetNewRoomPosition(currentDoor);
                    string doorToConnectTo = GetDoorToConnectTo(currentDoor.direction);

                    if (placedRooms.ContainsKey(newRoomPos))
                    {
                        openDoorways.Remove(currentDoor); 
                        roomPlacedThisIteration = true; 
                        break; 
                    }
                    GameObject prefab = FindAndRemoveValidRoomFromBag(roomBag, doorToConnectTo);
                    if (prefab != null)
                    {
                        PlaceRoom(prefab, newRoomPos, doorToConnectTo);
                        openDoorways.Remove(currentDoor); 
                        roomPlacedThisIteration = true;
                        break; 
                    }
                }
                if (!roomPlacedThisIteration) break;
            }
            if (failsafe <= 0) Debug.LogWarning("FloorGenerator: Hit failsafe in main loop.");


            // --- 4. Place the End Room ---
            Vector2Int endRoomPos = Vector2Int.zero;
            bool endRoomPlaced = false;
            OpenDoorway farthestDoorway = null;
            int maxDistanceSqr = -1;
            for (int i = openDoorways.Count - 1; i >= 0; i--)
            {
                OpenDoorway currentDoor = openDoorways[i];
                Vector2Int newPos = GetNewRoomPosition(currentDoor);
                if (placedRooms.ContainsKey(newPos))
                {
                    openDoorways.RemoveAt(i);
                    continue;
                }
                int distanceSqr = newPos.sqrMagnitude;
                if (distanceSqr > maxDistanceSqr)
                {
                    maxDistanceSqr = distanceSqr;
                    farthestDoorway = currentDoor;
                }
            }
            if (farthestDoorway != null)
            {
                endRoomPos = GetNewRoomPosition(farthestDoorway);
                string endDoorToConnect = GetDoorToConnectTo(farthestDoorway.direction);
                GameObject endPrefab = GetEndRoomPrefab(endDoorToConnect);
                if (endPrefab != null)
                {
                    PlaceRoom(endPrefab, endRoomPos, endDoorToConnect);
                    endRoomPlaced = true;
                    openDoorways.Remove(farthestDoorway);
                }
            }
            if (!endRoomPlaced) Debug.LogError("FloorGenerator: Could not find any valid spot to place the End Room!");

            // --- 5. Place the Enemy Room ---
            OpenDoorway bestEnemyDoorway = null;
            int maxMinDistanceSqr = -1; 
            for (int i = openDoorways.Count - 1; i >= 0; i--)
            {
                OpenDoorway currentDoor = openDoorways[i];
                Vector2Int newPos = GetNewRoomPosition(currentDoor);
                string doorToConnectTo = GetDoorToConnectTo(currentDoor.direction);

                if (placedRooms.ContainsKey(newPos))
                {
                    openDoorways.RemoveAt(i);
                    continue;
                }
                if (!CanRoomFit(enemySpawnRoomPrefab, doorToConnectTo))
                {
                    continue; 
                }
                int distToStartSqr = newPos.sqrMagnitude;
                int distToEndSqr = (newPos - endRoomPos).sqrMagnitude;
                int score = Mathf.Min(distToStartSqr, distToEndSqr);

                if (score > maxMinDistanceSqr)
                {
                    maxMinDistanceSqr = score;
                    bestEnemyDoorway = currentDoor;
                }
            }
            if (bestEnemyDoorway != null)
            {
                Vector2Int enemyRoomPos = GetNewRoomPosition(bestEnemyDoorway);
                string enemyDoorToConnect = GetDoorToConnectTo(bestEnemyDoorway.direction);
                PlaceRoom(enemySpawnRoomPrefab, enemyRoomPos, enemyDoorToConnect);
                openDoorways.Remove(bestEnemyDoorway);

                // --- Spawn Enemy and add to list ---
                if (enemyPrefab != null)
                {
                    Vector3 spawnPos = new Vector3(enemyRoomPos.x * gridSize, 1, enemyRoomPos.y * gridSize);
                    GameObject enemyObj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
                    spawnedEnemies.Add(enemyObj); // Add to list for later cleanup
                }
            }
            else
            {
                Debug.LogError("FloorGenerator: Could not find any valid spot to place the Enemy Room!");
            }

            // --- 6. Cap all remaining Dead Ends ---
            failsafe = 1000;
            for (int i = openDoorways.Count - 1; i >= 0 && failsafe > 0; i--)
            {
                failsafe--;
                OpenDoorway currentDoor = openDoorways[i];
                openDoorways.RemoveAt(i); 
                Vector2Int newPos = GetNewRoomPosition(currentDoor);
                string doorToConnect = GetDoorToConnectTo(currentDoor.direction);
                if (placedRooms.ContainsKey(newPos)) continue;
                GameObject deadEndPrefab = GetDeadEndPrefab(doorToConnect);
                if (deadEndPrefab != null)
                {
                    PlaceRoom(deadEndPrefab, newPos, doorToConnect);
                }
            }
        
            // --- 7. Bake NavMesh (AT THE END) ---
            if (navMeshSurface != null)
            {
                navMeshSurface.BuildNavMesh();
            }
        }


        // --- ALL YOUR HELPER FUNCTIONS (UNCHANGED) ---
        #region Helper Functions
    
        void PlaceRoom(GameObject prefab, Vector2Int gridPos, string doorToIgnore = "")
        {
            Vector3 worldPos = new Vector3(gridPos.x * gridSize, 0, gridPos.y * gridSize);
            GameObject roomObj = Instantiate(prefab, worldPos, Quaternion.identity, this.transform);
            allRoomCenters.Add(worldPos); 

            Room newRoom = roomObj.GetComponent<Room>();
            placedRooms.Add(gridPos, newRoom);
            if (newRoom.Door_North != null && doorToIgnore != "North")
                openDoorways.Add(new OpenDoorway { gridPos = gridPos, direction = "North" });
            if (newRoom.Door_South != null && doorToIgnore != "South")
                openDoorways.Add(new OpenDoorway { gridPos = gridPos, direction = "South" });
            if (newRoom.Door_East != null && doorToIgnore != "East")
                openDoorways.Add(new OpenDoorway { gridPos = gridPos, direction = "East" });
            if (newRoom.Door_West != null && doorToIgnore != "West")
                openDoorways.Add(new OpenDoorway { gridPos = gridPos, direction = "West" });
        }
    
        int SelectDoorIndex()
        {
            float sprawlChance = 1.0f - density;
            if (Random.Range(0.0f, 1.0f) > sprawlChance)
            {
                return Random.Range(0, openDoorways.Count);
            }
            doorIndices.Clear();
            for (int i = 0; i < openDoorways.Count; i++) { doorIndices.Add(i); }
            ShuffleList(doorIndices);
            foreach (int index in doorIndices)
            {
                OpenDoorway door = openDoorways[index];
                if (IsDoorFacingAway(door.gridPos, door.direction)) return index;
            }
            return doorIndices[0]; 
        }

        bool IsDoorFacingAway(Vector2Int roomPos, string doorDirection)
        {
            if (doorDirection == "North") return roomPos.y >= 0;
            if (doorDirection == "South") return roomPos.y <= 0;
            if (doorDirection == "East") return roomPos.x >= 0;
            if (doorDirection == "West") return roomPos.x <= 0;
            return false;
        }

        void ShuffleList(List<int> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                int value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        void ShuffleList(List<GameObject> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                GameObject value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        GameObject FindAndRemoveValidRoomFromBag(List<GameObject> bag, string doorDirection)
        {
            for (int i = 0; i < bag.Count; i++)
            {
                if (CanRoomFit(bag[i], doorDirection))
                {
                    GameObject prefab = bag[i];
                    bag.RemoveAt(i); 
                    return prefab;  
                }
            }
            return null; 
        }
    
        bool CanRoomFit(GameObject prefab, string doorDirection)
        {
            if (prefab == null) return false;
            Room room = prefab.GetComponent<Room>();
            if (room == null) return false;
            if (doorDirection == "North" && room.Door_North != null) return true;
            if (doorDirection == "South" && room.Door_South != null) return true;
            if (doorDirection == "East" && room.Door_East != null) return true;
            if (doorDirection == "West" && room.Door_West != null) return true;
            return false;
        }

        Vector2Int GetNewRoomPosition(OpenDoorway door)
        {
            Vector2Int newPos = door.gridPos;
            if (door.direction == "North") newPos.y += 1;
            else if (door.direction == "South") newPos.y -= 1;
            else if (door.direction == "East") newPos.x += 1;
            else if (door.direction == "West") newPos.x -= 1;
            return newPos;
        }

        string GetDoorToConnectTo(string doorDirection)
        {
            if (doorDirection == "North") return "South";
            if (doorDirection == "South") return "North";
            if (doorDirection == "East") return "West";
            if (doorDirection == "West") return "East";
            return "";
        }

        GameObject GetEndRoomPrefab(string doorDirection)
        {
            if (doorDirection == "North") return endRoomPrefab_N;
            if (doorDirection == "South") return endRoomPrefab_S;
            if (doorDirection == "East") return endRoomPrefab_E;
            if (doorDirection == "West") return endRoomPrefab_W;
            return null;
        }

        GameObject GetDeadEndPrefab(string doorDirection)
        {
            if (doorDirection == "North") return deadEndPrefab_N;
            if (doorDirection == "South") return deadEndPrefab_S;
            if (doorDirection == "East") return deadEndPrefab_E;
            if (doorDirection == "West") return deadEndPrefab_W;
            return null;
        }
        #endregion
    }
}