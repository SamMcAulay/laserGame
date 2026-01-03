using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using Speedrun;
using UnityEngine.Serialization;

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
        public GameObject playerPrefab; 
        public GameObject enemyPrefab;

        [Header("Room Prefabs")]
        public GameObject startRoomPrefab;
        public List<GameObject> roomPrefabs;
        public GameObject enemySpawnRoomPrefab;

        [FormerlySerializedAs("endRoomPrefab_N")] [Header("End Room Prefabs")]
        public GameObject endRoomPrefabN;
        [FormerlySerializedAs("endRoomPrefab_S")] public GameObject endRoomPrefabS;
        [FormerlySerializedAs("endRoomPrefab_E")] public GameObject endRoomPrefabE;
        [FormerlySerializedAs("endRoomPrefab_W")] public GameObject endRoomPrefabW;

        [FormerlySerializedAs("deadEndPrefab_N")] [Header("Dead End Prefabs")]
        public GameObject deadEndPrefabN;
        [FormerlySerializedAs("deadEndPrefab_S")] public GameObject deadEndPrefabS;
        [FormerlySerializedAs("deadEndPrefab_E")] public GameObject deadEndPrefabE;
        [FormerlySerializedAs("deadEndPrefab_W")] public GameObject deadEndPrefabW;

        [Header("Navigation")]
        public NavMeshSurface navMeshSurface;

        [Header("Generation Settings")]
        public int gridSize = 20;
        [Range(0.0f, 1.0f)]
        public float density = 1.0f;

        // Public list for the ai
        public static List<Vector3> allRoomCenters = new List<Vector3>();

        // private states
        private int _currentLevel = 1;
        private CharacterController _playerInstance; 
        private List<GameObject> _spawnedEnemies = new List<GameObject>(); 
        private Dictionary<Vector2Int, Room> _placedRooms = new Dictionary<Vector2Int, Room>();
        private List<OpenDoorway> _openDoorways = new List<OpenDoorway>();
        private List<int> _doorIndices = new List<int>();
        private System.Random _rng = new System.Random();
        private List<GameObject> _roomBag = new List<GameObject>();

        private class OpenDoorway { public Vector2Int GridPos; public string Direction; }

        void Awake()
        {
            // singleton stuff
            if (Instance == null) { Instance = this; }
            else { Destroy(gameObject); }
        }

        void Start()
        {
            // generate the floor and spawn the player
            GenerateNewFloor();
            SpawnPlayer();

            if (SpeedrunManager.Instance != null)
            {
                // start the speedrun timer
                SpeedrunManager.Instance.StartRun();
            }
        }
    
        void SpawnPlayer()
        {
            // spawn the player, instantiate the prefab at 0,0
            if (playerPrefab == null) return;
        
            GameObject playerObj = Instantiate(playerPrefab, new Vector3(0, 1, 0), Quaternion.identity);
            _playerInstance = playerObj.GetComponent<CharacterController>();
        }

        // teleport the player back to 0,0 either on reset or level complete
        void TeleportPlayer()
        {
            if (_playerInstance == null)
            {
                // yoinks the charachter controller
                _playerInstance = FindAnyObjectByType<CharacterController>();
                if (_playerInstance == null)
                {
                    SpawnPlayer(); 
                    return;
                }
            }

            if (_playerInstance != null)
            {
                _playerInstance.enabled = false; 
                _playerInstance.transform.position = new Vector3(0, 1, 0);
                _playerInstance.enabled = true;
            }
        }

        // level up
        public void GoToNextLevel()
        {
            if (SpeedrunManager.Instance != null)
            {
                // complete the floor on the speedrun side
                SpeedrunManager.Instance.CompleteFloor();
            }

            _currentLevel++;
            Debug.Log($"--- Starting Level {_currentLevel} ---");

            DestroyCurrentFloor();
            TeleportPlayer();
            GenerateNewFloor();
        }

        // clears all the lists of doorways, rooms, ect
        public void DestroyCurrentFloor()
        {
            allRoomCenters.Clear();
            _placedRooms.Clear();
            _openDoorways.Clear();
            _roomBag.Clear();

            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
        
            foreach (GameObject enemy in _spawnedEnemies)
            {
                if (enemy != null) Destroy(enemy);
            }
            _spawnedEnemies.Clear();
        }

        // this is the fat one, it generates the new floor
        public void GenerateNewFloor()
        {
            // spawns the floor in intervals, defines the intervals
            int roomsToSpawn = baseRoomCount + ((_currentLevel - 1) * roomCountIncreasePerLevel);
            GenerateFloorInternal(roomsToSpawn);
        }
    
        void GenerateFloorInternal(int totalRooms)
        {
            allRoomCenters.Clear(); 

            // Calculate Enemy Settings
            // Level 1-2: 1 enemy. Level 3-4: 2 enemies. etc.
            int enemiesToSpawn = (_currentLevel + 1) / 2;
            int enemiesPlacedCount = 0;

            // Calculate Milestones for spawning
            // If we have 20 rooms and 2 enemies, interval is ~6. Spawns at 6 and 12.
            int enemySpawnInterval = totalRooms / (enemiesToSpawn + 1);
            Queue<int> enemyMilestones = new Queue<int>();
            for (int i = 1; i <= enemiesToSpawn; i++)
            {
                enemyMilestones.Enqueue(enemySpawnInterval * i);
            }

            // Create the room Bag
            _roomBag.Clear();
            // We reserve a few slots for end room, etc, but we want the floor to grow
            // so we generally fill the bag up to totalRooms
            int roomsToPlace = Mathf.Max(0, totalRooms - 2); 

            while (_roomBag.Count < roomsToPlace)
            {
                List<GameObject> tempDeck = new List<GameObject>(roomPrefabs);
                ShuffleList(tempDeck);
                _roomBag.AddRange(tempDeck);
            }
            ShuffleList(_roomBag);
            if (_roomBag.Count > roomsToPlace)
            {
                _roomBag.RemoveRange(roomsToPlace, _roomBag.Count - roomsToPlace);
            }

            // Place Start Room 
            PlaceRoom(startRoomPrefab, Vector2Int.zero);

            // Main Generation Loop
            int failsafe = 10000;
            int normalRoomsPlaced = 0;

            while (_roomBag.Count > 0 && _openDoorways.Count > 0 && failsafe > 0)
            {
                failsafe--;

                // Time to spawn an enemy? 
                bool trySpawnEnemy = enemyMilestones.Count > 0 && normalRoomsPlaced >= enemyMilestones.Peek();

                bool roomPlacedThisIteration = false;

                // Spawn Enemy at FURTHEST point from spawn if triggered
                if (trySpawnEnemy)
                {
                    // Sort all open doorways by distance from spawn
                    List<OpenDoorway> sortedDoors = new List<OpenDoorway>(_openDoorways);
                    sortedDoors.Sort((a, b) => b.GridPos.sqrMagnitude.CompareTo(a.GridPos.sqrMagnitude));

                    // Try to fit enemy room at the furthest valid spot
                    foreach (OpenDoorway door in sortedDoors)
                    {
                        string doorToConnectTo = GetDoorToConnectTo(door.Direction);
                        if (CanRoomFit(enemySpawnRoomPrefab, doorToConnectTo))
                        {
                            Vector2Int newPos = GetNewRoomPosition(door);
                            PlaceRoom(enemySpawnRoomPrefab, newPos, doorToConnectTo);
                            SpawnEnemyInRoom(newPos);
                            
                            _openDoorways.Remove(door);
                            enemyMilestones.Dequeue(); 
                            enemiesPlacedCount++;
                            roomPlacedThisIteration = true;
                            break; 
                        }
                    }
                    
                    // If we successfully placed an enemy we continue the loop 
                    // (potentially effectively "replacing" a normal room turn, or just adding to it)
                    if (roomPlacedThisIteration) continue; 
                }

                // Normal Room Placement
                _doorIndices.Clear();
                for (int i = 0; i < _openDoorways.Count; i++) { _doorIndices.Add(i); }
                ShuffleList(_doorIndices); 

                foreach (int index in _doorIndices)
                {
                    OpenDoorway currentDoor = _openDoorways[index];
                    Vector2Int newRoomPos = GetNewRoomPosition(currentDoor);
                    string doorToConnectTo = GetDoorToConnectTo(currentDoor.Direction);

                    if (_placedRooms.ContainsKey(newRoomPos))
                    {
                        _openDoorways.Remove(currentDoor); 
                        roomPlacedThisIteration = true; 
                        break; 
                    }
                    
                    GameObject prefab = FindAndRemoveValidRoomFromBag(_roomBag, doorToConnectTo);
                    if (prefab != null)
                    {
                        PlaceRoom(prefab, newRoomPos, doorToConnectTo);
                        _openDoorways.Remove(currentDoor); 
                        roomPlacedThisIteration = true;
                        normalRoomsPlaced++;
                        break; 
                    }
                }
                if (!roomPlacedThisIteration) break;
            }


            // Place the End Room
            Vector2Int endRoomPos = Vector2Int.zero;
            bool endRoomPlaced = false;
            OpenDoorway farthestDoorway = null;
            int maxDistanceSqr = -1;
            
            // Find farthest door for the portal
            for (int i = _openDoorways.Count - 1; i >= 0; i--)
            {
                OpenDoorway currentDoor = _openDoorways[i];
                Vector2Int newPos = GetNewRoomPosition(currentDoor);
                if (_placedRooms.ContainsKey(newPos)) { _openDoorways.RemoveAt(i); continue; }
                
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
                string endDoorToConnect = GetDoorToConnectTo(farthestDoorway.Direction);
                GameObject endPrefab = GetEndRoomPrefab(endDoorToConnect);
                if (endPrefab != null)
                {
                    PlaceRoom(endPrefab, endRoomPos, endDoorToConnect);
                    endRoomPlaced = true;
                    _openDoorways.Remove(farthestDoorway);
                }
            }

            // Place Remaining Enemies
            // If the map generation was too cramped or tight, we might have missed a milestone.
            // Try to place any remaining enemies now, as far from Start/End as possible.
            int remainingEnemies = enemiesToSpawn - enemiesPlacedCount;
            for(int k=0; k < remainingEnemies; k++)
            {
                OpenDoorway bestEnemyDoorway = null;
                int maxMinDistanceSqr = -1; 

                for (int i = _openDoorways.Count - 1; i >= 0; i--)
                {
                    OpenDoorway currentDoor = _openDoorways[i];
                    Vector2Int newPos = GetNewRoomPosition(currentDoor);
                    string doorToConnectTo = GetDoorToConnectTo(currentDoor.Direction);

                    if (_placedRooms.ContainsKey(newPos)) { _openDoorways.RemoveAt(i); continue; }
                    if (!CanRoomFit(enemySpawnRoomPrefab, doorToConnectTo)) continue; 

                    int distToStartSqr = newPos.sqrMagnitude;
                    int distToEndSqr = endRoomPlaced ? (newPos - endRoomPos).sqrMagnitude : distToStartSqr;
                    
                    // Maximizing the Minimum Distance to significant points
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
                    string enemyDoorToConnect = GetDoorToConnectTo(bestEnemyDoorway.Direction);
                    PlaceRoom(enemySpawnRoomPrefab, enemyRoomPos, enemyDoorToConnect);
                    SpawnEnemyInRoom(enemyRoomPos);
                    _openDoorways.Remove(bestEnemyDoorway);
                }
            }

            // Cap all remaining Dead Ends
            failsafe = 1000;
            for (int i = _openDoorways.Count - 1; i >= 0 && failsafe > 0; i--)
            {
                failsafe--;
                OpenDoorway currentDoor = _openDoorways[i];
                _openDoorways.RemoveAt(i); 
                Vector2Int newPos = GetNewRoomPosition(currentDoor);
                string doorToConnect = GetDoorToConnectTo(currentDoor.Direction);
                
                if (_placedRooms.ContainsKey(newPos)) continue;
                
                GameObject deadEndPrefab = GetDeadEndPrefab(doorToConnect);
                if (deadEndPrefab != null)
                {
                    PlaceRoom(deadEndPrefab, newPos, doorToConnect);
                }
            }
        
            //  Bake NavMesh 
            if (navMeshSurface != null)
            {
                navMeshSurface.BuildNavMesh();
            }
        }

        // Spawns the enemy prefab at the location of the room 
        void SpawnEnemyInRoom(Vector2Int gridPos)
        {
            if (enemyPrefab != null)
            {
                Vector3 spawnPos = new Vector3(gridPos.x * gridSize, 1, gridPos.y * gridSize);
                GameObject enemyObj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
                _spawnedEnemies.Add(enemyObj);
            }
        }

        #region Helper Functions
        void PlaceRoom(GameObject prefab, Vector2Int gridPos, string doorToIgnore = "")
        {
            Vector3 worldPos = new Vector3(gridPos.x * gridSize, 0, gridPos.y * gridSize);
            GameObject roomObj = Instantiate(prefab, worldPos, Quaternion.identity, this.transform);
            allRoomCenters.Add(worldPos); 

            Room newRoom = roomObj.GetComponent<Room>();
            _placedRooms.Add(gridPos, newRoom);
            if (newRoom.Door_North != null && doorToIgnore != "North")
                _openDoorways.Add(new OpenDoorway { GridPos = gridPos, Direction = "North" });
            if (newRoom.Door_South != null && doorToIgnore != "South")
                _openDoorways.Add(new OpenDoorway { GridPos = gridPos, Direction = "South" });
            if (newRoom.Door_East != null && doorToIgnore != "East")
                _openDoorways.Add(new OpenDoorway { GridPos = gridPos, Direction = "East" });
            if (newRoom.Door_West != null && doorToIgnore != "West")
                _openDoorways.Add(new OpenDoorway { GridPos = gridPos, Direction = "West" });
        }
    
        int SelectDoorIndex()
        {
            float sprawlChance = 1.0f - density;
            if (_rng.NextDouble() > sprawlChance) // Using rng.NextDouble for consistency
            {
                return _rng.Next(_openDoorways.Count);
            }
            _doorIndices.Clear();
            for (int i = 0; i < _openDoorways.Count; i++) { _doorIndices.Add(i); }
            ShuffleList(_doorIndices);
            foreach (int index in _doorIndices)
            {
                OpenDoorway door = _openDoorways[index];
                if (IsDoorFacingAway(door.GridPos, door.Direction)) return index;
            }
            return _doorIndices[0]; 
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
                int k = _rng.Next(n + 1);
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
                int k = _rng.Next(n + 1);
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
            Vector2Int newPos = door.GridPos;
            if (door.Direction == "North") newPos.y += 1;
            else if (door.Direction == "South") newPos.y -= 1;
            else if (door.Direction == "East") newPos.x += 1;
            else if (door.Direction == "West") newPos.x -= 1;
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
            if (doorDirection == "North") return endRoomPrefabN;
            if (doorDirection == "South") return endRoomPrefabS;
            if (doorDirection == "East") return endRoomPrefabE;
            if (doorDirection == "West") return endRoomPrefabW;
            return null;
        }

        GameObject GetDeadEndPrefab(string doorDirection)
        {
            if (doorDirection == "North") return deadEndPrefabN;
            if (doorDirection == "South") return deadEndPrefabS;
            if (doorDirection == "East") return deadEndPrefabE;
            if (doorDirection == "West") return deadEndPrefabW;
            return null;
        }
        #endregion
    }
}