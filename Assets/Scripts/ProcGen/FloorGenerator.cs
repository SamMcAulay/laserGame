using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

namespace ProcGen
{
    public class FloorGenerator : MonoBehaviour
    {
        [Header("Player & Enemy")]
        public GameObject playerPrefab;
        public GameObject enemyPrefab;

        [Header("Room Prefabs")]
        [Tooltip("A 1-door room where the player spawns")]
        public GameObject startRoomPrefab;

        [Tooltip("A single list of all 'filler' rooms (hallways, junctions, puzzles)")]
        public List<GameObject> roomPrefabs;

        [Tooltip("The special room prefab for the enemy to spawn in")]
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
        [Tooltip("Total number of rooms (Start, end, enemy, and cap rooms")]
        public int numberOfRooms = 15;

        [Header("Density Settings")]
        [Tooltip("1.0 = Full Random (more clumping). 0.0 = Prioritize Sprawling away from start.")]
        [Range(0.0f, 1.0f)]
        public float density = 1.0f;


        // --- Private Generator State ---
        private Dictionary<Vector2Int, Room> placedRooms = new();
        private List<OpenDoorway> openDoorways = new();
        private List<int> doorIndices = new(); 
        private System.Random rng = new();
        private List<GameObject> roomBag = new(); 

        public static List<Vector3> allRoomCenters = new();

        private class OpenDoorway { public Vector2Int gridPos; public string direction; }

        // --- Main Generation Function ---
        void Start()
        {
            GenerateFloor();
        }

        void GenerateFloor()
        {
            // This clears the list of all the room centers from previous runs. this works in conjunction with the AI roaming system
            allRoomCenters.Clear();
            
            // This here is our room bag logic, we will basically keep adding the room list to this "bag" until we have enough to meet the room amount
            roomBag.Clear();
           
            int roomsToPlace = Mathf.Max(0, numberOfRooms);
            
            // failsafe for if whatever reason my room list is empty ( Has happened before if i change a variable and remove the serialsed old name )
            if (roomPrefabs == null || roomPrefabs.Count == 0)
            {
                Debug.LogError("FloorGenerator: 'roomPrefabs' list is empty! Cannot fill room bag.", this);
                return;
            }

            // Keep adding shuffled "decks" of your rooms until the bag is big enough, this is the list thing mentioned above
            while (roomBag.Count < roomsToPlace)
            {
                List<GameObject> tempDeck = new List<GameObject>(roomPrefabs);
                ShuffleList(tempDeck); // Shuffle this smaller deck
                roomBag.AddRange(tempDeck);
            }

            // once big enough we shuffle the list
            ShuffleList(roomBag);

            // Reduce the list to meet the amount of rooms we need to spawn.
            if (roomBag.Count > roomsToPlace)
            {
                roomBag.RemoveRange(roomsToPlace, roomBag.Count - roomsToPlace);
            }
            // The Reason I am using the above logic, rather than a super simple Pick a random item from the list method
            // Is so i can somewhat control the consistency in the rooms, every room should appear atleast once, 
            // and there should also be an even spread of the different types

            // --- Place Start Room & Player ---
            // This just placed the player at (0,0). since our spawn room is placed first
            PlaceRoom(startRoomPrefab, Vector2Int.zero);
            if (playerPrefab != null)
            {
                Instantiate(playerPrefab, new Vector3(0, 1, 0), Quaternion.identity);
            }

            // --- Main Generation Loop ---
            // again another failsafe in the case that we cannot find a spot for a room
            int failsafe = 10000;
            while (roomBag.Count > 0 && openDoorways.Count > 0 && failsafe > 0)
            {
                failsafe--;
            
                int selectedIndex = SelectDoorIndex(); // This works with my density logic to select a door socket to attach to
                OpenDoorway currentDoor = openDoorways[selectedIndex];
                Vector2Int newRoomPos = GetNewRoomPosition(currentDoor);
                string doorToConnectTo = GetDoorToConnectTo(currentDoor.direction);
                
                if (placedRooms.ContainsKey(newRoomPos))
                {
                    openDoorways.RemoveAt(selectedIndex);
                    continue; 
                }

                // Find a room from the bag that fits
                GameObject prefab = FindAndRemoveValidRoomFromBag(roomBag, doorToConnectTo);
            
                if (prefab == null)
                {
                    // No room in our *entire bag* fits this door, this happens often as say we only have south doors left, but no rooms with a north connecting door, it will circle back here
                    openDoorways.RemoveAt(selectedIndex);
                    continue; 
                }

                // --- SUCCESS ---
                // once we finally find a valid spot, we remove the used door socket from our list of availible sockets, and places our room
                openDoorways.RemoveAt(selectedIndex);
                PlaceRoom(prefab, newRoomPos, doorToConnectTo);
            }
            // if for whatever reason the loop keeps going past the amount of doorways availible, it will catch here
            // this was surprisingly useful as due to my rooms material, i had no idea that 1000s of rooms were spawning inside each other, only for this catch! so it is useful i swear
            if (failsafe <= 0) Debug.LogWarning("FloorGenerator: Hit failsafe in main loop.");


            // --- Place the End Room ---
            // This works the same (kinda) as the other room placer
            // however we try our best to find the socket as far away as possible from the start room
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


            // --- Place the Enemy Room ---
            // again, same as above, however we try to find a spot as far away from both the start and the end
            OpenDoorway bestEnemyDoorway = null;
            int maxMinDistanceSqr = -1; 
            
            for (int i = openDoorways.Count - 1; i >= 0; i--)
            {
                OpenDoorway currentDoor = openDoorways[i];
                Vector2Int newPos = GetNewRoomPosition(currentDoor);
                string doorToConnectTo = GetDoorToConnectTo(currentDoor.direction);

                // Check for collision
                if (placedRooms.ContainsKey(newPos))
                {
                    openDoorways.RemoveAt(i);
                    continue;
                }
            
                // Check if the enemy room prefab can fit here
                if (!CanRoomFit(enemySpawnRoomPrefab, doorToConnectTo))
                {
                    continue; // This door is valid, but not for this room
                }

                // Calculate score, find the minimum distance to start OR end
                int distToStartSqr = newPos.sqrMagnitude; // Distance from (0,0)
                int distToEndSqr = (newPos - endRoomPos).sqrMagnitude; // Distance from end
                int score = Mathf.Min(distToStartSqr, distToEndSqr);

                if (score > maxMinDistanceSqr)
                {
                    maxMinDistanceSqr = score;
                    bestEnemyDoorway = currentDoor;
                }
            }

            // Now place the enemy room at the best spot
            if (bestEnemyDoorway != null)
            {
                Vector2Int enemyRoomPos = GetNewRoomPosition(bestEnemyDoorway);
                string enemyDoorToConnect = GetDoorToConnectTo(bestEnemyDoorway.direction);
            
                PlaceRoom(enemySpawnRoomPrefab, enemyRoomPos, enemyDoorToConnect);
                openDoorways.Remove(bestEnemyDoorway);

                // Spawn the enemy
                if (enemyPrefab != null)
                {
                    Vector3 spawnPos = new Vector3(enemyRoomPos.x * gridSize, 1, enemyRoomPos.y * gridSize);
                    Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
                }
            }
            else
            {
                Debug.LogError("FloorGenerator: Could not find any valid spot to place the Enemy Room!");
            }

            // --- Cap all remaining Dead Ends ---
            failsafe = 1000;
        
            // We MUST iterate backwards so we can safely remove items, otherwise list goes poof
            for (int i = openDoorways.Count - 1; i >= 0 && failsafe > 0; i--)
            {
                failsafe--;
                OpenDoorway currentDoor = openDoorways[i];
            
                // We can safely remove it from the list.
                // because we are only processing it once.
                openDoorways.RemoveAt(i); 

                Vector2Int newPos = GetNewRoomPosition(currentDoor);
                string doorToConnect = GetDoorToConnectTo(currentDoor.direction);

                // Check if spot is already filled (by the Start/End/Enemy room)
                if (placedRooms.ContainsKey(newPos))
                {
                    continue; // This was a blocked door, we nuke it
                }
            
                // Spot is empty, so place a cap
                GameObject deadEndPrefab = GetDeadEndPrefab(doorToConnect);
                if (deadEndPrefab != null)
                {
                    // We pass "doorToConnect" to ignore the
                    // single door on the dead-end prefab,
                    // so it doesn't add itself back to the list.
                    PlaceRoom(deadEndPrefab, newPos, doorToConnect);
                }
                else
                {
                    // This should be caught by ValidatePrefabs, but as a failsafe:
                    Debug.LogWarning($"Tried to cap door at {newPos} but the prefab for {doorToConnect} is null!");
                }
            }
        
            // --- Bake NavMesh ---
            if (navMeshSurface != null)
            {
                navMeshSurface.BuildNavMesh();
            }
            else
            {
                Debug.LogError("NavMeshSurface not assigned in FloorGenerator!", this);
            }
        }

        // --- Helper Functions ---

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
                if (IsDoorFacingAway(door.gridPos, door.direction))
                {
                    return index; // Found a good one!
                }
            }
            return doorIndices[0]; // Fallback
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
        
        // Finds a valid room from the bag that fits, REMOVES it, and returns it.
        GameObject FindAndRemoveValidRoomFromBag(List<GameObject> bag, string doorDirection)
        {
            // Search the bag for the first room that fits
            for (int i = 0; i < bag.Count; i++)
            {
                if (CanRoomFit(bag[i], doorDirection))
                {
                    GameObject prefab = bag[i];
                    bag.RemoveAt(i); // Found one, remove it
                    return prefab;   // Return it
                }
            }
            return null; // No room in the entire bag fits
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
    }
}