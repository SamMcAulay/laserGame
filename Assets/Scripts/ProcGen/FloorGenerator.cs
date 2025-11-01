using System.Collections.Generic;
using UnityEngine;

namespace ProcGen
{
    public class FloorGenerator : MonoBehaviour
    {
        [Header("Player")]
        public GameObject playerPrefab;

        [Header("Room Prefabs")]
        [Tooltip("A 1-door room where the player spawns")]
        public GameObject startRoomPrefab;

        [Tooltip("A single list of all rooms (hallways, junctions, puzzles)")]
        public List<GameObject> roomPrefabs; // <-- BACK TO ONE LIST

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

        [Header("Generation Settings")]
        public int gridSize = 20;
        public int numberOfRooms = 15;

        [Header("Density Settings")]
        [Tooltip("1.0 = Full Random (more clumping). 0.0 = Prioritize Sprawling away from start (more tangents).")]
        [Range(0.0f, 1.0f)]
        public float density = 1.0f; // Default to 1.0 (full random)


        // --- Private Generator State ---
        private Dictionary<Vector2Int, Room> placedRooms = new Dictionary<Vector2Int, Room>();
        private List<OpenDoorway> openDoorways = new List<OpenDoorway>();
        private List<int> doorIndices = new List<int>(); // For shuffling
        private System.Random rng = new System.Random(); // For shuffling

        private class OpenDoorway
        {
            public Vector2Int gridPos;
            public string direction;
        }

        // --- Main Generation Function ---
        void Start()
        {
            GenerateFloor();
        }

        void GenerateFloor()
        {
            // 1. Place the Start Room
            PlaceRoom(startRoomPrefab, Vector2Int.zero);

            // 2. Place the Player
            if (playerPrefab != null)
            {
                Instantiate(playerPrefab, new Vector3(0, 1, 0), Quaternion.identity);
            }

            // 3. Start the main generation loop
            int roomsPlaced = 1;
            int roomsToPlace = Mathf.Max(1, numberOfRooms - 1);
            int failsafe = 10000;

            while (roomsPlaced < roomsToPlace && openDoorways.Count > 0 && failsafe > 0)
            {
                failsafe--;
                
                int selectedIndex = SelectDoorIndex();
                OpenDoorway currentDoor = openDoorways[selectedIndex];

                Vector2Int newRoomPos = GetNewRoomPosition(currentDoor);
                string doorToConnectTo = GetDoorToConnectTo(currentDoor.direction);

                // 1. Check for Collision
                if (placedRooms.ContainsKey(newRoomPos))
                {
                    // This spot is blocked. Remove the door
                    openDoorways.RemoveAt(selectedIndex);
                    continue; // Try a different door
                }

                // 2. Check for a valid prefab
                GameObject prefab = FindRandomRoomWithDoor(doorToConnectTo);
                if (prefab == null)
                {
                    // No rooms in our list can connect here. Remove the door
                    openDoorways.RemoveAt(selectedIndex);
                    continue; // Try a different door
                }
                
                openDoorways.RemoveAt(selectedIndex);
                PlaceRoom(prefab, newRoomPos, doorToConnectTo);
                roomsPlaced++;
            }

            if (failsafe <= 0)
            {
                Debug.LogWarning("FloorGenerator: Hit failsafe in main loop.");
            }

            // 6. Place the End Room
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

            bool endRoomPlaced = false;
            if (farthestDoorway != null)
            {
                Vector2Int endRoomPos = GetNewRoomPosition(farthestDoorway);
                string endDoorToConnect = GetDoorToConnectTo(farthestDoorway.direction);
                GameObject endPrefab = GetEndRoomPrefab(endDoorToConnect);

                if (endPrefab != null)
                {
                    PlaceRoom(endPrefab, endRoomPos, endDoorToConnect);
                    endRoomPlaced = true;
                    openDoorways.Remove(farthestDoorway);
                }
            }

            if (!endRoomPlaced)
            {
                Debug.LogError("FloorGenerator: Could not find any valid spot to place the End Room!");
            }

            // 7. Cap all remaining Dead Ends
            failsafe = 1000;
            while (openDoorways.Count > 0 && failsafe > 0)
            {
                failsafe--;
                OpenDoorway currentDoor = openDoorways[0];
                openDoorways.RemoveAt(0);
                Vector2Int newPos = GetNewRoomPosition(currentDoor);
                string doorToConnect = GetDoorToConnectTo(currentDoor.direction);

                if (placedRooms.ContainsKey(newPos))
                {
                    continue;
                }
                GameObject deadEndPrefab = GetDeadEndPrefab(doorToConnect);
                if (deadEndPrefab != null)
                {
                    PlaceRoom(deadEndPrefab, newPos, doorToConnect);
                }
            }
        }
        // Selects a door index based on the 'density' (sprawl) setting.
        int SelectDoorIndex()
        {
            // 1. Check if we should try to sprawl
            float sprawlChance = 1.0f - density;
            if (Random.Range(0.0f, 1.0f) > sprawlChance)
            {
                // We are NOT sprawling. Pick a fully random door
                return Random.Range(0, openDoorways.Count);
            }

            // 2. We ARE sprawling. Try to find a "favorable" door
            doorIndices.Clear();
            for (int i = 0; i < openDoorways.Count; i++)
            {
                doorIndices.Add(i);
            }
            ShuffleList(doorIndices);

            foreach (int index in doorIndices)
            {
                OpenDoorway door = openDoorways[index];
                if (IsDoorFacingAway(door.gridPos, door.direction))
                {
                    return index; 
                }
            }

            // 3. Fallback: No favorable doors were found
            // Just return the first random index from our shuffled list
            return doorIndices[0];
        }

       
        /// Checks if a door is "favorable" (facing away from the 0,0 start)
        bool IsDoorFacingAway(Vector2Int roomPos, string doorDirection)
        {
            if (doorDirection == "North") return roomPos.y >= 0;
            if (doorDirection == "South") return roomPos.y <= 0;
            if (doorDirection == "East") return roomPos.x >= 0;
            if (doorDirection == "West") return roomPos.x <= 0;
            return false;
        }

        
        /// Simple Fisher-Yates shuffle for our index list.
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
        
        /// Instantiates a room prefab and updates the 'openDoorways' list.
        void PlaceRoom(GameObject prefab, Vector2Int gridPos, string doorToIgnore = "")
        {
            Vector3 worldPos = new Vector3(gridPos.x * gridSize, 0, gridPos.y * gridSize);
            GameObject roomObj = Instantiate(prefab, worldPos, Quaternion.identity, this.transform);
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

    
        /// Finds a random room from the SINGLE 'roomPrefabs' list.
        GameObject FindRandomRoomWithDoor(string doorDirection)
        {
            List<GameObject> validRooms = new List<GameObject>();
        
            // Loop through our single list of prefabs
            foreach (var prefab in roomPrefabs)
            {
                Room room = prefab.GetComponent<Room>();
                if (room == null) continue;

                // Check if it has the door we need
                if (doorDirection == "North" && room.Door_North != null)
                    validRooms.Add(prefab);
                else if (doorDirection == "South" && room.Door_South != null)
                    validRooms.Add(prefab);
                else if (doorDirection == "East" && room.Door_East != null)
                    validRooms.Add(prefab);
                else if (doorDirection == "West" && room.Door_West != null)
                    validRooms.Add(prefab);
            }

            if (validRooms.Count == 0)
                return null; // No valid rooms found

            // Pick a random one from the valid list
            return validRooms[Random.Range(0, validRooms.Count)];
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