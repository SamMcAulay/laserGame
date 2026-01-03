using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace Speedrun
{
    public class SpeedrunManager : MonoBehaviour
    {
        public static SpeedrunManager Instance;

        [Header("UI References")]
        [Tooltip("The text component displaying the current running floor time.")]
        public TextMeshProUGUI currentFloorText;
        
        [Tooltip("The vertical layout group where completed floors will be listed.")]
        public Transform historyContainer;
        
        [Tooltip("Prefab for the history row. Must have a SpeedrunRow component.")]
        public GameObject historyRowPrefab;

        [Header("Settings")]
        public Color underTimeColor = Color.green;
        public Color overTimeColor = Color.red;

        // State 
        private float _currentFloorTimer;
        private bool _isRunning;
        private int _currentFloorIndex; // 0 for Floor 1, 1 for Floor 2...

        // Data 
        private RunData _currentRun = new RunData();
        private RunData _bestRun = new RunData();
        private string _saveFilePath;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            _saveFilePath = Application.persistentDataPath + "/speedrun_best.json";
            LoadBestRun();
        }

        void Update()
        {
            if (_isRunning)
            {
                _currentFloorTimer += Time.deltaTime;
                UpdateCurrentFloorUI();
            }
        }

        public void StartRun()
        {
            // Reset state
            _currentRun = new RunData();
            _currentFloorTimer = 0f;
            _currentFloorIndex = 0;
            _isRunning = true;

            // Clear old UI
            foreach (Transform child in historyContainer)
            {
                Destroy(child.gameObject);
            }

            UpdateCurrentFloorUI();
        }

        public void CompleteFloor()
        {
            if (!_isRunning) return;

            // Record the time for this floor
            float timeTaken = _currentFloorTimer;
            _currentRun.splitTimes.Add(timeTaken);

            // Add to UI History (The "Previous Floors" list)
            AddHistoryRow(_currentFloorIndex + 1, timeTaken);

            // Reset for next floor
            _currentFloorTimer = 0f;
            _currentFloorIndex++;
        }

        public void StopTimer()
        {
            _isRunning = false;
            CheckForNewBestRun();
        }

        // UI Logic

        private void UpdateCurrentFloorUI()
        {
            if (currentFloorText == null) return;

            // Format: Floor X: 0:00
            string timeStr = FormatTime(_currentFloorTimer);
            
            // Calculate Live Delta (Current Time - Best Time for this floor)
            // This will start negative (Green) and turn Red if you go over.
            float bestTime = GetBestTimeForFloor(_currentFloorIndex);
            
            string deltaStr = "";
            if (bestTime > 0)
            {
                float delta = _currentFloorTimer - bestTime;
                deltaStr = FormatDelta(delta);
            }

            currentFloorText.text = $"Floor {_currentFloorIndex + 1}: {timeStr} {deltaStr}";
        }

        private void AddHistoryRow(int floorNum, float timeTaken)
        {
            if (historyRowPrefab == null || historyContainer == null) return;

            // Create the row
            GameObject rowObj = Instantiate(historyRowPrefab, historyContainer);
    
            // Format the text strings, Time and Colour
            string timeStr = FormatTime(timeTaken);
            float bestTime = GetBestTimeForFloor(floorNum - 1);
            string deltaStr = (bestTime > 0) ? FormatDelta(timeTaken - bestTime) : " (New)";
            string finalString = $"Floor {floorNum}: {timeStr} {deltaStr}";

            // Find the SpeedrunRow script and apply the text
            SpeedrunRow rowScript = rowObj.GetComponent<SpeedrunRow>();
            if (rowScript != null)
            {
                rowScript.Setup(finalString);
            }
            else
            {
                // fallback to try find text directly
                var simpleText = rowObj.GetComponent<TextMeshProUGUI>();
                if (simpleText != null) simpleText.text = finalString;
            }
        }

        // does what it says on the tin
        private string FormatTime(float time)
        {
            int minutes = Mathf.FloorToInt(time / 60F);
            int seconds = Mathf.FloorToInt(time % 60F);
            return $"{minutes}:{seconds:00}";
        }
        
        // again, same thing, formats the delta
        private string FormatDelta(float delta)
        {
            string sign = delta < 0 ? "-" : "+";
            string colorHex = delta < 0 ? ColorUtility.ToHtmlStringRGB(underTimeColor) : ColorUtility.ToHtmlStringRGB(overTimeColor);
            
            float absDelta = Mathf.Abs(delta);
            int minutes = Mathf.FloorToInt(absDelta / 60F);
            int seconds = Mathf.FloorToInt(absDelta % 60F);

            return $"<color=#{colorHex}>{sign}{minutes}:{seconds:00}</color>";
        }

        // Data Logic

        private float GetBestTimeForFloor(int floorIndex)
        {
            if (_bestRun != null && floorIndex < _bestRun.splitTimes.Count)
            {
                return _bestRun.splitTimes[floorIndex];
            }
            return -1f; // No data
        }

        private void CheckForNewBestRun()
        {
            // Definition of Best Run:
            // Furthest Distance (Most floors cleared)
            // If tied, Lowest Total Time.

            bool isNewBest = false;

            if (_bestRun == null || _bestRun.splitTimes.Count == 0)
            {
                isNewBest = true;
            }
            else if (_currentRun.splitTimes.Count > _bestRun.splitTimes.Count)
            {
                isNewBest = true;
            }
            else if (_currentRun.splitTimes.Count == _bestRun.splitTimes.Count)
            {
                if (GetTotalTime(_currentRun) < GetTotalTime(_bestRun))
                {
                    isNewBest = true;
                }
            }

            if (isNewBest)
            {
                Debug.Log("NEW BEST RUN!");
                _bestRun = _currentRun;
                SaveBestRun();
            }
        }

        private float GetTotalTime(RunData run)
        {
            float total = 0;
            foreach (float t in run.splitTimes) total += t;
            return total;
        }

        private void SaveBestRun()
        {
            string json = JsonUtility.ToJson(_bestRun);
            File.WriteAllText(_saveFilePath, json);
        }

        private void LoadBestRun()
        {
            if (File.Exists(_saveFilePath))
            {
                string json = File.ReadAllText(_saveFilePath);
                _bestRun = JsonUtility.FromJson<RunData>(json);
            }
        }
    }

    [System.Serializable]
    public class RunData
    {
        // Stores the time taken for each floor index
        public List<float> splitTimes = new List<float>();
    }
}